using SocialApi.Caching;
using Xunit;

namespace SocialTests;

public class CoalescingTests
{
    [Fact]
    public async Task GetOrComputeAsync_RunsOnlyOnce_ForConcurrentRequests()
    {
        // Arrange
        var coalescer = new RequestCoalescer<string, string>();
        int computeCount = 0;

        Func<Task<string>> factory = async () =>
        {
            Interlocked.Increment(ref computeCount);
            await Task.Delay(100); // Simulate work
            return "Data";
        };

        // Act - 100 concurrent tasks
        var tasks = new Task<string>[100];
        for (int i = 0; i < 100; i++)
        {
            tasks[i] = Task.Run(() => coalescer.GetOrComputeAsync("ColdKey", factory));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, computeCount); // DB hit exactly once
        Assert.All(results, r => Assert.Equal("Data", r)); // All 100 got the data
    }
}