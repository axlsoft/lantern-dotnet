using Xunit;

namespace Lantern.Telemetry.Tests;

public class LanternScopeTests
{
    [Fact]
    public void Current_IsNull_WhenNoScopeActive()
    {
        Assert.Null(LanternScope.Current);
    }

    [Fact]
    public void ForTest_SetsCurrent_AndRestoresOnDispose()
    {
        Assert.Null(LanternScope.Current);

        using (LanternScope.ForTest("test-1", "My Test"))
        {
            Assert.Equal("test-1", LanternScope.Current?.TestId);
            Assert.Equal("My Test", LanternScope.Current?.TestName);
        }

        Assert.Null(LanternScope.Current);
    }

    [Fact]
    public void NestedScopes_RestoreOuterContextOnInnerDispose()
    {
        using (LanternScope.ForTest("outer", "Outer Test"))
        {
            Assert.Equal("outer", LanternScope.Current?.TestId);

            using (LanternScope.ForTest("inner", "Inner Test"))
            {
                Assert.Equal("inner", LanternScope.Current?.TestId);
            }

            Assert.Equal("outer", LanternScope.Current?.TestId);
        }

        Assert.Null(LanternScope.Current);
    }

    [Fact]
    public async Task AsyncContinuations_PreserveContext()
    {
        using (LanternScope.ForTest("async-test"))
        {
            await Task.Yield();
            Assert.Equal("async-test", LanternScope.Current?.TestId);

            await Task.Delay(1);
            Assert.Equal("async-test", LanternScope.Current?.TestId);
        }
    }

    [Fact]
    public async Task ParallelTasks_HaveIndependentContexts()
    {
        // Each task establishes its own scope; they should not interfere.
        var task1 = Task.Run(async () =>
        {
            using (LanternScope.ForTest("task-1"))
            {
                await Task.Delay(10);
                return LanternScope.Current?.TestId;
            }
        });

        var task2 = Task.Run(async () =>
        {
            using (LanternScope.ForTest("task-2"))
            {
                await Task.Delay(10);
                return LanternScope.Current?.TestId;
            }
        });

        var results = await Task.WhenAll(task1, task2);
        Assert.Contains("task-1", results);
        Assert.Contains("task-2", results);
    }

    [Fact]
    public void ForTest_ThrowsOnEmptyTestId()
    {
        Assert.Throws<ArgumentException>(() => LanternScope.ForTest(""));
        Assert.Throws<ArgumentException>(() => LanternScope.ForTest("   "));
    }
}
