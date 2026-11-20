
namespace Tolmacz.Tests;

public class BeamSearchTests
{
    private const int Start = 0;
    private const int End = 1;
    private const int A = 2;
    private const int B = 3;
    private const int C = 4;
    private const int VocabularySize = 5;

    private static BeamSearchOptions Options(int beamSize = 2, int maxTokens = 10) =>
        new(beamSize, maxTokens, Start, End, [true, false, false, false, false]);

    [Fact]
    public void Does_not_finish_before_producing_a_token()
    {
        var decoder = new ScriptedDecoder(new()
        {
            [""] = [(End, 0.9), (A, 0.1)],
            ["2"] = [(End, 1)],
        });

        var result = BeamSearch.Run(decoder, Options());

        Assert.Equal([A], result.Tokens);
        Assert.False(result.ReachedLimit);
    }

    [Fact]
    public void Poorly_ranked_endings_do_not_stop_the_search()
    {
        var decoder = new ScriptedDecoder(new()
        {
            [""] = [(A, 0.6), (B, 0.4)],
            ["2"] = [(C, 0.95), (End, 0.05)],
            ["3"] = [(C, 0.95), (End, 0.05)],
            ["2 4"] = [(End, 1)],
            ["3 4"] = [(End, 1)],
        });

        var result = BeamSearch.Run(decoder, Options());

        Assert.Equal([A, C], result.Tokens);
    }

    [Fact]
    public void Unfinished_beam_is_not_returned_when_a_hypothesis_finished()
    {
        var decoder = new ScriptedDecoder(new()
        {
            [""] = [(A, 0.9), (B, 0.1)],
            ["2"] = [(C, 0.9), (A, 0.1)],
            ["3"] = [(End, 1)],
            ["2 4"] = [(C, 1)],
            ["2 2"] = [(C, 1)],
            ["2 4 4"] = [(C, 1)],
            ["2 2 4"] = [(C, 1)],
        });

        var result = BeamSearch.Run(decoder, Options(maxTokens: 3));

        Assert.Equal([B], result.Tokens);
        Assert.True(result.ReachedLimit);
    }

    [Fact]
    public void Weak_unfinished_beams_at_the_limit_are_not_reported()
    {
        var decoder = new ScriptedDecoder(new()
        {
            [""] = [(B, 0.6), (A, 0.4)],
            ["3"] = [(End, 1)],
            ["2"] = [(C, 1)],
            ["2 4"] = [(C, 0.5), (B, 0.5)],
        });

        var result = BeamSearch.Run(decoder, Options(maxTokens: 3));

        Assert.Equal([B], result.Tokens);
        Assert.False(result.ReachedLimit);
    }

    [Fact]
    public void Reports_when_nothing_finished_within_the_limit()
    {
        var decoder = new ScriptedDecoder(new()
        {
            [""] = [(A, 1)],
            ["2"] = [(A, 1)],
        });

        var result = BeamSearch.Run(decoder, Options(beamSize: 1, maxTokens: 2));

        Assert.True(result.ReachedLimit);
        Assert.Equal([A, A], result.Tokens);
    }

    [Fact]
    public void Cancellation_stops_the_search()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            BeamSearch.Run(new ScriptedDecoder([]), Options(), cancellation.Token));
    }

    /// <summary>Returns probabilities chosen by the tokens each beam has generated.</summary>
    private sealed class ScriptedDecoder(Dictionary<string, (int Token, double Probability)[]> script) : IBeamDecoder
    {
        private List<List<long>> _histories = [[]];

        public float[] Step(IReadOnlyList<long> lastTokens)
        {
            var logits = new float[lastTokens.Count * VocabularySize];
            Array.Fill(logits, -30f);

            for (var beam = 0; beam < lastTokens.Count; beam++)
            {
                if (lastTokens[beam] != Start)
                {
                    _histories[beam].Add(lastTokens[beam]);
                }

                var path = string.Join(' ', _histories[beam]);
                foreach (var (token, probability) in script.GetValueOrDefault(path, []))
                {
                    logits[beam * VocabularySize + token] = MathF.Log((float)probability);
                }
            }

            return logits;
        }

        public void Reorder(IReadOnlyList<int> parents) =>
            _histories = parents.Select(parent => new List<long>(_histories[parent])).ToList();
    }
}
