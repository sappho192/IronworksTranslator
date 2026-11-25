using IronworksTranslator.Models.Cloudflare;
using IronworksTranslator.Models.Enums;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Translator;
using IronworksTranslator.ViewModels.Dialogs;

namespace IronworksTranslator.Tests.ViewModels;

public class TranslationReportViewModelTests
{
    [Fact]
    public void InitialState_StartsWithCurrentTranslationAndCannotSubmitUntilChanged()
    {
        var text = CreateChatText();
        var vm = CreateViewModel(text);

        Assert.Equal("새벽의 혈맹 모두", vm.SuggestedTranslation);
        Assert.Equal(TranslationReportViewModel.DraftSourceCurrent, vm.DraftSource);
        Assert.False(vm.IsDraftEdited);
        Assert.False(vm.CanSubmit);
        Assert.False(vm.HasNpcSpeaker);
        Assert.Null(vm.OriginalNpcSpeaker);
    }

    [Fact]
    public async Task LoadDraft_TracksSourceAndEditedState()
    {
        var text = CreateChatText();
        var vm = CreateViewModel(
            text,
            translateDraft: (engine, _, _, _, _) => Task.FromResult(
                engine == TranslatorEngine.Papago ? "새벽의 혈맹 여러분" : "새벽의 혈맹 모두들"));

        await vm.LoadPapagoDraftCommand.ExecuteAsync(null);

        Assert.Equal("새벽의 혈맹 여러분", vm.SuggestedTranslation);
        Assert.Equal(TranslationReportViewModel.DraftSourcePapago, vm.DraftSource);
        Assert.False(vm.IsDraftEdited);
        Assert.True(vm.CanSubmit);

        vm.SuggestedTranslation = "새벽의 혈맹 동료들";
        Assert.True(vm.IsDraftEdited);

        vm.ResetToCurrentDraftCommand.Execute(null);
        Assert.Equal(TranslationReportViewModel.DraftSourceCurrent, vm.DraftSource);
        Assert.Equal("새벽의 혈맹 모두", vm.SuggestedTranslation);
        Assert.False(vm.IsDraftEdited);
        Assert.False(vm.CanSubmit);
    }

    [Fact]
    public void DraftCommands_RespectCurrentEngineAndDeepLAvailability()
    {
        var papagoText = CreateChatText(TranslatorEngine.Papago);
        var vmNoDeepL = CreateViewModel(papagoText, isDeepLAvailable: false);

        Assert.False(vmNoDeepL.LoadPapagoDraftCommand.CanExecute(null));
        Assert.False(vmNoDeepL.LoadDeepLDraftCommand.CanExecute(null));
        Assert.Equal("chat.report.draft.deepl_unavailable", vmNoDeepL.DeepLDraftToolTip);

        var miLmmtText = CreateChatText(TranslatorEngine.MiLMMT);
        var vmWithDeepL = CreateViewModel(miLmmtText, isDeepLAvailable: true);

        Assert.True(vmWithDeepL.LoadPapagoDraftCommand.CanExecute(null));
        Assert.True(vmWithDeepL.LoadDeepLDraftCommand.CanExecute(null));
        Assert.Null(vmWithDeepL.DeepLDraftToolTip);
    }

    [Fact]
    public void BuildRequest_StripsSpeakerForChatAndPreservesNpcSpeakerForDialogue()
    {
        var chatText = CreateChatText();
        chatText.OriginalSpeaker = "ShouldNeverBeSent";
        var chatVm = CreateViewModel(chatText);
        chatVm.Comment = "어투가 부자연스러움";

        var chatPayload = chatVm.BuildRequest("1.2.3", new DateTime(2026, 11, 25, 13, 0, 0, DateTimeKind.Utc));

        Assert.Equal(BiggsReportRequest.CurrentSchemaVersion, chatPayload.SchemaVersion);
        Assert.Equal("chat", chatPayload.TranslationKind);
        Assert.Null(chatPayload.NpcSpeaker);
        Assert.Equal("MiLMMT", chatPayload.Engine);
        Assert.Equal("MiLMMT-46-1B-v1.0.Q4_K_M.gguf", chatPayload.ModelId);
        Assert.Equal("abc123", chatPayload.ModelSha256);

        var dialogueText = new TranslationText("暁の血盟の皆", TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean)
        {
            TranslatedText = "새벽의 혈맹 모두",
            Author = "야슈톨라",
            OriginalSpeaker = "Y'shtola",
            Engine = TranslatorEngine.Papago,
            ModelId = "ignored-for-non-milmmt",
            TranslationKind = MiLMMTTranslationKind.Dialogue,
        };
        var dialogueVm = CreateViewModel(dialogueText);
        dialogueVm.GlossaryHintText = "暁の血盟 -> 새벽의 혈맹";

        var dialoguePayload = dialogueVm.BuildRequest("1.2.3");

        Assert.True(dialogueVm.HasNpcSpeaker);
        Assert.Equal("dialogue", dialoguePayload.TranslationKind);
        Assert.Equal("Y'shtola", dialoguePayload.NpcSpeaker);
        Assert.Null(dialoguePayload.ModelId);
        Assert.Null(dialoguePayload.ModelSha256);
        Assert.Single(dialoguePayload.GlossaryTerms);
        Assert.Equal("暁の血盟", dialoguePayload.GlossaryTerms[0].Source);
        Assert.Equal("새벽의 혈맹", dialoguePayload.GlossaryTerms[0].Target);
    }

    [Fact]
    public async Task LoadDraft_FailureShowsStatusAndKeepsExistingDraft()
    {
        var text = CreateChatText();
        var vm = CreateViewModel(
            text,
            translateDraft: (_, _, _, _, _) => throw new InvalidOperationException("boom"));

        await vm.LoadPapagoDraftCommand.ExecuteAsync(null);

        Assert.Equal("새벽의 혈맹 모두", vm.SuggestedTranslation);
        Assert.Equal(TranslationReportViewModel.DraftSourceCurrent, vm.DraftSource);
        Assert.Equal("chat.report.draft.failed", vm.DraftStatusMessage);
        Assert.True(vm.HasDraftStatusMessage);
        Assert.False(vm.IsLoadingDraft);
    }

    private static TranslationText CreateChatText(TranslatorEngine engine = TranslatorEngine.MiLMMT)
    {
        return new TranslationText("暁の血盟の皆", TranslationLanguageCode.Japanese, TranslationLanguageCode.Korean)
        {
            TranslatedText = "새벽의 혈맹 모두",
            Author = "PlayerOne",
            Engine = engine,
            ModelId = engine == TranslatorEngine.MiLMMT ? "MiLMMT-46-1B-v1.0.Q4_K_M.gguf" : null,
            ModelSha256 = engine == TranslatorEngine.MiLMMT ? "abc123" : null,
            TranslationKind = MiLMMTTranslationKind.Chat,
        };
    }

    private static TranslationReportViewModel CreateViewModel(
        TranslationText text,
        Func<TranslatorEngine, string, TranslationLanguageCode, TranslationLanguageCode, CancellationToken, Task<string>>? translateDraft = null,
        bool isDeepLAvailable = true)
    {
        return new TranslationReportViewModel(
            text,
            translateDraft ?? ((_, input, _, _, _) => Task.FromResult(input)),
            isDeepLAvailable,
            key => key);
    }
}
