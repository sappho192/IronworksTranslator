using IronworksTranslator.Utils.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace IronworksTranslator.Tests.Utils;

public sealed class EncryptedLogSinkTests : IDisposable
{
    private readonly RSA _rsa = RSA.Create(3072);

    public void Dispose()
    {
        Log.CloseAndFlush();
        _rsa.Dispose();
    }

    [Fact]
    public void Emit_WritesV2BinaryLogAndDoesNotLeakPlaintext()
    {
        const string sensitiveMessage = "Free Company secret chat";
        var tempFile = GetTempFilePath();

        try
        {
            using (var sink = new EncryptedLogSink(tempFile, _rsa.ExportSubjectPublicKeyInfoPem()))
            {
                sink.Emit(CreateLogEvent("Adding {Message}", sensitiveMessage));
            }

            var encryptedBytes = File.ReadAllBytes(tempFile);
            Assert.Equal(
                Encoding.ASCII.GetBytes(EncryptedLogSink.Magic),
                encryptedBytes.Take(EncryptedLogSink.Magic.Length).ToArray());
            Assert.DoesNotContain(
                sensitiveMessage,
                Encoding.UTF8.GetString(encryptedBytes, 0, encryptedBytes.Length));
            Assert.Contains(sensitiveMessage, DecryptFile(tempFile, _rsa));
        }
        finally
        {
            DeleteIfExists(tempFile);
        }
    }

    [Fact]
    public void Emit_V2BinaryLogIsSmallerThanV1StyleEnvelopeForShortMessages()
    {
        var v2File = GetTempFilePath();
        var publicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();
        var messages = Enumerable
            .Range(0, 50)
            .Select(index => $"short sensitive chat {index:D2}")
            .ToArray();

        try
        {
            using (var sink = new EncryptedLogSink(v2File, publicKeyPem))
            {
                foreach (var message in messages)
                {
                    sink.Emit(CreateLogEvent("Adding {Message}", message));
                }
            }

            var v1StyleSize = EstimateV1StyleEnvelopeSize(messages, publicKeyPem);
            var v2Size = new FileInfo(v2File).Length;
            Assert.True(v2Size < v1StyleSize / 2, $"v2={v2Size}, v1Style={v1StyleSize}");
        }
        finally
        {
            DeleteIfExists(v2File);
        }
    }

    [Fact]
    public void Emit_FailsToDecryptWithDifferentPrivateKey()
    {
        var tempFile = GetTempFilePath();

        try
        {
            using (var sink = new EncryptedLogSink(tempFile, _rsa.ExportSubjectPublicKeyInfoPem()))
            {
                sink.Emit(CreateLogEvent("Adding {Message}", "party chat"));
            }

            using var otherKey = RSA.Create(3072);
            var exception = Record.Exception(() => DecryptFile(tempFile, otherKey));
            Assert.IsAssignableFrom<CryptographicException>(exception);
        }
        finally
        {
            DeleteIfExists(tempFile);
        }
    }

    [Fact]
    public void Emit_FailsToDecryptWhenFrameTagIsTampered()
    {
        var tempFile = GetTempFilePath();

        try
        {
            using (var sink = new EncryptedLogSink(tempFile, _rsa.ExportSubjectPublicKeyInfoPem()))
            {
                sink.Emit(CreateLogEvent("Adding {Message}", "party chat"));
            }

            var bytes = File.ReadAllBytes(tempFile);
            bytes[^1] ^= 0x01;
            File.WriteAllBytes(tempFile, bytes);

            var exception = Record.Exception(() => DecryptFile(tempFile, _rsa));
            Assert.IsAssignableFrom<CryptographicException>(exception);
        }
        finally
        {
            DeleteIfExists(tempFile);
        }
    }

    [Fact]
    public void MigrateLegacyTextLogs_WritesEncryptedCopyAndDeletesPlaintext()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"IronworksTranslator.Tests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        var legacyLogPath = Path.Combine(tempDirectory, "legacy.txt");
        var encryptedLogPath = Path.Combine(tempDirectory, "encrypted.iwlog");
        const string sensitiveMessage = "legacy tell message";

        try
        {
            File.WriteAllText(legacyLogPath, sensitiveMessage);
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(new EncryptedLogSink(encryptedLogPath, _rsa.ExportSubjectPublicKeyInfoPem()))
                .CreateLogger();

            LegacyLogMigrator.MigrateLegacyTextLogs(tempDirectory);
            Log.CloseAndFlush();

            Assert.False(File.Exists(legacyLogPath));
            var encryptedBytes = File.ReadAllBytes(encryptedLogPath);
            Assert.DoesNotContain(
                sensitiveMessage,
                Encoding.UTF8.GetString(encryptedBytes, 0, encryptedBytes.Length));
            Assert.Contains(sensitiveMessage, DecryptFile(encryptedLogPath, _rsa));
        }
        finally
        {
            Log.CloseAndFlush();
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    private static LogEvent CreateLogEvent(string template, string message)
    {
        var parser = new MessageTemplateParser();
        var messageTemplate = parser.Parse(template);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            messageTemplate,
            [new LogEventProperty("Message", new ScalarValue(message))]);
    }

    private static long EstimateV1StyleEnvelopeSize(IEnumerable<string> messages, string publicKeyPem)
    {
        using var publicKey = RSA.Create();
        publicKey.ImportFromPem(publicKeyPem);

        long total = 0;
        foreach (var message in messages)
        {
            var rendered = Encoding.UTF8.GetBytes($"[2026-01-01 00:00:00.000 +00:00 INF] Adding {message}{Environment.NewLine}");
            var encryptedKeyLength = publicKey.Encrypt(new byte[32], RSAEncryptionPadding.OaepSHA256).Length;
            total += 145;
            total += Base64Length(encryptedKeyLength);
            total += Base64Length(12);
            total += Base64Length(rendered.Length);
            total += Base64Length(16);
            total += Environment.NewLine.Length;
        }

        return total;
    }

    private static int Base64Length(int byteLength)
    {
        return ((byteLength + 2) / 3) * 4;
    }

    private static string DecryptFile(string filePath, RSA rsa)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var magic = Encoding.ASCII.GetString(reader.ReadBytes(EncryptedLogSink.Magic.Length));
        if (magic != EncryptedLogSink.Magic)
        {
            throw new InvalidDataException("Unsupported encrypted log format.");
        }

        var version = reader.ReadByte();
        if (version != EncryptedLogSink.FormatVersion)
        {
            throw new InvalidDataException($"Unsupported encrypted log version: {version}");
        }

        _ = ReadShortString(reader);
        var algorithm = ReadShortString(reader);
        if (algorithm != EncryptedLogSink.Algorithm)
        {
            throw new InvalidDataException($"Unsupported encrypted log algorithm: {algorithm}");
        }

        var encryptedKey = reader.ReadBytes(reader.ReadUInt16());
        var key = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
        var output = new StringBuilder();

        try
        {
            while (stream.Position < stream.Length)
            {
                _ = reader.ReadUInt64();
                var nonce = reader.ReadBytes(12);
                var ciphertext = reader.ReadBytes(reader.ReadInt32());
                var tag = reader.ReadBytes(16);
                var plaintext = new byte[ciphertext.Length];

                try
                {
                    using var aes = new AesGcm(key, tagSizeInBytes: 16);
                    aes.Decrypt(nonce, ciphertext, tag, plaintext);
                    output.Append(Encoding.UTF8.GetString(plaintext));
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }

        return output.ToString();
    }

    private static string ReadShortString(BinaryReader reader)
    {
        return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadByte()));
    }

    private static string GetTempFilePath()
    {
        return Path.Combine(Path.GetTempPath(), $"IronworksTranslator.Tests.{Guid.NewGuid():N}.iwlog");
    }

    private static void DeleteIfExists(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }
}
