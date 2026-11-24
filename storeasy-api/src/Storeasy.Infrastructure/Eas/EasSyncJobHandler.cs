using Storeasy.Application.Dtos.EasDtos;
using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;

namespace Storeasy.Infrastructure.Eas;

/// <summary>
/// <see cref="StoreJobKind.EasSync"/>: reads the recent EAS builds of one linked app, or every linked app of the job's
/// organization, with <see cref="IEasService.SyncAsync"/>.
/// </summary>
/// <param name="easService"></param>
public sealed class EasSyncJobHandler(IEasService easService) : IStoreJobHandler
{
    private readonly IEasService _easService = easService;

    /// <inheritdoc/>
    public StoreJobKind Kind => StoreJobKind.EasSync;

    /// <inheritdoc/>
    public async Task<StoreJobOutcome> ExecuteAsync(IStoreJobContext context, CancellationToken cancellationToken)
    {
        var payload = context.GetPayload<EasSyncPayload>() ?? new EasSyncPayload();
        var step = await context.BeginStepAsync("eas.read", StepDirection.Inbound, payload.AppId == null ? "Every linked app" : $"App {payload.AppId}", cancellationToken: cancellationToken);
        var summary = await _easService.SyncAsync(context.OrganizationId, payload.AppId, cancellationToken);

        await context.EndStepAsync(step,
                                   summary.Failed.Count == 0 ? StoreJobStepStatus.Succeeded : StoreJobStepStatus.Failed,
                                   summary.Failed.Count == 0 ? $"{summary.Projects} projects, {summary.Builds} builds." : $"Not read: {string.Join(", ", summary.Failed)}.",
                                   cancellationToken);

        return new StoreJobOutcome($"EAS: {summary.Projects} projects, {summary.Builds} builds read.", summary);
    }
}
