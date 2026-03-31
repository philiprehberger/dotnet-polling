# Philiprehberger.Polling

[![CI](https://github.com/philiprehberger/dotnet-polling/actions/workflows/ci.yml/badge.svg)](https://github.com/philiprehberger/dotnet-polling/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Philiprehberger.Polling.svg)](https://www.nuget.org/packages/Philiprehberger.Polling)
[![Last updated](https://img.shields.io/github/last-commit/philiprehberger/dotnet-polling)](https://github.com/philiprehberger/dotnet-polling/commits/main)

Poll any async operation until a condition is met with configurable intervals, timeouts, and backoff strategies.

## Installation

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

### Max Attempts

Limit polling to a fixed number of attempts, independent of timeout:

```csharp
var result = await Poll
    .Until(
        async () => await client.GetJobStatusAsync(jobId),
        status => status == "done")
    .Every(TimeSpan.FromSeconds(1))
    .WithMaxAttempts(10)
    .ExecuteAsync();

if (!result.Succeeded)
    Console.WriteLine($"Gave up after {result.Attempts} attempts");
```

### Polling Context

Use a context-aware predicate to access attempt metadata:

```csharp
var result = await Poll
    .Until(
        async () => await GetValueAsync(),
        (value, context) =>
        {
            Console.WriteLine($"Attempt {context.AttemptNumber}, elapsed {context.Elapsed}");
            return value > 100;
        })
    .Every(TimeSpan.FromSeconds(1))
    .ExecuteAsync();
```

The `PollContext` provides `AttemptNumber` (1-based), `Elapsed` time, and `LastException`.

### Exception Filtering

Only retry on specific exception types; other exceptions propagate immediately:

```csharp
var result = await Poll
    .Until(async () => await db.PingAsync())
    .Every(TimeSpan.FromSeconds(1))
    .OnlyRetryOn<TimeoutException>()
    .WithTimeout(TimeSpan.FromSeconds(30))
    .ExecuteAsync();
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
| `Until<T>(Func<Task<T>>, Func<T, PollContext, bool>)` | Create a poll builder with a context-aware predicate |
| `Until(Func<Task>)` | Create a poll builder for a side-effect operation that succeeds when it stops throwing |

### `PollBuilder<T>` / `PollBuilder`

| Method | Description |
|--------|-------------|
| `Every(TimeSpan)` | Set the base interval between attempts (default 500 ms) |
| `WithTimeout(TimeSpan)` | Set the maximum total polling duration |
| `WithMaxAttempts(int)` | Set the maximum number of polling attempts |
| `Until(Func<T, PollContext, bool>)` | Set a context-aware predicate (`PollBuilder<T>` only) |
| `WithBackoff(BackoffStrategy)` | Set the backoff strategy (default Constant) |
| `OnlyRetryOn<TException>()` | Only retry on the specified exception type |
| `OnAttempt(Action<T, int>)` | Register a callback after each attempt |
| `WithCancellation(CancellationToken)` | Provide a cancellation token |
| `ExecuteAsync()` | Run the poll loop and return a `PollResult<T>` |

### `PollContext`

| Property | Type | Description |
|----------|------|-------------|
| `AttemptNumber` | `int` | Number of attempts made so far (1-based) |
| `Elapsed` | `TimeSpan` | Total time elapsed since polling started |
| `LastException` | `Exception?` | The last exception encountered, if any |

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

## Development

```bash
dotnet build src/Philiprehberger.Polling.csproj --configuration Release
```

## Support

If you find this project useful:

⭐ [Star the repo](https://github.com/philiprehberger/dotnet-polling)

🐛 [Report issues](https://github.com/philiprehberger/dotnet-polling/issues?q=is%3Aissue+is%3Aopen+label%3Abug)

💡 [Suggest features](https://github.com/philiprehberger/dotnet-polling/issues?q=is%3Aissue+is%3Aopen+label%3Aenhancement)

❤️ [Sponsor development](https://github.com/sponsors/philiprehberger)

🌐 [All Open Source Projects](https://philiprehberger.com/open-source-packages)

💻 [GitHub Profile](https://github.com/philiprehberger)

🔗 [LinkedIn Profile](https://www.linkedin.com/in/philiprehberger)

## License

[MIT](LICENSE)
