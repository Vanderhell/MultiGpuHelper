using System;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Models
{
    /// <summary>
    /// Immutable result of a GPU device selection operation.
    /// </summary>
    public sealed class GpuSelectionResult
    {
        /// <summary>
        /// Selected device ID (-1 if no selection).
        /// </summary>
        public int SelectedDeviceId { get; }

        /// <summary>
        /// Selected device information (null if no selection).
        /// </summary>
        public GpuDeviceInfo SelectedDevice { get; }

        /// <summary>
        /// Policy used for selection.
        /// </summary>
        public GpuPolicy Policy { get; }

        /// <summary>
        /// Whether selection succeeded.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Deterministic diagnostic reason (why selected or why failed).
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Total number of devices available for selection.
        /// </summary>
        public long TotalDevices { get; }

        /// <summary>
        /// Number of devices matching the selection policy.
        /// </summary>
        public long AvailableDevices { get; }

        /// <summary>
        /// Initialize a successful selection result.
        /// </summary>
        public static GpuSelectionResult Success(
            GpuDeviceInfo selectedDevice,
            GpuPolicy policy,
            string reason,
            long totalDevices,
            long availableDevices)
        {
            if (selectedDevice == null)
                throw new ArgumentNullException(nameof(selectedDevice));

            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));

            return new GpuSelectionResult(
                selectedDevice.DeviceId,
                selectedDevice,
                policy,
                isSuccess: true,
                reason,
                totalDevices,
                availableDevices);
        }

        /// <summary>
        /// Initialize a failed selection result.
        /// </summary>
        public static GpuSelectionResult Failure(
            GpuPolicy policy,
            string reason,
            long totalDevices,
            long availableDevices)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason cannot be null or empty.", nameof(reason));

            return new GpuSelectionResult(
                -1,
                null,
                policy,
                isSuccess: false,
                reason,
                totalDevices,
                availableDevices);
        }

        /// <summary>
        /// Initialize a selection result (use factory methods instead).
        /// </summary>
        private GpuSelectionResult(
            int selectedDeviceId,
            GpuDeviceInfo selectedDevice,
            GpuPolicy policy,
            bool isSuccess,
            string reason,
            long totalDevices,
            long availableDevices)
        {
            SelectedDeviceId = selectedDeviceId;
            SelectedDevice = selectedDevice;
            Policy = policy;
            IsSuccess = isSuccess;
            Reason = reason;
            TotalDevices = totalDevices;
            AvailableDevices = availableDevices;
        }
    }
}
