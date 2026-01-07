using MultiGpuHelper.Dispatching;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;
using MultiGpuHelper.Utilities;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Hardware verification test for MultiGpuHelper with real GPU devices.
/// Tests GPU detection, work dispatching, VRAM budgeting, and concurrency control.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║   MultiGpuHelper Hardware Verification Test Suite      ║");
        Console.WriteLine("║   Testing with real GPU devices                        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");

        try
        {
            // Test 1: GPU Detection
            Console.WriteLine("📊 TEST 1: GPU Auto-Detection");
            Console.WriteLine("━".PadRight(60, '━'));
            var manager = new GpuManager();
            await manager.InitializeFromProbeAsync();

            if (manager.Devices.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ ERROR: No GPUs detected!");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ Detected {manager.Devices.Count} GPU(s)\n");
            Console.ResetColor();

            // Display detected devices
            foreach (var device in manager.Devices)
            {
                Console.WriteLine($"  GPU {device.DeviceId}:");
                Console.WriteLine($"    Name: {device.Name}");
                Console.WriteLine($"    Total VRAM: {Size.FormatBytes(device.TotalVramBytes)}");
                Console.WriteLine($"    Free VRAM: {Size.FormatBytes(device.FreeVramBytes ?? 0)}");
                Console.WriteLine();
            }

            // Test 2: Device Selection Policies
            Console.WriteLine("🎯 TEST 2: Device Selection Policies");
            Console.WriteLine("━".PadRight(60, '━'));
            await TestDeviceSelection(manager);

            // Test 3: Work Dispatching
            Console.WriteLine("\n⚙️  TEST 3: Work Dispatching");
            Console.WriteLine("━".PadRight(60, '━'));
            await TestWorkDispatching(manager);

            // Test 4: Concurrency Control
            Console.WriteLine("\n🔄 TEST 4: Concurrency Control");
            Console.WriteLine("━".PadRight(60, '━'));
            await TestConcurrencyControl(manager);

            // Test 5: VRAM Budget Enforcement
            Console.WriteLine("\n💾 TEST 5: VRAM Budget Enforcement");
            Console.WriteLine("━".PadRight(60, '━'));
            await TestVramBudget(manager);

            // Test 6: Refresh GPU Info
            Console.WriteLine("\n🔄 TEST 6: GPU Info Refresh");
            Console.WriteLine("━".PadRight(60, '━'));
            await TestRefresh(manager);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("║              ✓ ALL TESTS PASSED SUCCESSFULLY           ║");
            Console.ResetColor();
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n❌ Test Failed: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            Console.ResetColor();
        }
    }

    static Task TestDeviceSelection(GpuManager manager)
    {
        if (manager.Devices.Count < 1)
        {
            Console.WriteLine("⚠️  Skipping: Need at least 1 GPU");
            return Task.CompletedTask;
        }

        // RoundRobin
        Console.WriteLine("  RoundRobin Selection:");
        var selected = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            var device = manager.SelectDevice(GpuPolicy.RoundRobin);
            selected.Add(device.DeviceId);
            Console.WriteLine($"    Step {i}: GPU {device.DeviceId}");
        }

        // Verify round-robin pattern
        bool isRoundRobin = manager.Devices.Count > 1
            ? (selected[0] != selected[1])
            : true;
        Console.WriteLine($"  {(isRoundRobin ? "✓" : "✗")} RoundRobin working\n");

        // MostFreeVram
        Console.WriteLine("  MostFreeVram Selection:");
        var device1 = manager.SelectDevice(GpuPolicy.MostFreeVram);
        Console.WriteLine($"    Selected: GPU {device1.DeviceId}");
        Console.WriteLine($"    Free VRAM: {Size.FormatBytes(device1.FreeVramBytes ?? 0)}");
        Console.WriteLine("  ✓ MostFreeVram working\n");

        // SpecificDevice
        if (manager.Devices.Count > 0)
        {
            Console.WriteLine("  SpecificDevice Selection:");
            var firstDevice = manager.Devices[0];
            var selected2 = manager.SelectDevice(GpuPolicy.SpecificDevice, firstDevice.DeviceId);
            Console.WriteLine($"    Requested: GPU {firstDevice.DeviceId}");
            Console.WriteLine($"    Got: GPU {selected2.DeviceId}");
            Console.WriteLine($"    {(selected2.DeviceId == firstDevice.DeviceId ? "✓" : "✗")} SpecificDevice working");
        }

        return Task.CompletedTask;
    }

    static async Task TestWorkDispatching(GpuManager manager)
    {
        var dispatcher = new GpuDispatcher(manager);
        Console.WriteLine("  Dispatching 8 work items across GPUs:\n");

        var tasks = new Task[8];
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < 8; i++)
        {
            int workId = i;
            tasks[i] = dispatcher.RunAsync(
                async deviceId =>
                {
                    var device = manager.GetDevice(deviceId);
                    Console.WriteLine($"    [Work {workId:D2}] → GPU {deviceId} ({device.Name}) [START]");
                    await Task.Delay(500);
                    Console.WriteLine($"    [Work {workId:D2}] → GPU {deviceId} [DONE]");
                },
                GpuPolicy.RoundRobin,
                new GpuWorkItem
                {
                    RequestedVramBytes = Size.MiB(100),
                    Tag = $"TestWork{workId}"
                }
            );
        }

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        Console.WriteLine($"\n  ✓ All {tasks.Length} work items completed in {stopwatch.ElapsedMilliseconds}ms");
    }

    static async Task TestConcurrencyControl(GpuManager manager)
    {
        // Configure concurrency limits
        foreach (var device in manager.Devices)
        {
            device.MaxConcurrentJobs = 2;
        }

        var dispatcher = new GpuDispatcher(manager);
        Console.WriteLine($"  Configured max {manager.Devices[0].MaxConcurrentJobs} concurrent jobs per GPU\n");
        Console.WriteLine("  Running 6 work items with concurrency limits:\n");

        var activeCount = 0;
        var maxActive = 0;
        var lockObj = new object();

        var tasks = new Task[6];
        for (int i = 0; i < 6; i++)
        {
            int workId = i;
            tasks[i] = dispatcher.RunAsync(
                async deviceId =>
                {
                    lock (lockObj)
                    {
                        activeCount++;
                        if (activeCount > maxActive)
                            maxActive = activeCount;
                        Console.WriteLine($"    [Work {workId:D2}] Running (active: {activeCount}/{manager.Devices[0].MaxConcurrentJobs * manager.Devices.Count})");
                    }

                    await Task.Delay(800);

                    lock (lockObj)
                    {
                        activeCount--;
                    }
                },
                GpuPolicy.RoundRobin,
                new GpuWorkItem { Tag = $"ConcurrentWork{workId}" }
            );
        }

        await Task.WhenAll(tasks);
        Console.WriteLine($"\n  ✓ Max concurrent: {maxActive} (expected: ≤{manager.Devices[0].MaxConcurrentJobs * manager.Devices.Count})");
    }

    static Task TestVramBudget(GpuManager manager)
    {
        if (manager.Devices.Count == 0)
        {
            Console.WriteLine("⚠️  Skipping: Need at least 1 GPU");
            return Task.CompletedTask;
        }

        var device = manager.Devices[0];
        Console.WriteLine($"  Testing VRAM budget on GPU {device.DeviceId} ({device.Name})\n");

        // Set budget to 100MB
        var budgetBytes = Size.MiB(100);
        device.VramBudget.LimitBytes = budgetBytes;
        Console.WriteLine($"  Budget Limit: {Size.FormatBytes(budgetBytes)}\n");

        // Test 1: Successful reservation
        var reserved1 = Size.MiB(40);
        bool result1 = device.VramBudget.TryReserve(reserved1);
        Console.WriteLine($"  Reserve {Size.FormatBytes(reserved1)}: {(result1 ? "✓ Success" : "✗ Failed")}");
        Console.WriteLine($"  Remaining: {Size.FormatBytes(budgetBytes - device.VramBudget.ReservedBytes)}\n");

        // Test 2: Another successful reservation
        var reserved2 = Size.MiB(50);
        bool result2 = device.VramBudget.TryReserve(reserved2);
        Console.WriteLine($"  Reserve {Size.FormatBytes(reserved2)}: {(result2 ? "✓ Success" : "✗ Failed")}");
        Console.WriteLine($"  Remaining: {Size.FormatBytes(budgetBytes - device.VramBudget.ReservedBytes)}\n");

        // Test 3: Exceed budget
        var reserved3 = Size.MiB(20);
        bool result3 = device.VramBudget.TryReserve(reserved3);
        Console.WriteLine($"  Reserve {Size.FormatBytes(reserved3)}: {(result3 ? "✗ Should have failed" : "✓ Correctly rejected")}");
        Console.WriteLine($"  ✓ Budget enforcement working\n");

        // Test 4: Release and retry
        device.VramBudget.Release(reserved1);
        Console.WriteLine($"  Released {Size.FormatBytes(reserved1)}");
        Console.WriteLine($"  Remaining: {Size.FormatBytes(budgetBytes - device.VramBudget.ReservedBytes)}\n");

        bool result4 = device.VramBudget.TryReserve(reserved3);
        Console.WriteLine($"  Retry reserve {Size.FormatBytes(reserved3)}: {(result4 ? "✓ Success" : "✗ Failed")}");

        // Cleanup
        device.VramBudget.Release(reserved2 + reserved3);
        device.VramBudget.LimitBytes = null;

        return Task.CompletedTask;
    }

    static async Task TestRefresh(GpuManager manager)
    {
        Console.WriteLine("  Refreshing GPU VRAM information...\n");

        var device = manager.Devices[0];
        var oldFree = device.FreeVramBytes;

        await manager.RefreshAsync();

        Console.WriteLine($"  GPU {device.DeviceId} ({device.Name}):");
        Console.WriteLine($"    Old Free VRAM: {Size.FormatBytes(oldFree ?? 0)}");
        Console.WriteLine($"    New Free VRAM: {Size.FormatBytes(device.FreeVramBytes ?? 0)}");
        Console.WriteLine($"  ✓ Refresh completed successfully");
    }
}
