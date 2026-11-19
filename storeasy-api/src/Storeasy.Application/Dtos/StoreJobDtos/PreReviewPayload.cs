namespace Storeasy.Application.Dtos.StoreJobDtos;

/// <summary>
/// Payload of a <see cref="StoreJobKind.PreReview"/> job.
/// </summary>
/// <param name="PreReviewId">The pre-review to run.</param>
public sealed record PreReviewPayload(long PreReviewId);
