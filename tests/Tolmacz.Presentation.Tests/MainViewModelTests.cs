using Tolmacz.Presentation;

namespace Tolmacz.Presentation.Tests;

public class MainViewModelTests
{
    private readonly FakeTranslator _translator = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTextSizeStore _textSizeStore = new();

    private MainViewModel CreateViewModel() => new(_translator, _clipboard, _textSizeStore);

    private async Task<MainViewModel> CreateReadyViewModelAsync()
    {
        var viewModel = CreateViewModel();
        await viewModel.InitializeAsync();
        return viewModel;
    }

    [Theory]
    [InlineData("", "0 characters")]
    [InlineData("A", "1 character")]
    [InlineData("Abc", "3 characters")]
    public void Character_count_follows_the_source_text(string text, string expected)
    {
        var viewModel = CreateViewModel();

        viewModel.SourceText = text;

        Assert.Equal(expected, viewModel.CharacterCountLabel);
    }

    [Fact]
    public void Text_size_starts_at_the_default()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("100%", viewModel.TextSizeLabel);
        Assert.Equal(16, viewModel.EditorFontSize);
        Assert.False(viewModel.ResetTextSizeCommand.CanExecute());
    }

    [Fact]
    public async Task Increasing_text_size_scales_the_text_boxes_and_saves_it()
    {
        var viewModel = CreateViewModel();
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        await viewModel.IncreaseTextSizeCommand.ExecuteAsync();

        Assert.Equal("110%", viewModel.TextSizeLabel);
        Assert.Equal(17.6, viewModel.EditorFontSize, 6);
        Assert.Contains(nameof(MainViewModel.EditorFontSize), changed);
        Assert.Equal(110, _textSizeStore.Saved);
    }

    [Theory]
    [InlineData(80, false, true)]
    [InlineData(200, true, false)]
    public void Text_size_stops_at_its_limits(int saved, bool canDecrease, bool canIncrease)
    {
        _textSizeStore.Saved = saved;

        var viewModel = CreateViewModel();

        Assert.Equal(canDecrease, viewModel.DecreaseTextSizeCommand.CanExecute());
        Assert.Equal(canIncrease, viewModel.IncreaseTextSizeCommand.CanExecute());
    }

    [Theory]
    [InlineData(5, "80%")]
    [InlineData(999, "200%")]
    [InlineData(124, "120%")]
    public void Invalid_saved_text_size_is_corrected(int saved, string expected)
    {
        _textSizeStore.Saved = saved;

        Assert.Equal(expected, CreateViewModel().TextSizeLabel);
    }

    [Fact]
    public async Task Reset_returns_text_to_the_default_size()
    {
        _textSizeStore.Saved = 150;
        var viewModel = CreateViewModel();

        await viewModel.ResetTextSizeCommand.ExecuteAsync();

        Assert.Equal("100%", viewModel.TextSizeLabel);
        Assert.Equal(100, _textSizeStore.Saved);
    }

    [Fact]
    public async Task Text_size_save_failure_is_shown_instead_of_thrown()
    {
        var viewModel = CreateViewModel();
        _textSizeStore.Failure = new InvalidOperationException("storage busy");

        await viewModel.IncreaseTextSizeCommand.ExecuteAsync();

        Assert.Equal("110%", viewModel.TextSizeLabel);
        Assert.Contains("storage busy", viewModel.Status);
    }

    private static async Task<MainViewModel> TranslateAsync(MainViewModel viewModel, string text)
    {
        viewModel.SourceText = text;
        await viewModel.TranslateCommand.ExecuteAsync();
        return viewModel;
    }

    [Fact]
    public async Task Editing_the_source_removes_the_old_translation()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");

        viewModel.SourceText = "C";

        Assert.Empty(viewModel.TranslatedText);
        Assert.False(viewModel.CopyCommand.CanExecute());
    }

    [Fact]
    public async Task Swap_keeps_text_typed_after_the_last_translation()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");
        viewModel.SourceText = "typed text";

        await viewModel.SwapCommand.ExecuteAsync();

        Assert.Equal("typed text", viewModel.SourceText);
        Assert.Equal("English", viewModel.SourceLanguage);
    }

    [Fact]
    public async Task Swap_shows_each_hint_in_the_language_of_its_box()
    {
        var viewModel = await CreateReadyViewModelAsync();
        Assert.Equal("Wpisz lub wklej tekst…", viewModel.SourcePlaceholder);
        Assert.Equal("Translation appears here", viewModel.TargetPlaceholder);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await viewModel.SwapCommand.ExecuteAsync();

        Assert.Equal("Type or paste text…", viewModel.SourcePlaceholder);
        Assert.Equal("Tu pojawi się tłumaczenie", viewModel.TargetPlaceholder);
        Assert.Contains(nameof(MainViewModel.SourcePlaceholder), changed);
        Assert.Contains(nameof(MainViewModel.TargetPlaceholder), changed);
    }

    [Fact]
    public async Task Swap_moves_a_current_translation_to_the_source()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");

        await viewModel.SwapCommand.ExecuteAsync();

        Assert.Equal("translated A", viewModel.SourceText);
        Assert.Empty(viewModel.TranslatedText);
    }

    [Fact]
    public async Task Failed_translation_leaves_no_old_result()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");
        _translator.Failure = new InvalidOperationException("boom");

        await viewModel.TranslateCommand.ExecuteAsync();

        Assert.Empty(viewModel.TranslatedText);
        Assert.Contains("boom", viewModel.Status);
    }

    [Fact]
    public async Task Cancelled_translation_leaves_no_old_result()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");
        _translator.Failure = new OperationCanceledException();

        await viewModel.TranslateCommand.ExecuteAsync();

        Assert.Empty(viewModel.TranslatedText);
        Assert.Equal("Cancelled.", viewModel.Status);
    }

    [Fact]
    public async Task Result_is_discarded_when_the_source_changed_during_translation()
    {
        var viewModel = await CreateReadyViewModelAsync();
        viewModel.SourceText = "A";
        _translator.BeforeReturning = () => viewModel.SourceText = "B";

        await viewModel.TranslateCommand.ExecuteAsync();

        Assert.Equal("B", viewModel.SourceText);
        Assert.Empty(viewModel.TranslatedText);
    }

    [Fact]
    public async Task Incomplete_translation_is_reported()
    {
        var viewModel = await CreateReadyViewModelAsync();
        _translator.IsComplete = false;

        await TranslateAsync(viewModel, "A");

        Assert.Contains("missing", viewModel.Status);
    }

    [Fact]
    public async Task Clipboard_failure_is_shown_instead_of_thrown()
    {
        var viewModel = await TranslateAsync(await CreateReadyViewModelAsync(), "A");
        _clipboard.Failure = new InvalidOperationException("clipboard busy");

        await viewModel.CopyCommand.ExecuteAsync();

        Assert.Contains("clipboard busy", viewModel.Status);
    }

    private sealed class FakeTranslator : ITranslator
    {
        public Exception? Failure { get; set; }
        public Action? BeforeReturning { get; set; }
        public bool IsComplete { get; set; } = true;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TranslationResult> TranslateAsync(
            string text,
            TranslationDirection direction,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            BeforeReturning?.Invoke();
            return Task.FromResult(new TranslationResult($"translated {text}", IsComplete));
        }
    }

    private sealed class FakeTextSizeStore : ITextSizeStore
    {
        public int? Saved { get; set; }
        public Exception? Failure { get; set; }

        public int? Load() => Saved;

        public void Save(int percent)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            Saved = percent;
        }
    }

    private sealed class FakeClipboard : ITextClipboard
    {
        public Exception? Failure { get; set; }

        public Task SetTextAsync(string text) =>
            Failure is null ? Task.CompletedTask : Task.FromException(Failure);
    }
}
