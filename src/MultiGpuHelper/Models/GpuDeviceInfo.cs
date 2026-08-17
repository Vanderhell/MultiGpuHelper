using System;
using MultiGpuHelper.Enums;

namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Immutable GPU device information.
    /// </summary>
    public sealed class GpuDeviceInfo
    {
        /// <summary>
        /// Unique device identifier (GPU ordinal).
        /// </summary>
        public int DeviceId { get; }

        /// <summary>
        /// Human-readable device name (e.g., "NVIDIA RTX 4090").
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// GPU backend (NVIDIA, AMD, etc.).
        /// </summary>
        public GpuBackendKind Backend { get; }

        /// <summary>
        /// Hardware vendor, or <see cref="GpuVendor.Unknown"/> when it cannot be determined.
        /// </summary>
        public GpuVendor Vendor { get; }

        /// <summary>
        /// Memory information (total, free, state).
        /// </summary>
        public GpuMemoryInfo MemoryInfo { get; }

        /// <summary>
        /// Device availability state.
        /// </summary>
        public GpuAvailabilityState AvailabilityState { get; }

        /// <summary>
        /// Soft VRAM budget limit in bytes (0 = no limit).
        /// </summary>
        public long VramBudgetLimitBytes { get; }

        /// <summary>
        /// Maximum concurrent jobs on this device.
        /// </summary>
        public int MaxConcurrentJobs { get; }

        /// <summary>
        /// Initialize device information.
        /// </summary>
        /// <param name="deviceId">Device ordinal</param>
        /// <param name="deviceName">Human-readable name</param>
        /// <param name="backend">GPU backend kind</param>
        /// <param name="memoryInfo">Memory information</param>
        /// <param name="availabilityState">Availability state</param>
        /// <param name="vramBudgetLimitBytes">Soft VRAM limit (bytes)</param>
        /// <param name="maxConcurrentJobs">Max concurrent jobs</param>
        /// <param name="vendor">Hardware vendor, if known</param>
        public GpuDeviceInfo(
            int deviceId,
            string deviceName,
            GpuBackendKind backend,
            GpuMemoryInfo memoryInfo,
            GpuAvailabilityState availabilityState,
            long vramBudgetLimitBytes = 0,
            int maxConcurrentJobs = 1,
            GpuVendor vendor = GpuVendor.Unknown)
        {
            if (deviceId < 0)
                throw new ArgumentException("Device ID must be non-negative.", nameof(deviceId));

            if (string.IsNullOrWhiteSpace(deviceName))
                throw new ArgumentException("Device name cannot be null or empty.", nameof(deviceName));

            if (memoryInfo == null)
                throw new ArgumentNullException(nameof(memoryInfo));

            if (maxConcurrentJobs < 1)
                throw new ArgumentException("Max concurrent jobs must be at least 1.", nameof(maxConcurrentJobs));

            DeviceId = deviceId;
            DeviceName = deviceName;
            Backend = backend;
            Vendor = vendor;
            MemoryInfo = memoryInfo;
            AvailabilityState = availabilityState;
            VramBudgetLimitBytes = vramBudgetLimitBytes;
            MaxConcurrentJobs = maxConcurrentJobs;
        }
    }
}
