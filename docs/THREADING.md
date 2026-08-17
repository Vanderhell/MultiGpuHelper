# Threading semantics

Concurrent `GpuManager` registration, lookup, and selection calls are synchronized. Round-robin counters are updated inside the corresponding selection lock. Concurrent `GpuDispatcher.RunAsync` calls share a semaphore per device and honor the `MaxConcurrentJobs` value captured when that semaphore is first created.

`GpuManager.Devices` returns a new list, but the contained `GpuDevice` instances are mutable references. Changing `IsEnabled`, `MaxConcurrentJobs`, `FreeVramBytes`, `VramBudget`, or `VramBudget.LimitBytes` concurrently with selection or dispatch is not supported.

`VramBudget.TryReserve`, `Release`, `CanReserve`, and `ReservedBytes` synchronize reservation state. Configure `LimitBytes` before concurrent use and do not mutate it while reservations are active.

Queued jobs do not reserve VRAM. A reservation is attempted after a concurrency slot is acquired and released after callback completion, exception, or cancellation.
