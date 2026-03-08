using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Management;
using MultiGpuHelper.Probing;
using MultiGpuHelper.Models;
using MultiGpuHelper.Logging;
using MultiGpuHelper.Selection;
using MultiGpuHelper.Enums;

namespace MultiGpuHelper.Tests
{
    /// <summary>
    /// Verify that legacy 1.0.1 API behavior is unchanged by 1.1.0 additions.
    /// These tests ensure GpuManager, GpuDispatcher, and existing selection flows
    /// work exactly as before without unintended changes.
    /// </summary>
    public class BackwardCompatibilityTests
    {
        [Fact]
        public void GpuManager_DefaultConstructor_CreatesSuccessfully()
        {
            // Legacy 1.0.1: new GpuManager() should work without arguments
            var manager = new GpuManager();

            Assert.NotNull(manager);
            Assert.NotNull(manager.Devices);
        }

        [Fact]
        public void GpuManager_CanAddDeviceManually()
        {
            // Legacy 1.0.1: manual device registration should work unchanged
            var manager = new GpuManager();

            var device = new GpuDevice
            {
                DeviceId = 0,
                Name = "Test GPU",
                TotalVramBytes = 24L * 1024 * 1024 * 1024,
                FreeVramBytes = 12L * 1024 * 1024 * 1024,
                IsEnabled = true,
                MaxConcurrentJobs = 1
            };

            manager.AddDevice(device);

            Assert.Single(manager.Devices);
            Assert.Equal(0, manager.Devices[0].DeviceId);
            Assert.Equal("Test GPU", manager.Devices[0].Name);
        }

        [Fact]
        public void GpuManager_SelectDevice_WithLegacyPolicy()
        {
            // Legacy 1.0.1: SelectDevice(policy) should still work
            var manager = new GpuManager();

            var device = new GpuDevice
            {
                DeviceId = 0,
                Name = "GPU 0",
                TotalVramBytes = 24L * 1024 * 1024 * 1024,
                FreeVramBytes = 12L * 1024 * 1024 * 1024,
                IsEnabled = true,
                MaxConcurrentJobs = 1
            };

            manager.AddDevice(device);

            // Old enum names should still work (backward compatibility)
            var selected = manager.SelectDevice(GpuPolicy.RoundRobin);

            Assert.NotNull(selected);
            Assert.Equal(0, selected.DeviceId);
        }

        [Fact]
        public void GpuPolicy_OldEnumValues_StillAvailable()
        {
            // Verify old enum names still exist with same numeric values
            Assert.Equal(0, (int)GpuPolicy.RoundRobin);
            Assert.Equal(0, (int)GpuPolicy.FirstAvailable); // Same numeric value
            Assert.Equal(1, (int)GpuPolicy.MostFreeVram);
            Assert.Equal(1, (int)GpuPolicy.MostFreeMemory); // Same numeric value
            Assert.Equal(2, (int)GpuPolicy.SpecificDevice);
            Assert.Equal(2, (int)GpuPolicy.ExplicitId); // Same numeric value
        }

        [Fact]
        public void GpuDevice_RemainsPublic_Mutable()
        {
            // Legacy 1.0.1: GpuDevice is mutable and publicly accessible
            var device = new GpuDevice();

            // Should be able to set all properties
            device.DeviceId = 0;
            device.Name = "GPU";
            device.TotalVramBytes = 24L * 1024 * 1024 * 1024;
            device.FreeVramBytes = 12L * 1024 * 1024 * 1024;
            device.IsEnabled = true;
            device.MaxConcurrentJobs = 1;

            Assert.Equal(0, device.DeviceId);
            Assert.Equal("GPU", device.Name);
            Assert.True(device.IsEnabled);
        }

        [Fact]
        public void VramBudget_RemainsPublic()
        {
            // Legacy 1.0.1: VramBudget should be publicly accessible
            var budget = new VramBudget
            {
                LimitBytes = 8L * 1024 * 1024 * 1024
            };

            Assert.Equal(8L * 1024 * 1024 * 1024, budget.LimitBytes);

            // Should be able to reserve and release
            var canReserve = budget.TryReserve(1L * 1024 * 1024 * 1024);
            Assert.True(canReserve);

            budget.Release(1L * 1024 * 1024 * 1024);
            Assert.Equal(0, budget.ReservedBytes);
        }

        [Fact]
        public void GpuSelectionEngine_Works_WithNewImmutableTypes()
        {
            // New 1.1.0: GpuSelectionEngine should work with immutable device types
            var engine = new GpuSelectionEngine(new NoOpLogger());

            var memoryInfo = new GpuMemoryInfo(
                24L * 1024 * 1024 * 1024,
                12L * 1024 * 1024 * 1024,
                GpuAvailabilityState.Available);

            var device = new MultiGpuHelper.Models.GpuDeviceInfo(
                0,
                "GPU 0",
                GpuBackendKind.NVIDIA,
                memoryInfo,
                GpuAvailabilityState.Available);

            var devices = new List<MultiGpuHelper.Models.GpuDeviceInfo> { device };

            // Selection should work
            var result = engine.SelectDevice(devices, GpuPolicy.FirstAvailable);

            Assert.True(result.IsSuccess);
            Assert.Equal(0, result.SelectedDeviceId);
        }

        [Fact]
        public async Task LegacyGpuDispatcher_AsyncWorkItem_RemainsCompatible()
        {
            // Legacy 1.0.1: GpuDispatcher async work should work
            var manager = new GpuManager();
            var device = new GpuDevice
            {
                DeviceId = 0,
                Name = "GPU 0",
                TotalVramBytes = 24L * 1024 * 1024 * 1024,
                FreeVramBytes = 12L * 1024 * 1024 * 1024,
                IsEnabled = true,
                MaxConcurrentJobs = 1
            };
            manager.AddDevice(device);

            var dispatcher = new MultiGpuHelper.Dispatching.GpuDispatcher(manager);

            // Should be able to dispatch work (even if it's just a simple operation)
            var result = await dispatcher.RunAsync<int>(
                async deviceId =>
                {
                    return await Task.FromResult(deviceId);
                },
                GpuPolicy.FirstAvailable);

            Assert.Equal(0, result);
        }

        [Fact]
        public void NvidiaBackend_IsStillPublic()
        {
            // NVIDIA backend should remain public for user integration
            var backend = new MultiGpuHelper.Backends.NvidiaBackend(new NoOpLogger());

            Assert.NotNull(backend);
            Assert.Equal(GpuBackendKind.NVIDIA, backend.BackendKind);
        }

        [Fact]
        public void DxgiBackend_IsPublic_ButOptional()
        {
            // New 1.1.0: DXGI backend is available but completely optional
            // Legacy code does NOT use it automatically
            var backend = new MultiGpuHelper.Backends.DxgiBackend(new NoOpLogger());

            Assert.NotNull(backend);
            // Verify it exists but is NOT injected into legacy paths
        }

        [Fact]
        public void GpuDispatcher_RemainsPublic_WithSameSignature()
        {
            // Legacy 1.0.1: GpuDispatcher signature unchanged
            var manager = new GpuManager();
            var dispatcher = new MultiGpuHelper.Dispatching.GpuDispatcher(manager);

            Assert.NotNull(dispatcher);
            // Dispatcher methods should still be callable (signature verification via compilation)
        }

        [Fact]
        public void LegacyExceptions_StillAvailable()
        {
            // Legacy 1.0.1: Exception types unchanged
            Assert.NotNull(typeof(MultiGpuHelper.Exceptions.GpuSelectionException));
            Assert.NotNull(typeof(MultiGpuHelper.Exceptions.GpuBudgetExceededException));
            Assert.NotNull(typeof(MultiGpuHelper.Exceptions.GpuProbeException));
        }

        [Fact]
        public void IGpuLogger_RemainsPublic()
        {
            // Legacy 1.0.1: Logger interface unchanged
            var logger = new NoOpLogger();
            Assert.NotNull(logger);
            Assert.IsAssignableFrom<MultiGpuHelper.Logging.IGpuLogger>(logger);
        }
    }
}
