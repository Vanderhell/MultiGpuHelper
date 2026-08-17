using System.Collections.Generic;
using System.Threading.Tasks;
using MultiGpuHelper.Backends;
using MultiGpuHelper.Enums;
using MultiGpuHelper.Models;
using Xunit;

namespace MultiGpuHelper.Tests
{
    public class WmiBackendTests
    {
        private readonly WmiBackend _backend = new WmiBackend();

        [Fact]
        public void BackendKind_IsWmi()
        {
            Assert.Equal(GpuBackendKind.Wmi, _backend.BackendKind);
        }

        [Theory]
        [InlineData("NVIDIA GeForce RTX 4090", "", GpuVendor.Nvidia)]
        [InlineData("Display adapter", "PCI\\VEN_1002&DEV_744C", GpuVendor.Amd)]
        [InlineData("Intel Arc A770", "", GpuVendor.Intel)]
        [InlineData("Generic display adapter", "", GpuVendor.Unknown)]
        public void DetectVendor_UsesActualMapping(string name, string pnpId, GpuVendor expected)
        {
            Assert.Equal(expected, WmiBackend.DetectVendor(name, pnpId));
        }

        [Fact]
        public async Task DetectDevicesAsync_ReturnsAListWithoutRequiringHardware()
        {
            IReadOnlyList<GpuDeviceInfo> devices = await _backend.DetectDevicesAsync();
            Assert.NotNull(devices);
            foreach (var device in devices)
            {
                Assert.Equal(GpuBackendKind.Wmi, device.Backend);
                Assert.Equal(GpuAvailabilityState.Unavailable, device.MemoryInfo.State);
            }
        }

        [Fact]
        public async Task RefreshMemoryAsync_EmptyInput_RemainsEmpty()
        {
            var result = await _backend.RefreshMemoryAsync(new List<GpuDeviceInfo>());
            Assert.Empty(result);
        }
    }
}
