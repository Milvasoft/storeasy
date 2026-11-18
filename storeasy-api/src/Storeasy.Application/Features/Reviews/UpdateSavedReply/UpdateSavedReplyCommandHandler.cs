using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.UpdateSavedReply;

/// <summary>
/// Handles <see cref="UpdateSavedReplyCommand"/>.
/// </summary>
/// <param name="SavedReplyRepository"></param>
/// <param name="AppRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record UpdateSavedReplyCommandHandler(IStoreasyRepositoryBase<SavedReply> SavedReplyRepository,
                                             IStoreasyRepositoryBase<App> AppRepository,
                                             IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<UpdateSavedReplyCommand, int>
{
    private readonly IStoreasyRepositoryBase<SavedReply> _savedReplyRepository = SavedReplyRepository;
    private readonly IStoreasyRepositoryBase<App> _appRepository = AppRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(UpdateSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var savedReply = await _savedReplyRepository.GetFirstOrDefaultAsync(s => s.Id == request.SavedReplyId && s.OrganizationId == request.OrganizationId,
                                                                            projection: s => new SavedReply { Id = s.Id, Name = s.Name, Variants = s.Variants, AppIds = s.AppIds },
                                                                            cancellationToken: cancellationToken);

        if (savedReply == null)
            return Response<int>.Error(0, MessageKey.SavedReplyNotFound);

        if (request.AppIds != null)
        {
            var appIds = request.AppIds.Distinct().ToList();
            var apps = await _appRepository.GetAllAsync(a => appIds.Contains(a.Id) && a.OrganizationId == request.OrganizationId,
                                                        projection: a => new App { Id = a.Id },
                                                        cancellationToken: cancellationToken);

            if (apps.Count != appIds.Count)
                return Response<int>.Error(0, MessageKey.AppNotFound);

            savedReply.AppIds = appIds;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
            savedReply.Name = request.Name.Trim();

        if (request.Variants != null)
            savedReply.Variants = SavedReplies.Normalize(request.Variants);

        await _savedReplyRepository.UpdateAsync(savedReply, cancellationToken, s => s.Name, s => s.Variants, s => s.AppIds);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.SavedReplyChanged, savedReply.Id.ToString(), $"Saved reply '{savedReply.Name}' updated.", cancellationToken: cancellationToken);

        return Response<int>.Success(savedReply.Id);
    }
}
