using IronworksTranslator.Models.Enums;
using Serilog;
using System.Diagnostics;
using System.IO;

namespace IronworksTranslator.Utils.Translator
{
    internal static class MiLMMTNativeBackendSelector
    {
        private const string ProbeExeName = "IronworksMiLMMTNativeProbe.exe";
        private const int ProbeTimeoutMilliseconds = 15000;

        internal static IReadOnlyList<LocalModelDevicePriority> GetCandidates(
            LocalModelDevicePriority requestedPriority,
            bool avx2Supported)
        {
            if (requestedPriority == LocalModelDevicePriority.Cpu || !avx2Supported)
            {
                return [LocalModelDevicePriority.Cpu];
            }

            return requestedPriority == LocalModelDevicePriority.Vulkan
                ? [
                    LocalModelDevicePriority.Vulkan,
                    LocalModelDevicePriority.Cuda,
                    LocalModelDevicePriority.Cpu
                ]
                : [
                    LocalModelDevicePriority.Cuda,
                    LocalModelDevicePriority.Vulkan,
                    LocalModelDevicePriority.Cpu
                ];
        }

        internal static LocalModelDevicePriority ResolveBackend(
            LocalModelDevicePriority requestedPriority,
            bool avx2Supported,
            Func<LocalModelDevicePriority, bool> gpuProbe)
        {
            ArgumentNullException.ThrowIfNull(gpuProbe);

            foreach (var candidate in GetCandidates(requestedPriority, avx2Supported))
            {
                if (candidate == LocalModelDevicePriority.Cpu || gpuProbe(candidate))
                {
                    return candidate;
                }
            }

            return LocalModelDevicePriority.Cpu;
        }

        internal static string GetLlamaPath(
            LocalModelDevicePriority devicePriority,
            string applicationBaseDirectory)
        {
            var packName = devicePriority switch
            {
                LocalModelDevicePriority.Cuda => "milmmt-cuda",
                LocalModelDevicePriority.Vulkan => "milmmt-vulkan",
                _ => throw new ArgumentOutOfRangeException(nameof(devicePriority), devicePriority, "CPU has no custom MiLMMT runtime pack.")
            };

            return Path.Combine(
                applicationBaseDirectory,
                "runtimes",
                "win-x64",
                "native",
                packName,
                "llama.dll");
        }

        internal static bool ProbeGpuBackend(LocalModelDevicePriority devicePriority)
        {
            var llamaPath = GetLlamaPath(devicePriority, AppContext.BaseDirectory);
            if (!File.Exists(llamaPath))
            {
                Log.Warning(
                    "MiLMMT {DevicePriority} runtime pack is missing: {LlamaPath}",
                    devicePriority,
                    llamaPath);
                return false;
            }

            var probePath = Path.Combine(AppContext.BaseDirectory, ProbeExeName);
            if (!File.Exists(probePath))
            {
                Log.Warning("MiLMMT native probe executable is missing: {ProbePath}", probePath);
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = probePath,
                    WorkingDirectory = AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                startInfo.ArgumentList.Add("--backend");
                startInfo.ArgumentList.Add(devicePriority == LocalModelDevicePriority.Cuda ? "cuda" : "vulkan");
                startInfo.ArgumentList.Add("--native-library");
                startInfo.ArgumentList.Add(llamaPath);

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Log.Warning("Failed to start the MiLMMT {DevicePriority} native probe.", devicePriority);
                    return false;
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(ProbeTimeoutMilliseconds))
                {
                    process.Kill(true);
                    process.WaitForExit();
                    Log.Warning(
                        "MiLMMT {DevicePriority} native probe timed out after {TimeoutMilliseconds} ms.",
                        devicePriority,
                        ProbeTimeoutMilliseconds);
                    return false;
                }

                Task.WaitAll(standardOutputTask, standardErrorTask);
                var standardOutput = standardOutputTask.Result.Trim();
                var standardError = standardErrorTask.Result.Trim();
                if (process.ExitCode == 0)
                {
                    Log.Information(
                        "MiLMMT {DevicePriority} native probe passed. {ProbeOutput}",
                        devicePriority,
                        standardOutput);
                    return true;
                }

                Log.Warning(
                    "MiLMMT {DevicePriority} native probe failed with exit code {ExitCode}. Output: {ProbeOutput}. Error: {ProbeError}",
                    devicePriority,
                    process.ExitCode,
                    standardOutput,
                    standardError);
                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "MiLMMT {DevicePriority} native probe could not run.", devicePriority);
                return false;
            }
        }
    }

    internal sealed class MiLMMTNativeBackendSession
    {
        private LocalModelDevicePriority? selectedDevicePriority;

        internal LocalModelDevicePriority Select(
            LocalModelDevicePriority requestedPriority,
            bool avx2Supported,
            Func<LocalModelDevicePriority, bool> gpuProbe)
        {
            return selectedDevicePriority ??= MiLMMTNativeBackendSelector.ResolveBackend(
                requestedPriority,
                avx2Supported,
                gpuProbe);
        }
    }
}
