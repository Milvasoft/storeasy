namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.ApplyChangePlan"/> job: the approved plan to apply.
/// </summary>
/// <param name="PlanId"></param>
public sealed record ApplyChangePlanPayload(long PlanId);
