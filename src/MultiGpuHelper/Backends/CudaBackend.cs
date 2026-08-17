using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MultiGpuHelper.Abstractions;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Backends
{
    /// <summary>
    /// CUDA GPU backend implementation using NVIDIA Driver API (nvcuda.dll).
    /// Detects and enumerates NVIDIA GPUs via CUDA Driver API.
    /// Does not require CUDA runtime/toolkit; works with driver-only installations.
    /// </summary>
    public class CudaBackend : IGpuBackend
    {
        private readonly IGpuLogger _logger;
        private static bool _driverApiInitialized = false;

        public GpuBackendKind BackendKind => GpuBackendKind.Cuda;

        public CudaBackend(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Detect available NVIDIA GPUs via CUDA Driver API.
        /// Returns empty list if CUDA driver is unavailable on the current system.
        /// </summary>
        public async Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var available = IsAvailableSync();
                    if (!available)
                    {
                        _logger.Debug("CUDA driver not available");
                        return new List<GpuDeviceInfo>();
                    }

                    return DetectDevicesSync();
                }
                catch (Exception ex)
                {
                    _logger.Warn($"CUDA backend detection failed: {ex.Message}");
                    return new List<GpuDeviceInfo>();
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Refresh VRAM information for detected devices.
        /// Re-enumerates CUDA devices and updates memory info.
        /// </summary>
        public async Task<IReadOnlyList<GpuDeviceInfo>> RefreshMemoryAsync(IReadOnlyList<GpuDeviceInfo> devices)
        {
            try
            {
                var latest = await DetectDevicesAsync().ConfigureAwait(false);

                // Map old device IDs to updated memory info
                var result = new List<GpuDeviceInfo>();
                foreach (var device in devices)
                {
                    var updated = latest.FirstOrDefault(d => d.DeviceId == device.DeviceId);
                    if (updated != null)
                    {
                        result.Add(updated);
                    }
                    else
                    {
                        var staleMemory = new GpuMemoryInfo(
                            device.MemoryInfo.TotalBytes,
                            device.MemoryInfo.FreeBytes,
                            GpuAvailabilityState.Error);

                        var staleDevice = new GpuDeviceInfo(
                            device.DeviceId,
                            device.DeviceName,
                            device.Backend,
                            staleMemory,
                            GpuAvailabilityState.Unavailable,
                            device.VramBudgetLimitBytes,
                            device.MaxConcurrentJobs,
                            device.Vendor);

                        result.Add(staleDevice);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn($"CUDA backend memory refresh failed: {ex.Message}");
                return new List<GpuDeviceInfo>();
            }
        }

        /// <summary>
        /// Check whether CUDA driver is available on this system.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            return await Task.Run(() => IsAvailableSync()).ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronous check for CUDA driver availability.
        /// </summary>
        private bool IsAvailableSync()
        {
            try
            {
                // Try to initialize the driver API
                var initResult = DriverApiInitialize();
                return initResult == CuResult.Success;
            }
            catch (DllNotFoundException)
            {
                _logger.Debug("nvcuda.dll not found");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                _logger.Debug("CUDA driver entry point not found");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Debug($"CUDA driver availability check failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize the driver API if not already initialized.
        /// </summary>
        private CuResult DriverApiInitialize()
        {
            if (_driverApiInitialized)
                return CuResult.Success;

            var result = CuInit(0);
            if (result == CuResult.Success)
            {
                _driverApiInitialized = true;
            }

            return result;
        }

        /// <summary>
        /// Synchronous device detection via CUDA Driver API.
        /// </summary>
        private List<GpuDeviceInfo> DetectDevicesSync()
        {
            var devices = new List<GpuDeviceInfo>();

            try
            {
                // Get device count
                var result = CuDeviceGetCount(out int deviceCount);
                if (result != CuResult.Success || deviceCount <= 0)
                {
                    _logger.Debug($"CUDA device count query failed: {result}");
                    return devices;
                }

                // Enumerate each device
                for (int i = 0; i < deviceCount; i++)
                {
                    try
                    {
                        var device = QueryDevice(i);
                        if (device != null)
                        {
                            devices.Add(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn($"Failed to query CUDA device {i}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"CUDA device enumeration failed: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Query a specific CUDA device for its properties using driver API.
        /// </summary>
        private GpuDeviceInfo QueryDevice(int deviceId)
        {
            try
            {
                // Get device handle
                var getDeviceResult = CuDeviceGet(out CuDevice device, deviceId);
                if (getDeviceResult != CuResult.Success)
                {
                    _logger.Warn($"Failed to get CUDA device {deviceId}: {getDeviceResult}");
                    return null;
                }

                // Get device name
                var nameBuffer = new System.Text.StringBuilder(256);
                var nameResult = CuDeviceGetName(nameBuffer, nameBuffer.Capacity, device);
                var deviceName = nameResult == CuResult.Success ? nameBuffer.ToString() : $"CUDA Device {deviceId}";

                // Get total device memory
                var memResult = CuDeviceTotalMem(out ulong totalBytes, device);
                if (memResult != CuResult.Success)
                {
                    _logger.Warn($"Failed to get total memory for device {deviceId}: {memResult}");
                    totalBytes = 0;
                }

                // Free memory: driver API does not provide direct free memory query
                // Return 0 and mark as unavailable
                long freeBytes = 0;
                var memoryState = GpuAvailabilityState.Unavailable;

                var memoryInfo = new GpuMemoryInfo(
                    (long)totalBytes,
                    freeBytes,
                    memoryState);

                return new GpuDeviceInfo(
                    deviceId,
                    deviceName,
                    GpuBackendKind.Cuda,
                    memoryInfo,
                    GpuAvailabilityState.Available,
                    vramBudgetLimitBytes: 0,
                    maxConcurrentJobs: 1,
                    vendor: GpuVendor.Nvidia);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Error querying CUDA device {deviceId}: {ex.Message}");
                return null;
            }
        }

        #region CUDA Driver API P/Invoke Declarations

        private enum CuResult
        {
            Success = 0,
            InvalidDevice = 100,
            InvalidContext = 201,
            InvalidHandle = 400,
            // ... other error codes omitted
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CuDevice
        {
            public int handle;
        }

        /// <summary>
        /// CUDA Driver API: cuInit
        /// </summary>
        [DllImport("nvcuda", EntryPoint = "cuInit", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern CuResult CuInit(uint flags);

        /// <summary>
        /// CUDA Driver API: cuDeviceGetCount
        /// </summary>
        [DllImport("nvcuda", EntryPoint = "cuDeviceGetCount", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern CuResult CuDeviceGetCount(out int count);

        /// <summary>
        /// CUDA Driver API: cuDeviceGet
        /// </summary>
        [DllImport("nvcuda", EntryPoint = "cuDeviceGet", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern CuResult CuDeviceGet(out CuDevice device, int ordinal);

        /// <summary>
        /// CUDA Driver API: cuDeviceGetName
        /// </summary>
        [DllImport("nvcuda", EntryPoint = "cuDeviceGetName", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern CuResult CuDeviceGetName(System.Text.StringBuilder name, int len, CuDevice device);

        /// <summary>
        /// CUDA Driver API: cuDeviceTotalMem_v2 (64-bit version for >4GB support)
        /// </summary>
        [DllImport("nvcuda", EntryPoint = "cuDeviceTotalMem_v2", CallingConvention = CallingConvention.Cdecl, SetLastError = true)]
        private static extern CuResult CuDeviceTotalMem(out ulong bytes, CuDevice device);

        #endregion
    }
}
