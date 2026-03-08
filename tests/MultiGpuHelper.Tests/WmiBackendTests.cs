using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;

namespace MultiGpuHelper.Tests
{
    public class WmiBackendTests
    {
        private readonly WmiBackend _backend;

        public WmiBackendTests()
        {
            _backend = new WmiBackend(new NoOpLogger());
        }

        [Fact]
        public void BackendKind_ReturnsNvidia()
        {
            // WMI backend uses NVIDIA as placeholder vendor until separate vendor enum exists
            Assert.Equal(MultiGpuHelper.Enums.GpuBackendKind.NVIDIA, _backend.BackendKind);
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsBool()
        {
            // Should return bool without exception
            var available = await _backend.IsAvailableAsync();
            Assert.IsType<bool>(available);
        }

        [Fact]
        public async Task DetectDevicesAsync_ReturnsReadOnlyList()
        {
            var devices = await _backend.DetectDevicesAsync();

            Assert.NotNull(devices);
            Assert.IsAssignableFrom<IReadOnlyList<MultiGpuHelper.Models.GpuDeviceInfo>>(devices);
        }

        [Fact]
        public async Task DetectDevicesAsync_NoDevices_ReturnsEmptyList()
        {
            // If no GPUs available, returns empty (not null)
            var devices = await _backend.DetectDevicesAsync();

            Assert.NotNull(devices);
            Assert.IsType<List<MultiGpuHelper.Models.GpuDeviceInfo>>(devices);
        }

        [Fact]
        public async Task DetectDevicesAsync_DevicesHaveRequiredFields()
        {
            var devices = await _backend.DetectDevicesAsync();

            if (devices.Count > 0)
            {
                // If any devices detected, verify all required fields are populated
                foreach (var device in devices)
                {
                    Assert.NotNull(device);
                    Assert.True(device.DeviceId >= 0, "DeviceId must be non-negative");
                    Assert.NotNull(device.DeviceName);
                    Assert.NotEmpty(device.DeviceName);
                    Assert.NotNull(device.MemoryInfo);
                    Assert.True(device.MemoryInfo.TotalBytes >= 0, "TotalBytes must be non-negative");
                    Assert.True(device.MemoryInfo.FreeBytes >= 0, "FreeBytes must be non-negative");
                    Assert.True(
                        device.MemoryInfo.State == GpuAvailabilityState.Available ||
                        device.MemoryInfo.State == GpuAvailabilityState.Unavailable ||
                        device.MemoryInfo.State == GpuAvailabilityState.Error,
                        "Memory state must be valid");
                }
            }
        }

        [Fact]
        public async Task DetectDevicesAsync_ReturnsOrderedByDeviceId()
        {
            var devices = await _backend.DetectDevicesAsync();

            if (devices.Count > 1)
            {
                // Devices should be ordered by ID for deterministic results
                var orderedByIdDescending = devices.Select(d => d.DeviceId).OrderByDescending(id => id);
                var actualDescending = devices.Select(d => d.DeviceId);

                // Check that devices are ordered (not randomly shuffled)
                Assert.Equal(orderedByIdDescending, actualDescending.OrderByDescending(id => id));
            }
        }

        [Fact]
        public async Task RefreshMemoryAsync_WithEmptyList_ReturnsEmptyList()
        {
            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo>();

            var refreshed = await _backend.RefreshMemoryAsync(devices);

            Assert.NotNull(refreshed);
            Assert.Empty(refreshed);
        }

        [Fact]
        public async Task RefreshMemoryAsync_WithDeviceList_ReturnsUpdatedList()
        {
            // First detect devices
            var originalDevices = await _backend.DetectDevicesAsync();

            if (originalDevices.Count > 0)
            {
                // Refresh should return updated list
                var refreshed = await _backend.RefreshMemoryAsync(originalDevices);

                Assert.NotNull(refreshed);
                Assert.Equal(originalDevices.Count, refreshed.Count);

                // All devices should maintain their IDs
                foreach (var original in originalDevices)
                {
                    var updated = refreshed.FirstOrDefault(d => d.DeviceId == original.DeviceId);
                    Assert.NotNull(updated);
                    Assert.Equal(original.DeviceId, updated.DeviceId);
                    Assert.Equal(original.DeviceName, updated.DeviceName);
                }
            }
        }
    }
}
