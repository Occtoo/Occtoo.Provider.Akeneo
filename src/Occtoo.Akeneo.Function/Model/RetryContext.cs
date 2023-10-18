using System;

namespace Occtoo.Akeneo.DataSync.Model;
public record DataSyncRetryContext(int CurrentAttempt, int MaxNumberOfAttempts, TimeSpan AttemptDelay)
{
    public static DataSyncRetryContext Empty(int maxRetries, TimeSpan attemptDelay) => new(0, maxRetries, attemptDelay);

    public DataSyncRetryContext NextAttempt() => this with { CurrentAttempt = CurrentAttempt + 1 };
}
