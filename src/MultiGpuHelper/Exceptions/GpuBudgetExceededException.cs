using System;

namespace MultiGpuHelper.Exceptions
{
    /// <summary>
    /// Thrown when VRAM budget is exceeded.
    /// </summary>
    public class GpuBudgetExceededException : Exception
    {
        public GpuBudgetExceededException(string message) : base(message)
        {
        }

        public GpuBudgetExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
