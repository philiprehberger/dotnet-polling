namespace Philiprehberger.Polling;

/// <summary>
/// Represents the outcome of a polling operation, including whether it succeeded,
/// the final value, number of attempts, elapsed time, and any last exception.
/// </summary>
/// <typeparam name="T">The type of the polled value.</typeparam>
/// <param name="Succeeded">Whether the predicate was satisfied before timeout.</param>
/// <param name="Value">The last value returned by the operation, or default if none.</param>
/// <param name="Attempts">The total number of poll attempts executed.</param>
/// <param name="Elapsed">The total wall-clock time spent polling.</param>
/// <param name="LastException">The last exception thrown by the operation, if any.</param>
public record PollResult<T>(
    bool Succeeded,
    T? Value,
    int Attempts,
    TimeSpan Elapsed,
    Exception? LastException)
{
    /// <summary>
    /// Gets a value indicating whether the poll ended because the timeout was reached
    /// without the predicate being satisfied.
    /// </summary>
    public bool IsTimedOut => !Succeeded && LastException is null;
}
