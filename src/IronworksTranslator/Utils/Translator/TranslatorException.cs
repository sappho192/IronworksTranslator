namespace IronworksTranslator.Utils.Translator
{
    [Serializable]
    public class TranslatorException : Exception
    {
        public TranslatorException() { }
        public TranslatorException(string message) : base(message) { }
        public TranslatorException(string message, Exception inner) : base(message, inner) { }
    }
}
