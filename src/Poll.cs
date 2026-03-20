namespace Philiprehberger.Polling;

/// <summary>
/// Static entry point for creating polling operations. Use <see cref="Until{T}(Func{Task{T}}, Func{T, bool})"/>
/// to poll an async operation until a predicate is satisfied, or <see cref="Until"/>
/// to poll a side-effect operation until it completes without throwing.
/// </summary>
public static class Poll
{
    /// <summary>
    /// Begins building a poll that repeatedly invokes <paramref name="operation"/>
    /// until <paramref name="predicate"/> returns <c>true</c> for the result.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the operation.</typeparam>
    /// <param name="operation">The async operation to poll.</param>
    /// <param name="predicate">
    /// A function that inspects each result and returns <c>true</c> when polling should stop.
    /// </param>
    /// <returns>A <see cref="PollBuilder{T}"/> for further configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="operation"/> or <paramref name="predicate"/> is null.
    /// </exception>
    public static PollBuilder<T> Until<T>(Func<Task<T>> operation, Func<T, bool> predicate)
    {
        return new PollBuilder<T>(operation, predicate);
    }

    /// <summary>
    /// Begins building a poll that repeatedly invokes <paramref name="operation"/>
    /// until <paramref name="predicate"/> returns <c>true</c> for the result and
    /// polling context.
    /// </summary>
    /// <typeparam name="T">The type of value returned by the operation.</typeparam>
    /// <param name="operation">The async operation to poll.</param>
    /// <param name="predicate">
    /// A function that inspects each result and the <see cref="PollContext"/>, returning
    /// <c>true</c> when polling should stop.
    /// </param>
    /// <returns>A <see cref="PollBuilder{T}"/> for further configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="operation"/> or <paramref name="predicate"/> is null.
    /// </exception>
    public static PollBuilder<T> Until<T>(Func<Task<T>> operation, Func<T, PollContext, bool> predicate)
    {
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        return new PollBuilder<T>(operation, _ => false).Until(predicate);
    }

    /// <summary>
    /// Begins building a poll that repeatedly invokes <paramref name="operation"/>
    /// until it completes without throwing an exception.
    /// </summary>
    /// <param name="operation">The async side-effect operation to poll.</param>
    /// <returns>A <see cref="PollBuilder"/> for further configuration.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="operation"/> is null.
    /// </exception>
    public static PollBuilder Until(Func<Task> operation)
    {
        return new PollBuilder(operation);
    }
}
