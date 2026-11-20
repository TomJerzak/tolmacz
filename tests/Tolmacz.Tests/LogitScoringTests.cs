
namespace Tolmacz.Tests;

public class LogitScoringTests
{
    private static readonly int[] AllowedTokens = [0, 2, 3];

    [Fact]
    public void Forbidden_tokens_do_not_change_the_scores()
    {
        float[] logits = [50f, 1f, 1f];
        bool[] forbidden = [true, false, false];

        var normalizer = LogitScoring.LogNormalizer(logits, forbidden);

        Assert.Equal(Math.Log(0.5), logits[1] - normalizer, 6);
    }

    [Fact]
    public void Allowed_probabilities_sum_to_one()
    {
        float[] logits = [-2f, 0.5f, 3f, 1f];
        bool[] forbidden = [false, true, false, false];

        var normalizer = LogitScoring.LogNormalizer(logits, forbidden);
        var total = AllowedTokens.Sum(token => Math.Exp(logits[token] - normalizer));

        Assert.Equal(1d, total, 6);
    }
}
