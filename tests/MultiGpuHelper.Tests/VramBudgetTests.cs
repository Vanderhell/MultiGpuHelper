using System.Threading.Tasks;
using Xunit;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Tests
{
    public class VramBudgetTests
    {
        [Fact]
        public void TryReserve_WithinLimit_Succeeds()
        {
            var budget = new VramBudget { LimitBytes = 1000 };

            bool result = budget.TryReserve(500);

            Assert.True(result);
            Assert.Equal(500, budget.ReservedBytes);
        }

        [Fact]
        public void TryReserve_ExceedsLimit_Fails()
        {
            var budget = new VramBudget { LimitBytes = 1000 };
            budget.TryReserve(800);

            bool result = budget.TryReserve(300);

            Assert.False(result);
            Assert.Equal(800, budget.ReservedBytes);
        }

        [Fact]
        public void Release_ReducesReservedBytes()
        {
            var budget = new VramBudget { LimitBytes = 1000 };
            budget.TryReserve(500);

            budget.Release(200);

            Assert.Equal(300, budget.ReservedBytes);
        }

        [Fact]
        public void Release_BelowZero_ClampsToZero()
        {
            var budget = new VramBudget();
            budget.TryReserve(500);

            budget.Release(600);

            Assert.Equal(0, budget.ReservedBytes);
        }

        [Fact]
        public void CanReserve_WithoutLimitBytes()
        {
            var budget = new VramBudget();

            bool result = budget.CanReserve(long.MaxValue);

            Assert.True(result);
        }

        [Fact]
        public void TryReserve_UnlimitedBudget_AlwaysSucceeds()
        {
            var budget = new VramBudget();

            bool result1 = budget.TryReserve(1000000);
            bool result2 = budget.TryReserve(1000000);

            Assert.True(result1);
            Assert.True(result2);
        }
    }
}
