using MultiGpuHelper.Enums;

namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Immutable GPU device memory information.
    /// </summary>
    public sealed class GpuMemoryInfo
    {
        /// <summary>
        /// Total VRAM in bytes.
        /// </summary>
        public long TotalBytes { get; }

        /// <summary>
        /// Free VRAM in bytes (best-effort; may be stale).
        /// </summary>
        public long FreeBytes { get; }

        /// <summary>
        /// Availability state of memory information.
        /// </summary>
        public GpuAvailabilityState State { get; }

        /// <summary>
        /// Initialize memory information.
        /// </summary>
        /// <param name="totalBytes">Total VRAM (bytes)</param>
        /// <param name="freeBytes">Free VRAM (bytes), or 0 if unknown</param>
        /// <param name="state">Availability state</param>
        public GpuMemoryInfo(long totalBytes, long freeBytes, GpuAvailabilityState state)
        {
            TotalBytes = totalBytes;
            FreeBytes = freeBytes;
            State = state;
        }

        /// <summary>
        /// Create memory info for an unavailable device.
        /// </summary>
        public static GpuMemoryInfo Unavailable()
        {
            return new GpuMemoryInfo(0, 0, GpuAvailabilityState.Unavailable);
        }

        /// <summary>
        /// Create memory info with an error state.
        /// </summary>
        public static GpuMemoryInfo Error()
        {
            return new GpuMemoryInfo(0, 0, GpuAvailabilityState.Error);
        }
    }
}
