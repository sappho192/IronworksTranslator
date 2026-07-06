using System.IO;
using System.Reflection;

namespace IronworksTranslator.Utils.Logging
{
    internal static class EncryptedLogResources
    {
        private const string PublicKeyResourceName = "IronworksTranslator.Resources.Crypto.log_public_key.pem";

        public static string LoadPublicKeyPem()
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(PublicKeyResourceName)
                ?? throw new InvalidOperationException($"Missing embedded resource: {PublicKeyResourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
