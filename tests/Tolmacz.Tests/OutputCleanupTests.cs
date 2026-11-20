namespace Tolmacz.Tests;

public class OutputCleanupTests
{
    [Theory]
    [InlineData("Marta Suplicy.", "Marta Suplicy (ang.).", "Marta Suplicy.")]
    [InlineData("Jardín de la Unión.", "Jardín de la Unión (ang.).", "Jardín de la Unión.")]
    [InlineData("Słowo (ang.) obok.", "Słowo (ang.) obok.", "Słowo (ang.) obok.")]
    [InlineData("A cat.", "Kot.", "Kot.")]
    [InlineData("U.S.A.", "U.S.A. (ang.).", "U.S.A.")]
    public void Removes_the_dictionary_marker_only_when_the_source_has_none(string source, string translation, string expected) =>
        Assert.Equal(expected, OutputCleanup.RemoveEnglishMarker(source, translation));

    [Theory]
    [InlineData("Age", "Kategoria: Wiek", "Wiek")]
    [InlineData("Age", "Kategoria:Wiek", "Wiek")]
    [InlineData("Category: Age", "Kategoria: Wiek", "Kategoria: Wiek")]
    [InlineData("Age category", "Kategoria wiekowa", "Kategoria wiekowa")]
    [InlineData("Category:", "Kategoria:", "Kategoria:")]
    [InlineData("The age.", "Wiek. Kategoria: inne.", "Wiek. Kategoria: inne.")]
    public void Removes_the_category_label_only_when_the_source_mentions_no_category(string source, string translation, string expected) =>
        Assert.Equal(expected, OutputCleanup.RemoveCategoryLabel(source, translation));

    [Theory]
    [InlineData("Cheese", "- Ser", "Ser")]
    [InlineData("Dog", "– Pies", "Pies")]
    [InlineData("- Hello", "- Cześć", "- Cześć")]
    [InlineData("Minus five", "-5", "-5")]
    [InlineData("Dash", "-", "-")]
    [InlineData("\"It is important\", he said.", "- To ważne - powiedział.", "- To ważne - powiedział.")]
    [InlineData("\"We were in shock,\" she said.", "— Byliśmy w szoku — stwierdziła.", "— Byliśmy w szoku — stwierdziła.")]
    [InlineData("\"The CCTV would help.\"", "- CCTV pomoże w Bielsku-Białej.", "CCTV pomoże w Bielsku-Białej.")]
    public void Removes_a_leading_dash_only_when_the_source_has_none(string source, string translation, string expected) =>
        Assert.Equal(expected, OutputCleanup.RemoveLeadingDash(source, translation));

    [Theory]
    [InlineData("19 500 km²", "19 500 km2", "19 500 km²")]
    [InlineData("5 m³ wody", "5 m3 of water", "5 m³ of water")]
    [InlineData("Model km2", "Model km2", "Model km2")]
    [InlineData("10 km²", "10 km2 and 4 m2", "10 km² and 4 m2")]
    public void Restores_unit_exponents_from_the_source(string source, string translation, string expected) =>
        Assert.Equal(expected, OutputCleanup.RestoreUnitExponents(source, translation));
}
