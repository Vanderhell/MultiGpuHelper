namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Policy for selecting which GPU to use for a work item.
    /// Each value represents a distinct selection algorithm.
    /// </summary>
    public enum GpuPolicy
    {
        /// <summary>
        /// Select the first available device in deterministic order (by device ID).
        /// </summary>
        FirstAvailable = 0,

        /// <summary>
        /// Select the GPU with the most available free VRAM.
        /// Falls back to FirstAvailable if memory information is unavailable.
        /// </summary>
        MostFreeMemory = 1,

        /// <summary>
        /// Rotate through available devices across consecutive selections.
        /// </summary>
        RoundRobin = 2,

        /// <summary>
        /// Select a specific device by ID.
        /// Fails if the device is not found or unavailable.
        /// </summary>
        SpecificDevice = 3
    }
}
