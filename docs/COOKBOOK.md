# Cookbook

## List available GPUs

Goal: inspect NVIDIA devices visible to `nvidia-smi`.

```csharp
var devices = await new NvidiaBackend().DetectDevicesAsync();
foreach (var device in devices) Console.WriteLine(device.DeviceName);
```

What happens: the backend executes and parses `nvidia-smi`. Limitation: an empty list does not distinguish every failure reason.

## Prefer the most free memory

```csharp
var result = new GpuSelectionEngine().SelectDevice(devices, GpuPolicy.MostFreeMemory);
```

What happens: only available devices with available memory measurements are compared; otherwise selection falls back to first available. Limitation: measurements may become stale immediately.

## Round-robin

```csharp
var engine = new GpuSelectionEngine();
var first = engine.SelectDevice(devices, GpuPolicy.RoundRobin);
var second = engine.SelectDevice(devices, GpuPolicy.RoundRobin);
```

What happens: consecutive calls on the same engine rotate through available devices ordered by ID. Limitation: a new engine starts a new sequence.

## Target a specific GPU

```csharp
var result = engine.SelectDevice(devices, GpuPolicy.SpecificDevice, explicitDeviceId: 2);
```

What happens: only the exact available ID succeeds. Limitation: IDs are backend-local.

## Limit concurrent jobs and reserve VRAM

```csharp
device.MaxConcurrentJobs = 1;
device.VramBudget.LimitBytes = 4L * 1024 * 1024 * 1024;
var item = new GpuWorkItem { RequestedVramBytes = 1024 * 1024 * 1024 };
await dispatcher.RunAsync((id, token) => WorkAsync(id, token), GpuPolicy.FirstAvailable, item);
```

What happens: the callback waits for a device slot, then makes a soft reservation for its execution. Limitation: this does not allocate physical VRAM.
