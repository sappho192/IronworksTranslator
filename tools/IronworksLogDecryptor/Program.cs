// Usage:
//   Generate a new key pair:
//     $env:IRONWORKS_LOG_KEY_PASSPHRASE = "<passphrase>"
//     dotnet run --project tools/IronworksLogDecryptor -- --generate-keypair --public-key log_public_key.pem --private-key log_private_key.pem
//
//   Decrypt an IWLOG2 file:
//     $env:IRONWORKS_LOG_KEY_PASSPHRASE = "<passphrase>"
//     dotnet run --project tools/IronworksLogDecryptor -- --input latest.iwlog --private-key log_private_key.pem --output latest.txt
//
//   The passphrase can also be typed interactively when IRONWORKS_LOG_KEY_PASSPHRASE is not set.

using System.Security.Cryptography;
using System.Text;

const int TagSizeBytes = 16;
const string Magic = "IWLOG2";
const byte FormatVersion = 2;
const string Algorithm = "RSA-OAEP-SHA256+A256GCM";

try
{
    var options = Options.Parse(args);
    if (options.GenerateKeypair)
    {
        GenerateKeypair(options);
        return 0;
    }

    DecryptLog(options);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static void GenerateKeypair(Options options)
{
    var publicKeyPath = options.PublicKeyPath ?? "log_public_key.pem";
    var privateKeyPath = options.PrivateKeyPath ?? "log_private_key.pem";
    var passphrase = ReadPassphrase(required: true);

    using var rsa = RSA.Create(3072);
    var pbe = new PbeParameters(
        PbeEncryptionAlgorithm.Aes256Cbc,
        HashAlgorithmName.SHA256,
        iterationCount: 100_000);

    File.WriteAllText(publicKeyPath, rsa.ExportSubjectPublicKeyInfoPem(), Encoding.ASCII);
    File.WriteAllText(privateKeyPath, rsa.ExportEncryptedPkcs8PrivateKeyPem(passphrase, pbe), Encoding.ASCII);

    Console.WriteLine($"Public key: {Path.GetFullPath(publicKeyPath)}");
    Console.WriteLine($"Encrypted private key: {Path.GetFullPath(privateKeyPath)}");
}

static void DecryptLog(Options options)
{
    if (string.IsNullOrWhiteSpace(options.InputPath) ||
        string.IsNullOrWhiteSpace(options.PrivateKeyPath) ||
        string.IsNullOrWhiteSpace(options.OutputPath))
    {
        throw new ArgumentException("Usage: --input <file.iwlog> --private-key <private.pem> --output <file.txt>");
    }

    using var rsa = LoadPrivateKey(options.PrivateKeyPath);
    using var input = File.OpenRead(options.InputPath);
    using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
    using var output = new StreamWriter(options.OutputPath, append: false, Encoding.UTF8);

    ReadHeader(reader, rsa, out var key);

    try
    {
        while (input.Position < input.Length)
        {
            ReadFrame(reader, key, output);
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(key);
    }
}

static void ReadHeader(BinaryReader reader, RSA rsa, out byte[] key)
{
    var magic = Encoding.ASCII.GetString(ReadExact(reader, Magic.Length, "log header magic"));
    if (magic != Magic)
    {
        throw new InvalidDataException($"Unsupported encrypted log format. Expected {Magic}.");
    }

    var version = reader.ReadByte();
    if (version != FormatVersion)
    {
        throw new InvalidDataException($"Unsupported encrypted log version: {version}");
    }

    _ = ReadShortString(reader, "key id");
    var algorithm = ReadShortString(reader, "algorithm");
    if (algorithm != Algorithm)
    {
        throw new InvalidDataException($"Unsupported encrypted log algorithm: {algorithm}");
    }

    var encryptedKeyLength = reader.ReadUInt16();
    var encryptedKey = ReadExact(reader, encryptedKeyLength, "encrypted session key");
    key = rsa.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
}

static void ReadFrame(BinaryReader reader, byte[] key, StreamWriter output)
{
    _ = reader.ReadUInt64();
    var nonce = ReadExact(reader, 12, "frame nonce");
    var ciphertextLength = reader.ReadInt32();
    if (ciphertextLength < 0)
    {
        throw new InvalidDataException($"Invalid ciphertext length: {ciphertextLength}");
    }

    var ciphertext = ReadExact(reader, ciphertextLength, "frame ciphertext");
    var tag = ReadExact(reader, TagSizeBytes, "frame authentication tag");
    var plaintext = new byte[ciphertext.Length];

    try
    {
        using var aes = new AesGcm(key, TagSizeBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        output.Write(Encoding.UTF8.GetString(plaintext));
    }
    catch (CryptographicException ex)
    {
        throw new CryptographicException("Failed to decrypt a log frame. The file may be corrupted or tampered with.", ex);
    }
    finally
    {
        CryptographicOperations.ZeroMemory(plaintext);
    }
}

static string ReadShortString(BinaryReader reader, string fieldName)
{
    var length = reader.ReadByte();
    return Encoding.UTF8.GetString(ReadExact(reader, length, fieldName));
}

static byte[] ReadExact(BinaryReader reader, int length, string fieldName)
{
    var bytes = reader.ReadBytes(length);
    if (bytes.Length != length)
    {
        throw new EndOfStreamException($"Unexpected end of file while reading {fieldName}.");
    }

    return bytes;
}

static RSA LoadPrivateKey(string privateKeyPath)
{
    var pem = File.ReadAllText(privateKeyPath);
    var rsa = RSA.Create();

    if (pem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
    {
        rsa.ImportFromEncryptedPem(pem, ReadPassphrase(required: true));
        return rsa;
    }

    rsa.ImportFromPem(pem);
    return rsa;
}

static string ReadPassphrase(bool required)
{
    var passphrase = Environment.GetEnvironmentVariable("IRONWORKS_LOG_KEY_PASSPHRASE");
    if (!string.IsNullOrEmpty(passphrase) || !required)
    {
        return passphrase ?? string.Empty;
    }

    Console.Error.Write("Private key passphrase: ");
    var builder = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.Error.WriteLine();
            break;
        }

        if (key.Key == ConsoleKey.Backspace)
        {
            if (builder.Length > 0)
            {
                builder.Length--;
            }

            continue;
        }

        builder.Append(key.KeyChar);
    }

    if (builder.Length == 0)
    {
        throw new InvalidOperationException("A private key passphrase is required.");
    }

    return builder.ToString();
}

internal sealed record Options(
    bool GenerateKeypair,
    string? InputPath,
    string? PrivateKeyPath,
    string? PublicKeyPath,
    string? OutputPath)
{
    public static Options Parse(string[] args)
    {
        var generateKeypair = false;
        string? inputPath = null;
        string? privateKeyPath = null;
        string? publicKeyPath = null;
        string? outputPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--generate-keypair":
                    generateKeypair = true;
                    break;
                case "--input":
                    inputPath = ReadValue(args, ref i);
                    break;
                case "--private-key":
                    privateKeyPath = ReadValue(args, ref i);
                    break;
                case "--public-key":
                    publicKeyPath = ReadValue(args, ref i);
                    break;
                case "--output":
                    outputPath = ReadValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return new Options(generateKeypair, inputPath, privateKeyPath, publicKeyPath, outputPath);
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {args[index]}.");
        }

        index++;
        return args[index];
    }
}
