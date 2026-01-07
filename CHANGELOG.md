# CHANGELOG

All notable changes to MultiGpuHelper will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] – 2024-01-07 – Initial Release

### Added
- **Core GPU Management**
  - `GpuManager` for device registry and selection
  - `GpuDevice` model with VRAM tracking
  - Support for multiple GPU devices

- **GPU Auto-Detection**
  - `NvidiaSmiProbeProvider` for automatic NVIDIA GPU detection
  - Graceful fallback if nvidia-smi is unavailable
  - `IGpuProbeProvider` abstraction for extensibility

- **Device Selection Policies**
  - `GpuPolicy.RoundRobin` – distribute work evenly
  - `GpuPolicy.MostFreeVram` – select GPU with most available memory
  - `GpuPolicy.SpecificDevice` – route to explicit GPU ID

- **VRAM Soft-Budgeting**
  - `VramBudget` class with thread-safe reservations
  - Per-device VRAM limits
  - `TryReserve()` / `Release()` API
  - Automatic budget enforcement

- **Concurrency Control**
  - Per-GPU semaphores via `SemaphoreSlim`
  - `MaxConcurrentJobs` configuration per device
  - Thread-safe work dispatching

- **Work Dispatching**
  - `GpuDispatcher` for async work scheduling
  - Support for async/sync lambdas with/without return values
  - Timeout and cancellation token support
  - `GpuWorkItem` for work metadata (tags, VRAM requests, priorities)

- **Logging**
  - `IGpuLogger` abstraction for custom logging
  - `NoOpLogger` default implementation

- **Utilities**
  - `Size` helper for human-readable byte formatting (MiB, GiB)
  - XML documentation for all public APIs

- **Error Handling**
  - `GpuSelectionException` – no suitable GPU found
  - `GpuProbeException` – GPU detection failed
  - `GpuBudgetExceededException` – VRAM budget exceeded
  - Rich context in exception messages

- **NuGet Packaging**
  - Auto-generated .nupkg and .snupkg (symbol package)
  - Strong-name signing with 2048-bit RSA key
  - MIT license metadata
  - Package tags: `gpu;cuda;ai;inference;multi-gpu;scheduler`

- **CI/CD**
  - GitHub Actions workflow (Windows, .NET 8.x)
  - Automated build, test, and pack
  - Artifact upload to GitHub

- **Documentation**
  - Comprehensive README with quick-start examples
  - VERSIONING.md with SemVer policy and release workflow
  - MIT LICENSE

- **Sample Applications**
  - .NET 8 console sample with async dispatching
  - .NET Framework 4.7.2 sample (device selection)
  - Hardware verification test for real GPU devices

- **Testing**
  - 13 unit tests (xUnit)
  - VramBudget functionality tests
  - GpuManager selection policy tests
  - Hardware test for P4000 GPUs

### Tested On
- **Hardware**: NVIDIA Quadro P4000 (2 units)
- **Frameworks**: .NET 8.0, .NET Standard 2.0, .NET Framework 4.7.2
- **OS**: Windows 10/11

### Known Limitations
- NVIDIA GPU detection only (extensible to AMD ROCm, Intel oneAPI)
- Requires nvidia-smi for auto-detection (gracefully handles missing driver)
- Net472 sample requires .NET Framework SDK (code compiles, may need runtime)

---

## [Unreleased]

### Planned (Future)
- [ ] AMD ROCm probe provider
- [ ] Intel oneAPI probe provider
- [ ] OpenCL support
- [ ] GPU memory profiling hooks
- [ ] Work queue persistence
- [ ] Multi-machine GPU clustering
- [ ] Performance optimizations
- [ ] Linux/macOS support (after AMD/Intel providers)
