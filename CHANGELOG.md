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

## [1.1.0] – 2026-03-08 – Transparent GPU Selection with Full Backward Compatibility

### Summary
1.1.0 adds transparent GPU device selection as the primary value proposition. Existing 1.0.x functionality (GpuManager, GpuDispatcher, VRAM budgeting) remains fully supported for backward compatibility. New users should start with GpuSelectionEngine and GpuSelectionResult for deterministic selection with reason text.

### Breaking Changes
**None**. All 1.0.x public APIs remain functional and unchanged.

### Open-Source Credibility & Documentation
**Comprehensive documentation and honest positioning**:
- [README.md](README.md) — Refreshed with v1.1.0 examples, scope clarity, and roadmap
- [docs/SUPPORTED_MATRIX.md](docs/SUPPORTED_MATRIX.md) — Platform & backend coverage matrix (tested vs planned)
- [docs/LIMITATIONS.md](docs/LIMITATIONS.md) — Honest limitations, trade-offs, and non-features
- [docs/SELECTION_RULES_1_1_0.md](docs/SELECTION_RULES_1_1_0.md) — Deterministic selection policy specification
- [docs/ARCHITECTURE_1_1_0.md](docs/ARCHITECTURE_1_1_0.md) — Layering, public API, backend extensibility
- [samples/SampleBasic/](samples/SampleBasic/) — Working example demonstrating all three selection policies
- Version note: 1.0.1 exists, 1.1.0 is next scoped improvement release
- No exaggerated claims; scope boundaries clearly defined
- Known limitations documented and honest about test coverage

### Selection Policies
**Three deterministic policies** for device selection:
1. **FirstAvailable** — Select the first available device (by device ID, ascending)
2. **MostFreeMemory** — Select device with most free VRAM (falls back to FirstAvailable if memory info unavailable)
3. **ExplicitId** — Select specific device by ID (fails if not found or unavailable)

All policies return `GpuSelectionResult` with:
- Selected device ID and info (or null if failed)
- Deterministic reason text explaining the selection or failure
- Policy used
- Success/failure status

Features:
- Deterministic: Same input always produces same output
- Explicit failure: No silent assumptions; reason text explains all failures
- Safe fallback: MostFreeMemory falls back to FirstAvailable when memory unavailable
- 20 unit tests covering all branches, edge cases, and fallback scenarios

Legacy enum values deprecated (RoundRobin, MostFreeVram, SpecificDevice) but maintained for backward compatibility.

### Backend Implementation
**NVIDIA Backend** (First production backend):
- Class: `NvidiaBackend` (in `Backends/NvidiaBackend.cs`)
- Interface: `IGpuBackend` (public abstraction for extensibility)
- Detection: Via `nvidia-smi` command-line tool
- Supported Data:
  - Device ID (GPU ordinal, 0-based)
  - Device name (model string, e.g., "NVIDIA RTX 4090")
  - Total VRAM (bytes)
  - Free VRAM (bytes, best-effort from kernel)
  - Backend kind (set to NVIDIA)
  - Availability state (Available, Unavailable, Error)
- Graceful Degradation: Returns empty device list if nvidia-smi unavailable
- Deterministic: Devices ordered by ID; same input always produces same output
- Unit Tests: 8 new tests covering detection, refresh, ordering, field validation

**Future Backends**:
- AMD ROCm (planned for v1.2+)
- Intel oneAPI (planned for v1.3+)

### Scope Definition
**1.1.0 is a scope-locked release** focused on lightweight GPU device selection and transparent backend extensibility.

**In Scope**:
- Device enumeration and unified metadata (ID, name, VRAM)
- Three deterministic selection policies (first-available, most-free-memory, explicit-device)
- Async work dispatch with timeout and cancellation support
- Soft VRAM budget reservation (best-effort, not enforced)
- Pluggable GPU probers (`IGpuProbeProvider`) for backend extensibility
- Sample applications and comprehensive README
- NVIDIA GPU support via `nvidia-smi`

**Explicitly Out-of-Scope** (see [docs/NON_GOALS_1_1_0.md](docs/NON_GOALS_1_1_0.md)):
- Scheduling, queueing, or job orchestration
- Round-robin or weighted selection policies
- Clustering, distributed logic, or multi-machine coordination
- Performance profiling, workload prediction, or confidence scoring
- Persistence or state caching
- Advanced telemetry, metrics, or monitoring dashboards
- Auto-magic backend detection or fallback chains

### Changed
- **Package Metadata Refinement**
  - Updated description: "Lightweight open-source .NET helper for transparent GPU selection across backends"
  - Normalized tags: `gpu;cuda;multi-gpu;vram;device-selection` (removed AI/inference language)
  - Clarified positioning: selection helper, not orchestration framework

- **Documentation**
  - Added [SCOPE_1_1_0.md](docs/SCOPE_1_1_0.md) – explicit in-scope features
  - Added [NON_GOALS_1_1_0.md](docs/NON_GOALS_1_1_0.md) – explicit out-of-scope items
  - Added [POSITIONING.md](docs/POSITIONING.md) – market positioning and target users
  - Added [PUBLIC_API_DRAFT_1_1_0.md](docs/PUBLIC_API_DRAFT_1_1_0.md) – high-level API types
  - Refined README and NuGet package descriptions for clarity

### Unchanged (Stable API)
- All public APIs remain unchanged from 1.0.0
- Full backward compatibility maintained
- Symbol package (.snupkg) and strong-name signing continue
- NVIDIA GPU detection via nvidia-smi as primary backend

---

## [1.1.0.2] – 2026-03-08 – Vendor Detection Fix

### Fixed
- **WMI Backend Vendor Detection** (EXEC_MGH_1_1_0_16)
  - Removed incorrect NVIDIA placeholder hardcoding on all WMI devices
  - Implemented best-effort vendor detection from WMI device name and PNP device ID
  - Supports detection of: NVIDIA, AMD, Intel, Unknown
  - Returns Unknown for unidentified vendors (not false NVIDIA)
  - Zero breaking changes; backward compatible

### Changed
- WmiBackend now correctly identifies device vendors instead of placeholder NVIDIA

### Tests Added
- 14 new vendor detection tests covering all vendor types
- All 77 tests passing (42 original + 13 backward compat + 8 wrapper + 14 vendor)

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
