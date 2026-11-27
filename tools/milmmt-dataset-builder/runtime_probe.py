import importlib.metadata as metadata
import json
import shutil
import subprocess
import sys

report = {"python": sys.version.split()[0], "packages": {}}
for name in ("torch", "transformers", "peft", "accelerate", "datasets", "safetensors", "sentencepiece"):
    try:
        report["packages"][name] = metadata.version(name)
    except metadata.PackageNotFoundError:
        report["packages"][name] = None
report["disk_free_bytes"] = shutil.disk_usage(".").free
if shutil.which("nvidia-smi"):
    result = subprocess.run(
        ["nvidia-smi", "--query-gpu=name,memory.total,driver_version", "--format=csv,noheader"],
        capture_output=True,
        text=True,
        timeout=15,
    )
    report["nvidia_smi_exit_code"] = result.returncode
    report["nvidia_smi"] = result.stdout.strip()
else:
    report["nvidia_smi"] = None
try:
    import torch

    report["torch_imported"] = torch.__version__
    report["torch_cuda_build"] = torch.version.cuda
    report["cuda_available"] = torch.cuda.is_available()
    if report["cuda_available"]:
        report["gpu_name"] = torch.cuda.get_device_name(0)
        report["gpu_memory_bytes"] = torch.cuda.get_device_properties(0).total_memory
        try:
            report["bf16_native_supported"] = torch.cuda.is_bf16_supported(including_emulation=False)
        except TypeError:
            report["bf16_native_supported"] = None
except Exception as error:
    report["torch_import_error_type"] = type(error).__name__
print(json.dumps(report, indent=2))
