using Milvasoft.Components.CQRS.Query;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Core.Abstractions;
using Storeasy.Application.Dtos.StoreJobDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Events;
using Storeasy.Domain.Stores;

namespace Storeasy.Application.Features.StoreJobs.GetActivityBoard;

/// <summary>
/// Handles <see cref="GetActivityBoardQuery"/>.
/// </summary>
/// <param name="versionStateRepository"></param>
/// <param name="buildRepository"></param>
/// <param name="storeAppRepository"></param>
/// <param name="jobRepository"></param>
/// <param name="eventRepository"></param>
/// <param name="endpointRepository"></param>
/// <param name="timeProvider"></param>
public class GetActivityBoardQueryHandler(IStoreasyRepositoryBase<AppVersionState> versionStateRepository,
                                          IStoreasyRepositoryBase<Build> buildRepository,
                                          IStoreasyRepositoryBase<StoreApp> storeAppRepository,
                                          IStoreasyRepositoryBase<StoreJob> jobRepository,
                                          IStoreasyRepositoryBase<WebhookEvent> eventRepository,
                                          IStoreasyRepositoryBase<WebhookEndpoint> endpointRepository,
                                          TimeProvider timeProvider) : IInterceptable, IQueryHandler<GetActivityBoardQuery, ActivityBoardDto>
{
    /// <summary>
    /// Most rows of each list.
    /// </summary>
    public const int MaxRows = 40;

    /// <summary>
    /// How far ahead a TestFlight expiry is named.
    /// </summary>
    public const int ExpiryDays = 14;

    /// <summary>
    /// How long a version that left App Review stays on the board.
    /// </summary>
    public const int RecentVersionDays = 30;

    private static readonly List<string> _withReview = ["WAITING_FOR_REVIEW", "IN_REVIEW"];

    private readonly IStoreasyRepositoryBase<AppVersionState> _versionStateRepository = versionStateRepository;
    private readonly IStoreasyRepositoryBase<Build> _buildRepository = buildRepository;
    private readonly IStoreasyRepositoryBase<StoreApp> _storeAppRepository = storeAppRepository;
    private readonly IStoreasyRepositoryBase<StoreJob> _jobRepository = jobRepository;
    private readonly IStoreasyRepositoryBase<WebhookEvent> _eventRepository = eventRepository;
    private readonly IStoreasyRepositoryBase<WebhookEndpoint> _endpointRepository = endpointRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task<Response<ActivityBoardDto>> Handle(GetActivityBoardQuery request, CancellationToken cancellationToken)
    {
        var organizationId = request.OrganizationId;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var hoursStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(-23);
        var recentSince = now.AddDays(-RecentVersionDays);
        var expiryUntil = now.AddDays(ExpiryDays);

        var states = await _versionStateRepository.GetAllAsync(s => s.OrganizationId == organizationId && (s.ChangedDate >= recentSince || _withReview.Contains(s.State)),
                                                               projection: s => new AppVersionState
                                                               {
                                                                   AppId = s.AppId,
                                                                   StoreAppId = s.StoreAppId,
                                                                   Store = s.Store,
                                                                   VersionString = s.VersionString,
                                                                   BuildNumber = s.BuildNumber,
                                                                   State = s.State,
                                                                   PreviousState = s.PreviousState,
                                                                   ChangedDate = s.ChangedDate,
                                                                   SubmittedDate = s.SubmittedDate,
                                                               },
                                                               cancellationToken: cancellationToken);

        var builds = await _buildRepository.GetAllAsync(b => b.OrganizationId == organizationId
                                                              && (b.ProcessingState == BuildProcessingState.Processing
                                                                  || (b.ProcessingState == BuildProcessingState.Valid && b.ExpiryDate > now && b.ExpiryDate <= expiryUntil)),
                                                        projection: b => new Build
                                                        {
                                                            StoreAppId = b.StoreAppId,
                                                            Store = b.Store,
                                                            Version = b.Version,
                                                            BuildNumber = b.BuildNumber,
                                                            ProcessingState = b.ProcessingState,
                                                            UploadedDate = b.UploadedDate,
                                                            ExpiryDate = b.ExpiryDate,
                                                        },
                                                        cancellationToken: cancellationToken);

        var storeApps = await _storeAppRepository.GetAllAsync(s => s.OrganizationId == organizationId,
                                                              projection: s => new StoreApp { Id = s.Id, AppId = s.AppId },
                                                              cancellationToken: cancellationToken);

        var jobs = await _jobRepository.GetAllAsync(j => j.OrganizationId == organizationId && j.Store != null && j.EndDate >= hoursStart,
                                                    projection: j => new StoreJob { Store = j.Store, Status = j.Status, EndDate = j.EndDate },
                                                    cancellationToken: cancellationToken);

        var events = await _eventRepository.GetAllAsync(e => e.OrganizationId == organizationId && e.ReceivedDate >= hoursStart,
                                                        projection: e => new WebhookEvent { WebhookEndpointId = e.WebhookEndpointId, ReceivedDate = e.ReceivedDate },
                                                        cancellationToken: cancellationToken);

        var endpoints = await _endpointRepository.GetAllAsync(e => e.OrganizationId == organizationId,
                                                              projection: e => new WebhookEndpoint { Id = e.Id, Source = e.Source },
                                                              cancellationToken: cancellationToken);

        var appOf = storeApps.ToDictionary(s => s.Id, s => s.AppId);
        var storeOfEndpoint = endpoints.ToDictionary(e => e.Id, e => StoreOf(e.Source));

        var versions = states.GroupBy(s => s.StoreAppId)
                             .Select(g => g.OrderByDescending(s => s.ChangedDate).First())
                             .OrderByDescending(s => s.ChangedDate)
                             .Take(MaxRows)
                             .Select(s => new ActivityBoardVersionDto
                             {
                                 AppId = s.AppId,
                                 StoreAppId = s.StoreAppId,
                                 Store = s.Store,
                                 VersionString = s.VersionString,
                                 BuildNumber = s.BuildNumber,
                                 State = s.State,
                                 PreviousState = s.PreviousState,
                                 ChangedDate = s.ChangedDate,
                                 SubmittedDate = s.SubmittedDate,
                             })
                             .ToList();

        var boardBuilds = builds.Where(b => appOf.ContainsKey(b.StoreAppId))
                                .OrderBy(b => b.ProcessingState == BuildProcessingState.Processing ? 0 : 1)
                                .ThenBy(b => b.ExpiryDate ?? DateTime.MaxValue)
                                .Take(MaxRows)
                                .Select(b => new ActivityBoardBuildDto
                                {
                                    AppId = appOf[b.StoreAppId],
                                    StoreAppId = b.StoreAppId,
                                    Store = b.Store,
                                    Version = b.Version,
                                    BuildNumber = b.BuildNumber,
                                    ProcessingState = b.ProcessingState,
                                    UploadedDate = b.UploadedDate,
                                    ExpiryDate = b.ExpiryDate,
                                })
                                .ToList();

        var hours = new Dictionary<(StoreKind Store, int Index), ActivityBoardHourDto>();

        ActivityBoardHourDto Bucket(StoreKind store, DateTime date)
        {
            var index = Math.Clamp((int)Math.Floor((date - hoursStart).TotalHours), 0, 23);

            if (!hours.TryGetValue((store, index), out var hour))
            {
                hour = new ActivityBoardHourDto { Store = store, Hour = hoursStart.AddHours(index) };
                hours[(store, index)] = hour;
            }

            return hour;
        }

        foreach (var job in jobs.Where(j => j.Store.HasValue && j.EndDate.HasValue))
        {
            if (job.Status == StoreJobStatus.Succeeded)
                Bucket(job.Store.Value, job.EndDate.Value).Succeeded++;
            else if (job.Status == StoreJobStatus.Failed)
                Bucket(job.Store.Value, job.EndDate.Value).Failed++;
        }

        foreach (var webhookEvent in events)
        {
            if (storeOfEndpoint.GetValueOrDefault(webhookEvent.WebhookEndpointId) is StoreKind store)
                Bucket(store, webhookEvent.ReceivedDate).Inbound++;
        }

        return Response<ActivityBoardDto>.Success(new ActivityBoardDto
        {
            GeneratedDate = now,
            HoursStartDate = hoursStart,
            Versions = versions,
            Builds = boardBuilds,
            Hours = [.. hours.Values.OrderBy(h => h.Store).ThenBy(h => h.Hour)],
        });
    }

    private static StoreKind? StoreOf(WebhookSource source) => source switch
    {
        WebhookSource.AppStoreConnect => StoreKind.Apple,
        WebhookSource.GooglePlay => StoreKind.Google,
        _ => null,
    };
}
