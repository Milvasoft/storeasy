using Cronos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Milvasoft.JobScheduling;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Feedback;
using Storeasy.Infrastructure.Jobs;
using Storeasy.Infrastructure.Notifications;
using Storeasy.Infrastructure.Rules;
using Storeasy.Infrastructure.StoreClients;
using Storeasy.Infrastructure.TestFlight;

namespace Storeasy.Infrastructure.Events;

/// <summary>
/// Registrations of the events phase (docs/urun-plani.md phase 4): inbound webhooks, notifications and outgoing webhooks,
/// rejection tracking, reviews, TestFlight, the rule watcher and the deadline reminders.
/// </summary>
public static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the events phase services. The rules catalog (<see cref="IRulesCatalog"/>) and the store content services
    /// come from the content phase.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddEventServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(WebhookOptions.SectionName).Get<WebhookOptions>() ?? new WebhookOptions());
        services.AddSingleton(configurationManager.GetSection(OrgWebhookOptions.SectionName).Get<OrgWebhookOptions>() ?? new OrgWebhookOptions());
        services.AddSingleton(configurationManager.GetSection(EventScheduleOptions.SectionName).Get<EventScheduleOptions>() ?? new EventScheduleOptions());

        var watcherOptions = configurationManager.GetSection(RuleWatcherOptions.SectionName).Get<RuleWatcherOptions>() ?? new RuleWatcherOptions();

        services.AddSingleton(watcherOptions);

        // Notifications and outgoing webhooks.
        services.TryAddSingleton<IUserNotificationPublisher, NullUserNotificationPublisher>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IOrgWebhookPublisher, OrgWebhookPublisher>();
        services.AddScoped<IOrgWebhookSender, OrgWebhookSender>();
        services.AddSingleton<IHostResolver, DnsHostResolver>();
        services.AddSingleton<IWebhookUrlGuard, WebhookUrlGuard>();
        services.AddScoped<IDeadlineNotifier, DeadlineNotifier>();

        services.AddHttpClient(OrgWebhookSender.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(provider => GuardedConnection.CreateHandler(provider.GetRequiredService<IWebhookUrlGuard>(), provider.GetRequiredService<IHostResolver>()));

        services.AddHostedService<WebhookDeliveryRunner>();

        // Inbound webhooks, version states and rejections.
        services.AddScoped<IWebhookInbox, WebhookInbox>();
        services.AddSingleton<IGooglePushTokenValidator, GooglePushTokenValidator>();
        services.AddHttpClient(GooglePushTokenValidator.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IVersionStateTracker, VersionStateTracker>();
        services.AddScoped<ScheduledStoreSyncs>();

        // Store calls (App Store Connect webhooks and versions, reviews, TestFlight).
        services.AddScoped<StoreApiClient>();
        services.AddScoped<StoreEventsGateway>();
        services.AddScoped<IStoreEventGateway>(provider => provider.GetRequiredService<StoreEventsGateway>());
        services.AddScoped<IStoreReviewGateway>(provider => provider.GetRequiredService<StoreEventsGateway>());
        services.AddScoped<ITestFlightGateway>(provider => provider.GetRequiredService<StoreEventsGateway>());
        services.AddScoped<TestFlightStore>();

        services.AddScoped<IStoreJobHandler, ProcessWebhookEventJobHandler>();
        services.AddScoped<IStoreJobHandler, RegisterWebhooksJobHandler>();
        services.AddScoped<IStoreJobHandler, PingWebhooksJobHandler>();
        services.AddScoped<IStoreJobHandler, SyncVersionStatesJobHandler>();
        services.AddScoped<IStoreJobHandler, SyncReviewsJobHandler>();
        services.AddScoped<IStoreJobHandler, ReplyToReviewJobHandler>();
        services.AddScoped<IStoreJobHandler, SyncTestFlightJobHandler>();
        services.AddScoped<IStoreJobHandler, UpdateTestFlightJobHandler>();

        // Rules catalog watcher.
        services.AddScoped<IRuleImpactFinder, RuleImpactFinder>();
        services.AddScoped<IRuleSourceFetcher, RuleSourceFetcher>();
        services.AddScoped<IRuleWatcher, RuleWatcher>();

        services.AddHttpClient(RuleSourceFetcher.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });

        // Schedules (UTC).
        services.AddMilvaCronJob<VersionStateSyncJob>(c => Schedule(c, "*/15 * * * *"));
        services.AddMilvaCronJob<MsSyncJob>(c => Schedule(c, "5-59/15 * * * *"));
        services.AddMilvaCronJob<WingetSyncJob>(c => Schedule(c, "10-59/15 * * * *"));
        services.AddMilvaCronJob<ChromeSyncJob>(c => Schedule(c, "12-59/15 * * * *"));
        services.AddMilvaCronJob<FirefoxSyncJob>(c => Schedule(c, "13-59/15 * * * *"));
        services.AddMilvaCronJob<ReviewSyncJob>(c => Schedule(c, "*/30 * * * *"));
        services.AddMilvaCronJob<TestFlightSyncJob>(c => Schedule(c, "0 */6 * * *"));
        services.AddMilvaCronJob<DeadlineReminderJob>(c => Schedule(c, "0 8 * * *"));
        services.AddMilvaCronJob<RuleWatcherJob>(c => Schedule(c, "0 6 * * *"));

        return services;
    }

    private static void Schedule(IScheduleConfig config, string cron)
    {
        config.TimeZoneInfo = TimeZoneInfo.Utc;
        config.CronExpression = cron;
        config.CronFormat = CronFormat.Standard;
    }
}
