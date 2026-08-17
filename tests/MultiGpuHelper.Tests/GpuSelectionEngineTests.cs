using System;
using System.Collections.Generic;
using Xunit;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;
using MultiGpuHelper.Selection;

namespace MultiGpuHelper.Tests
{
    public class GpuSelectionEngineTests
    {
        private readonly GpuSelectionEngine _engine;

        public GpuSelectionEngineTests()
        {
            _engine = new GpuSelectionEngine(new NoOpLogger());
        }

        private GpuDeviceInfo CreateDevice(
            int deviceId,
            string name,
            long totalBytes,
            long freeBytes,
            GpuAvailabilityState state = GpuAvailabilityState.Available)
        {
            var memory = new GpuMemoryInfo(totalBytes, freeBytes, state);
            return new GpuDeviceInfo(
                deviceId,
                name,
                GpuBackendKind.NvidiaSmi,
                memory,
                state);
        }

        #region FirstAvailable Tests

        [Fact]
        public void FirstAvailable_WithEmptyList_ReturnsFail()
        {
            var devices = new List<GpuDeviceInfo>();

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.False(result.IsSuccess);
            Assert.Null(result.SelectedDevice);
            Assert.Equal(-1, result.SelectedDeviceId);
            Assert.Equal(GpuPolicy.FirstAvailable, result.Policy);
            Assert.Contains("No devices", result.Reason);
        }

        [Fact]
        public void FirstAvailable_WithOneAvailableDevice_SelectsIt()
        {
            var device = CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
            var devices = new List<GpuDeviceInfo> { device };

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId);
            Assert.Equal("GPU 0", result.SelectedDevice.DeviceName);
            Assert.Contains("FirstAvailable", result.Reason);
            Assert.Contains("device 0", result.Reason);
        }

        [Fact]
        public void FirstAvailable_WithMultipleDevices_SelectsFirstById()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(2, "GPU 2", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId); // First by ID, not by list order
        }

        [Fact]
        public void FirstAvailable_WithUnavailableDevice_SkipsIt()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024, GpuAvailabilityState.Unavailable),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024, GpuAvailabilityState.Available)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.SelectedDeviceId);
        }

        [Fact]
        public void FirstAvailable_AllUnavailable_ReturnsFail()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024, GpuAvailabilityState.Unavailable),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024, GpuAvailabilityState.Unavailable)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.False(result.IsSuccess);
            Assert.Contains("No devices available", result.Reason);
        }

        #endregion

        #region MostFreeMemory Tests

        [Fact]
        public void MostFreeMemory_WithEmptyList_ReturnsFail()
        {
            var devices = new List<GpuDeviceInfo>();

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.False(result.IsSuccess);
            Assert.Contains("No devices", result.Reason);
        }

        [Fact]
        public void MostFreeMemory_WithOneDevice_SelectsIt()
        {
            var device = CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
            var devices = new List<GpuDeviceInfo> { device };

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId);
        }

        [Fact]
        public void MostFreeMemory_SelectsDeviceWithMostFreeMemory()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 4L * 1024 * 1024 * 1024),  // 4 GiB free
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024), // 12 GiB free
                CreateDevice(2, "GPU 2", 24L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024)   // 8 GiB free
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.SelectedDeviceId);
            Assert.Contains("12", result.Reason); // GiB amount in reason
        }

        [Fact]
        public void MostFreeMemory_WithTiedMemory_SelectsByLowestId()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(2, "GPU 2", 24L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024),
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 10L * 1024 * 1024 * 1024)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId); // Deterministic: lowest ID for tie
        }

        [Fact]
        public void MostFreeMemory_NoMemoryInfo_FallsBackToFirstAvailable()
        {
            var memoryError = new GpuMemoryInfo(0, 0, GpuAvailabilityState.Error);
            var deviceWithoutMemory = new GpuDeviceInfo(
                0, "GPU 0", GpuBackendKind.NvidiaSmi, memoryError, GpuAvailabilityState.Available);

            var devices = new List<GpuDeviceInfo> { deviceWithoutMemory };

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId);
            Assert.Contains("fell back to FirstAvailable", result.Reason);
        }

        [Fact]
        public void MostFreeMemory_PartialMemoryInfo_UsesAvailable()
        {
            var memoryError = new GpuMemoryInfo(0, 0, GpuAvailabilityState.Error);
            var deviceWithoutMemory = new GpuDeviceInfo(
                0, "GPU 0", GpuBackendKind.NvidiaSmi, memoryError, GpuAvailabilityState.Available);

            var deviceWithMemory = CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024);

            var devices = new List<GpuDeviceInfo> { deviceWithoutMemory, deviceWithMemory };

            var result = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.SelectedDeviceId); // Select the one with memory info
        }

        #endregion

        #region SpecificDevice Tests

        [Fact]
        public void RoundRobin_RotatesIndependentlyFromFirstAvailable()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 100, 50, GpuAvailabilityState.Available),
                CreateDevice(1, "GPU 1", 200, 100, GpuAvailabilityState.Available),
                CreateDevice(2, "GPU 2", 300, 150, GpuAvailabilityState.Unavailable)
            };

            Assert.Equal(0, _engine.SelectDevice(devices, GpuPolicy.FirstAvailable).SelectedDeviceId);
            Assert.Equal(0, _engine.SelectDevice(devices, GpuPolicy.RoundRobin).SelectedDeviceId);
            Assert.Equal(1, _engine.SelectDevice(devices, GpuPolicy.RoundRobin).SelectedDeviceId);
            Assert.Equal(0, _engine.SelectDevice(devices, GpuPolicy.RoundRobin).SelectedDeviceId);
        }

        [Fact]
        public void SpecificDevice_NoIdProvided_ReturnsFail()
        {
            var device = CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
            var devices = new List<GpuDeviceInfo> { device };

            var result = _engine.SelectDevice(devices, GpuPolicy.SpecificDevice, null);

            Assert.False(result.IsSuccess);
            Assert.Contains("No device ID specified", result.Reason);
        }

        [Fact]
        public void SpecificDevice_ExactMatch_SelectsIt()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.SpecificDevice, 1);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.SelectedDeviceId);
            Assert.Equal("GPU 1", result.SelectedDevice.DeviceName);
        }

        [Fact]
        public void SpecificDevice_NotFound_ReturnsFail()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.SpecificDevice, 5);

            Assert.False(result.IsSuccess);
            Assert.Contains("Device 5 not found", result.Reason);
            Assert.Contains("available: 0, 1", result.Reason);
        }

        [Fact]
        public void SpecificDevice_DeviceUnavailable_ReturnsFail()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024, GpuAvailabilityState.Unavailable)
            };

            var result = _engine.SelectDevice(devices, GpuPolicy.SpecificDevice, 1);

            Assert.False(result.IsSuccess);
            Assert.Contains("is Unavailable", result.Reason);
        }

        #endregion

        #region Reason Text Tests

        [Fact]
        public void SelectionReason_IsDeterministic()
        {
            var devices = new List<GpuDeviceInfo>
            {
                CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024),
                CreateDevice(1, "GPU 1", 24L * 1024 * 1024 * 1024, 8L * 1024 * 1024 * 1024)
            };

            var result1 = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);
            var result2 = _engine.SelectDevice(devices, GpuPolicy.MostFreeMemory);

            Assert.Equal(result1.Reason, result2.Reason);
        }

        [Fact]
        public void FailureReason_ContainsPolicy()
        {
            var devices = new List<GpuDeviceInfo>();

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.Contains("FirstAvailable", result.Reason);
        }

        [Fact]
        public void SuccessReason_ContainsDeviceInfo()
        {
            var device = CreateDevice(0, "Test GPU", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
            var devices = new List<GpuDeviceInfo> { device };

            var result = _engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.Contains("device 0", result.Reason);
            Assert.Contains("Test GPU", result.Reason);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void SelectDevice_NullDeviceList_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _engine.SelectDevice(null, GpuPolicy.FirstAvailable));
        }

        [Fact]
        public void SelectDevice_InvalidPolicy_ReturnsFail()
        {
            var device = CreateDevice(0, "GPU 0", 24L * 1024 * 1024 * 1024, 12L * 1024 * 1024 * 1024);
            var devices = new List<GpuDeviceInfo> { device };

            var result = _engine.SelectDevice(devices, (GpuPolicy)999);

            Assert.False(result.IsSuccess);
            Assert.Contains("Unknown policy", result.Reason);
        }

        #endregion
    }
}
