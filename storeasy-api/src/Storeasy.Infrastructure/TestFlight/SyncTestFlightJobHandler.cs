using Storeasy.Application.Dtos.TestFlightDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.TestFlight;

/// <summary>
/// The <see cref="StoreJobKind.SyncTestFlight"/> job: reads and saves the TestFlight state of one App Store record.
/// </summary>
/// <param name="storeAppRepository"></param>
/// <param name="gateway"></param>
/// <param name="testFlightStore"></param>
public sealed class SyncTestFlightJobHandler(IStoreasyRepositoryBase<StoreApp> storeAppRepository, ITestFlightGateway gateway, TestFlightStore testFlightStore) : IStoreJobHandler
{
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly ITestFlightGateway _gateway = gateway;
    private readonly TestFlightStore _testFlightStore = testFlightStore;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.SyncTestFlight;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<SyncTestFlightPayload>() ?? throw new StoreJobException("The job has no store record.", transient: false);
        var (storeApp, connection) = await TestFlightJobs.OpenAsync(_storeAppRepository, context, payload.StoreAppId, cancellationToken);

        var step = await context.BeginStepAsync("testflight.read", StepDirection.Outbound, storeApp.ExternalId, cancellationToken: cancellationToken);
        var snapshot = await _gateway.ReadAsync(connection.SecretJson, storeApp.ExternalId, cancellationToken);

        await context.EndStepAsync(step, StoreJobStepStatus.Succeeded, cancellationToken: cancellationToken);

        var saveStep = await context.BeginStepAsync("testflight.save", StepDirection.Internal, cancellationToken: cancellationToken);
        var summary = await _testFlightStore.SaveAsync(context.OrganizationId, storeApp.Id, snapshot, cancellationToken);

        await context.EndStepAsync(saveStep, StoreJobStepStatus.Succeeded, summary, cancellationToken);

        return new StoreJobOutcome(summary);
    }
}
