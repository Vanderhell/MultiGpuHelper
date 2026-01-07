using System;

namespace MultiGpuHelper.Exceptions
{
    /// <summary>
    /// Thrown when GPU device selection fails.
    /// </summary>
    public class GpuSelectionException : Exception
    {
        public GpuSelectionException(string message) : base(message)
        {
        }

        public GpuSelectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
