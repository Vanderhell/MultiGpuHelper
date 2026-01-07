using MultiGpuHelper.Dispatching;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;
using MultiGpuHelper.Utilities;
using MultiGpuHelper.Logging;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== MultiGpuHelper Sample ===\n");

        // Example 1: Auto-detect and configure budgets
        Console.WriteLine("Example 1: Auto-detect GPUs");
        await AutoDetectExample();

        Console.WriteLine("\n" + new string('-', 50) + "\n");

        // Example 2: Manual registration
        Console.WriteLine("Example 2: Manual GPU registration");
        ManualRegistrationExample();

        Console.WriteLine("\n" + new string('-', 50) + "\n");

        // Example 3: Dispatching work
        Console.WriteLine("Example 3: Dispatching work");
        await DispatchingExample();
    }

    static async Task AutoDetectExample()
    {
        var manager = new GpuManager();

        try
        {
            // Try to probe for GPUs
            await manager.InitializeFromProbeAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Note: GPU probe failed (expected if NVIDIA drivers not installed): {ex.Message}");
            Console.WriteLine("Will use manual registration instead.\n");

            // Fallback: manually register some test devices
            manager.AddDevice(new GpuDevice
            {
                DeviceId = 0,
                Name = "NVIDIA RTX 4090 (Simulated)",
                TotalVramBytes = Size.GiB(24),
                FreeVramBytes = Size.GiB(20),
                IsEnabled = true,
                MaxConcurrentJobs = 2
            });

            manager.AddDevice(new GpuDevice
            {
                DeviceId = 1,
                Name = "NVIDIA RTX 4080 (Simulated)",
                TotalVramBytes = Size.GiB(16),
                FreeVramBytes = Size.GiB(14),
                IsEnabled = true,
                MaxConcurrentJobs = 1
            });
        }

        // Configure per-device budgets
        foreach (var device in manager.Devices)
        {
            device.VramBudget.LimitBytes = (long)(device.TotalVramBytes * 0.9);
            Console.WriteLine($"Device {device.DeviceId}: {device.Name}");
            Console.WriteLine($"  Total VRAM: {Size.FormatBytes(device.TotalVramBytes)}");
            Console.WriteLine($"  Budget: {Size.FormatBytes(device.VramBudget.LimitBytes ?? 0)}");
        }
    }

    static void ManualRegistrationExample()
    {
        var builder = new GpuRegistrationBuilder();

        builder
            .AddDevice(0, "NVIDIA RTX 4090", Size.GiB(24))
            .ConfigureDevice(0, budgetBytes: Size.GiB(20), maxConcurrentJobs: 2)
            .AddDevice(1, "NVIDIA RTX 4080", Size.GiB(16))
            .ConfigureDevice(1, budgetBytes: Size.GiB(14), maxConcurrentJobs: 1);

        var manager = builder.Build();

        Console.WriteLine($"Registered {manager.Devices.Count} devices:");
        foreach (var device in manager.Devices)
        {
            Console.WriteLine($"  {device.DeviceId}: {device.Name} - {Size.FormatBytes(device.TotalVramBytes)}");
        }
    }

    static async Task DispatchingExample()
    {
        // Create manager with simulated devices
        var builder = new GpuRegistrationBuilder();
        builder
            .AddDevice(0, "GPU 0", Size.GiB(24))
            .ConfigureDevice(0, budgetBytes: Size.GiB(20), maxConcurrentJobs: 2)
            .AddDevice(1, "GPU 1", Size.GiB(16))
            .ConfigureDevice(1, budgetBytes: Size.GiB(14), maxConcurrentJobs: 1);

        var manager = builder.Build();
        var dispatcher = new GpuDispatcher(manager);

        // Simulate some work
        Console.WriteLine("Dispatching 4 work items across 2 GPUs:\n");

        var tasks = new Task[4];
        for (int i = 0; i < 4; i++)
        {
            int workId = i;
            tasks[i] = dispatcher.RunAsync(
                async deviceId =>
                {
                    Console.WriteLine($"[Work {workId}] Started on GPU {deviceId}");
                    await Task.Delay(1000);
                    Console.WriteLine($"[Work {workId}] Completed on GPU {deviceId}");
                },
                GpuPolicy.RoundRobin,
                new GpuWorkItem
                {
                    RequestedVramBytes = Size.MiB(500),
                    Tag = $"Work{workId}"
                }
            );
        }

        await Task.WhenAll(tasks);
        Console.WriteLine("\nAll work items completed!");
    }
}
