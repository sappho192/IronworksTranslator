using Serilog;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace IronworksTranslator.Utils
{
    public sealed record SystemResourceSnapshot(
        ulong TotalRamBytes,
        ulong UsedRamBytes,
        ulong? TotalVramBytes,
        ulong? UsedVramBytes,
        string? VramAdapterName)
    {
        public ulong AvailableRamBytes => TotalRamBytes > UsedRamBytes
            ? TotalRamBytes - UsedRamBytes
            : 0;

        public ulong? AvailableVramBytes => TotalVramBytes is { } totalVram
            && UsedVramBytes is { } usedVram
            ? totalVram > usedVram
                ? totalVram - usedVram
                : 0
            : null;
    }

    public static class SystemResourceMonitor
    {
        public static SystemResourceSnapshot GetSnapshot()
        {
            var (totalRam, usedRam) = GetRamUsage();
            var (totalVram, usedVram, vramAdapterName) = GetVramUsage();

            return new SystemResourceSnapshot(totalRam, usedRam, totalVram, usedVram, vramAdapterName);
        }

        private static (ulong TotalBytes, ulong UsedBytes) GetRamUsage()
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
            {
                return (0, 0);
            }

            return (status.TotalPhys, status.TotalPhys - status.AvailPhys);
        }

        private static (ulong? TotalBytes, ulong? UsedBytes, string? AdapterName) GetVramUsage()
        {
            var primaryGpu = GetPrimaryGpuAdapter();
            var totalBytes = primaryGpu?.DedicatedVideoMemoryBytes
                ?? GetVramTotalBytesFromWmi();
            var usedBytes = GetDedicatedVramUsageFromPerformanceCounters(primaryGpu?.PerformanceCounterLuid)
                ?? GetDedicatedVramUsageFromWmi();
            var adapterName = primaryGpu?.Name
                ?? GetVramAdapterNameFromWmi();

            return (totalBytes, usedBytes, adapterName);
        }

        private static GpuAdapterInfo? GetPrimaryGpuAdapter()
        {
            try
            {
                var factoryGuid = typeof(IDXGIFactory1).GUID;
                Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref factoryGuid, out var factory));

                try
                {
                    var adapters = new List<GpuAdapterInfo>();
                    for (uint index = 0; factory.EnumAdapters1(index, out var adapter) == 0; index++)
                    {
                        try
                        {
                            Marshal.ThrowExceptionForHR(adapter.GetDesc1(out var desc));
                            var dedicatedVideoMemory = desc.DedicatedVideoMemory.ToUInt64();
                            if (dedicatedVideoMemory == 0 || (desc.Flags & DxgiAdapterFlagSoftware) != 0)
                            {
                                continue;
                            }

                            adapters.Add(new GpuAdapterInfo(
                                desc.Description,
                                dedicatedVideoMemory,
                                FormatPerformanceCounterLuid(desc.AdapterLuid)));
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(adapter);
                        }
                    }

                    return adapters
                        .OrderByDescending(adapter => adapter.DedicatedVideoMemoryBytes)
                        .FirstOrDefault();
                }
                finally
                {
                    Marshal.ReleaseComObject(factory);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query VRAM from DXGI.");
                return null;
            }
        }

        private static ulong? GetVramTotalBytesFromWmi()
        {
            try
            {
                ulong total = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        if (result["AdapterRAM"] is uint adapterRam)
                        {
                            total += adapterRam;
                        }
                    }
                }

                return total == 0 ? null : total;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query total VRAM.");
                return null;
            }
        }

        private static string? GetVramAdapterNameFromWmi()
        {
            try
            {
                string? adapterName = null;
                ulong largestAdapterRam = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        if (result["Name"] is not string name)
                        {
                            continue;
                        }

                        var adapterRam = result["AdapterRAM"] is uint ram
                            ? ram
                            : 0;
                        if (adapterName == null || adapterRam > largestAdapterRam)
                        {
                            adapterName = name;
                            largestAdapterRam = adapterRam;
                        }
                    }
                }

                return adapterName;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query VRAM adapter name.");
                return null;
            }
        }

        private static ulong? GetDedicatedVramUsageFromPerformanceCounters(string? performanceCounterLuid)
        {
            try
            {
                ulong total = 0;
                var category = new PerformanceCounterCategory("GPU Adapter Memory");
                foreach (var instanceName in category.GetInstanceNames())
                {
                    if (!string.IsNullOrEmpty(performanceCounterLuid)
                        && !instanceName.StartsWith(performanceCounterLuid, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    using var counter = new PerformanceCounter(
                        "GPU Adapter Memory",
                        "Dedicated Usage",
                        instanceName,
                        readOnly: true);
                    var value = counter.NextValue();
                    if (value > 0)
                    {
                        total += (ulong)value;
                    }
                }

                return total;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query dedicated VRAM usage from performance counters.");
                return null;
            }
        }

        private static ulong? GetDedicatedVramUsageFromWmi()
        {
            try
            {
                ulong total = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUMemory"))
                using (var results = searcher.Get())
                {
                    foreach (ManagementObject result in results)
                    {
                        if (result["DedicatedUsage"] != null)
                        {
                            total += Convert.ToUInt64(result["DedicatedUsage"]);
                        }
                    }
                }

                return total;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Failed to query VRAM usage.");
                return null;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx lpBuffer);

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 factory);

        private const uint DxgiAdapterFlagSoftware = 2;

        private static string FormatPerformanceCounterLuid(Luid luid)
        {
            return $"luid_0x{unchecked((uint)luid.HighPart):x8}_0x{luid.LowPart:x8}";
        }

        private sealed record GpuAdapterInfo(
            string Name,
            ulong DedicatedVideoMemoryBytes,
            string PerformanceCounterLuid);

        [StructLayout(LayoutKind.Sequential)]
        private sealed class MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;

            public MemoryStatusEx()
            {
                Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DxgiAdapterDesc1
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string Description;
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public UIntPtr DedicatedVideoMemory;
            public UIntPtr DedicatedSystemMemory;
            public UIntPtr SharedSystemMemory;
            public Luid AdapterLuid;
            public uint Flags;
        }

        [ComImport]
        [Guid("29038F61-3839-4626-91FD-086879011A05")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIAdapter1
        {
            [PreserveSig]
            int SetPrivateData();

            [PreserveSig]
            int SetPrivateDataInterface();

            [PreserveSig]
            int GetPrivateData();

            [PreserveSig]
            int GetParent();

            [PreserveSig]
            int EnumOutputs();

            [PreserveSig]
            int GetDesc();

            [PreserveSig]
            int CheckInterfaceSupport();

            [PreserveSig]
            int GetDesc1(out DxgiAdapterDesc1 desc);
        }

        [ComImport]
        [Guid("770AAE78-F26F-4DBA-A829-253C83D1B387")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDXGIFactory1
        {
            [PreserveSig]
            int SetPrivateData();

            [PreserveSig]
            int SetPrivateDataInterface();

            [PreserveSig]
            int GetPrivateData();

            [PreserveSig]
            int GetParent();

            [PreserveSig]
            int EnumAdapters();

            [PreserveSig]
            int MakeWindowAssociation();

            [PreserveSig]
            int GetWindowAssociation();

            [PreserveSig]
            int CreateSwapChain();

            [PreserveSig]
            int CreateSoftwareAdapter();

            [PreserveSig]
            int EnumAdapters1(uint adapter, out IDXGIAdapter1 ppAdapter);

            [PreserveSig]
            bool IsCurrent();
        }
    }
}
