using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using MultiGpuHelper.Abstractions;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Backends
{
    /// <summary>
    /// NVIDIA GPU backend implementation using nvidia-smi.
    /// Detects and probes NVIDIA GPUs on the current machine.
    /// </summary>
    public class NvidiaBackend : IGpuBackend
    {
        private readonly IGpuLogger _logger;

        public GpuBackendKind BackendKind => GpuBackendKind.NvidiaSmi;

        public NvidiaBackend(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Detect available NVIDIA GPUs via nvidia-smi.
        /// Returns empty list if nvidia-smi is unavailable or fails.
        /// </summary>
        public async Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync()
        {
            try
            {
                var available = await IsAvailableAsync().ConfigureAwait(false);
                if (!available)
                {
                    _logger.Debug("NVIDIA backend not available (nvidia-smi not found)");
                    return new List<GpuDeviceInfo>();
                }

                var output = await RunNvidiaSmiAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(output))
                {
                    _logger.Warn("nvidia-smi returned empty output");
                    return new List<GpuDeviceInfo>();
                }

                return ParseNvidiaSmiOutput(output);
            }
            catch (Exception ex)
            {
                _logger.Warn($"NVIDIA backend detection failed: {ex.Message}");
                return new List<GpuDeviceInfo>();
            }
        }

        /// <summary>
        /// Refresh VRAM information for detected devices.
        /// Re-probes nvidia-smi and updates memory info.
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
                        // Replace old device with updated version (new memory info)
                        result.Add(updated);
                    }
                    else
                    {
                        // Device not found in latest probe; keep old info with error state
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

                _logger.Debug("NVIDIA backend memory refreshed");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Warn($"NVIDIA backend refresh failed: {ex.Message}");
                // Return devices as-is with error memory state
                return devices.Select(d => new GpuDeviceInfo(
                    d.DeviceId,
                    d.DeviceName,
                    d.Backend,
                    GpuMemoryInfo.Error(),
                    GpuAvailabilityState.Error,
                    d.VramBudgetLimitBytes,
                    d.MaxConcurrentJobs,
                    d.Vendor)).ToList();
            }
        }

        /// <summary>
        /// Check if NVIDIA backend (nvidia-smi) is available on this system.
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var output = await RunNvidiaSmiAsync("--version").ConfigureAwait(false);
                return !string.IsNullOrEmpty(output);
            }
            catch
            {
                return false;
            }
        }

        private async Task<string> RunNvidiaSmiAsync(string arguments = "--query-gpu=index,name,memory.total,memory.free --format=csv,noheader,nounits")
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                if (!process.Start())
                    throw new InvalidOperationException("Failed to start nvidia-smi.");

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                var exitTask = Task.Run(() => process.WaitForExit(5000));
                var exited = await exitTask.ConfigureAwait(false);
                if (!exited)
                {
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                    throw new TimeoutException("nvidia-smi did not respond within 5 seconds");
                }

                var output = await outputTask.ConfigureAwait(false);
                var error = await errorTask.ConfigureAwait(false);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"nvidia-smi exited with code {process.ExitCode}: {error.Trim()}");

                return output;
            }
        }

        internal List<GpuDeviceInfo> ParseNvidiaSmiOutput(string output)
        {
            var devices = new List<GpuDeviceInfo>();
            var lines = output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                try
                {
                    var device = ParseLine(line);
                    if (device != null)
                    {
                        devices.Add(device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Failed to parse NVIDIA GPU line: {line}. Error: {ex.Message}");
                }
            }

            // Ensure deterministic ordering by device ID
            return devices.OrderBy(d => d.DeviceId).ToList();
        }

        private GpuDeviceInfo ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            // Split by comma, trim whitespace
            var parts = line.Split(',').Select(p => p.Trim()).ToArray();

            if (parts.Length < 4)
                return null;

            // Parse index
            if (!int.TryParse(parts[0], out var deviceId))
                return null;

            var name = parts[1];

            // Parse total memory (in MiB)
            if (!long.TryParse(parts[2], out var totalMib))
                return null;

            // Parse free memory (in MiB)
            long freeBytes = 0;
            var memoryState = GpuAvailabilityState.Available;
            if (!long.TryParse(parts[3], out var freeMib))
            {
                _logger.Debug($"Device {deviceId}: Free memory unavailable");
                memoryState = GpuAvailabilityState.Error;
            }
            else
            {
                freeBytes = freeMib * 1024 * 1024;
            }

            // Convert MiB to bytes
            long totalBytes = totalMib * 1024 * 1024;

            var memoryInfo = new GpuMemoryInfo(
                totalBytes,
                freeBytes,
                memoryState);

            return new GpuDeviceInfo(
                deviceId,
                name,
                GpuBackendKind.NvidiaSmi,
                memoryInfo,
                GpuAvailabilityState.Available,
                vramBudgetLimitBytes: 0,
                maxConcurrentJobs: 1,
                vendor: GpuVendor.Nvidia);
        }
    }
}
