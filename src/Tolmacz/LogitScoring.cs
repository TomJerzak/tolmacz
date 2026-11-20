namespace Tolmacz;

/// <summary>Converts raw model scores into probabilities.</summary>
internal static class LogitScoring
{
    /// <summary>
    /// Returns the value to subtract from a logit to get its log probability among the
    /// allowed tokens. Forbidden tokens are left out, so they cannot distort the scores.
    /// </summary>
    public static double LogNormalizer(ReadOnlySpan<float> logits, ReadOnlySpan<bool> forbidden)
    {
        var max = float.NegativeInfinity;
        for (var token = 0; token < logits.Length; token++)
        {
            if (!forbidden[token])
            {
                max = MathF.Max(max, logits[token]);
            }
        }

        var sum = 0d;
        for (var token = 0; token < logits.Length; token++)
        {
            if (!forbidden[token])
            {
                sum += Math.Exp(logits[token] - max);
            }
        }

        return max + Math.Log(sum);
    }
}
