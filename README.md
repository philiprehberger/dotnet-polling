# Philiprehberger.Polling

[![CI](https://github.com/philiprehberger/dotnet-polling/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-polling/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.Polling.svg)](https://www.nuget.org/packages/Philiprehberger.Polling)
[![License](https://img.shields.io/github/license/philiprehberger/dotnet-polling)](LICENSE)

Poll any async operation until a condition is met with configurable intervals, timeouts, and backoff strategies.

## Install

```bash
dotnet add package Philiprehberger.Polling
```

## Usage

```csharp
using Philiprehberger.Polling;

// Poll an API until the order status is "completed"
var result = await Poll
    .Until(
        async () => await client.GetOrderStatusAsync(orderId),
        status => status == "completed")
    .Every(TimeSpan.FromSeconds(2))
    .WithTimeout(TimeSpan.FromMinutes(5))
    .WithBackoff(BackoffStrategy.Exponential)
    .ExecuteAsync();

if (result.Succeeded)
    Console.WriteLine($"Order completed after {result.Attempts} attempts");
```

### Side-effect polling

Poll an operation that throws until it succeeds:

```csharp
var result = await Poll
    .Until(async () => await db.PingAsync())
    .Every(TimeSpan.FromSeconds(1))
    .WithTimeout(TimeSpan.FromSeconds(30))
    .ExecuteAsync();
```

### Backoff strategies

```csharp
// Constant (default) — same interval every attempt
.WithBackoff(BackoffStrategy.Constant)

// Linear — interval grows by base each attempt (500ms, 1s, 1.5s, ...)
.WithBackoff(BackoffStrategy.Linear)

// Exponential — interval doubles each attempt (500ms, 1s, 2s, 4s, ...)
.WithBackoff(BackoffStrategy.Exponential)

// Exponential with jitter — exponential + random jitter to avoid thundering herd
.WithBackoff(BackoffStrategy.ExponentialWithJitter)
```

### Observability

```csharp
var result = await Poll
    .Until(
        async () => await service.GetHealthAsync(),
        health => health.IsHealthy)
    .Every(TimeSpan.FromSeconds(1))
    .OnAttempt((value, attempt) =>
        Console.WriteLine($"Attempt {attempt}: healthy={value.IsHealthy}"))
    .ExecuteAsync();
```

### Cancellation

```csharp
using var cts = new CancellationTokenSource();

var result = await Poll
    .Until(
        async () => await GetValueAsync(),
        v => v > 100)
    .Every(TimeSpan.FromMilliseconds(200))
    .WithCancellation(cts.Token)
    .ExecuteAsync();
```

## API

### `Poll`

| Method | Description |
|--------|-------------|
| `Until<T>(Func<Task<T>>, Func<T, bool>)` | Create a poll builder that checks a predicate against returned values |
| `Until(Func<Task>)` | Create a poll builder for a side-effect operation that succeeds when it stops throwing |

### `PollBuilder<T>` / `PollBuilder`

| Method | Description |
|--------|-------------|
| `Every(TimeSpan)` | Set the base interval between attempts (default 500 ms) |
| `WithTimeout(TimeSpan)` | Set the maximum total polling duration |
| `WithBackoff(BackoffStrategy)` | Set the backoff strategy (default Constant) |
| `OnAttempt(Action<T, int>)` | Register a callback after each attempt |
| `WithCancellation(CancellationToken)` | Provide a cancellation token |
| `ExecuteAsync()` | Run the poll loop and return a `PollResult<T>` |

### `PollResult<T>`

| Property | Type | Description |
|----------|------|-------------|
| `Succeeded` | `bool` | Whether the predicate was satisfied |
| `Value` | `T?` | The last value returned by the operation |
| `Attempts` | `int` | Total number of attempts executed |
| `Elapsed` | `TimeSpan` | Total wall-clock time spent polling |
| `LastException` | `Exception?` | The last exception thrown, if any |
| `IsTimedOut` | `bool` | Whether polling ended due to timeout |

### `BackoffStrategy`

| Value | Description |
|-------|-------------|
| `Constant` | Same interval every attempt |
| `Linear` | Interval grows by base each attempt |
| `Exponential` | Interval doubles each attempt |
| `ExponentialWithJitter` | Exponential with random jitter |

## License

MIT
