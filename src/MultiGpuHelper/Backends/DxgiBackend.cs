using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiGpuHelper.Abstractions;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Backends
{
    /// <summary>
    /// OBSOLETE: This class is provided for backward compatibility only.
    /// DxgiBackend was renamed to WmiBackend because the implementation uses WMI (Windows Management Instrumentation),
    /// not actual DXGI APIs.
    ///
    /// Use WmiBackend instead. This wrapper will be removed in a future version.
    ///
    /// Historical context:
    /// - Version 1.1.0.1: Initial implementation incorrectly named "DxgiBackend"
    /// - Implementation: WMI-based GPU enumeration via Win32_VideoController queries
    /// - Truthfulness fix: Renamed to WmiBackend to accurately describe the technology used
    /// </summary>
    [Obsolete("Use WmiBackend instead. DxgiBackend was renamed because it uses WMI, not DXGI APIs. This wrapper will be removed in v1.2.0.", false)]
    public class DxgiBackend : IGpuBackend
    {
        private readonly WmiBackend _inner;

        public GpuBackendKind BackendKind => _inner.BackendKind;

        public DxgiBackend(IGpuLogger logger = null)
        {
            _inner = new WmiBackend(logger);
        }

        public Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync()
        {
            return _inner.DetectDevicesAsync();
        }

        public Task<IReadOnlyList<GpuDeviceInfo>> RefreshMemoryAsync(IReadOnlyList<GpuDeviceInfo> devices)
        {
            return _inner.RefreshMemoryAsync(devices);
        }

        public Task<bool> IsAvailableAsync()
        {
            return _inner.IsAvailableAsync();
        }
    }
}
