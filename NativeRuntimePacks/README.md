# IronworksTranslator MiLMMT 자체 native runtime pack handoff

작성일: 2026-07-24

## 목적과 경계

이 handoff는 IronworksTranslator가 NuGet의 CUDA/Vulkan native backend 대신 검증된
MiLMMT 전용 native DLL 묶음을 앱과 함께 배포하도록 준비한다. `LLamaSharp`의 managed
C# API는 계속 NuGet `0.27.0`을 사용한다. CUDA와 Vulkan pack은 같은 llama.cpp
`3f7c29d318e317b63f54c558bc69803963d7d88c`에서 만들었으므로 managed/native ABI가
고정되어 있다.

생성 명령:

```powershell
pwsh -NoProfile -File scripts\new_ironworks_milmmt_runtime_pack_bundle.ps1 `
  -Clean `
  -RequireRuntimePrerequisites
```

기본 생성 위치:

```text
artifacts/release/ironworkstranslator-milmmt-native-runtime-packs-win-x64-20260724/
├── runtimes/win-x64/native/milmmt-cuda/
├── runtimes/win-x64/native/milmmt-vulkan/
├── runtime-packs-manifest.json
├── Verify-IronworksTranslator-MiLMMT-RuntimePacks.ps1
├── Verify-WindowsNativePackage.ps1
└── LICENSES/llama.cpp-MIT.txt
```

같은 이름의 `.zip`은 전달용 archive다. GGUF 모델은 포함하지 않는다.

## 포함 native pack

| Pack | native DLL | 고객 PC prerequisite | 상태 |
|---|---|---|---|
| `milmmt-cuda` | `llama`, `ggml`, `ggml-base`, `ggml-cpu`, `ggml-cuda` | NVIDIA display driver (`nvcuda.dll`), VC++ v14 x64 runtime | local RTX 4080 SUPER에서 strict CUDA/27층 offload 확인. `sm_61/75/86/89/120a` native cubin, `compute_120` PTX fallback 포함 |
| `milmmt-vulkan` | `llama`, `ggml`, `ggml-base`, `ggml-cpu`, `ggml-vulkan` | Vulkan 1.2+ driver/loader, VC++ v14 x64 runtime | Windows NVIDIA와 AMD iGPU에서 strict probe, full offload, 60행 self-repeat 확인 |

CUDA의 generic `compute_120` PTX는 미래 GPU 실행 fallback일 뿐 release-support 선언이
아니다. `sm_120a`와 PTX JIT의 실제 RTX 50 hardware matrix가 완료될 때까지 이 pack을
그 GPU 계열의 product-release asset으로 표현하지 않는다.

Vulkan loader `vulkan-1.dll`은 bundle에 넣지 않는다. 자체 loader를 별도로 배포하는
제품 정책이 승인되지 않은 한, 고객 GPU driver가 제공하는 system loader를 사용하고
strict probe가 실패하면 CPU path로 fallback한다.

## IronworksTranslator project 변경

1. handoff archive를 프로젝트의 `NativeRuntimePacks/` 아래에 푼다.
2. `LLamaSharp` 0.27.0은 유지한다.
3. `LLamaSharp.Backend.Cuda12`와 `LLamaSharp.Backend.Vulkan` package reference를
   제거한다. CPU fallback을 현 단계에서 NuGet으로 유지한다면
   `LLamaSharp.Backend.Cpu`는 남긴다.
4. 다음 content 항목을 project file에 넣어 build와 publish가 두 pack을 보존하게 한다.

```xml
<ItemGroup>
  <Content Include="NativeRuntimePacks\runtimes\win-x64\native\**\*">
    <Link>runtimes\win-x64\native\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
  <Content Include="NativeRuntimePacks\runtime-packs-manifest.json">
    <Link>runtimes\win-x64\native\milmmt-runtime-packs-manifest.json</Link>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

`Verify-*.ps1`, README, license는 installer/release asset의 audit 자료다. 실행
directory로 복사할 필요는 없다.

## 시작 시 native pack 선택

`NativeLibraryConfig.WithLibrary(fullPath)`는 다른 automatic backend 설정을 무시하고,
native library가 로드된 후에는 다시 설정할 수 없다. 따라서 첫 `LLamaWeights` 또는
다른 LLamaSharp native API 호출 전에 아래와 같이 한 pack만 선택한다.

```csharp
private static void ConfigureMiLMMTNativePack(LocalModelDevicePriority device)
{
    var nativeRoot = Path.Combine(
        AppContext.BaseDirectory, "runtimes", "win-x64", "native");

    var packName = device switch
    {
        LocalModelDevicePriority.Cuda => "milmmt-cuda",
        LocalModelDevicePriority.Vulkan => "milmmt-vulkan",
        _ => throw new InvalidOperationException("CPU uses the separate CPU backend path."),
    };
    var llamaPath = Path.Combine(nativeRoot, packName, "llama.dll");
    if (!File.Exists(llamaPath))
    {
        throw new FileNotFoundException("MiLMMT native pack is missing.", llamaPath);
    }

    var config = NativeLibraryConfig.All.WithLibrary(llamaPath);
    config.WithLogCallback((level, message) => /* existing Serilog callback */);
}
```

권장 선택 순서는 `CUDA → Vulkan → CPU`다. CUDA/Vulkan availability는 UI 설정값만으로
가정하지 말고, 해당 pack을 대상으로 strict native probe를 한 번 실행하거나 native
log에서 `CUDA0` 또는 `Vulkan0` buffer가 실제로 초기화되었는지 확인한다.

기존 `MiLMMTTranslator.ConfigureNativeLibrary`는 static lock으로 backend 변경에 앱
재시작이 필요하다고 이미 처리한다. CPU를 포함해 어떤 device priority 변경도 재시작
대상으로 통일한다. 이미 CUDA pack을 로드한 프로세스 안에서 CPU pack 또는 Vulkan
pack으로 바꾸지 않는다.

## 인수 및 release gate

handoff 받는 쪽은 unpack 뒤 다음을 실행한다.

```powershell
pwsh -NoProfile -File .\Verify-IronworksTranslator-MiLMMT-RuntimePacks.ps1 `
  -BundlePath . `
  -RequireRuntimePrerequisites
```

이 검사는 각 package의 exact file set, hash, CUDA fatbin/dependency evidence, Vulkan
shader-profile hash, 그리고 현 host의 driver/VC++ prerequisite를 확인한다.

IronworksTranslator publish build 뒤에는 publish directory의 두 `llama.dll` 경로를
명시해 C# smoke를 각각 실행한다. CUDA는 `27/27` GPU layer offload와 CUDA native log,
Vulkan은 strict `Vulkan0` probe를 release evidence로 남긴다. 모델 prompt는 MiLMMT
raw prompt를 쓰고 automatic chat template은 사용하지 않는다.

## 현재 handoff 검증

2026-07-24의 생성 run은 `-RequireRuntimePrerequisites`와 bundle self-verifier를
통과했다. 생성된 bundle 안의 absolute DLL 경로를 `LlamaSharpSmoke`에 직접 지정한
한 문장 smoke도 다음 결과를 확인했다.

| backend | strict buffer | offload | output |
|---|---|---:|---|
| CUDA | `CUDA0, CPU` | `27/27` | `Please close the door.` → `문 닫아 주세요.` |
| Vulkan device 0 | `Vulkan0, CPU` | `27/27` | `Please close the door.` → `문 닫아 주세요.` |

이 결과는 handoff folder 안의 sidecar DLL 경로 해석과 native selection을 확인한
smoke다. Intel GPU 및 실제 clean Windows machine acceptance, 그리고 CUDA의 CC 12
native/PTX hardware gate는 여전히 별도 release gate다.
