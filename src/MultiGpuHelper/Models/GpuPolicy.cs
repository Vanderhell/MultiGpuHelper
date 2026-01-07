namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Policy for selecting which GPU to use for a work item.
    /// </summary>
    public enum GpuPolicy
    {
        /// <summary>
        /// Select GPUs in round-robin order.
        /// </summary>
        RoundRobin = 0,

        /// <summary>
        /// Select the GPU with the most available VRAM.
        /// </summary>
        MostFreeVram = 1,

        /// <summary>
        /// Use the specific device ID provided in GpuWorkItem.
        /// </summary>
        SpecificDevice = 2
    }
}
