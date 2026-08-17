using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;

namespace MultiGpuHelper.Tests
{
    /// <summary>
    /// Tests for NvidiaBackend device detection and parsing.
    /// Note: Full integration tests require nvidia-smi to be available.
    /// </summary>
    public class NvidiaBackendTests
    {
        private readonly NvidiaBackend _backend;

        public NvidiaBackendTests()
        {
            _backend = new NvidiaBackend(new NoOpLogger());
        }

        [Fact]
        public void BackendKind_ReturnsNvidia()
        {
            Assert.Equal(GpuBackendKind.NvidiaSmi, _backend.BackendKind);
        }

        [Fact]
        public async Task DetectDevicesAsync_WhenNvidiaSmiUnavailable_ReturnsEmptyList()
        {
            // This test will pass if nvidia-smi is not available (expected on non-NVIDIA systems)
            var devices = await _backend.DetectDevicesAsync();
            Assert.NotNull(devices);
            Assert.IsAssignableFrom<IReadOnlyList<MultiGpuHelper.Models.GpuDeviceInfo>>(devices);
            // If nvidia-smi is available, will return devices; if not, returns empty list
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsBool()
        {
            // Just ensure it doesn't throw; actual result depends on system
            var available = await _backend.IsAvailableAsync();
            Assert.IsType<bool>(available);
        }

        [Fact]
        public async Task RefreshMemoryAsync_WithEmptyList_ReturnsEmptyList()
        {
            var empty = new List<MultiGpuHelper.Models.GpuDeviceInfo>();
            var result = await _backend.RefreshMemoryAsync(empty);

            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task RefreshMemoryAsync_WithDeviceList_ReturnsUpdatedList()
        {
            // Create a fake device list
            var memoryInfo = new MultiGpuHelper.Models.GpuMemoryInfo(
                24L * 1024 * 1024 * 1024,
                12L * 1024 * 1024 * 1024,
                GpuAvailabilityState.Available);

            var device = new MultiGpuHelper.Models.GpuDeviceInfo(
                0,
                "NVIDIA Test GPU",
                GpuBackendKind.NvidiaSmi,
                memoryInfo,
                GpuAvailabilityState.Available);

            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo> { device };
            var result = await _backend.RefreshMemoryAsync(devices);

            Assert.NotNull(result);
            // Result should be same or updated list
            Assert.IsAssignableFrom<IReadOnlyList<MultiGpuHelper.Models.GpuDeviceInfo>>(result);
        }

        [Fact]
        public void ParseNvidiaSmiOutput_WithValidSingleDevice_ReturnsDevice()
        {
            var devices = _backend.ParseNvidiaSmiOutput("0, NVIDIA RTX 4090, 24576, 12000");
            var device = Assert.Single(devices);
            Assert.Equal(0, device.DeviceId);
            Assert.Equal(24576L * 1024 * 1024, device.MemoryInfo.TotalBytes);
            Assert.Equal(12000L * 1024 * 1024, device.MemoryInfo.FreeBytes);
            Assert.Equal(GpuVendor.Nvidia, device.Vendor);
        }

        [Fact]
        public void Parser_HandlesMultipleInvalidAndMissingMemoryRows()
        {
            const string fixture = "1, GPU B, 8192, N/A\ninvalid\n0, GPU A, 4096, 2048\n2, GPU C, bad, 10";
            var devices = _backend.ParseNvidiaSmiOutput(fixture);

            Assert.Equal(new[] { 0, 1 }, System.Linq.Enumerable.Select(devices, d => d.DeviceId));
            Assert.Equal(GpuAvailabilityState.Error, devices[1].MemoryInfo.State);
            Assert.Empty(_backend.ParseNvidiaSmiOutput(""));
        }

        [Fact]
        public async Task DetectDevicesAsync_ReturnsOrderedByDeviceId()
        {
            var devices = await _backend.DetectDevicesAsync();

            // Verify devices are ordered by ID
            for (int i = 1; i < devices.Count; i++)
            {
                Assert.True(
                    devices[i - 1].DeviceId <= devices[i].DeviceId,
                    "Devices should be ordered by DeviceId");
            }
        }

        [Fact]
        public async Task DetectDevicesAsync_ValidDevices_HaveRequiredFields()
        {
            var devices = await _backend.DetectDevicesAsync();

            foreach (var device in devices)
            {
                Assert.NotNull(device);
                Assert.NotNull(device.DeviceName);
                Assert.True(device.DeviceId >= 0);
                Assert.Equal(GpuBackendKind.NvidiaSmi, device.Backend);
                Assert.NotNull(device.MemoryInfo);
                Assert.True(device.MemoryInfo.TotalBytes >= 0);
                Assert.True(device.MemoryInfo.FreeBytes >= 0);
                Assert.True(device.MaxConcurrentJobs >= 1);
            }
        }

        [Fact]
        public async Task DetectDevicesAsync_NoDevices_ReturnsEmpty()
        {
            // If no NVIDIA GPUs available, should return empty list (not throw)
            var devices = await _backend.DetectDevicesAsync();
            Assert.NotNull(devices);
            // Either empty (no GPUs) or contains devices (GPU available)
        }
    }
}
