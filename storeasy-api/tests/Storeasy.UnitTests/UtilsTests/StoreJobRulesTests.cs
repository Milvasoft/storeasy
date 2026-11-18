using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.Infrastructure.StoreJobs;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Store job queue rules.")]
public class StoreJobRulesTests
{
    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(6, 900)]
    [InlineData(40, 900)]
    public void RetryDelay_ShouldDoubleAndBeCapped(int attempt, int expectedSeconds)
    {
        var delay = StoreJobRules.RetryDelay(attempt, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(900));

        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void RetryDelay_WithoutABaseDelay_ShouldBeZero()
    {
        StoreJobRules.RetryDelay(3, TimeSpan.Zero, TimeSpan.FromSeconds(900)).Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(1, 3, true, true)]
    [InlineData(3, 3, true, false)]
    [InlineData(1, 3, false, false)]
    public void ShouldRetry_OnlyTransientFailuresWithAttemptsLeft(int attempt, int maxAttempts, bool transient, bool expected)
    {
        StoreJobRules.ShouldRetry(attempt, maxAttempts, transient).Should().Be(expected);
    }

    [Theory]
    [InlineData(StoreJobStatus.Queued, false)]
    [InlineData(StoreJobStatus.Running, false)]
    [InlineData(StoreJobStatus.Succeeded, true)]
    [InlineData(StoreJobStatus.Failed, true)]
    [InlineData(StoreJobStatus.Cancelled, true)]
    public void IsFinished_ShouldBeTrueForFinalStates(StoreJobStatus status, bool expected)
    {
        StoreJobRules.IsFinished(status).Should().Be(expected);
    }

    [Fact]
    public void IsTransient_ShouldClassifyFailures()
    {
        StoreJobExecutor.IsTransient(new StoreJobException("503", transient: true)).Should().BeTrue();
        StoreJobExecutor.IsTransient(new StoreJobException("400", transient: false)).Should().BeFalse();
        StoreJobExecutor.IsTransient(new HttpRequestException("reset")).Should().BeTrue();
        StoreJobExecutor.IsTransient(new TimeoutException()).Should().BeTrue();
        StoreJobExecutor.IsTransient(new InvalidOperationException("bug")).Should().BeFalse();
    }
}
