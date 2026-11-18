using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Feedback;

namespace Storeasy.Application.Features.Reviews.DeleteSavedReply;

/// <summary>
/// Handles <see cref="DeleteSavedReplyCommand"/>.
/// </summary>
/// <param name="SavedReplyRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteSavedReplyCommandHandler(IStoreasyRepositoryBase<SavedReply> SavedReplyRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteSavedReplyCommand, int>
{
    private readonly IStoreasyRepositoryBase<SavedReply> _savedReplyRepository = SavedReplyRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteSavedReplyCommand request, CancellationToken cancellationToken)
    {
        var savedReply = await _savedReplyRepository.GetFirstOrDefaultAsync(s => s.Id == request.SavedReplyId && s.OrganizationId == request.OrganizationId,
                                                                            projection: s => new { s.Id, s.Name },
                                                                            cancellationToken: cancellationToken);

        if (savedReply == null)
            return Response<int>.Error(0, MessageKey.SavedReplyNotFound);

        await _savedReplyRepository.ExecuteDeleteAsync(savedReply.Id, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.SavedReplyChanged, savedReply.Id.ToString(), $"Saved reply '{savedReply.Name}' removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(savedReply.Id);
    }
}
