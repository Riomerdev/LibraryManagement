using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FeatureGate.Domain;

public static class FeatureFlagRolloutEvaluator
{
    public static bool IsInRollout(
        string flagKey,
        string userId,
        int rolloutPercentage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flagKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var percentage = Math.Clamp(rolloutPercentage, 0, 100);

        if (percentage == 0)
        {
            return false;
        }

        if (percentage == 100)
        {
            return true;
        }

        var input = Encoding.UTF8.GetBytes($"{flagKey}:{userId}");
        var hash = SHA256.HashData(input);
        var bucket = BinaryPrimitives.ReadUInt32BigEndian(hash.AsSpan(0, 4)) % 100;

        return bucket < percentage;
    }
}