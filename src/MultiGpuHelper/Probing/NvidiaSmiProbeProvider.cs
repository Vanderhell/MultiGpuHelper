using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MultiGpuHelper.Exceptions;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Probing
{
    /// <summary>
    /// GPU probe provider using nvidia-smi command-line tool.
    /// Gracefully handles missing or erroring nvidia-smi.
    /// </summary>
    public class NvidiaSmiProbeProvider : IGpuProbeProvider
    {
        private readonly IGpuLogger _logger;

        public NvidiaSmiProbeProvider(IGpuLogger logger = null)
        {
            _logger = logger ?? new NoOpLogger();
        }

        /// <summary>
        /// Probe NVIDIA GPUs via nvidia-smi.
        /// Returns empty list if nvidia-smi is not available or fails.
        /// </summary>
        public async Task<IList<GpuDevice>> ProbeAsync()
        {
            try
            {
                var output = await RunNvidiaSmiAsync();
                if (string.IsNullOrEmpty(output))
                {
                    _logger.Warn("nvidia-smi returned empty output");
                    return new List<GpuDevice>();
                }

                return ParseNvidiaSmiOutput(output);
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to probe GPUs: {ex.Message}");
                return new List<GpuDevice>();
            }
        }

        private async Task<string> RunNvidiaSmiAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=index,name,memory.total,memory.free --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();

                // Run synchronously within async context
                var output = await Task.Run(() => process.StandardOutput.ReadToEnd()).ConfigureAwait(false);
                var error = await Task.Run(() => process.StandardError.ReadToEnd()).ConfigureAwait(false);

                process.WaitForExit(5000);

                if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
                {
                    _logger.Debug($"nvidia-smi error: {error}");
                }

                return output;
            }
        }

        private List<GpuDevice> ParseNvidiaSmiOutput(string output)
        {
            var devices = new List<GpuDevice>();
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
                    _logger.Warn($"Failed to parse GPU line: {line}. Error: {ex.Message}");
                }
            }

            return devices;
        }

        private GpuDevice ParseLine(string line)
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
            if (!long.TryParse(parts[3], out var freeMib))
                return null;

            // Convert MiB to bytes
            long totalBytes = totalMib * 1024 * 1024;
            long freeBytes = freeMib * 1024 * 1024;

            return new GpuDevice
            {
                DeviceId = deviceId,
                Name = name,
                TotalVramBytes = totalBytes,
                FreeVramBytes = freeBytes,
                IsEnabled = true,
                MaxConcurrentJobs = 1
            };
        }
    }
}
