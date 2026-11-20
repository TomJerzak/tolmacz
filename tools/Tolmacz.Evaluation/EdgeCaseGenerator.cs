using System.Globalization;

namespace Tolmacz.Evaluation;

/// <summary>Creates a repeatable set of difficult texts for both translation directions.</summary>
internal sealed class EdgeCaseGenerator
{
    private const TranslationDirection Pl = TranslationDirection.PolishToEnglish;
    private const TranslationDirection En = TranslationDirection.EnglishToPolish;

    private static readonly string[] PolishSentences =
    [
        "Umowa wchodzi w życie z dniem podpisania przez obie strony.",
        "Czy możesz wysłać mi raport do końca tygodnia?",
        "Wczoraj wieczorem poszliśmy z dziećmi do kina.",
        "Serwer nie odpowiada od kilku minut.",
        "Proszę sprawdzić dane przed zapisaniem formularza.",
        "Pociąg do Krakowa odjeżdża z peronu drugiego.",
        "Nasza firma otworzyła nowe biuro w Gdańsku.",
        "Lekarz zalecił odpoczynek i picie dużej ilości wody.",
        "Nie udało się połączyć z bazą danych.",
        "Spotkanie zostało przeniesione na przyszły tydzień.",
    ];

    private static readonly string[] EnglishSentences =
    [
        "The agreement enters into force on the date of signature.",
        "Can you send me the report by the end of the week?",
        "Last night we went to the cinema with the children.",
        "The server has not responded for several minutes.",
        "Please check the data before saving the form.",
        "The train to Krakow departs from platform two.",
        "Our company opened a new office in Gdansk.",
        "The doctor recommended rest and drinking plenty of water.",
        "The connection to the database failed.",
        "The meeting has been moved to next week.",
    ];

    private static readonly string[] PolishShort =
    [
        "Tak", "Nie", "Dziękuję", "Proszę", "Dzień dobry", "Do widzenia", "Zapisz", "Anuluj", "Ustawienia",
        "Otwórz plik", "Kot", "Dom", "Teraz", "Jutro", "Nie wiem", "Co słychać?", "Dobranoc", "Przepraszam",
    ];

    private static readonly string[] EnglishShort =
    [
        "Yes", "No", "Thank you", "Please", "Good morning", "Goodbye", "Save", "Cancel", "Settings",
        "Open file", "Cat", "House", "Now", "Tomorrow", "I don't know", "How are you?", "Good night", "Sorry",
    ];

    private static readonly string[] Names =
    [
        "Warszawa", "Kraków", "Gdańsk", "Baden-Baden", "Bora-Bora", "Sing-Sing", "Jan Kowalski", "Anna Nowak",
        "London", "Paris", "New York", "John Smith", "Microsoft", "GitHub", "Tolmacz", "Wrocław",
    ];

    private static readonly string[] PolishAbbreviations =
    [
        "ul", "dr", "prof.", "itd", "itp.", "np. kot", "m.in. dom", "tzn. tak", "ok. 20 osób", "ul. Długa 5",
        "Dr Nowak przyszedł.", "Prof. Kowalski wykłada.", "Mieszkam przy ul. Polnej 7.", "Kupiłem m.in. chleb.",
    ];

    private static readonly string[] EnglishAbbreviations =
    [
        "Mr", "Dr", "Prof.", "etc", "e.g. this", "i.e. that", "No. 5", "St. John", "U.S.A.", "Dr. Smith is here.",
        "Mr. Brown arrived.", "See fig. 3 for details.", "We sell apples, pears, etc.", "He lives at 5 Main St.",
    ];

    private static readonly string[] Unknown = ["李明", "😀", "🚀", "Иван", "مرحبا", "東京", "♥", "Ω"];

    private readonly Random _random;
    private readonly List<EvaluationCase> _cases = [];

    private EdgeCaseGenerator(int seed) => _random = new Random(seed);

    public static IReadOnlyList<EvaluationCase> Create(int seed = 20261119)
    {
        var generator = new EdgeCaseGenerator(seed);
        generator.AddFixedCases();
        for (var index = 0; index < 40; index++)
        {
            generator.AddNumbers(index);
            generator.AddAddresses(index);
            generator.AddUnknownText(index);
            generator.AddLayouts(index);
        }

        for (var index = 0; index < 20; index++)
        {
            generator.AddLists(index);
            generator.AddTables(index);
            generator.AddPunctuation(index);
            generator.AddMixedText(index);
        }

        for (var index = 0; index < 5; index++)
        {
            generator.AddLongText(index);
        }

        for (var index = 0; index < 20; index++)
        {
            generator.AddTabVariants(index);
        }

        return generator._cases;
    }

    private void Add(string category, TranslationDirection direction, string source) =>
        _cases.Add(new EvaluationCase(
            string.Create(CultureInfo.InvariantCulture, $"edge-{category}-{_cases.Count:D5}"),
            category,
            direction,
            source));

    private void AddBoth(string category, Func<string[], string> polish, Func<string[], string> english)
    {
        Add(category, Pl, polish(PolishSentences));
        Add(category, En, english(EnglishSentences));
    }

    private void AddFixedCases()
    {
        Array.ForEach(PolishShort, text => Add("short", Pl, text));
        Array.ForEach(EnglishShort, text => Add("short", En, text));
        foreach (var name in Names)
        {
            Add("name", Pl, name);
            Add("name", En, name);
        }

        Array.ForEach(PolishAbbreviations, text => Add("abbreviation", Pl, text));
        Array.ForEach(EnglishAbbreviations, text => Add("abbreviation", En, text));
    }

    private string Pick(string[] values) => values[_random.Next(values.Length)];

    private void AddNumbers(int index)
    {
        var day = _random.Next(1, 29);
        var year = _random.Next(1990, 2031);
        var amount = _random.Next(10, 100000);
        var cents = _random.Next(10, 100);
        var time = string.Create(CultureInfo.InvariantCulture, $"{_random.Next(0, 24):D2}:{_random.Next(0, 60):D2}");
        var percent = _random.Next(1, 100);
        var (polish, english) = (index % 4) switch
        {
            0 => ($"Spotkanie odbędzie się {day}.05.{year} o {time}.", $"The meeting will take place on 05/{day}/{year} at {time}."),
            1 => ($"Faktura numer {amount} opiewa na {amount},{cents} zł.", $"Invoice number {amount} is for ${amount}.{cents}."),
            2 => ($"Wzrost wyniósł {percent}% w {year} roku.", $"Growth was {percent}% in {year}."),
            _ => ($"Zamówiono {day} sztuk produktu {amount}.", $"{day} units of product {amount} were ordered."),
        };
        Add("number", Pl, polish);
        Add("number", En, english);
    }

    private void AddAddresses(int index)
    {
        var path = string.Create(CultureInfo.InvariantCulture, $"https://example.com/docs/{index}?id={_random.Next(1000, 9999)}");
        var email = string.Create(CultureInfo.InvariantCulture, $"user{index}@example.com");
        Add("address", Pl, index % 2 == 0 ? $"Instrukcja jest dostępna pod adresem {path}." : $"Napisz do nas na adres {email}.");
        Add("address", En, index % 2 == 0 ? $"The manual is available at {path}." : $"Write to us at {email}.");
    }

    private void AddUnknownText(int index)
    {
        var value = Unknown[index % Unknown.Length];
        Add("unknown_text", Pl, $"Mój kolega {value} przyszedł do biura.");
        Add("unknown_text", En, $"My colleague {value} came to the office.");
    }

    private void AddLayouts(int index)
    {
        var separator = (index % 3) switch { 0 => "\n", 1 => "\n\n", _ => "\r\n" };
        var prefix = index % 5 == 0 ? "  " : string.Empty;
        var suffix = index % 7 == 0 ? "\n" : string.Empty;
        AddBoth(
            "layout",
            values => prefix + Pick(values) + separator + Pick(values) + suffix,
            values => prefix + Pick(values) + separator + Pick(values) + suffix);
    }

    private void AddLists(int index)
    {
        var count = 3 + index % 6;
        AddBoth(
            "list",
            values => string.Join('\n', Enumerable.Range(1, count).Select(number => $"{number}. {Pick(values)}")),
            values => string.Join('\n', Enumerable.Range(1, count).Select(number => $"{number}. {Pick(values)}")));
        var size = 8 + index;
        Add("numbered_text", Pl, string.Join(", ", Enumerable.Range(1, size).Select(number => $"pan Kowalski numer {number}")));
        Add("numbered_text", En, string.Join(", ", Enumerable.Range(1, size).Select(number => $"customer number {number}")));
    }

    private void AddTables(int index)
    {
        string[] polish = ["Imię", "Nazwisko", "Wiek", "Miasto", "Cena", "Ilość", "Razem", "Data"];
        string[] english = ["Name", "Surname", "Age", "City", "Price", "Quantity", "Total", "Date"];
        var columns = 2 + index % 5;
        Add("table", Pl, string.Join('\t', polish.Take(columns)));
        Add("table", En, string.Join('\t', english.Take(columns)));
    }

    private void AddPunctuation(int index)
    {
        var dots = string.Join(' ', Enumerable.Repeat(".", 4 + index));
        var page = index + 1;
        Add("punctuation", Pl, $"Spis treści {dots} {page}");
        Add("punctuation", En, $"Table of contents {dots} {page}");
    }

    private void AddMixedText(int index)
    {
        var code = index % 2 == 0 ? "var total = price * 2;" : "dotnet build";
        Add("mixed", Pl, $"Uruchom polecenie `{code}` na serwerze.");
        Add("mixed", En, $"Run the `{code}` command on the server.");
    }

    private void AddTabVariants(int index)
    {
        var (polish, english) = (index % 5) switch
        {
            0 => ("Kot\tśpi na kanapie.", "The cat\tis sleeping on the sofa."),
            1 => ("\t" + Pick(PolishSentences), "\t" + Pick(EnglishSentences)),
            2 => ("Imię\t\tWiek\nJan\t\t30", "Name\t\tAge\nJohn\t\t30"),
            3 => ("Produkt\tCena\nChleb\t5,50 zł", "Product\tPrice\nBread\t$5.50"),
            _ => ("Kod:\t123\tGotowe", "Code:\t123\tDone"),
        };
        Add("tab_text", Pl, polish);
        Add("tab_text", En, english);
    }

    private void AddLongText(int index)
    {
        var count = 20 + index * 10;
        AddBoth(
            "long_text",
            values => string.Join(' ', Enumerable.Range(0, count).Select(_ => Pick(values))),
            values => string.Join(' ', Enumerable.Range(0, count).Select(_ => Pick(values))));
    }
}
