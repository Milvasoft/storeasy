using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Domain.Studio;

namespace Storeasy.Application.Features.Mockups.DeleteMockupTemplate;

/// <summary>
/// Handles <see cref="DeleteMockupTemplateCommand"/>. A template of another organization is reported as not found.
/// </summary>
/// <param name="TemplateRepository"></param>
/// <param name="AuditTrail"></param>
[Log]
public record DeleteMockupTemplateCommandHandler(IStoreasyRepositoryBase<MockupTemplate> TemplateRepository, IAuditTrail AuditTrail) : IInterceptable, ICommandHandler<DeleteMockupTemplateCommand, int>
{
    private readonly IStoreasyRepositoryBase<MockupTemplate> _templateRepository = TemplateRepository;
    private readonly IAuditTrail _auditTrail = AuditTrail;

    /// <inheritdoc/>
    public async Task<Response<int>> Handle(DeleteMockupTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _templateRepository.GetFirstOrDefaultAsync(t => t.Id == request.TemplateId && t.OrganizationId == request.OrganizationId,
                                                                        projection: t => new MockupTemplate { Id = t.Id, Name = t.Name, AppId = t.AppId },
                                                                        cancellationToken: cancellationToken);

        if (template == null)
            return Response<int>.Error(0, MessageKey.MockupTemplateNotFound);

        await _templateRepository.ExecuteDeleteAsync(template.Id, cancellationToken: cancellationToken);

        await _auditTrail.WriteAsync(request.OrganizationId,
                                     AuditAction.MockupTemplateDeleted,
                                     template.Id.ToString(),
                                     $"Mockup template '{template.Name}' of app {template.AppId} deleted.",
                                     cancellationToken: cancellationToken);

        return Response<int>.Success(template.Id);
    }
}
