using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using MultiGpuHelper.Abstractions;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Backends
{
    /// <summary>
    /// Windows GPU backend implementation using WMI (Windows Management Instrumentation).
    /// Detects GPUs via WMI querying Win32_VideoController for GPU adapters.
    /// Available on Windows 7+ with WMI support; returns empty list on unsupported platforms.
    /// </summary>
    public class WmiBackend : IGpuBackend
    {
        private readonly IGpuLogger _logger;

        public GpuBackendKind BackendKind => GpuBackendKind.NVIDIA; // Placeholder; WMI is vendor-agnostic

        public WmiBackend(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Detect available GPUs via WMI (Windows Management Instrumentation).
        /// Queries Win32_VideoController for GPU adapters.
        /// Returns empty list if WMI is unavailable or no GPUs found.
        /// </summary>
        public async Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync()
        {
            try
            {
                var available = await IsAvailableAsync().ConfigureAwait(false);
                if (!available)
                {
                    _logger.Debug("WMI backend not available (WMI or GPU adapters not found)");
                    return new List<GpuDeviceInfo>();
                }

                var devices = QueryGpuAdapters();

                if (devices.Count == 0)
                {
                    _logger.Debug("WMI: No GPU adapters found");
                    return new List<GpuDeviceInfo>();
                }

                _logger.Debug($"WMI: Detected {devices.Count} GPU adapter(s)");
                return devices;
            }
            catch (Exception ex)
            {
                _logger.Warn($"WMI backend detection failed: {ex.Message}");
                return new List<GpuDeviceInfo>();
            }
        }

        /// <summary>
        /// Refresh VRAM information for detected devices.
        /// Re-queries WMI and updates memory info.
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
                        // Replace old device with updated version
                        result.Add(updated);
                    }
                    else
                    {
                        // Device not found; mark with error state
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
                            device.MaxConcurrentJobs);

                        result.Add(staleDevice);
                    }
                }

                _logger.Debug("WMI backend memory refreshed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn($"WMI backend refresh failed: {ex.Message}");
                return devices.Select(d => new GpuDeviceInfo(
                    d.DeviceId,
                    d.DeviceName,
                    d.Backend,
                    GpuMemoryInfo.Error(),
                    GpuAvailabilityState.Error,
                    d.VramBudgetLimitBytes,
                    d.MaxConcurrentJobs)).ToList();
            }
        }

        /// <summary>
        /// Check if WMI backend (WMI GPU adapter query) is available on this system.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to query WMI for GPU adapters
                    using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                    {
                        var collection = searcher.Get();
                        return collection.Count > 0;
                    }
                }
                catch
                {
                    return false;
                }
            }).ConfigureAwait(false);
        }

        private List<GpuDeviceInfo> QueryGpuAdapters()
        {
            var devices = new List<GpuDeviceInfo>();
            var deviceId = 0;

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController"))
                {
                    var collection = searcher.Get();

                    // Order by device name for deterministic results
                    var orderedResults = collection.Cast<ManagementObject>()
                        .OrderBy(mo => mo["Name"]?.ToString() ?? "")
                        .ToList();

                    foreach (var videoController in orderedResults)
                    {
                        try
                        {
                            var device = ParseVideoController(videoController, deviceId);
                            if (device != null)
                            {
                                devices.Add(device);
                                deviceId++;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"Failed to parse video controller: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"WMI query failed: {ex.Message}");
            }

            return devices;
        }

        private GpuDeviceInfo ParseVideoController(ManagementObject videoController, int logicalId)
        {
            var name = videoController["Name"]?.ToString() ?? "Unknown GPU";
            var adapterRAM = videoController["AdapterRAM"];
            var pnpDeviceId = videoController["PNPDeviceID"]?.ToString() ?? "";

            // Parse total memory
            long totalBytes = 0;
            if (adapterRAM != null && long.TryParse(adapterRAM.ToString(), out var ramBytes))
            {
                totalBytes = ramBytes;
            }

            // Free memory is not reliably available from WMI; mark as unknown
            var memoryInfo = totalBytes > 0
                ? new GpuMemoryInfo(totalBytes, 0, GpuAvailabilityState.Unavailable) // Total known, free unknown
                : GpuMemoryInfo.Unavailable(); // Total unknown

            // Detect vendor using truthful best-effort detection from available WMI data
            var vendor = DetectVendor(name, pnpDeviceId);

            return new GpuDeviceInfo(
                logicalId,
                name,
                vendor,
                memoryInfo,
                GpuAvailabilityState.Available,
                vramBudgetLimitBytes: 0,
                maxConcurrentJobs: 1);
        }

        /// <summary>
        /// Detect GPU vendor from WMI device name and PNP device ID.
        /// Uses truthful detection based on available WMI data; returns Unknown if vendor cannot be confidently determined.
        /// </summary>
        private GpuBackendKind DetectVendor(string deviceName, string pnpDeviceId)
        {
            if (string.IsNullOrEmpty(deviceName))
                return GpuBackendKind.Unknown;

            var nameLower = deviceName.ToLowerInvariant();
            var pnpLower = pnpDeviceId.ToLowerInvariant();

            // Check PNP device ID for vendor codes (VEN_xxxx format)
            // Common codes: VEN_10DE (NVIDIA), VEN_1002 (AMD), VEN_8086 (Intel)
            if (pnpLower.Contains("ven_10de"))
                return GpuBackendKind.NVIDIA;
            if (pnpLower.Contains("ven_1002"))
                return GpuBackendKind.AMD;
            if (pnpLower.Contains("ven_8086"))
                return GpuBackendKind.Intel;

            // Fallback to name-based detection (case-insensitive)

            // NVIDIA detection
            if (nameLower.Contains("nvidia") || nameLower.Contains("geforce") ||
                nameLower.Contains("quadro") || nameLower.Contains("tesla") ||
                nameLower.Contains("rtx") || nameLower.Contains("gtx"))
                return GpuBackendKind.NVIDIA;

            // AMD detection
            if (nameLower.Contains("amd") || nameLower.Contains("radeon") ||
                nameLower.Contains("rdna") || nameLower.Contains("epyc"))
                return GpuBackendKind.AMD;

            // Intel detection
            if (nameLower.Contains("intel") || nameLower.Contains("arc") ||
                nameLower.Contains("iris") || nameLower.Contains("uhd") ||
                nameLower.Contains("hd graphics") || nameLower.Contains("hd_graphics"))
                return GpuBackendKind.Intel;

            // Unable to determine vendor from available WMI data
            return GpuBackendKind.Unknown;
        }
    }
}
