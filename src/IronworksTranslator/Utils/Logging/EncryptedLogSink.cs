using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IronworksTranslator.Utils.Logging
{
    internal sealed class EncryptedLogSink : ILogEventSink, IDisposable
    {
        public const string Magic = "IWLOG2";
        public const byte FormatVersion = 2;
        public const string Algorithm = "RSA-OAEP-SHA256+A256GCM";

        private const int AesKeySizeBytes = 32;
        private const int NonceSizeBytes = 12;
        private const int NoncePrefixSizeBytes = 4;
        private const int TagSizeBytes = 16;

        private readonly object _syncRoot = new();
        private readonly ITextFormatter _innerFormatter;
        private readonly FileStream _stream;
        private readonly BinaryWriter _writer;
        private readonly byte[] _sessionKey;
        private readonly byte[] _noncePrefix;

        private ulong _sequence;
        private bool _disposed;

        public EncryptedLogSink(string filePath, string publicKeyPem)
            : this(
                filePath,
                publicKeyPem,
                new MessageTemplateTextFormatter(
                    "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
        {
        }

        internal EncryptedLogSink(string filePath, string publicKeyPem, ITextFormatter innerFormatter)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);

            _innerFormatter = innerFormatter;
            _sessionKey = RandomNumberGenerator.GetBytes(AesKeySizeBytes);
            _noncePrefix = RandomNumberGenerator.GetBytes(NoncePrefixSizeBytes);

            using var publicKey = RSA.Create();
            publicKey.ImportFromPem(publicKeyPem);
            var encryptedSessionKey = publicKey.Encrypt(_sessionKey, RSAEncryptionPadding.OaepSHA256);
            var keyId = CreateKeyId(publicKey);

            _stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(_stream, Encoding.UTF8, leaveOpen: true);
            WriteHeader(keyId, encryptedSessionKey);
        }

        public void Emit(LogEvent logEvent)
        {
            var plainBytes = Encoding.UTF8.GetBytes(RenderPlainText(logEvent));
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];

            lock (_syncRoot)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                var sequence = _sequence++;
                var nonce = CreateNonce(sequence);

                using (var aes = new AesGcm(_sessionKey, TagSizeBytes))
                {
                    aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
                }

                _writer.Write(sequence);
                _writer.Write(nonce);
                _writer.Write(cipherBytes.Length);
                _writer.Write(cipherBytes);
                _writer.Write(tag);
                _writer.Flush();
            }

            CryptographicOperations.ZeroMemory(plainBytes);
        }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _writer.Dispose();
                _stream.Dispose();
                CryptographicOperations.ZeroMemory(_sessionKey);
                CryptographicOperations.ZeroMemory(_noncePrefix);
            }
        }

        private string RenderPlainText(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            _innerFormatter.Format(logEvent, writer);
            return writer.ToString();
        }

        private void WriteHeader(string keyId, byte[] encryptedSessionKey)
        {
            _writer.Write(Encoding.ASCII.GetBytes(Magic));
            _writer.Write(FormatVersion);
            WriteShortString(keyId);
            WriteShortString(Algorithm);
            _writer.Write((ushort)encryptedSessionKey.Length);
            _writer.Write(encryptedSessionKey);
            _writer.Flush();
        }

        private void WriteShortString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > byte.MaxValue)
            {
                throw new InvalidOperationException($"Header value is too long: {value}");
            }

            _writer.Write((byte)bytes.Length);
            _writer.Write(bytes);
        }

        private byte[] CreateNonce(ulong sequence)
        {
            var nonce = new byte[NonceSizeBytes];
            Buffer.BlockCopy(_noncePrefix, 0, nonce, 0, NoncePrefixSizeBytes);
            BinaryPrimitives.WriteUInt64LittleEndian(nonce.AsSpan(NoncePrefixSizeBytes), sequence);
            return nonce;
        }

        private static string CreateKeyId(RSA rsa)
        {
            byte[] publicKey = rsa.ExportSubjectPublicKeyInfo();
            byte[] hash = SHA256.HashData(publicKey);
            return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        }
    }
}
