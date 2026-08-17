using System;
using System.Threading;
using System.Threading.Tasks;
using MultiGpuHelper.Dispatching;
using MultiGpuHelper.Management;
using MultiGpuHelper.Models;
using Xunit;

namespace MultiGpuHelper.Tests
{
    public class GpuDispatcherTests
    {
        [Fact]
        public async Task CallerCancellationBeforeDispatch_CancelsCallback()
        {
            var dispatcher = CreateDispatcher(out _);
            using var source = new CancellationTokenSource();
            source.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.RunAsync(
                (id, token) => Task.FromResult(id),
                GpuPolicy.FirstAvailable,
                ct: source.Token));
        }

        [Fact]
        public async Task ExecutionTimeout_ReachesCallback()
        {
            var dispatcher = CreateDispatcher(out _);
            var item = new GpuWorkItem { TimeoutMs = 20 };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.RunAsync<int>(
                async (_, token) =>
                {
                    await Task.Delay(Timeout.Infinite, token);
                    return 0;
                },
                GpuPolicy.FirstAvailable,
                item));
        }

        [Fact]
        public async Task CallbackException_PropagatesUnchanged()
        {
            var dispatcher = CreateDispatcher(out _);
            var expected = new InvalidOperationException("callback failed");

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.RunAsync<int>(
                (_, __) => Task.FromException<int>(expected),
                GpuPolicy.FirstAvailable));

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task Reservation_IsReleasedAfterSuccessAndFailure()
        {
            var dispatcher = CreateDispatcher(out var device);
            var item = new GpuWorkItem { RequestedVramBytes = 128 };

            await dispatcher.RunAsync((_, __) => Task.FromResult(1), GpuPolicy.FirstAvailable, item);
            Assert.Equal(0, device.VramBudget.ReservedBytes);

            await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.RunAsync<int>(
                (_, __) => Task.FromException<int>(new InvalidOperationException()),
                GpuPolicy.FirstAvailable,
                item));
            Assert.Equal(0, device.VramBudget.ReservedBytes);
        }

        [Fact]
        public async Task MaxConcurrency_QueuesSecondCallback()
        {
            var dispatcher = CreateDispatcher(out _);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondEntered = false;

            var first = dispatcher.RunAsync<int>(async (_, token) =>
            {
                entered.SetResult(true);
                await release.Task;
                return 1;
            }, GpuPolicy.FirstAvailable);

            await entered.Task;
            var second = dispatcher.RunAsync((_, __) =>
            {
                secondEntered = true;
                return Task.FromResult(2);
            }, GpuPolicy.FirstAvailable);

            await Task.Yield();
            Assert.False(secondEntered);
            release.SetResult(true);
            Assert.Equal(1, await first);
            Assert.Equal(2, await second);
        }

        private static GpuDispatcher CreateDispatcher(out GpuDevice device)
        {
            var manager = new GpuManager();
            device = new GpuDevice
            {
                DeviceId = 0,
                Name = "Test GPU",
                TotalVramBytes = 1024,
                FreeVramBytes = 1024,
                MaxConcurrentJobs = 1,
                VramBudget = new VramBudget { LimitBytes = 1024 }
            };
            manager.AddDevice(device);
            return new GpuDispatcher(manager);
        }
    }
}
