using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.CopySavedAnswers;

/// <summary>
/// Handles <see cref="CopySavedAnswersCommand"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AnswerRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CopySavedAnswersCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                             IStoreasyRepositoryBase<SavedAnswer> AnswerRepository,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CopySavedAnswersCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = AnswerRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CopySavedAnswersCommand request, CancellationToken cancellationToken)
    {
        var appIds = new[] { request.SourceAppId, request.TargetAppId };
        var found = await _appRepository.GetAllAsync(a => appIds.Contains(a.Id) && a.OrganizationId == request.OrganizationId,
                                                     projection: a => new App { Id = a.Id },
                                                     cancellationToken: cancellationToken);

        if (found.Count != appIds.Distinct().Count())
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var keys = request.Keys?.Count > 0 ? request.Keys : [.. Enum.GetValues<SavedAnswerKind>()];

        var source = await _answerRepository.GetAllAsync(a => a.AppId == request.SourceAppId && a.OrganizationId == request.OrganizationId && keys.Contains(a.Kind),
                                                         projection: a => new SavedAnswer { Kind = a.Kind, Answers = a.Answers },
                                                         cancellationToken: cancellationToken);

        var target = await _answerRepository.GetAllAsync(a => a.AppId == request.TargetAppId && a.OrganizationId == request.OrganizationId,
                                                         projection: a => new SavedAnswer { Id = a.Id, Kind = a.Kind },
                                                         cancellationToken: cancellationToken);

        var updated = target.Where(t => source.Any(s => s.Kind == t.Kind)).ToList();

        foreach (var row in updated)
            row.Answers = source.First(s => s.Kind == row.Kind).Answers;

        if (updated.Count > 0)
            await _answerRepository.UpdateAsync(updated, cancellationToken, a => a.Answers);

        var added = source.Where(s => target.All(t => t.Kind != s.Kind))
                          .Select(s => new SavedAnswer { OrganizationId = request.OrganizationId, AppId = request.TargetAppId, Kind = s.Kind, Answers = s.Answers })
                          .ToList();

        if (added.Count > 0)
            await _answerRepository.AddRangeAsync(added, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AnswersCopied,
                                     request.TargetAppId.ToString(),
                                     $"{source.Count} answer documents copied from app {request.SourceAppId} to app {request.TargetAppId}.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(source.Count);
    }
}
