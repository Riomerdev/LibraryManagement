using FeatureGate.Domain;

namespace FeatureGate.Tests;

public class FeatureFlagRolloutEvaluatorTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(100, true)]
    [InlineData(-10, false)]
    [InlineData(110, true)]
    public void IsInRollout_ClampsPercentage(int percentage, bool expected)
    {
        var result = FeatureFlagRolloutEvaluator.IsInRollout(
            "new-checkout",
            "user-001",
            percentage);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsInRollout_IsStableForSameFlagAndUser()
    {
        var first = FeatureFlagRolloutEvaluator.IsInRollout(
            "new-checkout",
            "user-001",
            10);

        var second = FeatureFlagRolloutEvaluator.IsInRollout(
            "new-checkout",
            "user-001",
            10);

        Assert.Equal(first, second);
    }

    [Fact]
    public void IsInRollout_DistributesUsersNearRequestedPercentage()
    {
        const int userCount = 1000;

        var enabledCount = Enumerable.Range(0, userCount)
            .Count(index => FeatureFlagRolloutEvaluator.IsInRollout(
                "new-checkout",
                $"user-{index:D4}",
                10));

        Assert.InRange(enabledCount, 70, 130);
    }

    [Theory]
    [InlineData("", "user-001")]
    [InlineData("new-checkout", " ")]
    public void IsInRollout_RejectsBlankIdentifiers(string flagKey, string userId)
    {
        Assert.Throws<ArgumentException>(() =>
            FeatureFlagRolloutEvaluator.IsInRollout(flagKey, userId, 10));
    }
}