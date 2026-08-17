using System.Collections.Generic;
using System.Threading.Tasks;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Abstractions
{
    /// <summary>
    /// Contract implemented by GPU discovery backends.
    /// Applications may use it to choose or compose discovery mechanisms.
    /// </summary>
    public interface IGpuBackend
    {
        /// <summary>
        /// Detect available GPU devices on this machine.
        /// </summary>
        /// <returns>List of detected devices; empty list if no devices found.</returns>
        Task<IReadOnlyList<GpuDeviceInfo>> DetectDevicesAsync();

        /// <summary>
        /// Refresh VRAM information for detected devices.
        /// </summary>
        /// <param name="devices">Devices to refresh (typically from a prior detection).</param>
        /// <returns>Updated device list with refreshed VRAM info.</returns>
        Task<IReadOnlyList<GpuDeviceInfo>> RefreshMemoryAsync(IReadOnlyList<GpuDeviceInfo> devices);

        /// <summary>
        /// Get the backend kind (NVIDIA, AMD, etc.).
        /// </summary>
        Enums.GpuBackendKind BackendKind { get; }

        /// <summary>
        /// Whether this backend is available on the current system.
        /// </summary>
        /// <returns>True if the backend can potentially detect devices; false otherwise.</returns>
        Task<bool> IsAvailableAsync();
    }
}
