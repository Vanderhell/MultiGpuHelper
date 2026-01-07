using System;

namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Represents a unit of work to be scheduled on a GPU.
    /// </summary>
    public class GpuWorkItem
    {
        /// <summary>
        /// Requested VRAM in bytes for this work item.
        /// </summary>
        public long RequestedVramBytes { get; set; }

        /// <summary>
        /// If set, use this specific device; otherwise, use policy-based selection.
        /// </summary>
        public int? SpecificDeviceId { get; set; }

        /// <summary>
        /// Optional tag/label for debugging and logging.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Optional timeout in milliseconds.
        /// </summary>
        public int? TimeoutMs { get; set; }

        /// <summary>
        /// Optional priority (higher = more important).
        /// </summary>
        public int Priority { get; set; } = 0;

        public GpuWorkItem()
        {
            Tag = "";
        }
    }
}
