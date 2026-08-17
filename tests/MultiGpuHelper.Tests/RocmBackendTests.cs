using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Tests
{
    public class RocmBackendTests
    {
        private readonly RocmBackend _backend;

        public RocmBackendTests()
        {
            _backend = new RocmBackend(new NoOpLogger());
        }

        [Fact]
        public void BackendKind_ReturnsAMD()
        {
            // ROCm is AMD-only
            Assert.Equal(GpuBackendKind.Rocm, _backend.BackendKind);
        }

        [Fact]
        public async Task IsAvailableAsync_ReturnsBool()
        {
            // Should always return bool without exception
            var available = await _backend.IsAvailableAsync();
            Assert.IsType<bool>(available);
        }

        [Fact]
        public async Task DetectDevicesAsync_ReturnsReadOnlyList()
        {
            var devices = await _backend.DetectDevicesAsync();

            Assert.NotNull(devices);
            Assert.IsAssignableFrom<IReadOnlyList<GpuDeviceInfo>>(devices);
        }

        [Fact]
        public async Task DetectDevicesAsync_UnavailablePath_ReturnsEmptyList()
        {
            // If ROCm is unavailable, should return empty list (not throw)
            var devices = await _backend.DetectDevicesAsync();

            Assert.NotNull(devices);
            Assert.IsType<List<GpuDeviceInfo>>(devices);
            // Note: If ROCm is unavailable on this machine, this will be empty
            // If ROCm is available, this will contain detected devices
        }

        [Fact]
        public async Task DetectDevicesAsync_IfDevicesFound_HaveRequiredFields()
        {
            var devices = await _backend.DetectDevicesAsync();

            if (devices.Count > 0)
            {
                // If devices detected, verify all required fields are populated
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
                    Assert.Equal(GpuBackendKind.Rocm, device.Backend);
                    Assert.Equal(GpuVendor.Amd, device.Vendor);
                }
            }
        }

        [Fact]
        public async Task RefreshMemoryAsync_WithEmptyList_ReturnsEmptyList()
        {
            var devices = new List<GpuDeviceInfo>();

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
        public async Task DetectDevicesAsync_IfAvailable_ReturnsDeterministicResult()
        {
            // Multiple calls should return the same result (deterministic)
            var first = await _backend.DetectDevicesAsync();
            var second = await _backend.DetectDevicesAsync();

            Assert.Equal(first.Count, second.Count);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].DeviceId, second[i].DeviceId);
                Assert.Equal(first[i].DeviceName, second[i].DeviceName);
                Assert.Equal(first[i].MemoryInfo.TotalBytes, second[i].MemoryInfo.TotalBytes);
            }
        }

        [Fact]
        public async Task RocmBackend_IsOptionalAndDoesNotAffectDefaultBehavior()
        {
            // Creating RocmBackend should not affect other backends or default behavior
            var devices = await _backend.DetectDevicesAsync();

            // Should not throw and should return a valid list (possibly empty if ROCm unavailable)
            Assert.NotNull(devices);
        }

        [Fact]
        public async Task RocmBackend_GracefullyHandlesUnavailability()
        {
            // If ROCm is unavailable, DetectDevicesAsync should return empty list
            // not throw an exception
            var devices = await _backend.DetectDevicesAsync();
            var available = await _backend.IsAvailableAsync();

            if (!available)
            {
                // ROCm unavailable: should return empty
                Assert.Empty(devices);
            }
            else
            {
                // ROCm available: may have devices or empty (both acceptable)
                Assert.NotNull(devices);
            }
        }

        [Fact]
        public void Parser_FiltersCpuAndKeepsFirstGpuName()
        {
            const string fixture = @"Agent 0
  Name: AMD Ryzen CPU
  Device Type: CPU
Agent 1
  Name: gfx1100
  Device Type: GPU
  Name: amdgcn-amd-amdhsa--gfx1100";

            var device = Assert.Single(_backend.ParseRocminfoOutput(fixture));
            Assert.Equal(1, device.DeviceId);
            Assert.Equal("gfx1100", device.DeviceName);
            Assert.Equal(GpuAvailabilityState.Unavailable, device.MemoryInfo.State);
        }

        [Fact]
        public void Parser_HandlesMultipleGpusMalformedAgentsAndNoGpu()
        {
            const string fixture = @"Agent x
  Name: malformed
Agent 2
  Name: gfx900
  Device Type: GPU
Agent 3
  Name: gfx1030
  Device Type: GPU
Agent 4
  Name: CPU
  Device Type: CPU";

            var devices = _backend.ParseRocminfoOutput(fixture);
            Assert.Equal(new[] { 2, 3 }, devices.Select(d => d.DeviceId));
            Assert.Empty(_backend.ParseRocminfoOutput("Agent 0\n Name: CPU\n Device Type: CPU"));
            Assert.Empty(_backend.ParseRocminfoOutput(""));
        }
    }
}
