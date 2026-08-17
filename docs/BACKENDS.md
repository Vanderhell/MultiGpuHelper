# Discovery backends

## NVIDIA SMI

Purpose: enumerate NVIDIA devices using `nvidia-smi`. The command must be on `PATH`. It can report device ordinal, name, total VRAM, and free VRAM. Missing or malformed memory fields are marked with a non-available memory state. Command failure, timeout, or absence produces no devices through the convenience API.

## CUDA

Purpose: enumerate NVIDIA devices through the CUDA Driver API on Windows. It requires the NVIDIA driver library. Device name and total VRAM may be available; free VRAM is not queried and remains unknown. Missing runtime and native errors produce an empty result.

## ROCm

Purpose: enumerate AMD GPU agents from `rocminfo`. The command must be on `PATH`. Free VRAM is not supplied by this source. Real AMD hardware behavior is not maintainer-verified for 1.1.0.

## WMI

Purpose: best-effort Windows adapter enumeration using `Win32_VideoController`. It requires Windows WMI. Vendor is inferred from PCI vendor IDs or recognizable names and may remain unknown. Total and free VRAM are left unknown because WMI `AdapterRAM` is not authoritative for modern adapters.

## Multiple backends

Backends return separate backend-local records. MultiGpuHelper 1.1.0 does not deduplicate the same physical adapter discovered by multiple mechanisms. Applications combining results must choose precedence or deduplicate using hardware identifiers available outside this API.
