namespace MultiGpuHelper.Enums
{
    /// <summary>
    /// Discovery mechanism that produced a device record.
    /// </summary>
    public enum GpuBackendKind
    {
        /// <summary>
        /// Backend not recognized or unavailable.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// NVIDIA System Management Interface command-line tool.
        /// </summary>
        NvidiaSmi = 1,

        /// <summary>
        /// NVIDIA CUDA Driver API.
        /// </summary>
        Cuda = 2,

        /// <summary>
        /// ROCm command-line tools.
        /// </summary>
        Rocm = 3,

        /// <summary>
        /// Windows Management Instrumentation.
        /// </summary>
        Wmi = 4
    }
}
