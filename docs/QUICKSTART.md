# Quick start

## Prerequisites

.NET SDK compatible with .NET Standard 2.0 and at least one backend dependency listed in [BACKENDS.md](BACKENDS.md).

## Install

```bash
dotnet add package MultiGpuHelper --version 1.1.0
```

## Discover and inspect devices

```csharp
using MultiGpuHelper.Backends;

var backend = new NvidiaBackend();
var devices = await backend.DetectDevicesAsync();
foreach (var device in devices)
{
    Console.WriteLine($"{device.DeviceId}: {device.DeviceName}");
    Console.WriteLine($"Vendor={device.Vendor}, backend={device.Backend}");
    Console.WriteLine($"Memory state={device.MemoryInfo.State}");
}
```

## Select a device

```csharp
using MultiGpuHelper.Models;
using MultiGpuHelper.Selection;

var result = new GpuSelectionEngine().SelectDevice(devices, GpuPolicy.FirstAvailable);
if (!result.IsSuccess)
    Console.WriteLine(result.Reason);
```

## Dispatch simple work

Discovery snapshots and scheduling registrations have separate roles. Register the device configuration used for dispatch:

```csharp
using MultiGpuHelper.Dispatching;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;

var manager = new GpuManager();
manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU 0", MaxConcurrentJobs = 1 });
var dispatcher = new GpuDispatcher(manager);

var value = await dispatcher.RunAsync(
    async (deviceId, cancellationToken) =>
    {
        await Task.Delay(10, cancellationToken);
        return deviceId;
    },
    GpuPolicy.FirstAvailable);
```

## Cancellation and error handling

Pass caller cancellation through `ct`. A positive `GpuWorkItem.TimeoutMs` creates a linked token covering the queue wait and callback execution. Callbacks must observe the supplied token. Catch `OperationCanceledException`, `GpuSelectionException`, and `GpuBudgetExceededException` as appropriate; callback exceptions are not wrapped.
