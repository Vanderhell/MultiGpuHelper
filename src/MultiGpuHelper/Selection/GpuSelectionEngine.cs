using System;
using System.Collections.Generic;
using System.Linq;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Selection
{
    /// <summary>
    /// Implements GPU device selection policies.
    /// </summary>
    public class GpuSelectionEngine
    {
        private readonly IGpuLogger _logger;
        private readonly object _roundRobinLock = new object();
        private long _roundRobinIndex;

        public GpuSelectionEngine(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Select a GPU device based on the specified policy.
        /// </summary>
        /// <param name="devices">Available devices (immutable)</param>
        /// <param name="policy">Selection policy</param>
        /// <param name="explicitDeviceId">Device ID for explicit selection (null for other policies)</param>
        /// <returns>Deterministic selection result with reason text</returns>
        public GpuSelectionResult SelectDevice(
            IReadOnlyList<GpuDeviceInfo> devices,
            GpuPolicy policy,
            int? explicitDeviceId = null)
        {
            if (devices == null)
                throw new ArgumentNullException(nameof(devices));

            long totalDevices = devices.Count;

            return policy switch
            {
                GpuPolicy.FirstAvailable => SelectFirstAvailable(devices, totalDevices),
                GpuPolicy.MostFreeMemory => SelectMostFreeMemory(devices, totalDevices),
                GpuPolicy.RoundRobin => SelectRoundRobin(devices, totalDevices),
                GpuPolicy.SpecificDevice => SelectSpecificDevice(devices, totalDevices, explicitDeviceId),
                _ => GpuSelectionResult.Failure(
                    policy,
                    $"Unknown policy: {policy}",
                    totalDevices,
                    0)
            };
        }

        /// <summary>
        /// Select the first available device (deterministic order).
        /// </summary>
        private GpuSelectionResult SelectFirstAvailable(
            IReadOnlyList<GpuDeviceInfo> devices,
            long totalDevices)
        {
            // Sort by device ID to ensure deterministic ordering
            var orderedDevices = devices.OrderBy(d => d.DeviceId).ToList();
            var availableDevices = orderedDevices
                .Where(d => d.AvailabilityState == GpuAvailabilityState.Available)
                .ToList();

            if (availableDevices.Count == 0)
            {
                var reason = totalDevices == 0
                    ? "FirstAvailable: No devices available (total devices: 0)"
                    : $"FirstAvailable: No devices available (checked {totalDevices} device(s), all unavailable)";

                _logger.Debug(reason);
                return GpuSelectionResult.Failure(
                    GpuPolicy.FirstAvailable,
                    reason,
                    totalDevices,
                    0);
            }

            var selected = availableDevices.First();
            var resultReason = $"FirstAvailable: Selected device {selected.DeviceId} ({selected.DeviceName})";
            _logger.Debug(resultReason);

            return GpuSelectionResult.Success(
                selected,
                GpuPolicy.FirstAvailable,
                resultReason,
                totalDevices,
                availableDevices.Count);
        }

        /// <summary>
        /// Select the device with the most free memory.
        /// Falls back to first-available if no memory info is available.
        /// </summary>
        private GpuSelectionResult SelectMostFreeMemory(
            IReadOnlyList<GpuDeviceInfo> devices,
            long totalDevices)
        {
            // Sort by device ID to ensure deterministic ordering for ties
            var orderedDevices = devices.OrderBy(d => d.DeviceId).ToList();
            var availableDevices = orderedDevices
                .Where(d => d.AvailabilityState == GpuAvailabilityState.Available)
                .ToList();

            if (availableDevices.Count == 0)
            {
                var reason = totalDevices == 0
                    ? "MostFreeMemory: No devices available (total devices: 0)"
                    : $"MostFreeMemory: No devices available (checked {totalDevices} device(s), all unavailable)";

                _logger.Debug(reason);
                return GpuSelectionResult.Failure(
                    GpuPolicy.MostFreeMemory,
                    reason,
                    totalDevices,
                    0);
            }

            // Filter devices with known memory information
            var devicesWithMemory = availableDevices
                .Where(d => d.MemoryInfo.State == GpuAvailabilityState.Available)
                .ToList();

            if (devicesWithMemory.Count == 0)
            {
                // No memory info available; fall back to first-available
                _logger.Debug("MostFreeMemory: No memory info available; falling back to FirstAvailable");
                var fallbackResult = SelectFirstAvailable(availableDevices, availableDevices.Count);

                // Update reason to reflect fallback
                var fallbackReason = $"MostFreeMemory: No memory information available, fell back to FirstAvailable: {fallbackResult.Reason}";
                return GpuSelectionResult.Success(
                    fallbackResult.SelectedDevice,
                    GpuPolicy.MostFreeMemory,
                    fallbackReason,
                    totalDevices,
                    availableDevices.Count);
            }

            // Sort by free memory (descending), then by device ID (ascending) for deterministic tie-breaking
            var selected = devicesWithMemory
                .OrderByDescending(d => d.MemoryInfo.FreeBytes)
                .ThenBy(d => d.DeviceId)
                .First();

            var freeGib = selected.MemoryInfo.FreeBytes / (1024.0 * 1024 * 1024);
            var resultReason = $"MostFreeMemory: Selected device {selected.DeviceId} ({selected.DeviceName}) with {freeGib:F1} GiB free";
            _logger.Debug(resultReason);

            return GpuSelectionResult.Success(
                selected,
                GpuPolicy.MostFreeMemory,
                resultReason,
                totalDevices,
                availableDevices.Count);
        }

        /// <summary>
        /// Select a device by explicit device ID.
        /// No fuzzy matching; exact match only.
        /// </summary>
        private GpuSelectionResult SelectSpecificDevice(
            IReadOnlyList<GpuDeviceInfo> devices,
            long totalDevices,
            int? explicitDeviceId)
        {
            if (!explicitDeviceId.HasValue)
            {
                var reason = "SpecificDevice: No device ID specified";
                _logger.Warn(reason);
                return GpuSelectionResult.Failure(
                    GpuPolicy.SpecificDevice,
                    reason,
                    totalDevices,
                    0);
            }

            var requestedId = explicitDeviceId.Value;

            // Find exact match
            var matching = devices.FirstOrDefault(d => d.DeviceId == requestedId);
            if (matching == null)
            {
                var reason = $"SpecificDevice: Device {requestedId} not found (available: {string.Join(", ", devices.Select(d => d.DeviceId))})";
                _logger.Warn(reason);
                return GpuSelectionResult.Failure(
                    GpuPolicy.SpecificDevice,
                    reason,
                    totalDevices,
                    0);
            }

            // Check availability
            if (matching.AvailabilityState != GpuAvailabilityState.Available)
            {
                var reason = $"SpecificDevice: Device {requestedId} ({matching.DeviceName}) is {matching.AvailabilityState}";
                _logger.Warn(reason);
                return GpuSelectionResult.Failure(
                    GpuPolicy.SpecificDevice,
                    reason,
                    totalDevices,
                    0);
            }

            var resultReason = $"SpecificDevice: Selected device {matching.DeviceId} ({matching.DeviceName}) as requested";
            _logger.Debug(resultReason);

            return GpuSelectionResult.Success(
                matching,
                GpuPolicy.SpecificDevice,
                resultReason,
                totalDevices,
                1);
        }

        private GpuSelectionResult SelectRoundRobin(
            IReadOnlyList<GpuDeviceInfo> devices,
            long totalDevices)
        {
            var available = devices
                .Where(d => d.AvailabilityState == GpuAvailabilityState.Available)
                .OrderBy(d => d.DeviceId)
                .ToList();

            if (available.Count == 0)
            {
                return GpuSelectionResult.Failure(
                    GpuPolicy.RoundRobin,
                    "RoundRobin: No devices available",
                    totalDevices,
                    0);
            }

            GpuDeviceInfo selected;
            lock (_roundRobinLock)
            {
                var index = (int)(_roundRobinIndex % available.Count);
                _roundRobinIndex = (_roundRobinIndex + 1) & long.MaxValue;
                selected = available[index];
            }

            return GpuSelectionResult.Success(
                selected,
                GpuPolicy.RoundRobin,
                $"RoundRobin: Selected device {selected.DeviceId} ({selected.DeviceName})",
                totalDevices,
                available.Count);
        }
    }
}
