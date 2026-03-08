# MultiGpuHelper 1.1.0 — Architecture

## Layering and Stability Tiers

```
┌──────────────────────────────────────────────┐
│  PRIMARY API (1.1.0 Focus)                   │
│  ──────────────────────────────────────────  │
│  • GpuSelectionEngine (deterministic)        │
│  • GpuSelectionResult (immutable outcome)    │
│  • GpuDeviceInfo (immutable device info)     │
│  • GpuMemoryInfo (immutable memory info)     │
│  • NvidiaBackend (GPU detection)             │
│  • GpuPolicy (selection strategies)          │
│  • GpuBackendKind, GpuAvailabilityState      │
│  STABILITY: Stable in 1.x                    │
├──────────────────────────────────────────────┤
│  COMPATIBILITY API (1.0.x Legacy)            │
│  ──────────────────────────────────────────  │
│  • GpuManager (device registry)              │
│  • GpuDispatcher (async dispatch)            │
│  • GpuDevice (mutable device, legacy)        │
│  • VramBudget (soft VRAM reservation)        │
│  • IGpuProbeProvider (legacy probe)          │
│  • NvidiaSmiProbeProvider (legacy probe)     │
│  STABILITY: Backward-compatible in 1.x       │
│  NOTE: Supported; use primary API for new    │
├──────────────────────────────────────────────┤
│  INTERNAL API (Not Public)                   │
│  ──────────────────────────────────────────  │
│  • IGpuBackend (internal backend abstraction)│
│  • Implementation details                    │
│  STABILITY: Subject to change; not public    │
└──────────────────────────────────────────────┘
```

## Public API Boundary

### What Is Public (Stable)

1. **GpuManager**
   - `InitializeFromProbeAsync()` - probe devices
   - `Devices` - enumerate devices (returns `GpuDevice` for backward compatibility)
   - `SelectDevice(policy, specificDeviceId)` - select by policy
   - `AddDevice(device)` - manual device registration
   - `GetDevice(deviceId)` - retrieve device by ID

2. **GpuDispatcher**
   - `RunAsync<T>(work, policy, workItem, cancellationToken)` - execute work on selected device

3. **GpuDevice** (mutable, for backward compatibility)
   - `DeviceId` - device ordinal
   - `Name` - device name
   - `TotalVramBytes` - total memory
   - `FreeVramBytes` - free memory (nullable)
   - `IsEnabled` - device enabled state
   - `MaxConcurrentJobs` - concurrency limit
   - `VramBudget` - soft VRAM reservation object

4. **GpuPolicy** (enum)
   - `RoundRobin` - rotate through devices
   - `MostFreeVram` - select device with most free memory
   - `SpecificDevice` - select explicit device ID

5. **GpuWorkItem**
   - `TimeoutMs` - execution timeout
   - `RequestedVramBytes` - soft VRAM reservation
   - `Tag` - work identifier

6. **Exceptions**
   - `GpuSelectionException` - selection failed
   - `GpuBudgetExceededException` - VRAM exceeded
   - `GpuProbeException` - probe failed

7. **Logging**
   - `IGpuLogger` - pluggable logger interface

### New Public Types (1.1.0+)

These are newly introduced immutable types recommended for new code:

1. **GpuDeviceInfo** (sealed, immutable)
   - Replaces mutable `GpuDevice` for read-only scenarios
   - Safe for concurrent reads
   - Used in `GpuSelectionResult`

2. **GpuMemoryInfo** (sealed, immutable)
   - Encapsulates memory state (total, free, availability)
   - Distinguishes unknown vs unavailable memory

3. **GpuSelectionResult** (sealed, immutable)
   - Outcome of a selection operation
   - Contains selected device, policy, deterministic reason
   - Replaces thrown exceptions for some call patterns (future evolution)

4. **GpuBackendKind** (enum)
   - Identifies GPU backend (Unknown, NVIDIA, AMD, Intel)
   - Attached to device info for clarity

5. **GpuAvailabilityState** (enum)
   - Device state (Available, Unavailable, Error)
   - Attached to memory info for clarity

### What Is Public (Extensibility)

8. **NvidiaBackend** (concrete implementation)
   - NVIDIA GPU detection via nvidia-smi
   - Returns `IReadOnlyList<GpuDeviceInfo>`
   - Gracefully handles missing nvidia-smi
   - Public in 1.1.0 for detection use cases

9. **DxgiBackend** (concrete implementation, v1.1.0.1+)
   - Windows GPU detection via WMI (DXGI adapters)
   - Returns `IReadOnlyList<GpuDeviceInfo>`
   - Gracefully handles non-Windows platforms
   - Strictly opt-in; no changes to default behavior
   - Free memory unknown; Total memory when available

### What Is Internal (Subject to Change)

1. **IGpuBackend** (internal only in 1.1.0)
   - Internal abstraction for backend implementations
   - Not part of public API; subject to change
   - Used by NvidiaBackend; future backends will also implement
   - Decision Rationale:
     - Only NVIDIA backend in 1.1.0; no user-implemented backends needed yet
     - Can be made public in 1.2+ if evidence suggests user extensions are necessary
     - Keeps API surface smaller for 1.1.0 initial release
     - No breaking change if made public later (non-users cannot break)

2. **IGpuProbeProvider** (legacy)
   - Still public for backward compatibility
   - Returns mutable `GpuDevice` objects
   - New code should use `IGpuBackend` internally

3. **GpuRegistrationBuilder**
   - Builder pattern for manual device setup
   - Public but minimal API surface

4. **VramBudget**
   - Internal reservation tracking
   - Not isolated; direct access possible from `GpuDevice`

5. **Logging Implementation**
   - `NoOpLogger` default

## Responsibility Boundaries

### GpuManager
- **Owns**: Device registry, selection logic, policy enforcement
- **Does Not**: Probe devices directly; delegates to IGpuProbeProvider (legacy) or IGpuBackend (new)
- **Does Not**: Execute work; GpuDispatcher handles that
- **Thread-Safe**: Yes, via `lock(_lockObject)`

### GpuDispatcher
- **Owns**: Work dispatch, timeout, cancellation, per-device semaphore enforcement
- **Does Not**: Device selection (uses GpuManager)
- **Does Not**: GPU execution; delegates to caller's lambda
- **Async**: Yes, fully async/await based

### IGpuProbeProvider (Legacy)
- **Owns**: Device detection via backend-specific tools (nvidia-smi, etc.)
- **Returns**: Mutable `GpuDevice` list
- **Examples**: `NvidiaSmiProbeProvider`

### IGpuBackend (Internal in 1.1.0)
- **Owns**: Unified interface for backend detection and refresh
- **Returns**: Immutable `GpuDeviceInfo` list
- **Access**: Internal only in 1.1.0 (not public API)
- **Rationale**: Only NVIDIA backend exists; no evidence of user-implemented backends yet; can be made public in 1.2 if needed
- **Future**: May be exposed as public extension point in 1.2+ when AMD/Intel backends are implemented

### IGpuLogger
- **Owns**: Log output control
- **Does Not**: Format log messages; library controls format
- **Default**: `NoOpLogger` (silent)

## How Backends Plug In

### Legacy Path (GpuManager + IGpuProbeProvider)

```
1. User creates GpuManager(probeProvider)
2. Calls manager.InitializeFromProbeAsync()
3. Manager delegates to probeProvider.ProbeAsync()
4. Probe returns List<GpuDevice> (mutable)
5. Manager stores devices in internal dictionary
6. User calls manager.SelectDevice(policy)
7. Manager returns selected GpuDevice
8. GpuDispatcher uses selected device
```

### New Path (IGpuBackend, Internal)

```
1. IGpuBackend implementation detects devices → GpuDeviceInfo[]
2. GpuManager internal logic can consume IGpuBackend
3. SelectDevice returns GpuDevice (for backward compat) or GpuSelectionResult (new API, future)
4. Immutable GpuDeviceInfo attached to result for safe inspection
```

**Transition**: 1.1.0 introduces `IGpuBackend` internally. Future versions may expose it to replace `IGpuProbeProvider`.

## Diagnostics and Reasons

### Selection Result Reasons
When a device is selected or selection fails, `GpuSelectionResult.Reason` contains deterministic text:

**Success Examples**:
- "RoundRobin: Selected device 0 (NVIDIA RTX 4090)"
- "MostFreeVram: Selected device 1 (NVIDIA RTX 4080) with 16.0 GiB free"
- "SpecificDevice: Selected device 2 as requested"

**Failure Examples**:
- "No devices available for selection"
- "RoundRobin: No enabled devices"
- "SpecificDevice: Device 5 not found or disabled"
- "MostFreeVram: No devices with free VRAM info"

Reasons are:
- **Deterministic**: Same inputs always produce same reason text
- **Plain-Text**: No codes or enums; human-readable
- **Concise**: One sentence, no verbose logging

### Selection Diagnostics

Selection logic produces reasons at these points:
1. **Before Selection**: "Total devices: 2, enabled: 2"
2. **Policy Application**: "RoundRobin: Rotating to device X"
3. **Failure Point**: "Device unavailable", "Budget exceeded", "Not found"

## Stability Tiers (1.x)

### Stable (Will Not Break)
- `GpuManager` public methods and properties
- `GpuDispatcher.RunAsync<T>()` signature and behavior
- `GpuPolicy` enum values and names
- `GpuWorkItem` constructor and properties
- Exception types and constructors
- `IGpuLogger` interface

### Internal (May Change)
- `IGpuBackend` (internal only)
- `IGpuProbeProvider.ProbeAsync()` return type (currently `IList<GpuDevice>`)
- `GpuDevice` internal structure (public but mutable; use `GpuDeviceInfo` for new code)
- `VramBudget` implementation

### New in 1.1.0 (Stable Going Forward)
- `GpuDeviceInfo`, `GpuMemoryInfo`, `GpuSelectionResult` types
- `GpuBackendKind`, `GpuAvailabilityState` enums
- All XML documentation

## Breaking Changes (None in 1.1.0)

1.1.0 is fully backward compatible with 1.0.x.

**Additions**:
- New immutable types for new code
- New internal abstractions (IGpuBackend)
- Enhanced metadata (GpuBackendKind, GpuAvailabilityState)

**Deletions**: None

**Modifications**: None to public APIs

**Deprecations**: None (legacy GpuDevice and IGpuProbeProvider remain public)

---

**Version**: 1.1.0
**Status**: Locked architecture for release
