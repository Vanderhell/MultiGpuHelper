namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Represents a GPU device.
    /// </summary>
    public class GpuDevice
    {
        /// <summary>
        /// Unique identifier for the device.
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// Human-readable name of the GPU (e.g., "NVIDIA RTX 4090").
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Total VRAM in bytes.
        /// </summary>
        public long TotalVramBytes { get; set; }

        /// <summary>
        /// Current free VRAM in bytes (optional, may be null before probing).
        /// </summary>
        public long? FreeVramBytes { get; set; }

        /// <summary>
        /// Whether this device is enabled for scheduling.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Maximum number of concurrent jobs on this device.
        /// </summary>
        public int MaxConcurrentJobs { get; set; }

        /// <summary>
        /// VRAM budget (soft-reservation) for this device.
        /// </summary>
        public VramBudget VramBudget { get; set; }

        public GpuDevice()
        {
            IsEnabled = true;
            MaxConcurrentJobs = 1;
            VramBudget = new VramBudget();
        }
    }
}
