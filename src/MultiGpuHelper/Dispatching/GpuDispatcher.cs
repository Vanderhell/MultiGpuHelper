using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MultiGpuHelper.Exceptions;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Dispatching
{
    /// <summary>
    /// Dispatcher for scheduling work across multiple GPUs.
    /// Manages concurrency limits, VRAM budgets, and device selection.
    /// </summary>
    public class GpuDispatcher
    {
        private readonly GpuManager _gpuManager;
        private readonly IGpuLogger _logger;
        private readonly Dictionary<int, SemaphoreSlim> _deviceSemaphores = new Dictionary<int, SemaphoreSlim>();
        private readonly object _semaphoreLock = new object();

        public GpuDispatcher(GpuManager gpuManager, IGpuLogger logger = null)
        {
            _gpuManager = gpuManager ?? throw new ArgumentNullException(nameof(gpuManager));
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Run async work on a GPU.
        /// </summary>
        public async Task<T> RunAsync<T>(
            Func<int, CancellationToken, Task<T>> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken ct = default)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            workItem = workItem ?? new GpuWorkItem();

            try
            {
                // Select device
                var device = _gpuManager.SelectDevice(policy, workItem.SpecificDeviceId);

                _logger.Debug($"Selected device {device.DeviceId} ({device.Name}) for work {workItem.Tag}");

                // Get semaphore for this device
                var semaphore = GetDeviceSemaphore(device.DeviceId);

                CancellationTokenSource timeoutSource = null;
                CancellationToken workCt = ct;
                if (workItem.TimeoutMs.HasValue && workItem.TimeoutMs.Value > 0)
                {
                    timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutSource.CancelAfter(workItem.TimeoutMs.Value);
                    workCt = timeoutSource.Token;
                }

                try
                {
                    await semaphore.WaitAsync(workCt).ConfigureAwait(false);
                }
                catch
                {
                    timeoutSource?.Dispose();
                    throw;
                }
                finally
                {
                    if (workCt.IsCancellationRequested && !ct.IsCancellationRequested)
                        _logger.Warn($"Work '{workItem.Tag}' reached its timeout while waiting for a device slot.");
                }

                var reserved = false;
                try
                {
                    if (workItem.RequestedVramBytes > 0)
                    {
                        reserved = device.VramBudget.TryReserve(workItem.RequestedVramBytes);
                        if (!reserved)
                            throw new GpuBudgetExceededException(
                                $"VRAM budget exceeded on device {device.DeviceId} for a request of {workItem.RequestedVramBytes} bytes.");
                    }

                    _logger.Debug($"Executing work '{workItem.Tag}' on device {device.DeviceId}");
                    var result = await work(device.DeviceId, workCt).ConfigureAwait(false);
                    _logger.Debug($"Work '{workItem.Tag}' completed on device {device.DeviceId}");
                    return result;
                }
                finally
                {
                    semaphore.Release();
                    if (reserved)
                        device.VramBudget.Release(workItem.RequestedVramBytes);
                    timeoutSource?.Dispose();
                }
            }
            catch (GpuSelectionException)
            {
                throw;
            }
            catch (GpuBudgetExceededException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.Warn($"Work '{workItem.Tag}' was cancelled");
                throw;
            }
        }

        public Task<T> RunAsync<T>(
            Func<int, Task<T>> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken ct = default)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));

            return RunAsync((deviceId, _) => work(deviceId), policy, workItem, ct);
        }

        /// <summary>
        /// Run async work without return value.
        /// </summary>
        public async Task RunAsync(
            Func<int, Task> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken ct = default)
        {
            await RunAsync(
                async (deviceId, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await work(deviceId).ConfigureAwait(false);
                    return true;
                },
                policy,
                workItem,
                ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Run synchronous work on a GPU.
        /// </summary>
        public Task<T> RunAsync<T>(
            Func<int, T> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken ct = default)
        {
            return RunAsync(
                deviceId => Task.FromResult(work(deviceId)),
                policy,
                workItem,
                ct);
        }

        /// <summary>
        /// Run synchronous work without return value.
        /// </summary>
        public Task RunAsync(
            Action<int> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken ct = default)
        {
            return RunAsync(
                deviceId =>
                {
                    work(deviceId);
                    return Task.FromResult(true);
                },
                policy,
                workItem,
                ct);
        }

        private SemaphoreSlim GetDeviceSemaphore(int deviceId)
        {
            lock (_semaphoreLock)
            {
                if (!_deviceSemaphores.TryGetValue(deviceId, out var semaphore))
                {
                    var device = _gpuManager.GetDevice(deviceId);
                    var maxConcurrent = device?.MaxConcurrentJobs ?? 1;
                    semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
                    _deviceSemaphores[deviceId] = semaphore;
                }
                return semaphore;
            }
        }
    }
}
