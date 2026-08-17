# Troubleshooting

## No GPU detected

Call `IsAvailableAsync`, verify the backend dependency, and check that the current process has permission to execute it. An empty list can also mean no matching devices.

## `nvidia-smi` not found

Install an NVIDIA driver providing `nvidia-smi` and ensure the executable is on `PATH` for the running process.

## ROCm not available

Verify that `rocminfo` runs in the same environment and account. WSL and host Windows installations have different paths and device exposure.

## WMI limitations

WMI discovery is Windows-only and intentionally reports memory as unknown. Use a vendor runtime backend when accurate memory data is required.

## GPU detected but memory unknown

Inspect `MemoryInfo.State`. CUDA, ROCm, and WMI do not provide free memory through the currently used query paths.

## Specific GPU cannot be selected

Confirm the ID is present and the device availability state is `Available`. IDs are backend-local and may change between discovery mechanisms.

## Timeout versus cancellation

Caller cancellation and timeout both produce `OperationCanceledException`. Check the caller token to distinguish caller cancellation. Execution timeout is cooperative: the callback must observe its supplied token.
