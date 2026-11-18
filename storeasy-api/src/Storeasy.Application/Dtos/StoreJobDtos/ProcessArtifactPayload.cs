namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.ProcessArtifact"/> job: the session to read, check and deliver.
/// </summary>
/// <param name="SessionId">Upload session.</param>
/// <param name="MaxAttempts">Attempts of the job (the last one marks the session failed instead of retrying).</param>
/// <param name="Force">Deliver even when the checks gave warnings (a member delivered a held session).</param>
public sealed record ProcessArtifactPayload(long SessionId, int MaxAttempts, bool Force = false);
