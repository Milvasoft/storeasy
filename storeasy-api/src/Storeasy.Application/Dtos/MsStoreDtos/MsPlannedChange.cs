namespace Storeasy.Application.Dtos.MsStoreDtos;

/// <summary>
/// The Microsoft part of a change plan: the change and the fingerprint of the store content it was planned on. The apply
/// job plans again and refuses to write when the fingerprint changed.
/// </summary>
public sealed class MsPlannedChange
{
    /// <summary>
    /// The change.
    /// </summary>
    public MsChangeInputDto Change { get; set; }

    /// <summary>
    /// Fingerprint of the planned content (<see cref="MsChangePlanDto.BaseHash"/>).
    /// </summary>
    public string BaseHash { get; set; }
}
