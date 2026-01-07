using System;

namespace MultiGpuHelper.Exceptions
{
    /// <summary>
    /// Thrown when GPU probe/detection fails.
    /// </summary>
    public class GpuProbeException : Exception
    {
        public GpuProbeException(string message) : base(message)
        {
        }

        public GpuProbeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
