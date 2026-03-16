# Changelog

## 0.1.0 (2026-03-15)

- Initial release
- Static `Poll.Until<T>()` entry point with predicate-based polling
- Side-effect overload `Poll.Until()` for fire-and-forget operations
- Fluent builder with `Every()`, `WithTimeout()`, `WithBackoff()`, `WithCancellation()`
- Backoff strategies: Constant, Linear, Exponential, ExponentialWithJitter
- `PollResult<T>` record with `Succeeded`, `Value`, `Attempts`, `Elapsed`, `IsTimedOut`
- `OnAttempt()` callback for observability
