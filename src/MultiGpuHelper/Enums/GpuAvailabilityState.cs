namespace MultiGpuHelper.Enums
{
    /// <summary>
    /// Enumeration of GPU device availability states.
    /// </summary>
    public enum GpuAvailabilityState
    {
        /// <summary>
        /// Device detected and accessible.
        /// </summary>
        Available = 0,

        /// <summary>
        /// Device not accessible (driver missing, disabled, etc.).
        /// </summary>
        Unavailable = 1,

        /// <summary>
        /// Device state unknown or probe returned an error.
        /// </summary>
        Error = 2
    }
}
