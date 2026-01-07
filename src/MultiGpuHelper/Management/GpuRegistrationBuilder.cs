using System;
using System.Collections.Generic;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Management
{
    /// <summary>
    /// Builder for registering and configuring GPU devices.
    /// </summary>
    public class GpuRegistrationBuilder
    {
        private readonly Dictionary<int, GpuDevice> _devices = new Dictionary<int, GpuDevice>();

        /// <summary>
        /// Add a device manually.
        /// </summary>
        public GpuRegistrationBuilder AddDevice(int deviceId, string name, long totalVramBytes)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Device name cannot be empty.", nameof(name));

            if (totalVramBytes <= 0)
                throw new ArgumentException("Total VRAM must be positive.", nameof(totalVramBytes));

            if (_devices.ContainsKey(deviceId))
                throw new InvalidOperationException($"Device {deviceId} is already registered.");

            var device = new GpuDevice
            {
                DeviceId = deviceId,
                Name = name,
                TotalVramBytes = totalVramBytes,
                FreeVramBytes = totalVramBytes,
                IsEnabled = true,
                MaxConcurrentJobs = 1
            };

            _devices[deviceId] = device;
            return this;
        }

        /// <summary>
        /// Configure a device's budget and concurrency settings.
        /// </summary>
        public GpuRegistrationBuilder ConfigureDevice(int deviceId, long? budgetBytes = null, int? maxConcurrentJobs = null, bool? enabled = null)
        {
            if (!_devices.TryGetValue(deviceId, out var device))
                throw new InvalidOperationException($"Device {deviceId} not found. Add it first with AddDevice().");

            if (budgetBytes.HasValue && budgetBytes.Value <= 0)
                throw new ArgumentException("Budget must be positive.", nameof(budgetBytes));

            if (maxConcurrentJobs.HasValue && maxConcurrentJobs.Value <= 0)
                throw new ArgumentException("MaxConcurrentJobs must be positive.", nameof(maxConcurrentJobs));

            if (budgetBytes.HasValue)
                device.VramBudget.LimitBytes = budgetBytes.Value;

            if (maxConcurrentJobs.HasValue)
                device.MaxConcurrentJobs = maxConcurrentJobs.Value;

            if (enabled.HasValue)
                device.IsEnabled = enabled.Value;

            return this;
        }

        /// <summary>
        /// Build the GpuManager with configured devices.
        /// </summary>
        public GpuManager Build()
        {
            var manager = new GpuManager();

            foreach (var device in _devices.Values)
            {
                manager.AddDevice(device);
            }

            return manager;
        }
    }
}
