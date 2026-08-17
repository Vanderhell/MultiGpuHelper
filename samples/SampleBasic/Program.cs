using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Models;
using MultiGpuHelper.Selection;

namespace MultiGpuHelper.SampleBasic
{
    /// <summary>
    /// Basic sample demonstrating MultiGpuHelper 1.1.0 selection policies.
    ///
    /// This sample shows:
    /// 1. Device enumeration via NVIDIA backend
            /// 2. Device selection using all four policies
    /// 3. Selection result interpretation
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            try
            {
                await RunSample();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static async Task RunSample()
        {
            Console.WriteLine("=== MultiGpuHelper 1.1.0 Sample ===\n");

            // Step 1: Detect GPUs
            Console.WriteLine("Step 1: Detecting GPUs...\n");
            var backend = new NvidiaBackend();
            var devices = await backend.DetectDevicesAsync();

            if (devices.Count == 0)
            {
                Console.WriteLine("No GPUs detected. Please ensure:");
                Console.WriteLine("  - nvidia-smi is installed (CUDA Toolkit or driver)");
                Console.WriteLine("  - nvidia-smi is in PATH");
                Console.WriteLine("  - At least one NVIDIA GPU is present");
                return;
            }

            // Print detected devices
            Console.WriteLine($"Found {devices.Count} GPU(s):\n");
            foreach (var device in devices)
            {
                var totalGiB = device.MemoryInfo.TotalBytes / (1024.0 * 1024 * 1024);
                var freeGiB = device.MemoryInfo.FreeBytes / (1024.0 * 1024 * 1024);
                var state = device.MemoryInfo.State;

                Console.WriteLine($"  Device {device.DeviceId}: {device.DeviceName}");
                Console.WriteLine($"    Total: {totalGiB:F1} GiB, Free: {freeGiB:F1} GiB ({state})");
            }
            Console.WriteLine();

            // Step 2: Test all four selection policies
            Console.WriteLine("Step 2: Testing selection policies...\n");
            var engine = new GpuSelectionEngine();

            TestPolicy(engine, devices, GpuPolicy.FirstAvailable, null);
            TestPolicy(engine, devices, GpuPolicy.MostFreeMemory, null);
            TestPolicy(engine, devices, GpuPolicy.RoundRobin, null);
            TestPolicy(engine, devices, GpuPolicy.SpecificDevice, devices[0].DeviceId);

            // Step 3: Test explicit selection with invalid ID
            if (devices.Count > 0)
            {
                var invalidId = devices[devices.Count - 1].DeviceId + 1;
                Console.WriteLine($"\nTesting SpecificDevice with invalid device {invalidId}:");
                TestPolicy(engine, devices, GpuPolicy.SpecificDevice, invalidId);
            }

            Console.WriteLine("\n=== Sample Complete ===");
        }

        static void TestPolicy(
            GpuSelectionEngine engine,
            IReadOnlyList<GpuDeviceInfo> devices,
            GpuPolicy policy,
            int? explicitDeviceId)
        {
            var result = engine.SelectDevice(devices, policy, explicitDeviceId);

            Console.WriteLine($"{policy}:");
            if (result.IsSuccess)
            {
                Console.WriteLine($"  ✓ {result.Reason}");
                Console.WriteLine($"    Total devices: {result.TotalDevices}, Available: {result.AvailableDevices}");
            }
            else
            {
                Console.WriteLine($"  ✗ {result.Reason}");
            }
            Console.WriteLine();
        }
    }
}
