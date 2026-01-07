namespace MultiGpuHelper.Logging
{
    /// <summary>
    /// Default no-op logger that discards all messages.
    /// </summary>
    public class NoOpLogger : IGpuLogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
    }
}
