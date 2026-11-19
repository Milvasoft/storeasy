using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Organizations;

namespace Storeasy.Application.Features.AiSettings.RemoveAiKey;

/// <summary>
/// Handles <see cref="RemoveAiKeyCommand"/>: deletes the row with the sealed key (no soft delete keeps it around).
/// </summary>
/// <param name="SettingRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record RemoveAiKeyCommandHandler(IStoreasyRepositoryBase<OrganizationAiSetting> SettingRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<RemoveAiKeyCommand, int>
{
    private readonly IStoreasyRepositoryBase<OrganizationAiSetting> _settingRepository = SettingRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(RemoveAiKeyCommand request, CancellationToken cancellationToken)
    {
        var removed = await _settingRepository.ExecuteDeleteAsync(s => s.OrganizationId == request.OrganizationId, cancellationToken: cancellationToken);

        if (removed > 0)
            await _auditTrail.WriteAsync(request.OrganizationId, AuditAction.AiKeyRemoved, request.OrganizationId.ToString(), "Anthropic API key removed.", cancellationToken: cancellationToken);

        return Response<int>.Success(request.OrganizationId);
    }
}
