namespace Philiprehberger.Polling;

/// <summary>
/// Provides context about the current polling state to predicates and callbacks.
/// </summary>
public class PollContext
{
    /// <summary>
    /// Number of attempts made so far (1-based).
    /// </summary>
    public int AttemptNumber { get; init; }

    /// <summary>
    /// Total time elapsed since polling started.
    /// </summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>
    /// The last exception encountered, if any.
    /// </summary>
    public Exception? LastException { get; init; }
}
