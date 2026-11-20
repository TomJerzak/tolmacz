
namespace Tolmacz.Tests;

public class NumberCheckTests
{
    [Theory]
    [InlineData("1 2 3 4 5 6", "1 2 3 4 5", true)]
    [InlineData("1 1 2 3 4 5", "1 2 3 4 5", true)]
    [InlineData("1 2 3 4 5 6", "6, 5, 4, 3, 2, 1", false)]
    [InlineData("1,250 2 3 4 5 6", "1 250 2 3 4 5 6", false)]
    [InlineData("Only 1 number", "Tylko jedna liczba", false)]
    [InlineData("1 2 3 4 5 6", "1 1 2 3 4 5 6", true)]
    [InlineData("1 2 3 4 5 6", "1, 2, 3, 4, 5, 6", false)]
    public void Detects_missing_or_duplicated_numbers(string source, string translation, bool expected) =>
        Assert.Equal(expected, NumberCheck.ChangedNumbers(source, translation));
}
