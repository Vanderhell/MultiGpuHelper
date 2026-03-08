# MultiGpuHelper 1.1.0 — Public API Surface

## Overview

This document clarifies the public API surface for 1.1.0, organized into three stability tiers:
1. **Primary API** — Recommended for new code; central to 1.1.0 value proposition
2. **Compatibility API** — Supported for backward compatibility; not recommended for new code
3. **Internal API** — Not part of public contract; subject to change

---

## Primary API (1.1.0 Focus)

These types and interfaces form the recommended entry point for new users.

### Core Selection API

#### `GpuSelectionEngine`
```csharp
namespace MultiGpuHelper.Selection
{
    public class GpuSelectionEngine
    {
        public GpuSelectionEngine(IGpuLogger logger = null);

        public GpuSelectionResult SelectDevice(
            IReadOnlyList<GpuDeviceInfo> devices,
            GpuPolicy policy,
            int? explicitDeviceId = null);
    }
}
```
**Stability**: Stable in 1.x. Central to 1.1.0 value proposition.
**Purpose**: Deterministic GPU device selection using three documented policies.

---

#### `GpuSelectionResult`
```csharp
namespace MultiGpuHelper.Models
{
    public sealed class GpuSelectionResult
    {
        public int SelectedDeviceId { get; }
        public GpuDeviceInfo SelectedDevice { get; }
        public GpuPolicy Policy { get; }
        public bool IsSuccess { get; }
        public string Reason { get; }
        public long TotalDevices { get; }
        public long AvailableDevices { get; }

        public static GpuSelectionResult Success(...);
        public static GpuSelectionResult Failure(...);
    }
}
```
**Stability**: Stable in 1.x. Immutable contract.
**Purpose**: Selection outcome with deterministic reason text.

---

#### `GpuDeviceInfo`
```csharp
namespace MultiGpuHelper.Models
{
    public sealed class GpuDeviceInfo
    {
        public int DeviceId { get; }
        public string DeviceName { get; }
        public GpuBackendKind Backend { get; }
        public GpuMemoryInfo MemoryInfo { get; }
        public GpuAvailabilityState AvailabilityState { get; }
        public long VramBudgetLimitBytes { get; }
        public int MaxConcurrentJobs { get; }
    }
}
```
**Stability**: Stable in 1.x. Immutable contract.
**Purpose**: Device metadata as immutable value type.

---

#### `GpuMemoryInfo`
```csharp
namespace MultiGpuHelper.Models
{
    public sealed class GpuMemoryInfo
    {
        public long TotalBytes { get; }
        public long FreeBytes { get; }
        public GpuAvailabilityState State { get; }

        public static GpuMemoryInfo Unavailable();
        public static GpuMemoryInfo Error();
    }
}
```
**Stability**: Stable in 1.x. Immutable contract.
**Purpose**: Device memory info with availability state.

---

### Backend Detection API

#### `NvidiaBackend`
```csharp
namespace MultiGpuHelper.Backends
{
    public class NvidiaBackend
    {
        public NvidiaBackend(IGpuLogger logger = null);

        public Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync();
        public Task<IReadOnlyList<GpuDeviceInfo>> RefreshMemoryAsync(
            IReadOnlyList<GpuDeviceInfo> devices);
        public Task<bool> IsAvailableAsync();

        public GpuBackendKind BackendKind { get; }
    }
}
```
**Stability**: Stable in 1.x. NVIDIA detection only in 1.1.0.
**Purpose**: GPU device enumeration via nvidia-smi.

---

### Enums

#### `GpuPolicy`
```csharp
namespace MultiGpuHelper.Models
{
    public enum GpuPolicy
    {
        FirstAvailable = 0,      // Primary for 1.1.0
        MostFreeMemory = 1,      // Primary for 1.1.0
        ExplicitId = 2,          // Primary for 1.1.0

        // Deprecated (legacy names, kept for compatibility)
        RoundRobin = 0,          // Use FirstAvailable
        MostFreeVram = 1,        // Use MostFreeMemory
        SpecificDevice = 2       // Use ExplicitId
    }
}
```
**Stability**: Enum values stable. New names preferred over deprecated names.
**Purpose**: Device selection strategy.

---

#### `GpuBackendKind`
```csharp
namespace MultiGpuHelper.Enums
{
    public enum GpuBackendKind
    {
        Unknown = 0,  // Unrecognized backend
        NVIDIA = 1,   // NVIDIA CUDA (implemented in 1.1.0)
        AMD = 2,      // AMD ROCm (planned v1.2+)
        Intel = 3     // Intel oneAPI (planned v1.3+)
    }
}
```
**Stability**: Stable. New backends added as values in future versions.
**Purpose**: GPU vendor/backend identifier.

---

#### `GpuAvailabilityState`
```csharp
namespace MultiGpuHelper.Enums
{
    public enum GpuAvailabilityState
    {
        Available = 0,    // Device detected and accessible
        Unavailable = 1,  // Device not accessible
        Error = 2         // Device state unknown or probe error
    }
}
```
**Stability**: Stable. Represents device accessibility.
**Purpose**: Device and memory availability state.

---

### Logging

#### `IGpuLogger`
```csharp
namespace MultiGpuHelper.Logging
{
    public interface IGpuLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
```
**Stability**: Stable. Used throughout library for optional logging.
**Purpose**: Pluggable logging abstraction.

---

## Compatibility API (1.0.1 Legacy)

These types remain public for backward compatibility with 1.0.x code, but are **not the recommended starting point** for new users.

### Legacy Device Management

#### `GpuManager`
```csharp
namespace MultiGpuHelper.Management
{
    public class GpuManager
    {
        public GpuManager(IGpuProbeProvider probeProvider = null, IGpuLogger logger = null);

        public IReadOnlyList<GpuDevice> Devices { get; }
        public void AddDevice(GpuDevice device);
        public bool RemoveDevice(int deviceId);
        public GpuDevice GetDevice(int deviceId);
        public async Task RefreshAsync();
        public GpuDevice SelectDevice(GpuPolicy policy, int? specificDeviceId = null);
        public async Task InitializeFromProbeAsync();
    }
}
```
**Stability**: Backward-compatible in 1.x. Implementation may change internally.
**Status**: Supported but not recommended for new code. Use `GpuSelectionEngine` instead.
**Note**: Returns mutable `GpuDevice`; consider `GpuSelectionEngine` + immutable `GpuDeviceInfo` for new code.

---

#### `GpuDevice`
```csharp
namespace MultiGpuHelper.Models
{
    public class GpuDevice
    {
        public int DeviceId { get; set; }
        public string Name { get; set; }
        public long TotalVramBytes { get; set; }
        public long? FreeVramBytes { get; set; }
        public bool IsEnabled { get; set; }
        public int MaxConcurrentJobs { get; set; }
        public VramBudget VramBudget { get; set; }
    }
}
```
**Stability**: Backward-compatible in 1.x. Mutable type (legacy).
**Status**: Supported for existing code. Use `GpuDeviceInfo` (immutable) for new code.
**Note**: `GpuManager.SelectDevice()` returns this type. `GpuSelectionEngine` works with `GpuDeviceInfo` instead.

---

### Legacy Work Dispatch

#### `GpuDispatcher`
```csharp
namespace MultiGpuHelper.Dispatching
{
    public class GpuDispatcher
    {
        public GpuDispatcher(GpuManager manager);

        public async Task<T> RunAsync<T>(
            Func<int, Task<T>> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken cancellationToken = default);

        public async Task RunAsync(
            Func<int, Task> work,
            GpuPolicy policy,
            GpuWorkItem workItem = null,
            CancellationToken cancellationToken = default);
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for existing code. Not the focus of 1.1.0. Consider building your own dispatch on top of `GpuSelectionEngine` for new projects.

---

#### `GpuWorkItem`
```csharp
namespace MultiGpuHelper.Models
{
    public class GpuWorkItem
    {
        public long RequestedVramBytes { get; set; }
        public int TimeoutMs { get; set; }
        public string Tag { get; set; }
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for `GpuDispatcher` users. Not part of new 1.1.0 selection story.

---

### Legacy VRAM Management

#### `VramBudget`
```csharp
namespace MultiGpuHelper.Models
{
    public class VramBudget
    {
        public long LimitBytes { get; set; }
        public long ReservedBytes { get; private set; }

        public bool CanReserve(long bytes);
        public bool TryReserve(long bytes);
        public void Release(long bytes);
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for existing code. Soft reservation only; not enforced by OS.
**Note**: Attached to `GpuDevice` for legacy compatibility. Not part of new `GpuDeviceInfo` / `GpuSelectionEngine` story.

---

### Legacy Probing

#### `IGpuProbeProvider`
```csharp
namespace MultiGpuHelper.Probing
{
    public interface IGpuProbeProvider
    {
        Task<IList<GpuDevice>> ProbeAsync();
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for existing code using `GpuManager`. Being superseded by `IGpuBackend` (internal). Not recommended for new implementations.

---

#### `NvidiaSmiProbeProvider`
```csharp
namespace MultiGpuHelper.Probing
{
    public class NvidiaSmiProbeProvider : IGpuProbeProvider
    {
        public NvidiaSmiProbeProvider(IGpuLogger logger = null);
        public Task<IList<GpuDevice>> ProbeAsync();
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for existing `GpuManager` code. Use `NvidiaBackend` for new code.

---

### Legacy Device Registration

#### `GpuRegistrationBuilder`
```csharp
namespace MultiGpuHelper.Management
{
    public class GpuRegistrationBuilder
    {
        public GpuRegistrationBuilder AddDevice(int deviceId, string name, long totalVramBytes);
        public GpuRegistrationBuilder ConfigureDevice(int deviceId, long? budgetBytes = null, int? maxConcurrentJobs = null, bool? enabled = null);
        public GpuManager Build();
    }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Supported for manual device setup in existing code. Not recommended for new code. Use `NvidiaBackend` for auto-detection or build custom device lists for `GpuSelectionEngine`.
**Purpose**: Legacy builder pattern for manual GPU device registration and configuration.

---

### Legacy Exceptions

```csharp
namespace MultiGpuHelper.Exceptions
{
    public class GpuSelectionException : Exception { }
    public class GpuBudgetExceededException : Exception { }
    public class GpuProbeException : Exception { }
}
```
**Stability**: Backward-compatible in 1.x.
**Status**: Still thrown by legacy `GpuManager` and `GpuDispatcher`. New `GpuSelectionEngine` returns results instead.

---

## Internal API (Not Part of Public Contract)

The following are intentionally kept **internal** to avoid premature lock-in:

**Design Decision**: Backend abstraction (`IGpuBackend`) is intentionally kept internal in 1.1.0 to keep the API surface small. Only NVIDIA backend is needed in 1.1.0; AMD/Intel backends can be internal implementations. If evidence suggests user-implemented backends are necessary, `IGpuBackend` can be made public in 1.2+ without breaking changes (it's not currently used by any public API).

### Backend Abstraction (Internal)

#### `IGpuBackend` — **INTERNAL in 1.1.0**
```csharp
// internal interface IGpuBackend
// {
//     Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync();
//     Task<IReadOnlyList<GpuDeviceInfo>> RefreshMemoryAsync(IReadOnlyList<GpuDeviceInfo> devices);
//     Task<bool> IsAvailableAsync();
//     GpuBackendKind BackendKind { get; }
// }
```
**Decision**: Kept **internal** in 1.1.0
**Rationale**:
- Only NVIDIA backend exists in 1.1.0
- AMD/Intel backends can be internal implementations
- No evidence of user-implemented custom backends yet
- Can be made public in 1.2 if needed
- Keeps 1.1.0 API surface smaller

**Alternative**: Could be made public for extensibility, but deferred to 1.2 when use case is clearer.

---

### Implementation Details (Internal)

- Selection engine internals
- Backend implementation details
- Logger implementations (NoOpLogger, etc.)

---

## API Naming and Semantic Review

### GpuBackendKind Enum
**Review**: Represents vendor/backend technology (Unknown, NVIDIA, AMD, Intel)
**Assessment**: Naming is clear and not overloaded
**Decision**: Keep as-is. "Backend" clearly means vendor backend, not architectural backend.
**Future**: May add more vendors as backends are implemented.

---

### GpuPolicy Enum
**Review**: Has both old names (RoundRobin, MostFreeVram, SpecificDevice) and new names (FirstAvailable, MostFreeMemory, ExplicitId)
**Assessment**: New names are clearer; old names deprecated but functional
**Decision**: Keep both for backward compatibility. Document preference for new names.
**Future**: Could deprecate old names with compiler warnings in 1.2 if desired.

---

## Summary Table

| Type | Category | Stability | Recommended |
|------|----------|-----------|-------------|
| `GpuSelectionEngine` | Primary | Stable | ✓ Yes |
| `GpuSelectionResult` | Primary | Stable | ✓ Yes |
| `GpuDeviceInfo` | Primary | Stable | ✓ Yes |
| `GpuMemoryInfo` | Primary | Stable | ✓ Yes |
| `NvidiaBackend` | Primary | Stable | ✓ Yes |
| `GpuPolicy` (new names) | Primary | Stable | ✓ Yes |
| `GpuBackendKind` | Primary | Stable | ✓ Yes |
| `GpuAvailabilityState` | Primary | Stable | ✓ Yes |
| `IGpuLogger` | Primary | Stable | ✓ Yes |
| `GpuManager` | Compatibility | Backward-compatible | ⚠ For existing code |
| `GpuDispatcher` | Compatibility | Backward-compatible | ⚠ For existing code |
| `GpuDevice` | Compatibility | Backward-compatible | ⚠ For existing code |
| `GpuWorkItem` | Compatibility | Backward-compatible | ⚠ For existing code |
| `VramBudget` | Compatibility | Backward-compatible | ⚠ For existing code |
| `IGpuProbeProvider` | Compatibility | Backward-compatible | ⚠ For existing code |
| `NvidiaSmiProbeProvider` | Compatibility | Backward-compatible | ⚠ For existing code |
| `IGpuBackend` | Internal | Not public | ✗ Internal only |

---

**Version**: 1.1.0
**Last Updated**: 2026-03-08
**Status**: API surface locked for 1.1.0 release
