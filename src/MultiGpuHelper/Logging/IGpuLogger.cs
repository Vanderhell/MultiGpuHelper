namespace MultiGpuHelper.Logging
{
    /// <summary>
    /// Abstraction for GPU operation logging.
    /// </summary>
    public interface IGpuLogger
    {
        /// <summary>
        /// Log a debug-level message.
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// Log an info-level message.
        /// </summary>
        void Info(string message);

        /// <summary>
        /// Log a warning-level message.
        /// </summary>
        void Warn(string message);

        /// <summary>
        /// Log an error-level message.
        /// </summary>
        void Error(string message);
    }
}
