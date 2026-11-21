using Milvasoft.Components.CQRS.Command;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Interception.Interceptors.Logging;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Application.Dtos.WingetDtos;
using Storeasy.Application.Utils.Winget;
using Storeasy.Domain.MsStore;
using Storeasy.Domain.Winget;

namespace Storeasy.Application.Features.Winget.PlanWingetChange;

/// <summary>
/// Handles <see cref="PlanWingetChangeCommand"/>.
/// </summary>
/// <param name="ContentService"></param>
/// <param name="ChangePlanService"></param>
/// <param name="PullRequestRepository"></param>
/// <param name="InstallerRepository"></param>
/// <param name="Localizer"></param>
/// <param name="TimeProvider"></param>
[Log]
public record PlanWingetChangeCommandHandler(IStoreContentService ContentService,
                                            IChangePlanService ChangePlanService,
                                            IStoreasyRepositoryBase<WingetPullRequest> PullRequestRepository,
                                            IStoreasyRepositoryBase<MsInstallerPackage> InstallerRepository,
                                            IMilvaLocalizer Localizer,
                                            TimeProvider TimeProvider) : IInterceptable, ICommandHandler<PlanWingetChangeCommand, ChangePlanDto>
{
    private readonly IStoreContentService _contentService = ContentService;
    private readonly IChangePlanService _changePlanService = ChangePlanService;
    private readonly IStoreasyRepositoryBase<WingetPullRequest> _pullRequestRepository = PullRequestRepository;
    private readonly IStoreasyRepositoryBase<MsInstallerPackage> _installerRepository = InstallerRepository;
    private readonly IMilvaLocalizer _localizer = Localizer;
    private readonly TimeProvider _timeProvider = TimeProvider;

    /// <inheritdoc/>
    public async Task<Response<ChangePlanDto>> Handle(PlanWingetChangeCommand request, CancellationToken cancellationToken)
    {
        var app = await _contentService.GetStoreAppAsync(request.OrganizationId, request.StoreAppId, cancellationToken);

        if (app == null || app.Store != StoreKind.Winget)
            return Response<ChangePlanDto>.Error(null, MessageKey.StoreAppNotFound);

        // A new version is built on the latest version; a text edit on the version it edits.
        var baseVersion = request.Kind == WingetChangeKind.Listing && !string.IsNullOrWhiteSpace(request.Version) ? request.Version.Trim() : null;
        var read = await _contentService.ReadAsync(app, ContentParts.All, baseVersion, cancellationToken);

        if (!read.IsSuccess)
        {
            return read.ErrorDetail == null
                ? Response<ChangePlanDto>.Error(null, read.ErrorKey)
                : Response<ChangePlanDto>.Error(null, new ResponseMessage(read.ErrorKey, read.ErrorDetail));
        }

        var installers = new List<WingetInstallerInputDto>(request.Installers ?? []);

        if (request.FromMicrosoftStore && request.Kind == WingetChangeKind.NewVersion)
        {
            var sources = await _installerRepository.GetAllAsync(i => i.OrganizationId == request.OrganizationId && i.StoreApp.AppId == app.AppId && i.StoreApp.Store == StoreKind.Microsoft,
                                                                 projection: i => new MsInstallerPackage
                                                                 {
                                                                     PackageUrl = i.PackageUrl,
                                                                     PackageType = i.PackageType,
                                                                     Architectures = i.Architectures,
                                                                     Languages = i.Languages,
                                                                     InstallerParameters = i.InstallerParameters,
                                                                 },
                                                                 cancellationToken: cancellationToken);

            if (sources.Count == 0)
                return Response<ChangePlanDto>.Error(null, MessageKey.WingetNoMicrosoftInstallers);

            installers.AddRange(WingetInstallerSources.ToInputs(sources.OrderBy(s => s.PackageUrl, StringComparer.Ordinal).Select(WingetInstallerSources.From)));
        }

        var open = await _pullRequestRepository.GetAllAsync(p => p.StoreAppId == app.StoreAppId && p.OrganizationId == request.OrganizationId && p.IsOpen,
                                                            projection: p => new WingetPullRequest { Number = p.Number, PackageVersion = p.PackageVersion },
                                                            cancellationToken: cancellationToken);

        var openByVersion = open.Where(p => p.PackageVersion != null)
                                .GroupBy(p => p.PackageVersion, StringComparer.OrdinalIgnoreCase)
                                .ToDictionary(g => g.Key, g => g.Max(p => p.Number), StringComparer.OrdinalIgnoreCase);

        var result = WingetPlanner.Build(new WingetPlanInput(app.ExternalId,
                                                             read.Snapshot,
                                                             request.Kind,
                                                             request.Version,
                                                             installers,
                                                             request.Locales ?? [],
                                                             openByVersion,
                                                             _timeProvider.GetUtcNow().UtcDateTime));

        if (result.IsBlocked)
        {
            var blocking = WingetContent.Localize(result.Issues.Where(i => i.Blocking), _localizer);
            var response = Response<ChangePlanDto>.Error(null, new ResponseMessage(blocking[0].Key, blocking[0].Message));

            foreach (var issue in blocking.Skip(1))
                response.AddMessage(issue.Message, MessageType.Error);

            return response;
        }

        var draft = result.Draft;

        draft.Warnings.AddRange(WingetContent.Localize(result.Issues, _localizer).Select(i => new PlanWarning { Key = i.Key, Message = i.Message, Field = i.Field, Locale = i.Locale }));

        if (draft.CustomChanges is WingetPlannedChange change && change.Installers.Any(i => i.ExpectedSha256 == null))
            draft.Warnings.Add(new PlanWarning { Key = MessageKey.WingetSha256OnApply, Message = _localizer[MessageKey.WingetSha256OnApply], Field = "InstallerSha256" });

        return await _changePlanService.SaveAsync(app, read.Snapshot, draft, request.DryRun, request.Confirm, cancellationToken);
    }
}
