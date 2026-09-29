using IronworksTranslator.Models.Settings;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using IronworksTranslator.ViewModels.Pages;

namespace IronworksTranslator.Tests.ViewModels;

public class ComposeViewModelTests
{
    [Fact]
    public async Task Translate_UsesSelectedDirectionAndReversesForCheck()
    {
        var translator = new FakeTranslator((text, _, _) => Task.FromResult(text + "!"));
        var vm = Create(translator);
        vm.InputText = "  hello  ";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("hello!", vm.TranslatedText);
        Assert.Equal("hello!!", vm.BackTranslation);
        Assert.Equal(new[] {
            ("hello", TranslationLanguageCode.English, TranslationLanguageCode.Japanese),
            ("hello!", TranslationLanguageCode.Japanese, TranslationLanguageCode.English)
        }, translator.Calls);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Translate_DisablesSubmissionWhilePendingAndSkipsOptionalCheck()
    {
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var vm = Create(new FakeTranslator((_, _, _) => pending.Task));
        vm.InputText = "hello";
        vm.CheckTranslation = false;
        var operation = vm.TranslateCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.False(vm.TranslateCommand.CanExecute(null));
        Assert.False(vm.SwapLanguagesCommand.CanExecute(null));
        pending.SetResult("こんにちは");
        await operation;
        Assert.Equal("こんにちは", vm.TranslatedText);
        Assert.Empty(vm.BackTranslation);
        Assert.True(vm.TranslateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Translate_FailureClearsOldResultAndAllowsRetry()
    {
        var vm = Create(new FakeTranslator((_, _, _) => throw new InvalidOperationException()));
        vm.InputText = "hello";
        vm.TranslatedText = "old";
        vm.BackTranslation = "old check";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Empty(vm.TranslatedText);
        Assert.Empty(vm.BackTranslation);
        Assert.Equal("compose.failed", vm.Status);
        Assert.True(vm.IsIdle);
        Assert.False(vm.CopyCommand.CanExecute(null));
    }

    [Fact]
    public async Task Translate_SameLanguageDoesNotCallEngine()
    {
        var translator = new FakeTranslator((_, _, _) => throw new InvalidOperationException());
        var vm = Create(translator);
        vm.InputText = "hello";
        vm.TargetLanguage = vm.SourceLanguage;
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("hello", vm.TranslatedText);
        Assert.Equal("hello", vm.BackTranslation);
        Assert.Empty(translator.Calls);
    }

    [Fact]
    public async Task Translate_OriginalFallbackIsVisibleWithoutClaimingSuccess()
    {
        var vm = Create(new FakeTranslator((text, _, _) => Task.FromResult(text)));
        vm.InputText = "hello";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("hello", vm.TranslatedText);
        Assert.Equal("compose.unchanged", vm.Status);
    }

    [Fact]
    public void EmptyInputIsDisabledAndSwapKeepsInput()
    {
        var vm = Create(new FakeTranslator((text, _, _) => Task.FromResult(text)));
        vm.InputText = "  ";
        Assert.False(vm.TranslateCommand.CanExecute(null));
        vm.InputText = "hello";
        vm.SwapLanguagesCommand.Execute(null);
        Assert.Equal(TranslationLanguageCode.Japanese, vm.SourceLanguage);
        Assert.Equal(TranslationLanguageCode.English, vm.TargetLanguage);
        Assert.Equal("hello", vm.InputText);
    }

    [Fact]
    public async Task CompactMode_ReplacesInputWithoutBackTranslationAndPreventsDoubleTranslation()
    {
        var translator = new FakeTranslator((_, _, _) => Task.FromResult("こんにちは"));
        var vm = Create(translator);
        vm.Settings.ComposeShowDetails = false;
        vm.InputText = "hello";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("こんにちは", vm.InputText);
        Assert.Empty(vm.BackTranslation);
        Assert.Single(translator.Calls);
        Assert.False(vm.TranslateCommand.CanExecute(null));
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Single(translator.Calls);
        vm.InputText = "new message";
        Assert.True(vm.TranslateCommand.CanExecute(null));
        Assert.Empty(vm.TranslatedText);
    }

    [Fact]
    public async Task DetailsMode_PreservesOriginalAndSwitchingRestoresEachView()
    {
        var vm = Create(new FakeTranslator((text, _, _) => Task.FromResult(text + "!")));
        vm.InputText = "hello";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("hello", vm.InputText);
        Assert.Equal("hello!", vm.TranslatedText);
        Assert.Equal("hello!!", vm.BackTranslation);
        vm.Settings.ComposeShowDetails = false;
        Assert.Equal("hello!", vm.InputText);
        vm.Settings.ComposeShowDetails = true;
        Assert.Equal("hello", vm.InputText);
        Assert.Equal("hello!", vm.TranslatedText);
    }

    [Fact]
    public async Task CompactFailure_PreservesInputForRetry()
    {
        var vm = Create(new FakeTranslator((_, _, _) => throw new InvalidOperationException()));
        vm.Settings.ComposeShowDetails = false;
        vm.InputText = "hello";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal("hello", vm.InputText);
        Assert.True(vm.TranslateCommand.CanExecute(null));
    }

    [Fact]
    public async Task SavedLanguageChange_IsUsedByNextTranslation()
    {
        var translator = new FakeTranslator((text, _, _) => Task.FromResult(text + "!"));
        var vm = Create(translator);
        vm.Settings.ComposeSourceLanguage = TranslationLanguageCode.French;
        vm.Settings.ComposeTargetLanguage = TranslationLanguageCode.German;
        vm.InputText = "bonjour";
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.Equal(("bonjour", TranslationLanguageCode.French, TranslationLanguageCode.German), translator.Calls[0]);
    }

    private static ComposeViewModel Create(FakeTranslator translator) => new(() => translator, key => key, new TranslatorSettings { ComposeShowDetails = true })
    {
        SourceLanguage = TranslationLanguageCode.English,
        TargetLanguage = TranslationLanguageCode.Japanese
    };

    private sealed class FakeTranslator(Func<string, TranslationLanguageCode, TranslationLanguageCode, Task<string>> translate) : TranslatorBase
    {
        public List<(string, TranslationLanguageCode, TranslationLanguageCode)> Calls { get; } = [];
        public override TranslationLanguageCode[] SupportedSourceLanguages => Enum.GetValues<TranslationLanguageCode>();
        public override TranslationLanguageCode[] SupportedTargetLanguages => SupportedSourceLanguages;
        public override string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage) => throw new NotSupportedException();
        public override Task<string> TranslateAsync(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            Calls.Add((input, sourceLanguage, targetLanguage));
            return translate(input, sourceLanguage, targetLanguage);
        }
    }
}
