# MultiGpuHelper

MultiGpuHelper is a .NET Standard 2.0 library for discovering GPU adapters, selecting a device, and limiting concurrent GPU work. Discovery is best-effort: each backend reports only data available from its underlying operating-system, command-line, or driver API.

## What it does

- Discovers devices through `nvidia-smi`, the CUDA Driver API, ROCm `rocminfo`, or Windows WMI.
- Represents discovery results as immutable `GpuDeviceInfo` snapshots.
- Selects devices using first-available, most-free-memory, round-robin, or specific-device policies.
- Dispatches callbacks with per-device concurrency limits and soft VRAM reservations.

## What it does not do

The library does not execute CUDA, ROCm, DirectX, or machine-learning kernels. It does not allocate physical VRAM, guarantee that reported free memory remains current, or merge records from different discovery backends.

## Supported platforms and backends

| Backend | Mechanism | Platform requirement | Memory data |
|---|---|---|---|
| `NvidiaBackend` | `nvidia-smi` | NVIDIA tools on `PATH` | Total and free VRAM when the command reports both |
| `CudaBackend` | CUDA Driver API | Windows NVIDIA driver | Total VRAM; free VRAM unknown |
| `RocmBackend` | `rocminfo` | ROCm tools on `PATH` | Free VRAM unknown |
| `WmiBackend` | `Win32_VideoController` | Windows with WMI | VRAM intentionally unknown |

ROCm behavior has unit coverage but has not been maintainer-verified on AMD hardware. See [backend details](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/BACKENDS.md).

## Installation

```bash
dotnet add package MultiGpuHelper --version 1.1.0
```

## Quick start

```csharp
using MultiGpuHelper.Backends;
using MultiGpuHelper.Models;
using MultiGpuHelper.Selection;

var backend = new NvidiaBackend();
var devices = await backend.DetectDevicesAsync();
var selection = new GpuSelectionEngine().SelectDevice(
    devices,
    GpuPolicy.MostFreeMemory);

if (selection.IsSuccess)
    Console.WriteLine($"Selected {selection.SelectedDevice.DeviceName}");
else
    Console.WriteLine(selection.Reason);
```

## Device discovery

Backends implement `IGpuBackend`. An empty result means the backend was unavailable, failed, or detected no devices; use `IsAvailableAsync` when that distinction matters. `GpuDeviceInfo.Backend` identifies the discovery mechanism and `Vendor` identifies the hardware vendor.

## Device selection

`GpuSelectionEngine` accepts immutable discovery snapshots. `FirstAvailable`, `MostFreeMemory`, `RoundRobin`, and `SpecificDevice` have unique values and behavior. Unknown memory is represented by `GpuMemoryInfo.State`, not by treating zero as measured free VRAM.

## Dispatching work

`GpuDispatcher` operates on mutable `GpuDevice` scheduling registrations managed by `GpuManager`. Its cancellation-aware overload accepts `Func<int, CancellationToken, Task<T>>`. `GpuWorkItem.TimeoutMs` covers waiting and callback execution, but callback cancellation is cooperative.

## Error handling

Selection failures from `GpuManager` throw `GpuSelectionException`; budget rejection throws `GpuBudgetExceededException`; caller cancellation and timeouts surface as `OperationCanceledException`. Callback exceptions propagate unchanged. See [error handling](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/ERROR-HANDLING.md).

## Thread safety

Concurrent manager selection and dispatcher calls are synchronized. Objects returned by `GpuManager.Devices` remain mutable; do not change device configuration while work is being dispatched. See [threading semantics](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/THREADING.md).

## Limitations

- Device IDs are backend-local ordinals, not persistent physical identifiers.
- Results from multiple backends are not deduplicated.
- WMI VRAM is unknown because `AdapterRAM` is not authoritative for modern adapters.
- Soft VRAM reservations do not allocate or measure hardware memory.
- Hardware-dependent discovery varies with installed tools, drivers, and permissions.

## Documentation

- [Quick start](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/QUICKSTART.md)
- [Cookbook](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/COOKBOOK.md)
- [Backends](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/BACKENDS.md)
- [Error handling](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/ERROR-HANDLING.md)
- [Threading](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/THREADING.md)
- [Troubleshooting](https://github.com/Vanderhell/MultiGpuHelper/blob/main/docs/TROUBLESHOOTING.md)

## Contributing / Issues

Bug reports should include MultiGpuHelper version, OS, .NET version, GPU model, driver/runtime version, backend, a minimal reproduction, and the exception with stack trace. Use the [GitHub issue tracker](https://github.com/Vanderhell/MultiGpuHelper/issues).

## License

MIT. See [LICENSE](https://github.com/Vanderhell/MultiGpuHelper/blob/main/LICENSE).
