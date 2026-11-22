using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Utils.Chrome;
using Storeasy.Domain.Chrome;
using Storeasy.Domain.Intake;

namespace Storeasy.Application.Features.Chrome.PlanChromeChange;

/// <summary>
/// Handles <see cref="PlanChromeChangeCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="SessionRepository"></param>
/// <param name="ItemRepository"></param>
/// <param name="Localizer"></param>
[Log]
public record PlanChromeChangeCommandHandler(IStoreContentService ContentService,
                                            IChangePlanService ChangePlanService,
                                            IStoreasyRepositoryBase<ArtifactSession> SessionRepository,
                                            IStoreasyRepositoryBase<ChromeItem> ItemRepository,
                                            IMilvaLocalizer Localizer) : IInterceptable, ICommandHandler<PlanChromeChangeCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<ArtifactSession> _sessionRepository = SessionRepository;
    private readonly IStoreasyRepositoryBase<ChromeItem> _itemRepository = ItemRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanChromeChangeCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null || app.Store != StoreKind.Chrome)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        var read = await _contentService.ReadAsync(app, ContentParts.All, null, cancellationToken);

        if (!read.IsSuccess)
        {
            return read.ErrorDetail == null
                ? Response<ChangePlanDto>.Error(null, read.ErrorKey)
                : Response<ChangePlanDto>.Error(null, new ResponseMessage(read.ErrorKey, read.ErrorDetail));
        }

        ChromePlanPackage package = null;

        if (request.Kind == ChromeChangeKind.Publish && request.SessionId is long sessionId)
        {
            var session = await _sessionRepository.GetFirstOrDefaultAsync(s => s.Id == sessionId && s.OrganizationId == request.OrganizationId && s.AppId == app.AppId,
                                                                           projection: s => new ArtifactSession
                                                                           {
                                                                               Id = s.Id,
                                                                               Platform = s.Platform,
                                                                               FileName = s.FileName,
                                                                               ObjectKey = s.ObjectKey,
                                                                               Size = s.Size,
                                                                               Sha256 = s.Sha256,
                                                                               Package = s.Package,
                                                                               PackageDeleted = s.PackageDeleted,
                                                                           },
                                                                           cancellationToken: cancellationToken);

            if (session == null || session.Platform != ArtifactPlatform.Extension || session.Package == null)
                return Error(new ChromeIssue(MessageKey.ChromePackageNotExtension, true, sessionId));

            if (session.PackageDeleted || string.IsNullOrEmpty(session.ObjectKey))
                return Error(new ChromeIssue(MessageKey.ChromePackageUnavailable, true, sessionId));

            package = new ChromePlanPackage(session.Id, session.FileName, session.ObjectKey, session.Size, session.Sha256, session.Package);
        }

        var weeklyUsers = await _itemRepository.GetFirstOrDefaultAsync(i => i.StoreAppId == app.StoreAppId && i.OrganizationId == request.OrganizationId,
                                                                       projection: i => i.WeeklyUsers,
                                                                       cancellationToken: cancellationToken);

        var result = ChromePlanner.Build(new ChromePlanInput(app.ExternalId,
                                                             ChromeContent.FromSnapshot(read.Snapshot),
                                                             request.Kind,
                                                             package,
                                                             request.Staged,
                                                             request.DeployPercentage,
                                                             request.SkipReview,
                                                             request.TargetVersion,
                                                             request.NewVersion,
                                                             request.Reason,
                                                             weeklyUsers));

        if (result.IsBlocked)
            return Error([.. result.Issues.Where(i => i.Blocking)]);

        var draft = result.Draft;

        draft.Warnings.AddRange(ChromeManifest.Localize(result.Issues, _localizer).Select(i => new PlanWarning { Key = i.Issue.Key, Message = i.Message }));

        return await _changePlanService.SaveAsync(app, read.Snapshot, draft, request.DryRun, request.Confirm, cancellationToken);
    }

    private Response<ChangePlanDto> Error(params ChromeIssue[] issues)
    {
        var localized = ChromeManifest.Localize(issues, _localizer);
        var response = Response<ChangePlanDto>.Error(null, new ResponseMessage(localized[0].Issue.Key, localized[0].Message));

        foreach (var (_, message) in localized.Skip(1))
            response.AddMessage(message, MessageType.Error);

        return response;
    }
}
