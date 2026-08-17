using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// AMD ROCm GPU backend implementation using rocminfo command-line tool.
    /// Detects and enumerates AMD GPUs via ROCm on Linux/Windows (WSL).
    /// Gracefully handles unavailable ROCm installations.
    /// </summary>
    public class RocmBackend : IGpuBackend
    {
        private readonly IGpuLogger _logger;
        private static bool _rocminfoAvailable = false;
        private static bool _rocminfoChecked = false;

        public GpuBackendKind BackendKind => GpuBackendKind.Rocm;

        public RocmBackend(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Detect available AMD GPUs via rocminfo.
        /// Returns empty list if rocminfo is unavailable or no devices found.
        /// </summary>
        public async Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync()
        {
            try
            {
                var available = await IsAvailableAsync().ConfigureAwait(false);
                if (!available)
                {
                    _logger.Debug("ROCm backend not available (rocminfo not found)");
                    return new List<GpuDeviceInfo>();
                }

                var output = await RunRocminfoAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(output))
                {
                    _logger.Warn("rocminfo returned empty output");
                    return new List<GpuDeviceInfo>();
                }

                return ParseRocminfoOutput(output);
            }
            catch (Exception ex)
            {
                _logger.Warn($"ROCm backend detection failed: {ex.Message}");
                return new List<GpuDeviceInfo>();
            }
        }

        /// <summary>
        /// Refresh VRAM information for detected devices.
        /// Re-probes rocminfo and updates memory info.
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

                _logger.Debug("ROCm backend memory refreshed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn($"ROCm backend refresh failed: {ex.Message}");
                return new List<GpuDeviceInfo>();
            }
        }

        /// <summary>
        /// Check if ROCm backend (rocminfo) is available on this system.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            if (_rocminfoChecked)
                return _rocminfoAvailable;

            try
            {
                var output = await RunRocminfoAsync("--version").ConfigureAwait(false);
                _rocminfoAvailable = !string.IsNullOrEmpty(output);
            }
            catch
            {
                _rocminfoAvailable = false;
            }

            _rocminfoChecked = true;
            return _rocminfoAvailable;
        }

        /// <summary>
        /// Run rocminfo command to enumerate or query AMD GPUs.
        /// </summary>
        private async Task<string> RunRocminfoAsync(string arguments = "")
        {
            var psi = new ProcessStartInfo
            {
                FileName = "rocminfo",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                try
                {
                    if (!process.Start())
                        throw new InvalidOperationException("Failed to start rocminfo.");
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    var exited = await Task.Run(() => process.WaitForExit(5000)).ConfigureAwait(false);
                    if (!exited)
                    {
                        try { process.Kill(); } catch (InvalidOperationException) { }
                        await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                        throw new TimeoutException("rocminfo did not respond within 5 seconds.");
                    }
                    var output = await outputTask.ConfigureAwait(false);
                    var error = await errorTask.ConfigureAwait(false);
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException($"rocminfo exited with code {process.ExitCode}: {error.Trim()}");
                    return output;
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Failed to execute rocminfo: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Parse rocminfo output to extract GPU device information.
        /// </summary>
        internal List<GpuDeviceInfo> ParseRocminfoOutput(string output)
        {
            var devices = new List<GpuDeviceInfo>();
            if (string.IsNullOrWhiteSpace(output))
                return devices;

            var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            int? currentAgentId = null;
            string currentDeviceName = null;
            bool currentAgentIsGpu = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("Agent ", StringComparison.OrdinalIgnoreCase))
                {
                    AddAgentIfGpu(devices, currentAgentId, currentDeviceName, currentAgentIsGpu);

                    if (int.TryParse(trimmed.Split(' ').LastOrDefault(), out var agentId))
                    {
                        currentAgentId = agentId;
                        currentDeviceName = null;
                        currentAgentIsGpu = false;
                    }
                    else
                        currentAgentId = null;
                }
                else if (trimmed.StartsWith("Device Type:", StringComparison.OrdinalIgnoreCase) && currentAgentId.HasValue)
                {
                    currentAgentIsGpu = string.Equals(
                        trimmed.Substring("Device Type:".Length).Trim(),
                        "GPU",
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (trimmed.StartsWith("Name:", StringComparison.OrdinalIgnoreCase) &&
                         currentAgentId.HasValue && currentDeviceName == null)
                {
                    currentDeviceName = trimmed.Substring("Name:".Length).Trim();
                }
            }

            AddAgentIfGpu(devices, currentAgentId, currentDeviceName, currentAgentIsGpu);

            return devices.OrderBy(d => d.DeviceId).ToList();
        }

        private void AddAgentIfGpu(
            ICollection<GpuDeviceInfo> devices,
            int? agentId,
            string deviceName,
            bool isGpu)
        {
            if (agentId.HasValue && isGpu && !string.IsNullOrWhiteSpace(deviceName))
                devices.Add(CreateGpuDeviceInfo(agentId.Value, deviceName, null));
        }

        /// <summary>
        /// Create a GpuDeviceInfo object for an AMD device.
        /// </summary>
        private GpuDeviceInfo CreateGpuDeviceInfo(int deviceId, string deviceName, long? totalMemory)
        {
            var totalBytes = totalMemory ?? 0L;
            var freeBytes = 0L;
            var memoryState = GpuAvailabilityState.Unavailable; // ROCm rocminfo doesn't provide free memory

            var memoryInfo = new GpuMemoryInfo(
                totalBytes,
                freeBytes,
                memoryState);

            return new GpuDeviceInfo(
                deviceId,
                deviceName ?? $"AMD Device {deviceId}",
                GpuBackendKind.Rocm,
                memoryInfo,
                GpuAvailabilityState.Available,
                vramBudgetLimitBytes: 0,
                maxConcurrentJobs: 1,
                vendor: GpuVendor.Amd);
        }
    }
}
