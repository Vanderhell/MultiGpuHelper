using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;
using MultiGpuHelper.Probing;
using MultiGpuHelper.Logging;

namespace MultiGpuHelper.Tests
{
    public class GpuManagerTests
    {
        private class MockProbeProvider : IGpuProbeProvider
        {
            private readonly List<GpuDevice> _devices;

            public MockProbeProvider(List<GpuDevice>? devices = null)
            {
                _devices = devices ?? new List<GpuDevice>();
            }

            public Task<IList<GpuDevice>> ProbeAsync()
            {
                return Task.FromResult<IList<GpuDevice>>(_devices);
            }
        }

        [Fact]
        public void SelectDevice_RoundRobin_ReturnsEachDeviceInOrder()
        {
            var manager = new GpuManager();
            manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000 });
            manager.AddDevice(new GpuDevice { DeviceId = 1, Name = "GPU1", TotalVramBytes = 1000 });

            var dev0 = manager.SelectDevice(GpuPolicy.RoundRobin);
            var dev1 = manager.SelectDevice(GpuPolicy.RoundRobin);
            var dev2 = manager.SelectDevice(GpuPolicy.RoundRobin);

            Assert.Equal(0, dev0.DeviceId);
            Assert.Equal(1, dev1.DeviceId);
            Assert.Equal(0, dev2.DeviceId);
        }

        [Fact]
        public void SelectDevice_MostFreeVram_SelectsHighestFreeMemory()
        {
            var manager = new GpuManager();
            manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000, FreeVramBytes = 300 });
            manager.AddDevice(new GpuDevice { DeviceId = 1, Name = "GPU1", TotalVramBytes = 1000, FreeVramBytes = 800 });

            var selected = manager.SelectDevice(GpuPolicy.MostFreeVram);

            Assert.Equal(1, selected.DeviceId);
        }

        [Fact]
        public void SelectDevice_SpecificDevice_SelectsCorrectDevice()
        {
            var manager = new GpuManager();
            manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000 });
            manager.AddDevice(new GpuDevice { DeviceId = 1, Name = "GPU1", TotalVramBytes = 1000 });

            var selected = manager.SelectDevice(GpuPolicy.SpecificDevice, 1);

            Assert.Equal(1, selected.DeviceId);
        }

        [Fact]
        public void SelectDevice_NoEnabledDevices_ThrowsException()
        {
            var manager = new GpuManager();
            manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000, IsEnabled = false });

            Assert.Throws<MultiGpuHelper.Exceptions.GpuSelectionException>(() =>
                manager.SelectDevice(GpuPolicy.RoundRobin));
        }

        [Fact]
        public async Task InitializeFromProbeAsync_AddsProbeDevices()
        {
            var devices = new List<GpuDevice>
            {
                new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000 },
                new GpuDevice { DeviceId = 1, Name = "GPU1", TotalVramBytes = 2000 }
            };

            var manager = new GpuManager(new MockProbeProvider(devices));
            await manager.InitializeFromProbeAsync();

            Assert.Equal(2, manager.Devices.Count);
        }

        [Fact]
        public void RemoveDevice_DisablesDevice()
        {
            var manager = new GpuManager();
            manager.AddDevice(new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000 });

            bool removed = manager.RemoveDevice(0);

            Assert.True(removed);
            Assert.Empty(manager.Devices);
        }

        [Fact]
        public void GetDevice_ReturnsCorrectDevice()
        {
            var manager = new GpuManager();
            var device = new GpuDevice { DeviceId = 0, Name = "GPU0", TotalVramBytes = 1000 };
            manager.AddDevice(device);

            var retrieved = manager.GetDevice(0);

            Assert.NotNull(retrieved);
            Assert.Equal("GPU0", retrieved.Name);
        }
    }
}
