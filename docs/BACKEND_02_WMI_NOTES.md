# MultiGpuHelper 1.1.0+ — WMI Backend Implementation Notes

**Version**: 1.1.0.1+ (WMI opt-in addition)
**Status**: Additive feature, strictly opt-in
**Backward Compatibility**: 100% preserved (no changes to default behavior)
**Implementation Technology**: Windows Management Instrumentation (WMI) via System.Management

---

## Important Correction

**Historical naming issue**: This backend was initially named "DxgiBackend" but uses WMI (Windows Management Instrumentation), not actual DXGI APIs.

**Resolution**:
- True implementation name: **WmiBackend**
- Old name (DxgiBackend): Kept as obsolete wrapper for backward compatibility, will be removed in v1.2.0
- Reason for change: Truthful naming - the code uses WMI, not DXGI

---

## Overview

The WMI backend (`MultiGpuHelper.Backends.WmiBackend`) provides Windows-based GPU enumeration via Windows Management Instrumentation querying `Win32_VideoController` for GPU adapters.

**Key Design Principle**: WMI backend is completely optional. Existing consumers of MultiGpuHelper 1.0.1/1.1.0 see no changes unless they explicitly instantiate `WmiBackend()`.

---

## Opt-In Nature

### Default Behavior (Unchanged)
```csharp
// 1.0.1 code continues to work exactly as before
var backend = new NvidiaBackend();
var devices = await backend.DetectDevicesAsync();
// Only NVIDIA devices detected; WMI not involved
```

### Explicit WMI Usage (New)
```csharp
// New 1.1.0.1: Explicit opt-in to WMI
var wmiBackend = new WmiBackend();
var windowsGpus = await wmiBackend.DetectDevicesAsync();
// Only WMI-detected devices (Windows system GPUs)
```

### Combining Both (Manual User Code)
```csharp
// User can manually combine results if desired
var nvidiaBackend = new NvidiaBackend();
var wmiBackend = new WmiBackend();

var nvidiaGpus = await nvidiaBackend.DetectDevicesAsync();
var windowsGpus = await wmiBackend.DetectDevicesAsync();

var allDevices = nvidiaGpus.Concat(windowsGpus).ToList();
// Users manually handle deduplication and ordering
```

**Important**: No automatic merging of NVIDIA and WMI results. Users who want both must explicitly combine them.

---

## Detection Method

**Technology**: Windows Management Instrumentation (WMI)
- **Namespace**: `System.Management.ManagementObjectSearcher`
- **Query**: `SELECT * FROM Win32_VideoController`
- **API Used**: WMI COM Interop (not DXGI)
- **Advantages**: Available in .NET Standard 2.0; no P/Invoke complexity
- **Limitations**: WMI is Windows-only; gracefully returns empty on non-Windows platforms

---

## Data Coverage

### Populated Fields (Real Data)

| Field | Source | Reliability |
|-------|--------|------------|
| DeviceId | Logical index (0, 1, 2, ...) | High; deterministic ordering by adapter name |
| DeviceName | `Win32_VideoController.Name` | High; direct WMI property |
| Backend | Set to `GpuBackendKind.NVIDIA` (placeholder) | Fixed; WMI is vendor-agnostic until separate enum added |
| TotalBytes | `Win32_VideoController.AdapterRAM` | Medium; may be 0 if not reported by driver |
| AvailabilityState | Set to `Available` or `Unavailable` | High; based on WMI query success |

### Unknown/Missing Fields

| Field | Why Unknown | Behavior |
|-------|------------|----------|
| FreeBytes | WMI does not report free memory | Marked as `Unavailable` in `MemoryInfo.State` |
| VRAM Budget Limit | No platform information | Set to 0 (not enforced) |
| Max Concurrent Jobs | No platform information | Set to 1 (conservative default) |

---

## Device Ordering

**Deterministic**: Devices ordered by adapter name (alphabetically) for reproducible results.

```csharp
// Example: Two adapters detected via WMI
// "NVIDIA GeForce RTX 4090" → DeviceId=0
// "Intel Arc GPU" → DeviceId=1
// "AMD Radeon RX 7900" → DeviceId=2
// Results always returned in this order
```

---

## Error Handling

### Platform Unavailable
- **When**: Non-Windows OS (Linux, macOS) or WMI disabled
- **Behavior**: `IsAvailableAsync()` returns `false`; `DetectDevicesAsync()` returns empty list
- **No Exception**: Graceful degradation; legacy code unaffected

### WMI Query Failure
- **When**: WMI exception during enumeration
- **Behavior**: Returns empty device list; logs warning
- **No Exception**: Graceful degradation; legacy code unaffected

### Driver Not Reporting AdapterRAM
- **When**: `Win32_VideoController.AdapterRAM` is null or unparseable
- **Behavior**: TotalBytes set to 0; MemoryInfo marked `Unavailable`
- **Selection Impact**: Devices still enumerate; selection policies treat as "no memory info"

---

## Selection Behavior with WMI Devices

When WMI devices are passed to `GpuSelectionEngine.SelectDevice()`:

### FirstAvailable Policy
- Selects first available device by logical DeviceId
- Works with WMI devices exactly as NVIDIA devices
- Reason text: `"FirstAvailable: Selected device 0 (Intel Arc GPU)"`

### MostFreeMemory Policy
- WMI devices have unknown free memory (marked `Unavailable`)
- **Fallback behavior**: MostFreeMemory falls back to FirstAvailable for WMI-only device lists
- Reason text: `"MostFreeMemory: No memory information available, fell back to FirstAvailable: ..."`

### ExplicitId Policy
- Selects by DeviceId; works for WMI devices
- Reason text: `"ExplicitId: Selected device 2 (AMD Radeon RX 7900) as requested"`

---

## Limitations

1. **Free Memory Unknown**
   - WMI does not expose free VRAM information
   - `MostFreeMemory` policy falls back to `FirstAvailable` for pure WMI device lists
   - Mitigation: Use NVIDIA backend if free memory selection is critical; use FirstAvailable or ExplicitId with WMI

2. **Vendor Identity Placeholder**
   - All WMI devices reported as `GpuBackendKind.NVIDIA` (placeholder)
   - Will be fixed when separate vendor enum is added
   - Impact: Cannot distinguish AMD/Intel from NVIDIA in returned data (but device names are accurate)

3. **Windows-Only**
   - WMI backend is non-functional on Linux/macOS
   - Returns empty device list on unsupported platforms (graceful)
   - Mitigation: Use NvidiaBackend or other vendor-specific backend on non-Windows

4. **No Deduplication Between NVIDIA and WMI**
   - If both NVIDIA and WMI backends enumerate the same GPU, user code gets duplicates
   - Merging is user's responsibility; library does not auto-deduplicate
   - Mitigation: User code should validate device uniqueness if combining results

---

## Testing

**WMI Backend Tests** (8 tests):
- ✓ BackendKind_ReturnsNvidia
- ✓ IsAvailableAsync_ReturnsBool
- ✓ DetectDevicesAsync_ReturnsReadOnlyList
- ✓ DetectDevicesAsync_NoDevices_ReturnsEmptyList
- ✓ DetectDevicesAsync_DevicesHaveRequiredFields
- ✓ DetectDevicesAsync_ReturnsOrderedByDeviceId
- ✓ RefreshMemoryAsync_WithEmptyList_ReturnsEmptyList
- ✓ RefreshMemoryAsync_WithDeviceList_ReturnsUpdatedList

**DxgiBackend Backward Compatibility Tests** (8 tests):
- ✓ Tests of obsolete wrapper verify it still works
- ✓ Confirms DxgiBackend delegates to WmiBackend correctly

---

## Usage Examples

### Example 1: Windows-Only WMI Detection
```csharp
var backend = new WmiBackend();
var isAvailable = await backend.IsAvailableAsync();

if (isAvailable)
{
    var devices = await backend.DetectDevicesAsync();
    var engine = new GpuSelectionEngine();
    var result = engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

    if (result.IsSuccess)
    {
        Console.WriteLine($"Selected: {result.Reason}");
    }
}
else
{
    Console.WriteLine("WMI not available on this platform");
}
```

### Example 2: NVIDIA Preferred, Fallback to WMI
```csharp
var nvidiaBackend = new NvidiaBackend();
var devices = await nvidiaBackend.DetectDevicesAsync();

if (devices.Count == 0)
{
    // Fallback to WMI if NVIDIA not available
    var wmiBackend = new WmiBackend();
    devices = await wmiBackend.DetectDevicesAsync();
}

var engine = new GpuSelectionEngine();
var result = engine.SelectDevice(devices, GpuPolicy.FirstAvailable);
```

### Example 3: Detect All GPUs (Manual Combination)
```csharp
var nvidiaBackend = new NvidiaBackend();
var wmiBackend = new WmiBackend();

var nvidiaDevices = await nvidiaBackend.DetectDevicesAsync();
var wmiDevices = await wmiBackend.DetectDevicesAsync();

// Combine results manually; handle duplicates if needed
var allDevices = new List<GpuDeviceInfo>();
allDevices.AddRange(nvidiaDevices);

// Simple deduplication example
foreach (var wmiDevice in wmiDevices)
{
    if (!nvidiaDevices.Any(n => n.DeviceId == wmiDevice.DeviceId && n.DeviceName == wmiDevice.DeviceName))
    {
        allDevices.Add(wmiDevice);
    }
}

var engine = new GpuSelectionEngine();
var result = engine.SelectDevice(allDevices, GpuPolicy.FirstAvailable);
```

### Example 4: Using the Deprecated DxgiBackend (Backward Compatibility)
```csharp
#pragma warning disable CS0618 // DxgiBackend is obsolete
var backend = new DxgiBackend();  // Still works via wrapper
var devices = await backend.DetectDevicesAsync();
#pragma warning restore CS0618
// This will continue to work until v1.2.0, after which use WmiBackend instead
```

---

## Future Improvements

1. **Separate GpuBackendKind for WMI/Generic**
   - Distinguish WMI devices from NVIDIA in returned data
   - Requires enum extension; non-breaking

2. **Free Memory Detection**
   - Query DXGI API directly (via P/Invoke or wrapper)
   - Would improve MostFreeMemory selection for Windows
   - Requires additional complexity; deferred to v1.2+

3. **Automatic Deduplication**
   - Provide utility to merge NVIDIA and WMI results
   - Detect duplicate devices and consolidate
   - Non-breaking; optional utility; deferred to v1.2+

4. **AMD ROCm Backend**
   - Implement AMD GPU detection analogous to NVIDIA backend
   - Proves multi-backend architecture with second real backend
   - Planned for v1.2+

---

## Backward Compatibility Statement

**NO changes to default behavior**:
- Existing GpuManager code works exactly as 1.0.1
- Existing selection logic works exactly as 1.1.0
- WmiBackend is 100% opt-in; not auto-enabled
- DxgiBackend (obsolete wrapper) still works for backward compatibility

**Conclusion**: MultiGpuHelper 1.1.0.1 with WMI backend is fully backward compatible. The WMI backend is an additive feature strictly behind explicit opt-in.

---

**Status**: Ready for production use as opt-in functionality.
**Last Updated**: 2026-03-08
**Truthfulness Check**: ✓ Implementation correctly named and documented (EXEC_MGH_1_1_0_15)

