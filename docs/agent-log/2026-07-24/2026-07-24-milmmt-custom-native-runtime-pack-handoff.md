# MiLMMT 자체 CUDA/Vulkan native runtime pack handoff

작성일: 2026-07-24

## 목적

IronworksTranslator의 MiLMMT 경로가 NuGet의
`LLamaSharp.Backend.Cuda12`/`LLamaSharp.Backend.Vulkan` native DLL 대신,
MiLMMT 모델에 맞춰 최소화·검증된 자체 DLL pack을 앱과 함께 배포하도록 한다.

`LLamaSharp` managed API는 계속 `0.27.0` NuGet package를 사용한다. 이 handoff의
CUDA/Vulkan native DLL은 같은 LLamaSharp 0.27.0 대응 llama.cpp revision
`3f7c29d318e317b63f54c558bc69803963d7d88c`으로 만들었다.

## 전달 artifact

현재 handoff archive:

```text
D:\REPO\google-translategemma-4b-it-compact\artifacts\release\
  ironworkstranslator-milmmt-native-runtime-packs-win-x64-20260724.zip
```

- SHA-256: `9420541c18df1aba5429b8edab5afc499f9accfe67bcb33996647c2a4f665da7`
- 파일 크기: `29,366,993` bytes
- GGUF 모델은 archive에 포함하지 않는다.

archive를 이 repository의 `NativeRuntimePacks/` 아래에 풀면 필요한 runtime 경로는
다음과 같다.

```text
NativeRuntimePacks/
├── runtimes/win-x64/native/milmmt-cuda/
│   ├── llama.dll, ggml.dll, ggml-base.dll, ggml-cpu.dll, ggml-cuda.dll
│   └── native-manifest.json
├── runtimes/win-x64/native/milmmt-vulkan/
│   ├── llama.dll, ggml.dll, ggml-base.dll, ggml-cpu.dll, ggml-vulkan.dll
│   ├── shader-profile.json
│   └── native-manifest.json
├── runtime-packs-manifest.json
├── Verify-IronworksTranslator-MiLMMT-RuntimePacks.ps1
└── Verify-WindowsNativePackage.ps1
```

두 pack은 서로 다른 디렉터리에 같은 이름의 `llama.dll`을 가진다. 이 이름을 app
output root에 복사하면 안 된다. 실행 시 선택한 pack의 absolute `llama.dll` 경로를
LLamaSharp에 지정한다.

## Project file 변경

`src/IronworksTranslator/IronworksTranslator.csproj`에서 다음 managed package는 유지한다.

```xml
<PackageReference Include="LLamaSharp" Version="0.27.0" />
```

자체 pack으로 전환할 때는 아래 두 native backend package를 제거한다.

```xml
<PackageReference Include="LLamaSharp.Backend.Cuda12" Version="0.27.0" />
<PackageReference Include="LLamaSharp.Backend.Vulkan" Version="0.27.0" />
```

CPU fallback을 우선 기존 경로로 유지한다면
`LLamaSharp.Backend.Cpu` package는 남긴다. CUDA/Vulkan pack의 `ggml-cpu.dll`은
보조 CPU backend일 뿐이며, GPU driver가 없는 PC의 독립 CPU pack을 대체하지 않는다.

프로젝트 파일이 `src/IronworksTranslator/` 아래에 있으므로, 다음 content item을 추가해
build와 publish에서 두 native pack과 배포용 MIT license를 보존한다.

```xml
<ItemGroup>
  <Content Include="..\..\NativeRuntimePacks\runtimes\win-x64\native\**\*">
    <Link>runtimes\win-x64\native\%(RecursiveDir)%(Filename)%(Extension)</Link>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
  <Content Include="..\..\NativeRuntimePacks\runtime-packs-manifest.json">
    <Link>runtimes\win-x64\native\milmmt-runtime-packs-manifest.json</Link>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
  <Content Include="..\..\NativeRuntimePacks\LICENSES\llama.cpp-MIT.txt">
    <Link>LICENSES\llama.cpp-MIT.txt</Link>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
  </Content>
</ItemGroup>
```

## Native backend 선택

앱 시작 시 `IronworksMiLMMTNativeProbe.exe`가 GPU pack마다 별도 process에서
strict native probe를 수행한다. CUDA 우선 설정은 `CUDA → Vulkan → CPU`, Vulkan 우선
설정은 `Vulkan → CUDA → CPU` 순서이며, GPU pack은 AVX2 이상에서만 후보가 된다.
probe는 driver/backend buffer 초기화만 확인하고 GGUF load나 VRAM 부족은 판정하지 않는다.

`MiLMMTTranslator.ConfigureNativeLibrary`는 probe 결과의 한 backend만 첫
`LLamaWeights` load 전에 설정한다. backend 변경은 process restart를 요구한다.

CUDA 또는 Vulkan을 선택할 때는 automatic discovery 대신 다음 원칙을 쓴다.

```csharp
var nativeRoot = Path.Combine(
    AppContext.BaseDirectory, "runtimes", "win-x64", "native");
var packName = devicePriority == LocalModelDevicePriority.Cuda
    ? "milmmt-cuda"
    : "milmmt-vulkan";
var llamaPath = Path.Combine(nativeRoot, packName, "llama.dll");

NativeLibraryConfig.LLama.WithLibrary(llamaPath);
NativeLibraryConfig.All.WithLogCallback(/* existing Serilog callback */);
```

`NativeLibraryConfig.All.WithLibrary`는 LLamaSharp 0.27.0에서 llama와 mtmd 두 경로를
요구한다. MiLMMT의 text-only 경로에는 pack에 없는 `mtmd.dll`을 설정하지 말고
`NativeLibraryConfig.LLama.WithLibrary(fullPath)`를 사용한다. 이 호출은 다른 automatic
CUDA/Vulkan backend 설정을 무시하며,
이미 native library가 로드된 뒤에는 다시 설정할 수 없다. 따라서 한 process 안에서
CUDA, Vulkan, CPU를 hot-switch하지 않는다.

CUDA/Vulkan probe는 각각 `CUDA0` 또는 `Vulkan0` buffer가 실제 초기화됐는지 확인한다.
단순히 `GpuLayerCount = 99`를 설정한 것만으로 GPU 사용을 선언하지 않는다. GPU probe가
모두 실패하면 기존 NuGet CPU backend를 선택한다. 이후 모델 load/VRAM/추론 오류는
실행 중 backend를 바꾸지 않고 기존 원문 반환과 로그 처리로 남긴다.

## Runtime prerequisite와 지원 경계

| Pack | 필요 조건 | 비고 |
|---|---|---|
| CUDA | NVIDIA display driver (`nvcuda.dll`), VC++ v14 x64 Redistributable | `cudart`/`cublas`/`cublasLt` 없이 동작하는 driver-only pack |
| Vulkan | Vulkan 1.2+ graphics driver/loader, VC++ v14 x64 Redistributable | `vulkan-1.dll`은 bundle에 포함하지 않고 system driver loader를 사용 |

CUDA pack에는 `sm_61`, `sm_75`, `sm_86`, `sm_89`, `sm_120a` native cubin과
generic `compute_120` PTX fallback이 있다. generic PTX는 미래 GPU에서의 실행
fallback일 뿐 release-support 선언이 아니다. RTX 50의 native/forced-PTX hardware
matrix가 완료되기 전까지는 해당 경로를 product release evidence로 사용하지 않는다.

Vulkan은 Windows NVIDIA와 AMD iGPU에서 strict probe, full-layer offload, 60행
self-repeat을 통과했다. Intel GPU 및 실제 clean Windows machine은 별도 acceptance
대상이다.

## 수령 및 publish 검증

archive를 푼 직후 다음을 실행한다.

```powershell
pwsh -NoProfile -File .\NativeRuntimePacks\Verify-IronworksTranslator-MiLMMT-RuntimePacks.ps1 `
  -BundlePath .\NativeRuntimePacks `
  -RequireRuntimePrerequisites
```

이는 exact file set, DLL hash, CUDA fatbin/dependency evidence, Vulkan shader profile
hash 및 현재 PC의 driver/VC++ prerequisite를 검증한다.

publish 후에는 `runtimes/win-x64/native/milmmt-cuda/llama.dll`과
`milmmt-vulkan/llama.dll`을 각각 명시하는 C# smoke를 수행한다. MiLMMT raw prompt를
사용하고 automatic chat template은 적용하지 않는다.

## 현재 4B 모델 smoke 결과

다음 model도 이 pack으로 동작을 확인했다.

```text
C:\Users\tikim\AppData\Local\IronworksTranslator\data\model\milmmt-46-4b-v0.1\
  MiLMMT-46-4B-v0.1.Q4_K_M.gguf
```

- SHA-256: `9888198d9f1cbac935f6428a2a4aead1272f55c1d5ebacd395ab1575bd09b1ec`
- Gemma3 / 445 tensors / `f32`, `q4_k`, `q6_k`; Vulkan profile 허용 범위 통과
- RTX 4080 SUPER single-prompt smoke:
  - CUDA: `CUDA0`, `35/35` GPU layers, `Please close the door.` → `문 좀 닫아 주세요.`
  - Vulkan: `Vulkan0`, `35/35` GPU layers, 같은 출력

이는 runtime compatibility smoke일 뿐 4B 모델의 번역 품질이나 backend 간 성능을
보증하는 전체 benchmark는 아니다.
