using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.EasDtos;

namespace Storeasy.Application.Interfaces;

/// <summary>
/// EAS builds through the Expo API (docs/eas-builds.md): the organization's Expo token, project links, starting builds from
/// GitHub on a member's request, following them, and handing finished packages to the build intake. Nothing starts on its
/// own.
/// </summary>
public interface IEasService
{
    /// <summary>The Expo connection of the organization; null data without one.</summary>
    Task<Response<ExpoConnectionDto>> GetConnectionAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>Seals and saves the token after Expo named its actor.</summary>
    Task<Response<ExpoConnectionDto>> SaveConnectionAsync(int organizationId, string token, CancellationToken cancellationToken);

    /// <summary>Removes the token.</summary>
    Task<Response<int>> RemoveConnectionAsync(int organizationId, CancellationToken cancellationToken);

    /// <summary>The Expo projects the token can see, best matches of the app first.</summary>
    Task<Response<List<EasProjectDto>>> ListProjectsAsync(int organizationId, int? appId, CancellationToken cancellationToken);

    /// <summary>Links an app to a project (or unlinks it with a null project) and queues a sync of its builds.</summary>
    Task<Response<EasAppDto>> LinkProjectAsync(int organizationId, int appId, string projectId, CancellationToken cancellationToken);

    /// <summary>The app's link and its builds.</summary>
    Task<Response<EasAppDto>> GetAppAsync(int organizationId, int appId, CancellationToken cancellationToken);

    /// <summary>The build profiles: from eas.json in the linked repository when it can be read, else the defaults.</summary>
    Task<Response<EasProfilesDto>> GetProfilesAsync(int organizationId, int appId, string gitRef, CancellationToken cancellationToken);

    /// <summary>Asks Expo to build from GitHub, one build per platform, after the typed confirmation.</summary>
    Task<Response<List<EasBuildDto>>> StartBuildAsync(int organizationId, int appId, EasPlatform platform, string profile, string gitRef, EasChainDto chain, string confirm, CancellationToken cancellationToken);

    /// <summary>One build.</summary>
    Task<Response<EasBuildDto>> GetBuildAsync(int organizationId, long id, CancellationToken cancellationToken);

    /// <summary>Cancels a queued or running build.</summary>
    Task<Response<EasBuildDto>> CancelBuildAsync(int organizationId, long id, CancellationToken cancellationToken);

    /// <summary>Hands a finished build's package to the build intake.</summary>
    Task<Response<EasBuildDto>> SendToIntakeAsync(int organizationId, long id, CancellationToken cancellationToken);

    /// <summary>Queues a sync of one app's builds, or of every linked app.</summary>
    Task<Response<long>> QueueSyncAsync(int organizationId, int? appId, CancellationToken cancellationToken);

    /// <summary>Reads the recent builds of one linked app, or every linked app of the organization (the sync job).</summary>
    Task<EasSyncSummary> SyncAsync(int organizationId, int? appId, CancellationToken cancellationToken);

    /// <summary>Follows every build still running or not created yet (the schedule). Returns the builds that changed.</summary>
    Task<int> FollowAsync(CancellationToken cancellationToken);

    /// <summary>Queues a sync for every organization with a linked app (the hourly schedule).</summary>
    Task<int> QueueLinkedAsync(CancellationToken cancellationToken);

    /// <summary>Saves the app's after build defaults (intake, EAS Submit, review plan).</summary>
    Task<Response<EasAppDto>> SaveChainAsync(int organizationId, int appId, EasChainDto chain, CancellationToken cancellationToken);

    /// <summary>Starts an EAS Submit of a finished build after the typed confirmation.</summary>
    Task<Response<EasSubmissionDto>> StartSubmissionAsync(int organizationId, long buildId, string track, string releaseStatus, string confirm, CancellationToken cancellationToken);

    /// <summary>The EAS Submit submissions of an app, newest first.</summary>
    Task<Response<List<EasSubmissionDto>>> ListSubmissionsAsync(int organizationId, int appId, CancellationToken cancellationToken);

    /// <summary>One submission.</summary>
    Task<Response<EasSubmissionDto>> GetSubmissionAsync(int organizationId, long id, CancellationToken cancellationToken);
}
