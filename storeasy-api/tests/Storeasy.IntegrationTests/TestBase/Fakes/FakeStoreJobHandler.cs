using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.IntegrationTests.TestBase.Fakes;

/// <summary>
/// A store job handler for a kind that does not exist in production (<see cref="TestKind"/>), so the queue and the
/// runner are tested without any store. The payload's <c>mode</c> scripts the attempt.
/// </summary>
public sealed class FakeStoreJobHandler : IStoreJobHandler
{
    /// <summary>The test only job kind this handler executes.</summary>
    public const StoreJobKind TestKind = (StoreJobKind)900;

    /// <summary>A job kind no handler executes.</summary>
    public const StoreJobKind UnknownKind = (StoreJobKind)901;

    /// <summary>Two steps, then success.</summary>
    public const string Succeed = "succeed";

    /// <summary>Transient failure on the first attempt, success afterwards.</summary>
    public const string TransientOnce = "transient-once";

    /// <summary>Transient failure on every attempt.</summary>
    public const string TransientAlways = "transient-always";

    /// <summary>Permanent failure.</summary>
    public const string Fail = "fail";

    /// <summary>Reports progress until the job is cancelled (at most 20 seconds).</summary>
    public const string Wait = "wait";

    /// <summary>Payload of the test jobs.</summary>
    /// <param name="Mode"></param>
    public sealed record Script(string Mode);

    /// <inheritdoc/>
    public StoreJobKind Kind => TestKind;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var mode = context.GetPayload<Script>()?.Mode;

        var step = await context.BeginStepAsync("test.call", StepDirection.Outbound, "calling", 10, cancellationToken);

        switch (mode)
        {
            case Fail:
                throw new StoreJobException("The store refused the request.", transient: false);
            case TransientAlways:
            case TransientOnce when context.Attempt == 1:
                throw new StoreJobException("The store answered 503.", transient: true);
            case Wait:
                for (var i = 0; i < 400; i++)
                {
                    await context.ReportProgressAsync(step, i, cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }

                break;
        }

        await context.ReportProgressAsync(step, 10, cancellationToken);
        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, "done", cancellationToken);

        var save = await context.BeginStepAsync("test.save", StepDirection.Internal, cancellationToken: cancellationToken);
        await context.EndStepAsync(save, StoreJobStepStatus.Succeeded, cancellationToken: cancellationToken);

        return new StoreJobOutcome($"Done on attempt {context.Attempt}.", new { context.Attempt });
    }
}
