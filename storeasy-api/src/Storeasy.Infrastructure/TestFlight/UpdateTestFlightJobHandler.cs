using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.TestFlight;

/// <summary>
/// The <see cref="StoreJobKind.UpdateTestFlight"/> job: writes one TestFlight change and reads the state back. A beta
/// review submission is a real submission to Apple; the tests use a fake gateway.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="testFlightStore"></param>
public sealed class UpdateTestFlightJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository, ITestFlightGateway gateway, TestFlightStore testFlightStore) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly ITestFlightGateway _gateway = gateway;
    private readonly TestFlightStore _testFlightStore = testFlightStore;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.UpdateTestFlight;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<UpdateTestFlightPayload>() ?? throw new StoreJobException("The job has no change.", transient: false);
        var (storeApp, connection) = await TestFlightJobs.OpenAsync(_storeAppRepository, context, payload.StoreAppId, cancellationToken);

        var step = await context.BeginStepAsync($"testflight.{payload.Change.Operation.ToString().ToLowerInvariant()}", StepDirection.Outbound, cancellationToken: cancellationToken);
        var createdId = await _gateway.ApplyAsync(connection.SecretJson, storeApp.ExternalId, payload.Change, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, createdId, cancellationToken);

        if (payload.Change.Operation == TestFlightOperation.SubmitBetaReview && payload.BuildId.HasValue)
            await _testFlightStore.MarkSubmittedAsync(context.OrganizationId, storeApp.Id, payload.BuildId.Value, cancellationToken);

        var readStep = await context.BeginStepAsync("testflight.read", StepDirection.Outbound, cancellationToken: cancellationToken);
        var snapshot = await _gateway.ReadAsync(connection.SecretJson, storeApp.ExternalId, cancellationToken);
        var summary = await _testFlightStore.SaveAsync(context.OrganizationId, storeApp.Id, snapshot, cancellationToken);

        if (payload.Change.Operation == TestFlightOperation.SubmitBetaReview && payload.BuildId.HasValue)
            await _testFlightStore.MarkSubmittedAsync(context.OrganizationId, storeApp.Id, payload.BuildId.Value, cancellationToken);

        await context.EndStepAsync(readStep, StoreJobStepStatus.Succeeded, summary, cancellationToken);

        return new StoreJobOutcome($"{payload.Change.Operation} done; {summary}", new { Operation = payload.Change.Operation.ToString(), CreatedId = createdId });
    }
}
