using MultiGpuHelper.Management;
using MultiGpuHelper.Models;
using MultiGpuHelper.Utilities;
using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== MultiGpuHelper .NET Framework Sample ===\n");

        // Manual GPU registration example (for .NET Framework)
        var builder = new GpuRegistrationBuilder();

        builder
            .AddDevice(0, "NVIDIA RTX 4090", Size.GiB(24))
            .ConfigureDevice(0, budgetBytes: Size.GiB(20), maxConcurrentJobs: 2)
            .AddDevice(1, "NVIDIA RTX 4080", Size.GiB(16))
            .ConfigureDevice(1, budgetBytes: Size.GiB(14), maxConcurrentJobs: 1);

        var manager = builder.Build();

        Console.WriteLine("Registered Devices:");
        foreach (var device in manager.Devices)
        {
            Console.WriteLine($"  Device {device.DeviceId}: {device.Name}");
            Console.WriteLine($"    Total VRAM: {Size.FormatBytes(device.TotalVramBytes)}");
            Console.WriteLine($"    Budget: {Size.FormatBytes(device.VramBudget.LimitBytes ?? 0)}");
            Console.WriteLine($"    Max Concurrent: {device.MaxConcurrentJobs}");
        }

        // Select device using RoundRobin policy
        Console.WriteLine("\nDevice Selection Examples:");
        for (int i = 0; i < 3; i++)
        {
            var device = manager.SelectDevice(GpuPolicy.RoundRobin);
            Console.WriteLine($"  RoundRobin selection {i}: Device {device.DeviceId}");
        }

        // Select device using MostFreeVram policy
        var mostFreeDevice = manager.SelectDevice(GpuPolicy.MostFreeVram);
        Console.WriteLine($"  MostFreeVram selection: Device {mostFreeDevice.DeviceId}");

        // Select specific device
        var specificDevice = manager.SelectDevice(GpuPolicy.SpecificDevice, 1);
        Console.WriteLine($"  SpecificDevice selection (ID=1): Device {specificDevice.DeviceId}");

        Console.WriteLine("\nSample completed!");
    }
}
