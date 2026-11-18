using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.CreateSavedReply;

/// <summary>
/// Handles <see cref="CreateSavedReplyCommand"/>.
/// </summary>
/// <param name="SavedReplyRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record CreateSavedReplyCommandHandler(IStoreasyRepositoryBase<SavedReply> SavedReplyRepository,
                                             IStoreasyRepositoryBase<App> AppRepository,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<CreateSavedReplyCommand, int>
{
    private readonly IStoreasyRepositoryBase<SavedReply> _savedReplyRepository = SavedReplyRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(CreateSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var appIds = (request.AppIds ?? []).Distinct().ToList();

        if (appIds.Count > 0)
        {
            var apps = await _appRepository.GetAllAsync(a => appIds.Contains(a.Id) && a.OrganizationId == request.OrganizationId,
                                                        projection: a => new App { Id = a.Id },
                                                        cancellationToken: cancellationToken);

            if (apps.Count != appIds.Count)
                return Response<int>.Error(0, MessageKey.AppNotFound);
        }

        var savedReply = new SavedReply
        {
            OrganizationId = request.OrganizationId,
            Name = request.Name.Trim(),
            Variants = SavedReplies.Normalize(request.Variants),
            AppIds = appIds,
        };

        await _savedReplyRepository.AddAsync(savedReply, cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.SavedReplyChanged, savedReply.Id.ToString(), $"Saved reply '{savedReply.Name}' added.", cancellationToken: cancellationToken);

        return Response<int>.Success(savedReply.Id);
    }
}
