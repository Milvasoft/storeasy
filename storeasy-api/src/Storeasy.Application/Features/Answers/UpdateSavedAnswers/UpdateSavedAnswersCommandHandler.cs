using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Apps;

namespace Storeasy.Application.Features.Answers.UpdateSavedAnswers;

/// <summary>
/// Handles <see cref="UpdateSavedAnswersCommand"/>.
/// </summary>
/// <param name="AppRepository"></param>
/// <param name="AnswerRepository"></param>
/// <param name="SecretProtector"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateSavedAnswersCommandHandler(IStoreasyRepositoryBase<App> AppRepository,
                                               IStoreasyRepositoryBase<SavedAnswer> AnswerRepository,
                                               ISecretProtector SecretProtector,
                                               IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateSavedAnswersCommand, int>
{
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IStoreasyRepositoryBase<SavedAnswer> _answerRepository = AnswerRepository;
    private readonly ISecretProtector _secretProtector = SecretProtector;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateSavedAnswersCommand request, CancellationToken cancellationToken)
    {
        var appId = await _appRepository.GetFirstOrDefaultAsync(a => a.Id == request.AppId && a.OrganizationId == request.OrganizationId, projection: a => (int?)a.Id, cancellationToken: cancellationToken);

        if (appId == null)
            return Response<int>.Error(0, MessageKey.AppNotFound);

        var answers = SavedAnswerDocuments.Normalize(request.Key, request.Answers.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "{}" : request.Answers.GetRawText(), out var error);

        if (answers == null)
            return Response<int>.Error(0, new ResponseMessage(MessageKey.AnswersInvalid, error));

        var id = await _answerRepository.SaveAnswerAsync(_secretProtector, request.OrganizationId, request.AppId, request.Key, answers, request.DemoPassword, request.RemoveDemoAccountLogin, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.AnswersUpdated,
                                     request.AppId.ToString(),
                                     $"{request.Key} answers of app {request.AppId} saved{(string.IsNullOrEmpty(request.DemoPassword) ? string.Empty : " (demo password sealed)")}.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(id);
    }
}
