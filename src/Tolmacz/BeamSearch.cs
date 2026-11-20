namespace Tolmacz;

/// <summary>Supplies next-token scores to <see cref="BeamSearch"/>.</summary>
internal interface IBeamDecoder
{
    /// <summary>Advances every beam by one token.</summary>
    /// <param name="lastTokens">The latest token of each beam.</param>
    /// <returns>Logits for each beam, stored one beam after another.</returns>
    float[] Step(IReadOnlyList<long> lastTokens);

    /// <summary>Keeps only the decoder state of the given beams, in the given order.</summary>
    void Reorder(IReadOnlyList<int> parents);
}

/// <summary>Settings for one <see cref="BeamSearch"/> run.</summary>
/// <param name="BeamSize">The number of hypotheses kept at each step.</param>
/// <param name="MaxTokens">The maximum number of generated tokens.</param>
/// <param name="StartToken">The token that begins decoding.</param>
/// <param name="EndToken">The token that finishes a hypothesis.</param>
/// <param name="ForbiddenTokens">A flag for each vocabulary token that must never be produced.</param>
internal sealed record BeamSearchOptions(
    int BeamSize,
    int MaxTokens,
    long StartToken,
    int EndToken,
    bool[] ForbiddenTokens);

/// <summary>The best token sequence, without the end token.</summary>
/// <param name="Tokens">The generated tokens.</param>
/// <param name="ReachedLimit">True when the token limit may have cut the result short.</param>
internal sealed record BeamSearchResult(long[] Tokens, bool ReachedLimit);

/// <summary>Finds a high-scoring token sequence by following several candidates at once.</summary>
internal static class BeamSearch
{
    /// <summary>Searches for the best token sequence.</summary>
    public static BeamSearchResult Run(
        IBeamDecoder decoder,
        BeamSearchOptions options,
        CancellationToken cancellationToken = default)
    {
        var beams = new List<Hypothesis> { new([], 0) };
        var finished = new List<Hypothesis>();
        var stoppedEarly = false;

        for (var step = 0; step < options.MaxTokens; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var lastTokens = beams
                .Select(beam => beam.Tokens.Length == 0 ? options.StartToken : beam.Tokens[^1])
                .ToArray();
            var logits = decoder.Step(lastTokens);
            var candidates = SelectCandidates(beams, logits, options, allowEnd: step > 0);

            var next = new List<Hypothesis>(options.BeamSize);
            var parents = new List<int>(options.BeamSize);
            for (var rank = 0; rank < candidates.Count; rank++)
            {
                var candidate = candidates[rank];
                var parent = beams[candidate.Parent];
                if (candidate.Token == options.EndToken)
                {
                    // Low-ranked endings are ignored so they cannot stop the search early.
                    if (rank < options.BeamSize)
                    {
                        AddFinished(finished, new Hypothesis(parent.Tokens, candidate.Score), options.BeamSize);
                    }
                }
                else if (next.Count < options.BeamSize)
                {
                    next.Add(new Hypothesis([.. parent.Tokens, candidate.Token], candidate.Score));
                    parents.Add(candidate.Parent);
                }
            }

            if (next.Count == 0 || CannotImprove(finished, next[0], options.BeamSize))
            {
                stoppedEarly = true;
                break;
            }

            decoder.Reorder(parents);
            beams = next;
        }

        return CreateResult(finished, beams, stoppedEarly);
    }

    private static BeamSearchResult CreateResult(
        List<Hypothesis> finished,
        List<Hypothesis> beams,
        bool stoppedEarly)
    {
        var bestActive = beams.MaxBy(hypothesis => hypothesis.AverageScore)!;
        var bestFinished = finished.MaxBy(hypothesis => hypothesis.AverageScore);
        if (bestFinished is null)
        {
            return new BeamSearchResult(bestActive.Tokens, true);
        }

        // A better unfinished beam at the limit means the finished text may be cut short.
        var reachedLimit = !stoppedEarly && bestActive.AverageScore > bestFinished.AverageScore;
        return new BeamSearchResult(bestFinished.Tokens, reachedLimit);
    }

    private static List<Candidate> SelectCandidates(
        List<Hypothesis> beams,
        float[] logits,
        BeamSearchOptions options,
        bool allowEnd)
    {
        var vocabularySize = logits.Length / beams.Count;
        var perBeam = options.BeamSize * 2;
        var candidates = new List<Candidate>(beams.Count * perBeam);

        for (var parent = 0; parent < beams.Count; parent++)
        {
            var row = logits.AsSpan(parent * vocabularySize, vocabularySize);
            var normalizer = LogitScoring.LogNormalizer(row, options.ForbiddenTokens);
            var best = new PriorityQueue<int, float>(perBeam + 1);

            for (var token = 0; token < row.Length; token++)
            {
                if (options.ForbiddenTokens[token] || (!allowEnd && token == options.EndToken))
                {
                    continue;
                }

                best.Enqueue(token, row[token]);
                if (best.Count > perBeam)
                {
                    best.Dequeue();
                }
            }

            while (best.TryDequeue(out var token, out var logit))
            {
                candidates.Add(new Candidate(parent, token, beams[parent].Score + logit - normalizer));
            }
        }

        candidates.Sort((left, right) => right.Score.CompareTo(left.Score));
        return candidates;
    }

    private static void AddFinished(List<Hypothesis> finished, Hypothesis hypothesis, int capacity)
    {
        finished.Add(hypothesis);
        if (finished.Count > capacity)
        {
            finished.Remove(finished.MinBy(item => item.AverageScore)!);
        }
    }

    private static bool CannotImprove(List<Hypothesis> finished, Hypothesis bestActive, int capacity) =>
        finished.Count == capacity
        && finished.Min(hypothesis => hypothesis.AverageScore) >= bestActive.AverageScore;

    private sealed record Hypothesis(long[] Tokens, double Score)
    {
        /// <summary>The score per token, counting the end token.</summary>
        public double AverageScore => Score / (Tokens.Length + 1);
    }

    private readonly record struct Candidate(int Parent, int Token, double Score);
}
