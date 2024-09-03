namespace IronworksTranslator.Utils.Cloudflare
{
    public class CloudflareWorkerException : Exception
    {
        public int ErrorCode { get; }
        public string ErrorMessage { get; }

        public CloudflareWorkerException(int errorCode, string errorMessage)
            : base($"Cloudflare Worker Error {errorCode}: {errorMessage}")
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
