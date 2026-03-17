namespace Philiprehberger.Polling;

/// <summary>
/// Backoff strategies that control how the interval between poll attempts grows.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// The interval stays the same between every attempt.
    /// </summary>
    Constant,

    /// <summary>
    /// The interval increases by the base interval on every attempt (base * attempt).
    /// </summary>
    Linear,

    /// <summary>
    /// The interval doubles on every attempt (base * 2^attempt).
    /// </summary>
    Exponential,

    /// <summary>
    /// Exponential growth with random jitter added to avoid thundering-herd problems.
    /// </summary>
    ExponentialWithJitter
}

/// <summary>
/// Fluent builder that configures and executes a polling loop for an async operation
/// that returns a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the value returned by the polled operation.</typeparam>
public sealed class PollBuilder<T>
{
    private readonly Func<Task<T>> _operation;
    private readonly Func<T, bool> _predicate;
    private Func<T, PollContext, bool>? _contextPredicate;
    private TimeSpan _interval = TimeSpan.FromMilliseconds(500);
    private TimeSpan? _timeout;
    private int? _maxAttempts;
    private BackoffStrategy _backoff = BackoffStrategy.Constant;
    private Action<T, int>? _onAttempt;
    private CancellationToken _cancellationToken = CancellationToken.None;
    private Type? _retryOnExceptionType;

    internal PollBuilder(Func<Task<T>> operation, Func<T, bool> predicate)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    /// <summary>
    /// Sets the base interval between poll attempts. Defaults to 500 ms.
    /// </summary>
    /// <param name="interval">The delay between attempts.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> Every(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");

        _interval = interval;
        return this;
    }

    /// <summary>
    /// Sets the maximum total time the poll loop may run before giving up.
    /// When omitted the loop runs until the predicate is satisfied or cancellation is requested.
    /// </summary>
    /// <param name="timeout">The maximum duration.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of polling attempts. Polling stops after this many tries
    /// even if the timeout hasn't been reached.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> WithMaxAttempts(int maxAttempts)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be positive.");

        _maxAttempts = maxAttempts;
        return this;
    }

    /// <summary>
    /// Poll until the predicate (with context) returns true. This overload provides
    /// a <see cref="PollContext"/> containing attempt number, elapsed time, and the
    /// last exception to the predicate function.
    /// </summary>
    /// <param name="predicate">
    /// A function that inspects the result and polling context, returning <c>true</c> when polling should stop.
    /// </param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> Until(Func<T, PollContext, bool> predicate)
    {
        _contextPredicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        return this;
    }

    /// <summary>
    /// Only retry when the specified exception type is thrown. Other exceptions propagate immediately.
    /// </summary>
    /// <typeparam name="TException">The exception type to retry on.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> OnlyRetryOn<TException>() where TException : Exception
    {
        _retryOnExceptionType = typeof(TException);
        return this;
    }

    /// <summary>
    /// Sets the backoff strategy used to adjust the interval between attempts.
    /// Defaults to <see cref="BackoffStrategy.Constant"/>.
    /// </summary>
    /// <param name="strategy">The backoff strategy to use.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> WithBackoff(BackoffStrategy strategy)
    {
        _backoff = strategy;
        return this;
    }

    /// <summary>
    /// Registers a callback invoked after each poll attempt with the returned value
    /// and the one-based attempt number.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> OnAttempt(Action<T, int> callback)
    {
        _onAttempt = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    /// <summary>
    /// Provides a <see cref="CancellationToken"/> that can cancel the poll loop.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder<T> WithCancellation(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>
    /// Executes the polling loop asynchronously, returning a <see cref="PollResult{T}"/>
    /// that describes the outcome.
    /// </summary>
    /// <returns>A task that resolves to the poll result.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered.</exception>
    public async Task<PollResult<T>> ExecuteAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        Exception? lastException = null;
        T? lastValue = default;

        using var timeoutCts = _timeout.HasValue
            ? new CancellationTokenSource(_timeout.Value)
            : new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, _cancellationToken);

        var token = linkedCts.Token;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            attempt++;
            try
            {
                lastValue = await _operation().ConfigureAwait(false);
                lastException = null;

                _onAttempt?.Invoke(lastValue, attempt);

                var context = new PollContext
                {
                    AttemptNumber = attempt,
                    Elapsed = sw.Elapsed,
                    LastException = lastException
                };

                var satisfied = _contextPredicate is not null
                    ? _contextPredicate(lastValue, context)
                    : _predicate(lastValue);

                if (satisfied)
                {
                    sw.Stop();
                    return new PollResult<T>(true, lastValue, attempt, sw.Elapsed, null);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (_retryOnExceptionType is null || _retryOnExceptionType.IsInstanceOfType(ex))
            {
                lastException = ex;
            }

            if (_maxAttempts.HasValue && attempt >= _maxAttempts.Value)
            {
                sw.Stop();
                return new PollResult<T>(false, lastValue, attempt, sw.Elapsed, lastException);
            }

            var delay = ComputeDelay(attempt);

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                if (_cancellationToken.IsCancellationRequested)
                    throw;

                // Timeout expired
                return new PollResult<T>(false, lastValue, attempt, sw.Elapsed, lastException);
            }
        }
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var baseTicks = _interval.Ticks;

        return _backoff switch
        {
            BackoffStrategy.Constant => _interval,
            BackoffStrategy.Linear => TimeSpan.FromTicks(baseTicks * attempt),
            BackoffStrategy.Exponential => TimeSpan.FromTicks(baseTicks * (long)Math.Pow(2, attempt - 1)),
            BackoffStrategy.ExponentialWithJitter => TimeSpan.FromTicks(
                baseTicks * (long)Math.Pow(2, attempt - 1)
                + Random.Shared.NextInt64(baseTicks)),
            _ => _interval
        };
    }
}

/// <summary>
/// Fluent builder that configures and executes a polling loop for a side-effect async
/// operation that does not return a value.
/// </summary>
public sealed class PollBuilder
{
    private readonly Func<Task> _operation;
    private TimeSpan _interval = TimeSpan.FromMilliseconds(500);
    private TimeSpan? _timeout;
    private int? _maxAttempts;
    private BackoffStrategy _backoff = BackoffStrategy.Constant;
    private Action<int>? _onAttempt;
    private CancellationToken _cancellationToken = CancellationToken.None;
    private Type? _retryOnExceptionType;

    internal PollBuilder(Func<Task> operation)
    {
        _operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }

    /// <summary>
    /// Sets the base interval between poll attempts. Defaults to 500 ms.
    /// </summary>
    /// <param name="interval">The delay between attempts.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder Every(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");

        _interval = interval;
        return this;
    }

    /// <summary>
    /// Sets the maximum total time the poll loop may run before giving up.
    /// </summary>
    /// <param name="timeout">The maximum duration.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder WithTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");

        _timeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of attempts before giving up.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of attempts.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder WithMaxAttempts(int maxAttempts)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be positive.");

        _maxAttempts = maxAttempts;
        return this;
    }

    /// <summary>
    /// Sets the backoff strategy used to adjust the interval between attempts.
    /// Defaults to <see cref="BackoffStrategy.Constant"/>.
    /// </summary>
    /// <param name="strategy">The backoff strategy to use.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder WithBackoff(BackoffStrategy strategy)
    {
        _backoff = strategy;
        return this;
    }

    /// <summary>
    /// Registers a callback invoked after each successful poll attempt with the
    /// one-based attempt number.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder OnAttempt(Action<int> callback)
    {
        _onAttempt = callback ?? throw new ArgumentNullException(nameof(callback));
        return this;
    }

    /// <summary>
    /// Provides a <see cref="CancellationToken"/> that can cancel the poll loop.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder WithCancellation(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>
    /// Only retry when the specified exception type is thrown. Other exceptions propagate immediately.
    /// </summary>
    /// <typeparam name="TException">The exception type to retry on.</typeparam>
    /// <returns>This builder for chaining.</returns>
    public PollBuilder OnlyRetryOn<TException>() where TException : Exception
    {
        _retryOnExceptionType = typeof(TException);
        return this;
    }

    /// <summary>
    /// Executes the polling loop asynchronously. The operation succeeds when it
    /// completes without throwing an exception.
    /// </summary>
    /// <returns>A task that resolves to the poll result.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is triggered.</exception>
    public async Task<PollResult<bool>> ExecuteAsync()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var attempt = 0;
        Exception? lastException = null;

        using var timeoutCts = _timeout.HasValue
            ? new CancellationTokenSource(_timeout.Value)
            : new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, _cancellationToken);

        var token = linkedCts.Token;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            attempt++;
            try
            {
                await _operation().ConfigureAwait(false);
                lastException = null;

                _onAttempt?.Invoke(attempt);

                sw.Stop();
                return new PollResult<bool>(true, true, attempt, sw.Elapsed, null);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (_retryOnExceptionType is null || _retryOnExceptionType.IsInstanceOfType(ex))
            {
                lastException = ex;
            }

            if (_maxAttempts.HasValue && attempt >= _maxAttempts.Value)
            {
                sw.Stop();
                return new PollResult<bool>(false, false, attempt, sw.Elapsed, lastException);
            }

            var delay = ComputeDelay(attempt);

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();

                if (_cancellationToken.IsCancellationRequested)
                    throw;

                return new PollResult<bool>(false, false, attempt, sw.Elapsed, lastException);
            }
        }
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var baseTicks = _interval.Ticks;

        return _backoff switch
        {
            BackoffStrategy.Constant => _interval,
            BackoffStrategy.Linear => TimeSpan.FromTicks(baseTicks * attempt),
            BackoffStrategy.Exponential => TimeSpan.FromTicks(baseTicks * (long)Math.Pow(2, attempt - 1)),
            BackoffStrategy.ExponentialWithJitter => TimeSpan.FromTicks(
                baseTicks * (long)Math.Pow(2, attempt - 1)
                + Random.Shared.NextInt64(baseTicks)),
            _ => _interval
        };
    }
}
