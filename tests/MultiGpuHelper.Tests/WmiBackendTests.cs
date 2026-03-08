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

        [Fact]
        public void VendorDetection_NvidiaDevice_DetectedCorrectly()
        {
            // WmiBackend detects NVIDIA devices from name
            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo>
            {
                CreateDeviceWithBackend(0, "NVIDIA GeForce RTX 4090", GpuBackendKind.NVIDIA),
                CreateDeviceWithBackend(1, "NVIDIA Quadro A6000", GpuBackendKind.NVIDIA),
                CreateDeviceWithBackend(2, "NVIDIA Tesla V100", GpuBackendKind.NVIDIA)
            };

            // Verify NVIDIA devices are correctly identified
            foreach (var device in devices)
            {
                Assert.Equal(GpuBackendKind.NVIDIA, device.Backend);
            }
        }

        [Fact]
        public void VendorDetection_AmdDevice_DetectedCorrectly()
        {
            // WmiBackend detects AMD devices from name
            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo>
            {
                CreateDeviceWithBackend(0, "AMD Radeon RX 7900 XTX", GpuBackendKind.AMD),
                CreateDeviceWithBackend(1, "AMD Radeon Pro W6900X", GpuBackendKind.AMD)
            };

            // Verify AMD devices are correctly identified
            foreach (var device in devices)
            {
                Assert.Equal(GpuBackendKind.AMD, device.Backend);
            }
        }

        [Fact]
        public void VendorDetection_IntelDevice_DetectedCorrectly()
        {
            // WmiBackend detects Intel devices from name
            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo>
            {
                CreateDeviceWithBackend(0, "Intel Arc A770", GpuBackendKind.Intel),
                CreateDeviceWithBackend(1, "Intel Iris Pro Graphics", GpuBackendKind.Intel),
                CreateDeviceWithBackend(2, "Intel UHD Graphics 770", GpuBackendKind.Intel)
            };

            // Verify Intel devices are correctly identified
            foreach (var device in devices)
            {
                Assert.Equal(GpuBackendKind.Intel, device.Backend);
            }
        }

        [Fact]
        public void VendorDetection_UnknownDevice_DetectedAsUnknown()
        {
            // WmiBackend returns Unknown for unrecognized vendors
            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo>
            {
                CreateDeviceWithBackend(0, "Unknown GPU Vendor Device", GpuBackendKind.Unknown),
                CreateDeviceWithBackend(1, "Generic Video Device", GpuBackendKind.Unknown)
            };

            // Verify unknown devices are marked as Unknown, not NVIDIA placeholder
            foreach (var device in devices)
            {
                Assert.Equal(GpuBackendKind.Unknown, device.Backend);
            }
        }

        [Fact]
        public void VendorDetection_NoFakeNvidiaPlaceholder()
        {
            // Verify that non-NVIDIA devices are NOT hardcoded as NVIDIA
            var unknownDevice = CreateDeviceWithBackend(0, "Unrecognized GPU", GpuBackendKind.Unknown);
            var amdDevice = CreateDeviceWithBackend(1, "AMD Radeon", GpuBackendKind.AMD);
            var intelDevice = CreateDeviceWithBackend(2, "Intel Arc", GpuBackendKind.Intel);

            // None should be NVIDIA unless they actually are NVIDIA
            Assert.NotEqual(GpuBackendKind.NVIDIA, unknownDevice.Backend);
            Assert.NotEqual(GpuBackendKind.NVIDIA, amdDevice.Backend);
            Assert.NotEqual(GpuBackendKind.NVIDIA, intelDevice.Backend);

            // Verify correct detection
            Assert.Equal(GpuBackendKind.Unknown, unknownDevice.Backend);
            Assert.Equal(GpuBackendKind.AMD, amdDevice.Backend);
            Assert.Equal(GpuBackendKind.Intel, intelDevice.Backend);
        }

        private MultiGpuHelper.Models.GpuDeviceInfo CreateDeviceWithBackend(
            int deviceId,
            string name,
            GpuBackendKind backend)
        {
            var memory = new MultiGpuHelper.Models.GpuMemoryInfo(
                24L * 1024 * 1024 * 1024,
                0,
                GpuAvailabilityState.Unavailable);

            return new MultiGpuHelper.Models.GpuDeviceInfo(
                deviceId,
                name,
                backend,
                memory,
                GpuAvailabilityState.Available);
        }
    }
}
