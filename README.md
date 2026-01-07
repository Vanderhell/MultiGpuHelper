# MultiGpuHelper

A production-ready C# library for scheduling compute jobs across multiple GPUs. Designed for hobby projects involving AI inference, rendering, or other GPU-accelerated workloads.

## Features

- **Multi-GPU Support**: Distribute work across NVIDIA GPUs (and extensible to other vendors)
- **Device Discovery**: Automatic GPU detection via `nvidia-smi` with graceful fallback
- **Selection Policies**: Round-robin, most-available-VRAM, or explicit device targeting
- **VRAM Budgeting**: Soft-reservation system with per-device limits
- **Concurrency Control**: Per-GPU semaphores to limit parallel job execution
- **Async-First**: Built on async/await for responsive applications
- **Cross-Platform**: Targets .NET Standard 2.0 for .NET Framework and .NET Core/.NET compatibility

## Installation

### Via NuGet

```bash
dotnet add package MultiGpuHelper
```

### From Source

```bash
git clone https://github.com/Vanderhell/MultiGpuHelper.git
cd MultiGpuHelper
dotnet build
```

## Quick Start

### Auto-Detect GPUs

```csharp
using MultiGpuHelper.Management;
using MultiGpuHelper.Dispatching;
using MultiGpuHelper.Models;

var manager = new GpuManager();
await manager.InitializeFromProbeAsync(); // Detects GPUs via nvidia-smi

var dispatcher = new GpuDispatcher(manager);

// Run work on any available GPU
var result = await dispatcher.RunAsync(
    deviceId => {
        Console.WriteLine($"Running on GPU {deviceId}");
        return Task.FromResult(42);
    },
    GpuPolicy.RoundRobin
);
```

### Manual Registration

```csharp
using MultiGpuHelper.Management;
using MultiGpuHelper.Utilities;

var builder = new GpuRegistrationBuilder();
builder
    .AddDevice(0, "NVIDIA RTX 4090", Size.GiB(24))
    .ConfigureDevice(0, budgetBytes: Size.GiB(20), maxConcurrentJobs: 2)
    .AddDevice(1, "NVIDIA RTX 4080", Size.GiB(16))
    .ConfigureDevice(1, budgetBytes: Size.GiB(14), maxConcurrentJobs: 1);

var manager = builder.Build();
var dispatcher = new GpuDispatcher(manager);

// Work items will be distributed according to policy
```

## Selection Policies

- **RoundRobin**: Distributes work evenly across GPUs in sequence
- **MostFreeVram**: Always selects the GPU with the most available memory
- **SpecificDevice**: Routes work to a specific GPU by ID

## VRAM Budgeting

Set per-device VRAM limits:

```csharp
device.VramBudget.LimitBytes = Size.GiB(20);

// Try to reserve VRAM
if (!device.VramBudget.TryReserve(Size.MiB(500)))
{
    throw new GpuBudgetExceededException("Insufficient VRAM budget");
}

// Automatically released when work completes
```

## Advanced Usage

### Timeouts and Cancellation

```csharp
var workItem = new GpuWorkItem
{
    TimeoutMs = 30000, // 30-second timeout
    RequestedVramBytes = Size.MiB(512),
    Tag = "ProcessingTask"
};

await dispatcher.RunAsync(
    async deviceId => { /* work */ },
    GpuPolicy.MostFreeVram,
    workItem,
    cancellationToken
);
```

### Custom Logging

```csharp
public class ConsoleLogger : IGpuLogger
{
    public void Debug(string message) => Console.WriteLine($"[DEBUG] {message}");
    public void Info(string message) => Console.WriteLine($"[INFO] {message}");
    public void Warn(string message) => Console.WriteLine($"[WARN] {message}");
    public void Error(string message) => Console.WriteLine($"[ERROR] {message}");
}

var manager = new GpuManager(logger: new ConsoleLogger());
```

## API Overview

### Core Types

- `GpuDevice`: Represents a GPU device with VRAM and concurrency settings
- `VramBudget`: Thread-safe VRAM reservation system
- `GpuWorkItem`: Describes a unit of work with memory requirements and timeouts
- `GpuPolicy`: Enum for device selection strategies
- `GpuManager`: Manages device registry and selection logic
- `GpuDispatcher`: Main interface for scheduling work on GPUs

### Exceptions

- `GpuSelectionException`: Thrown when device selection fails
- `GpuProbeException`: Thrown when GPU detection fails
- `GpuBudgetExceededException`: Thrown when VRAM budget is exceeded

## Project Structure

```
MultiGpuHelper.sln
├── src/
│   └── MultiGpuHelper/              # Main library (netstandard2.0)
│       ├── Models/                  # GpuDevice, VramBudget, etc.
│       ├── Management/              # GpuManager, GpuRegistrationBuilder
│       ├── Dispatching/             # GpuDispatcher
│       ├── Probing/                 # GPU detection providers
│       ├── Logging/                 # Logging abstraction
│       └── Utilities/               # Helper functions (Size, etc.)
├── samples/
│   ├── SampleConsole/               # .NET 8 sample with async examples
│   └── SampleNetFramework/          # .NET Framework 4.7.2 sample
├── tests/
│   └── MultiGpuHelper.Tests/        # Unit tests (xUnit)
└── README.md
```

## Supported Platforms

- **.NET Framework**: 4.6.1+
- **.NET Core**: 2.1+
- **.NET**: 6.0, 8.0+

## GPU Support

- **NVIDIA**: Full support via `nvidia-smi`
- **AMD ROCm**: Future extensibility via `IGpuProbeProvider`
- **Intel oneAPI**: Future extensibility via `IGpuProbeProvider`

The library gracefully handles missing or non-functional GPU probes, returning an empty device list rather than crashing.

## Error Handling

All library boundaries throw meaningful custom exceptions with context:

```csharp
try
{
    await dispatcher.RunAsync(work, policy);
}
catch (GpuSelectionException ex)
{
    // No suitable device found
}
catch (GpuBudgetExceededException ex)
{
    // VRAM budget exceeded
}
catch (GpuProbeException ex)
{
    // GPU detection failed
}
```

## Thread Safety

- `GpuManager`: Fully thread-safe
- `VramBudget`: Thread-safe atomic operations
- `GpuDispatcher`: Safe for concurrent work dispatching
- Device semaphores prevent over-subscription

## Performance Considerations

1. **Concurrency Limits**: Set `MaxConcurrentJobs` conservatively to avoid GPU saturation
2. **VRAM Budgets**: Reserve headroom (typically 10-20% of total VRAM)
3. **Device Refresh**: Call `manager.RefreshAsync()` periodically for accurate VRAM info
4. **Selection Policy**: Use `MostFreeVram` for workloads with variable memory requirements

## Testing

Run the unit tests:

```bash
dotnet test tests/MultiGpuHelper.Tests/
```

Run the samples:

```bash
dotnet run --project samples/SampleConsole/
dotnet run --project samples/SampleNetFramework/  # Requires .NET Framework SDK
```

Run hardware verification test with real GPUs:

```bash
dotnet run --project samples/HardwareTest/
```

## Packaging & CI

### Building Locally

Build the solution:

```bash
dotnet build -c Release
```

### NuGet Package

The library is configured for automatic NuGet package generation.

**Build and pack**:

```bash
dotnet pack src/MultiGpuHelper/MultiGpuHelper.csproj -c Release -o ./artifacts
```

**Package location**:

- `.nupkg` (main package) → `artifacts/MultiGpuHelper.{version}.nupkg`
- `.snupkg` (symbol package) → `artifacts/MultiGpuHelper.{version}.snupkg`

**Local NuGet push** (for testing):

```bash
dotnet nuget push ./artifacts/MultiGpuHelper.1.0.0.nupkg -s <local-nuget-source>
```

### Strong-Name Signing

The assembly is **strongly signed** with a 2048-bit RSA key:

- Key file: `MultiGpuHelper.snk` (solution root)
- Configured in: `src/MultiGpuHelper/MultiGpuHelper.csproj`
- Property: `<SignAssembly>true</SignAssembly>`

This enables the library to be used in full-trust .NET Framework applications.

### CI/CD Pipeline

Automated builds run on GitHub Actions (`.github/workflows/ci.yml`):

**Triggers**:
- Every push to `main` or `develop`
- Every pull request to `main` or `develop`

**Pipeline steps**:
1. Checkout code
2. Setup .NET 8.x SDK
3. Restore dependencies
4. Build (Release configuration)
5. Run tests (if `tests/` exists)
6. Pack NuGet package
7. Upload artifacts (.nupkg + .snupkg)

**Artifacts**: Available on GitHub Actions run page under "nuget-packages"

### Versioning

This project follows **Semantic Versioning (SemVer)**.

See [VERSIONING.md](VERSIONING.md) for detailed versioning policy and release workflow.

Current version: **1.0.0**

## License

This library is released under the **MIT License**. See [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please ensure:

1. Code follows existing style conventions (English comments throughout)
2. New features include unit tests
3. Breaking changes are avoided or clearly documented
4. Documentation is updated

## Future Enhancements

- [ ] AMD ROCm probe provider
- [ ] Intel oneAPI probe provider
- [ ] OpenCL support
- [ ] GPU memory profiling hooks
- [ ] Work queue persistence
- [ ] Multi-machine GPU clustering

## Support

For issues, questions, or suggestions, please open an issue on GitHub.

---

**MultiGpuHelper** — Making multi-GPU scheduling simple and robust.
