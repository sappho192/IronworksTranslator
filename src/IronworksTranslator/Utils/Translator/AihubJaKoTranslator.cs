using IronworksTranslator.Models.Enums;
using IronworksTranslator.Utils.Translators;
using IronworksTranslator.Models.Settings;
using Serilog;
using IronworksTranslator.Models.Translator;
using IronworksTranslator.Utils.Aspect;
using EDMTranslator.Tokenization;
using EDMTranslator.Translation;
using System.IO;

namespace IronworksTranslator.Utils.Translator
{
    public sealed class IronworksJaKoTranslator : TranslatorBase
    {
        private readonly TranslationLanguageCode[] translationSourceLanguages = [
            TranslationLanguageCode.Japanese
        ];
        private readonly TranslationLanguageCode[] translationTargetLanguages = [
            TranslationLanguageCode.Korean
        ];

        public override TranslationLanguageCode[] SupportedSourceLanguages => translationSourceLanguages;
        public override TranslationLanguageCode[] SupportedTargetLanguages => translationTargetLanguages;

        private BertJa2GPTTokenizer? tokenizer;
        private AIhubJaKoTranslator? translator;
        private static readonly object lockObj = new();

        public IronworksJaKoTranslator()
        {
            Task.Run(InitTranslator).GetAwaiter().GetResult();
        }

        public override string Translate(string input, TranslationLanguageCode sourceLanguage, TranslationLanguageCode targetLanguage)
        {
            if (!SupportedSourceLanguages.Contains(sourceLanguage))
            {
                Log.Error("Unsupported sourceLanguage");
                return input;
            }
            if (!SupportedTargetLanguages.Contains(targetLanguage))
            {
                Log.Error("Unsupported targetLanguage");
                return input;
            }

            lock (lockObj)
            {
                string result = translator.Translate(input);
                return result;
            }
        }

        [TraceMethod]
        public async Task<bool> InitTranslator()
        {
            // Prepare the tokenizer
            var tokenizerDirectory = Path.Combine("data", "tokenizers");
            var encoderVocabPath = await BertJapaneseTokenizer.HuggingFace.GetVocabFromHub(
                "tohoku-nlp/bert-base-japanese-v2", tokenizerDirectory);
            var hubName = "skt/kogpt2-base-v2";
            var decoderVocabFilename = "tokenizer.json";
            var decoderVocabPath = 
                await Tokenizers.DotNet.HuggingFace.GetFileFromHub(
                    hubName, decoderVocabFilename, tokenizerDirectory);

            string encoderDictDir = Path.Combine("data", "unidic-mecab-2.1.2_bin");
            tokenizer = new BertJa2GPTTokenizer(
                encoderDictDir: encoderDictDir, encoderVocabPath: encoderVocabPath,
                decoderVocabPath: decoderVocabPath);

            string modelDir = Path.Combine("data", "model", "aihub-ja-ko-translator");
            translator = new AIhubJaKoTranslator(tokenizer, modelDir);

            return true;
        }
    }
}
