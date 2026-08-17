using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiGpuHelper.Exceptions;
using MultiGpuHelper.Abstractions;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;
using MultiGpuHelper.Selection;

namespace MultiGpuHelper.Management
{
    /// <summary>
    /// Manages GPU devices and device selection policies.
    /// Thread-safe.
    /// </summary>
    public class GpuManager
    {
        private readonly Dictionary<int, GpuDevice> _devices;
        private readonly IGpuBackend _backend;
        private readonly IGpuLogger _logger;
        private readonly object _lockObject = new object();
        private readonly GpuSelectionEngine _selectionEngine;

        public GpuManager(IGpuBackend backend = null, IGpuLogger logger = null)
        {
            _backend = backend ?? new NvidiaBackend(logger);
            _logger = logger ?? new NoOpLogger();
            _devices = new Dictionary<int, GpuDevice>();
            _selectionEngine = new GpuSelectionEngine(_logger);
        }

        /// <summary>
        /// Get all registered devices.
        /// </summary>
        public IReadOnlyList<GpuDevice> Devices
        {
            get
            {
                lock (_lockObject)
                {
                    return _devices.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Add or replace a device.
        /// </summary>
        public void AddDevice(GpuDevice device)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

            lock (_lockObject)
            {
                _devices[device.DeviceId] = device;
                _logger.Debug($"Device registered: {device.DeviceId} ({device.Name})");
            }
        }

        /// <summary>
        /// Remove a device by ID.
        /// </summary>
        public bool RemoveDevice(int deviceId)
        {
            lock (_lockObject)
            {
                return _devices.Remove(deviceId);
            }
        }

        /// <summary>
        /// Get a device by ID.
        /// </summary>
        public GpuDevice GetDevice(int deviceId)
        {
            lock (_lockObject)
            {
                if (_devices.TryGetValue(deviceId, out var device))
                    return device;
                return null;
            }
        }

        /// <summary>
        /// Refresh VRAM information for all devices.
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                var probed = await _backend.DetectDevicesAsync().ConfigureAwait(false);
                lock (_lockObject)
                {
                    foreach (var device in probed)
                    {
                        if (_devices.TryGetValue(device.DeviceId, out var existing))
                        {
                            // Update VRAM info only; preserve other settings
                            existing.FreeVramBytes = device.MemoryInfo.State == GpuAvailabilityState.Available
                                ? (long?)device.MemoryInfo.FreeBytes
                                : null;
                            existing.TotalVramBytes = device.MemoryInfo.TotalBytes;
                        }
                    }
                }
                _logger.Debug("GPU info refreshed");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to refresh GPU info: {ex.Message}");
            }
        }

        /// <summary>
        /// Select a GPU device based on the given policy.
        /// </summary>
        public GpuDevice SelectDevice(GpuPolicy policy, int? specificDeviceId = null)
        {
            lock (_lockObject)
            {
                var snapshots = _devices.Values.Select(device => new GpuDeviceInfo(
                    device.DeviceId,
                    string.IsNullOrWhiteSpace(device.Name) ? $"GPU {device.DeviceId}" : device.Name,
                    GpuBackendKind.Unknown,
                    new GpuMemoryInfo(
                        device.TotalVramBytes,
                        device.FreeVramBytes ?? 0,
                        device.FreeVramBytes.HasValue
                            ? GpuAvailabilityState.Available
                            : GpuAvailabilityState.Unavailable),
                    device.IsEnabled
                        ? GpuAvailabilityState.Available
                        : GpuAvailabilityState.Unavailable,
                    device.VramBudget?.LimitBytes ?? 0,
                    device.MaxConcurrentJobs)).ToList();

                var result = _selectionEngine.SelectDevice(snapshots, policy, specificDeviceId);
                if (!result.IsSuccess)
                    throw new GpuSelectionException(result.Reason);

                return _devices[result.SelectedDeviceId];
            }
        }

        /// <summary>
        /// Initialize devices by probing (e.g., via nvidia-smi).
        /// </summary>
        public async Task InitializeFromProbeAsync()
        {
            try
            {
                var probed = await _backend.DetectDevicesAsync().ConfigureAwait(false);
                lock (_lockObject)
                {
                    foreach (var device in probed)
                    {
                        _devices[device.DeviceId] = new GpuDevice
                        {
                            DeviceId = device.DeviceId,
                            Name = device.DeviceName,
                            TotalVramBytes = device.MemoryInfo.TotalBytes,
                            FreeVramBytes = device.MemoryInfo.State == GpuAvailabilityState.Available
                                ? (long?)device.MemoryInfo.FreeBytes
                                : null,
                            IsEnabled = device.AvailabilityState == GpuAvailabilityState.Available,
                            MaxConcurrentJobs = device.MaxConcurrentJobs,
                            VramBudget = new VramBudget
                            {
                                LimitBytes = device.VramBudgetLimitBytes > 0
                                    ? (long?)device.VramBudgetLimitBytes
                                    : null
                            }
                        };
                    }
                }
                _logger.Info($"Initialized {probed.Count} GPU devices from probe");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to initialize from probe: {ex.Message}");
                throw new GpuProbeException("Failed to probe GPUs.", ex);
            }
        }
    }
}
