using System;
using System.Threading;
using MultiGpuHelper.Exceptions;

namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Thread-safe VRAM budget management with soft-reservation support.
    /// </summary>
    public class VramBudget
    {
        private long _reservedBytes;
        private readonly object _lockObject = new object();

        /// <summary>
        /// Limit of VRAM that can be reserved (null means unlimited).
        /// </summary>
        public long? LimitBytes { get; set; }

        /// <summary>
        /// Current amount of reserved VRAM in bytes.
        /// </summary>
        public long ReservedBytes
        {
            get
            {
                lock (_lockObject)
                {
                    return _reservedBytes;
                }
            }
        }

        /// <summary>
        /// Try to reserve VRAM. Returns true if successful, false if budget exceeded.
        /// </summary>
        public bool TryReserve(long bytes)
        {
            if (bytes < 0)
                throw new ArgumentException("Cannot reserve negative bytes.", nameof(bytes));

            lock (_lockObject)
            {
                if (LimitBytes.HasValue && _reservedBytes + bytes > LimitBytes.Value)
                    return false;

                _reservedBytes += bytes;
                return true;
            }
        }

        /// <summary>
        /// Release previously reserved VRAM.
        /// </summary>
        public void Release(long bytes)
        {
            if (bytes < 0)
                throw new ArgumentException("Cannot release negative bytes.", nameof(bytes));

            lock (_lockObject)
            {
                _reservedBytes = Math.Max(0, _reservedBytes - bytes);
            }
        }

        /// <summary>
        /// Check if VRAM can be reserved without actually reserving.
        /// </summary>
        public bool CanReserve(long bytes)
        {
            if (bytes < 0)
                return false;

            lock (_lockObject)
            {
                if (LimitBytes.HasValue && _reservedBytes + bytes > LimitBytes.Value)
                    return false;

                return true;
            }
        }
    }
}
