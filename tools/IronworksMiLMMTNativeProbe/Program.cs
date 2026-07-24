using LLama.Native;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace IronworksMiLMMTNativeProbe
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var options = ProbeOptions.Parse(args);
                NativeLibraryConfig.LLama.WithLibrary(options.NativeLibraryPath);
                NativeLibraryConfig.All.WithLogCallback((level, message) =>
                    Console.Error.Write($"native:{level}: {message}"));

                var bufferTypes = GetAvailableBackendBufferTypes();
                var expectedPrefix = options.Backend == "cuda" ? "CUDA" : "Vulkan";
                var available = bufferTypes.Any(bufferType =>
                    bufferType.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase));

                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    backend = options.Backend,
                    nativeLibraryPath = options.NativeLibraryPath,
                    nativeBackendBufferTypes = bufferTypes,
                    available,
                }));

                return available ? 0 : 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static IReadOnlyList<string> GetAvailableBackendBufferTypes()
        {
            var bufferTypes = new List<string>();
            var count = NativeApi.ggml_backend_dev_count();
            for (nuint index = 0; index < count; index++)
            {
                var device = NativeApi.ggml_backend_dev_get(index);
                var bufferType = NativeApi.ggml_backend_dev_buffer_type(device);
                var name = Marshal.PtrToStringAnsi(NativeApi.ggml_backend_buft_name(bufferType));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    bufferTypes.Add(name);
                }
            }

            return bufferTypes;
        }

        private sealed record ProbeOptions(string Backend, string NativeLibraryPath)
        {
            internal static ProbeOptions Parse(string[] args)
            {
                string? backend = null;
                string? nativeLibraryPath = null;

                for (var index = 0; index < args.Length; index++)
                {
                    switch (args[index])
                    {
                        case "--backend":
                            backend = GetRequiredValue(args, ref index, "--backend").ToLowerInvariant();
                            break;
                        case "--native-library":
                            nativeLibraryPath = GetRequiredValue(args, ref index, "--native-library");
                            break;
                        default:
                            throw new ArgumentException($"Unsupported argument: {args[index]}");
                    }
                }

                if (backend is not "cuda" and not "vulkan")
                {
                    throw new ArgumentException("--backend must be cuda or vulkan.");
                }

                if (string.IsNullOrWhiteSpace(nativeLibraryPath))
                {
                    throw new ArgumentException("--native-library is required.");
                }

                var fullPath = Path.GetFullPath(nativeLibraryPath);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Native llama library was not found.", fullPath);
                }

                return new ProbeOptions(backend, fullPath);
            }

            private static string GetRequiredValue(string[] args, ref int index, string argumentName)
            {
                if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                {
                    throw new ArgumentException($"{argumentName} requires a value.");
                }

                return args[index];
            }
        }
    }
}
