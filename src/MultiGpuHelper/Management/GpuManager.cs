using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MultiGpuHelper.Exceptions;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;
using MultiGpuHelper.Probing;

namespace MultiGpuHelper.Management
{
    /// <summary>
    /// Manages GPU devices and device selection policies.
    /// Thread-safe.
    /// </summary>
    public class GpuManager
    {
        private readonly Dictionary<int, GpuDevice> _devices;
        private readonly IGpuProbeProvider _probeProvider;
        private readonly IGpuLogger _logger;
        private readonly object _lockObject = new object();
        private int _roundRobinIndex = 0;

        public GpuManager(IGpuProbeProvider probeProvider = null, IGpuLogger logger = null)
        {
            _probeProvider = probeProvider ?? new NvidiaSmiProbeProvider();
            _logger = logger ?? new NoOpLogger();
            _devices = new Dictionary<int, GpuDevice>();
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
                var probed = await _probeProvider.ProbeAsync().ConfigureAwait(false);
                lock (_lockObject)
                {
                    foreach (var device in probed)
                    {
                        if (_devices.TryGetValue(device.DeviceId, out var existing))
                        {
                            // Update VRAM info only; preserve other settings
                            existing.FreeVramBytes = device.FreeVramBytes;
                            existing.TotalVramBytes = device.TotalVramBytes;
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
                var enabledDevices = _devices.Values.Where(d => d.IsEnabled).ToList();

                if (enabledDevices.Count == 0)
                    throw new GpuSelectionException("No enabled GPU devices available.");

                switch (policy)
                {
                    case GpuPolicy.SpecificDevice:
                        if (!specificDeviceId.HasValue)
                            throw new GpuSelectionException("SpecificDevice policy requires a device ID.");

                        var device = enabledDevices.FirstOrDefault(d => d.DeviceId == specificDeviceId.Value);
                        if (device == null)
                            throw new GpuSelectionException($"GPU device {specificDeviceId} not found or disabled.");

                        return device;

                    case GpuPolicy.MostFreeVram:
                        var byFreeVram = enabledDevices
                            .Where(d => d.FreeVramBytes.HasValue)
                            .OrderByDescending(d => d.FreeVramBytes.Value)
                            .FirstOrDefault();

                        return byFreeVram ?? enabledDevices.First();

                    case GpuPolicy.RoundRobin:
                    default:
                        var selected = enabledDevices[_roundRobinIndex % enabledDevices.Count];
                        _roundRobinIndex++;
                        return selected;
                }
            }
        }

        /// <summary>
        /// Initialize devices by probing (e.g., via nvidia-smi).
        /// </summary>
        public async Task InitializeFromProbeAsync()
        {
            try
            {
                var probed = await _probeProvider.ProbeAsync().ConfigureAwait(false);
                lock (_lockObject)
                {
                    foreach (var device in probed)
                    {
                        _devices[device.DeviceId] = device;
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
