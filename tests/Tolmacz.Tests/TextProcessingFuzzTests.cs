namespace Tolmacz.Tests;

public class TextProcessingFuzzTests
{
    private const string Alphabet = "abcXYZąęłżź 0123.,;:!?-–—()[]\"'\n\r\t…😀李";

    private static string RandomText(Random random, int maxLength)
    {
        var text = new System.Text.StringBuilder();
        var length = random.Next(maxLength);
        var elements = System.Globalization.StringInfo.GetTextElementEnumerator(Alphabet);
        var symbols = new List<string>();
        while (elements.MoveNext())
        {
            symbols.Add(elements.GetTextElement());
        }

        for (var index = 0; index < length; index++)
        {
            text.Append(symbols[random.Next(symbols.Count)]);
        }

        return text.ToString();
    }

    [Fact]
    public void Segments_always_rebuild_random_text()
    {
        var random = new Random(20261119);
        for (var run = 0; run < 2000; run++)
        {
            var text = RandomText(random, run % 50 == 0 ? 3000 : 200);
            var segments = TextSegmenter.Split(text);

            Assert.Equal(text, string.Concat(segments.Select(segment => segment.Text)));
            Assert.All(segments, segment => Assert.True(segment.Text.Length <= TextSegmenter.MaxSegmentLength || !segment.IsTranslatable));
        }
    }

    [Fact]
    public void Splitting_near_the_middle_keeps_random_text()
    {
        var random = new Random(20261120);
        for (var run = 0; run < 2000; run++)
        {
            var text = RandomText(random, 200);
            if (TextSegmenter.TrySplitNearMiddle(text, out var left, out var separator, out var right))
            {
                Assert.Equal(text, left + separator + right);
                Assert.NotEmpty(left);
                Assert.NotEmpty(right);
            }
        }
    }

    [Fact]
    public void Text_cleanup_handles_random_text()
    {
        var random = new Random(20261121);
        for (var run = 0; run < 2000; run++)
        {
            var source = RandomText(random, 60);
            var translation = RandomText(random, 60);

            Assert.NotNull(RepetitionCleaner.Clean(source, translation));
            _ = RepetitionCleaner.LooksRepetitive(translation);
            _ = RepetitionCleaner.LostSourceRepeat(source, translation);
            _ = RepetitionCleaner.HasLongWordRun(source);
            _ = NumberCheck.ChangedNumbers(source, translation);
        }
    }
}
