# Error handling

- Backend convenience methods return an empty list when their dependency is unavailable or probing fails. `IsAvailableAsync` can be checked separately.
- Unknown memory uses `GpuMemoryInfo.State`; zero bytes must not be interpreted as a measurement unless the state is `Available`.
- `GpuManager.SelectDevice` throws `GpuSelectionException` when no eligible device can be selected.
- `GpuDispatcher` throws `GpuBudgetExceededException` when a soft reservation cannot be made.
- Caller cancellation and configured timeout surface as `OperationCanceledException`.
- Callback exceptions propagate unchanged.

```csharp
try
{
    await dispatcher.RunAsync((id, token) => DoWorkAsync(id, token), GpuPolicy.FirstAvailable);
}
catch (OperationCanceledException) { }
catch (GpuSelectionException ex) { Console.Error.WriteLine(ex.Message); }
catch (GpuBudgetExceededException ex) { Console.Error.WriteLine(ex.Message); }
```
