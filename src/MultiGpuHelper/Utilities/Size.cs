using System;

namespace MultiGpuHelper.Utilities
{
    /// <summary>
    /// Helper utility for working with storage sizes.
    /// </summary>
    public static class Size
    {
        /// <summary>
        /// Create a size in mebibytes (MiB).
        /// </summary>
        public static long MiB(long count) => count * 1024 * 1024;

        /// <summary>
        /// Create a size in gibibytes (GiB).
        /// </summary>
        public static long GiB(long count) => count * 1024 * 1024 * 1024;

        /// <summary>
        /// Format bytes as a human-readable string (e.g., "2.5 GB").
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "negative";

            const double gb = 1024.0 * 1024.0 * 1024.0;
            const double mb = 1024.0 * 1024.0;
            const double kb = 1024.0;

            if (bytes >= gb)
                return (bytes / gb).ToString("F2") + " GB";
            if (bytes >= mb)
                return (bytes / mb).ToString("F2") + " MB";
            if (bytes >= kb)
                return (bytes / kb).ToString("F2") + " KB";
            return bytes + " B";
        }
    }
}
