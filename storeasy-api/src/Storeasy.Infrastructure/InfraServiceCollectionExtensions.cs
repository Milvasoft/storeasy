using Cronos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Milvasoft.Caching.Builder;
using Milvasoft.Caching.Redis;
using Milvasoft.Caching.Redis.Accessor;
using Milvasoft.Core.Abstractions;
using Milvasoft.DataAccess.EfCore;
using Milvasoft.Interception.Decorator;
using Milvasoft.Interception.Ef;
using Milvasoft.JobScheduling;
using Storeasy.Application;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Aspects.UserActivityLogAspect;
using Storeasy.Application.Utils.Extensions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Ai;
using Storeasy.Infrastructure.Aso;
using Storeasy.Infrastructure.Email;
using Storeasy.Infrastructure.Events;
using Storeasy.Infrastructure.Stores.Experiments;
using Storeasy.Infrastructure.Jobs;
using Storeasy.Infrastructure.LazyImpl;
using Storeasy.Infrastructure.Logging;
using Storeasy.Infrastructure.Notifications;
using Storeasy.Infrastructure.Notifications.WebPush;
using Storeasy.Infrastructure.Persistence.Context;
using Storeasy.Infrastructure.Persistence.Repository;
using Storeasy.Infrastructure.Security;
using Storeasy.Infrastructure.Services;
using Storeasy.Infrastructure.Storage;
using Storeasy.Infrastructure.StoreJobs;
using Storeasy.Infrastructure.StoreMetrics;
using Storeasy.Infrastructure.Stores;
using Storeasy.Infrastructure.Stores.Content;
using Storeasy.Infrastructure.Stores.Monetization;
using Storeasy.Infrastructure.Studio;
using Storeasy.Infrastructure.Stores.Microsoft;
using Npgsql;

namespace Storeasy.Infrastructure;

/// <summary>
/// Infrastructure service collection extensions.
/// </summary>
public static class InfraServiceCollectionExtensions
{
    /// <summary>
    /// Adds infrastructure services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddScoped<IMilvaLogger, StoreasyDbLogger>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ISecurityStampStore, SecurityStampStore>();
        services.AddSingleton<ITokenRevocationList, TokenRevocationList>();
        services.AddScoped<IPermissionManager, PermissionManager>();
        services.AddScoped<IAccountManager, AccountManager>();
        services.AddScoped<IDeveloperService, DeveloperService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IOrganizationAccess, OrganizationAccess>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IStoreGateway, StoreGateway>();

        // AI features (Storeasy:Ai): reply drafts and pre-reviews, Claude through the Anthropic SDK with each organization's
        // own key; nothing is called until a member asks for a draft or a pre-review.
        services.AddSingleton(configurationManager.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions());
        services.AddHttpClient(AnthropicCalls.HttpClientName);
        services.AddHttpClient(PreReviewImageLoader.HttpClientName);
        services.AddScoped<IReplyDraftWriter, AnthropicReplyDraftWriter>();
        services.AddScoped<IListingSuggestionWriter, AnthropicListingSuggestionWriter>();
        services.AddScoped<IPreReviewer, AnthropicPreReviewer>();
        services.AddScoped<IPreReviewImageLoader, PreReviewImageLoader>();
        services.AddScoped<IStoreJobHandler, PreReviewJobHandler>();

        // Store content engine: live reads, change plans and the gateways of each store. The rules catalog is loaded and
        // validated here, so a broken catalog stops the startup instead of producing wrong readiness answers.
        var storeApiOptions = configurationManager.GetSection(StoreApiOptions.SectionName).Get<StoreApiOptions>() ?? new StoreApiOptions();

        services.AddSingleton(storeApiOptions);
        services.AddSingleton<IRulesCatalog>(RulesCatalog.Load(storeApiOptions.RulesPath));
        services.AddScoped<StoreHttp>();
        services.AddScoped<IStoreContentGateway, AppleContentGateway>();
        services.AddScoped<IStoreContentGateway, GoogleContentGateway>();
        services.AddScoped<IStoreContentService, StoreContentService>();
        services.AddScoped<IChangePlanService, ChangePlanService>();
        services.AddScoped<IReadinessService, ReadinessService>();
        services.AddScoped<IMonetizationGateway, AppleMonetizationGateway>();
        services.AddScoped<IMonetizationGateway, GoogleMonetizationGateway>();
        services.AddScoped<MonetizationStoreAccess>();
        services.AddScoped<IMonetizationService, MonetizationService>();
        services.AddScoped<IChangePlanExtension, MonetizationPlanExtension>();
        services.AddScoped<IReadinessContributor, MonetizationReadiness>();
        services.AddScoped<IAsoExperimentGateway, AppleExperimentGateway>();
        services.AddScoped<IAsoExperimentStore, AsoExperimentStore>();
        services.AddScoped<IChangePlanExtension, AsoExperimentPlanExtension>();
        services.AddSingleton(TimeProvider.System);

        // Store secrets are sealed with the master key from Storeasy__Encryption__MasterKey (user secrets locally). The
        // protector is created on first use and stops with a message naming the variable when the key is missing.
        var encryptionOptions = configurationManager.GetSection(EncryptionOptions.SectionName).Get<EncryptionOptions>() ?? new EncryptionOptions();

        services.AddSingleton(encryptionOptions);
        services.AddSingleton<ISecretProtector, EnvelopeSecretProtector>();

        // One client for App Store Connect and Google Play calls; tests replace its handler.
        services.AddHttpClient(StoreGateway.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(60));

        services.AddAccountServices(configurationManager);

        services.AddStoreJobServices(configurationManager);

        services.AddEventServices(configurationManager);

        // ASO keyword research and daily rank tracking from the stores' public search data.
        services.AddAsoServices(configurationManager);

        services.AddStoreMetricServices();

        // Microsoft Store (phase 5): client, token cache and job handlers.
        services.AddMicrosoftStoreServices(configurationManager);

        // Private bucket, presigned URLs only (Storage:S3). Without a bucket the upload endpoints answer 503.
        var storageOptions = configurationManager.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        services.AddSingleton(storageOptions);
        services.AddSingleton<IFileStorage>(new S3FileStorage(S3FileStorage.CreateClient(storageOptions), storageOptions));

        // Organization logos are redrawn as a square PNG before they are stored.
        services.AddSingleton<ILogoImageNormalizer, SkiaLogoImageNormalizer>();

        // Mockup studio (phase 6): renderer, fonts, screenshot sizes, export job.
        services.AddStudioServices(configurationManager);

        // Personal access, CI and upload tokens; package intake (OIDC exchange, upload sessions, processing, cleanup).
        services.AddIntakeServices(configurationManager, storageOptions);

        // Retry safety for the create endpoints marked [Idempotent]; see IdempotencyStore. Expired keys are deleted by IdempotencyKeyCleanerJob.
        services.AddScoped<IIdempotencyStore, IdempotencyStore>();

        // Must be registered before AddMilvaInterception: interception decorates only the IInterceptable
        // implementations registered up to that call, and the transaction comes from that decoration.
        services.AddScoped<ITransactionRunner, TransactionRunner>();

        services.AddTransient(typeof(Lazy<>), typeof(StoreasyLazy<>));

        services.AddInternalJobs();

        services.AddDataAccessServices(configurationManager);

        // Redis is the shared state of every instance: the [Cache] interceptor, the token revocation stamps and the
        // rate limit counters. The connection is lazy and retries in the background, so the application starts
        // (and every Redis consumer falls back or fails open) while Redis is unreachable.
        services.AddMilvaCaching(configurationManager)
                .WithRedisAccessor();

        // Every Redis key of this application starts with its own prefix (CacheKeys), so applications sharing one Redis
        // never read each other's entries. Milvasoft's Redis options have no key prefix of their own.
        var cacheKeyOptions = configurationManager.GetSection(CacheKeyOptions.SectionName).Get<CacheKeyOptions>() ?? new CacheKeyOptions();

        services.AddSingleton(cacheKeyOptions);
        services.AddSingleton<CacheKeys>();

        services.AddMilvaInterception(ApplicationAssembly.Assembly, configurationManager)
                .WithLogInterceptor()
                .WithResponseInterceptor()
                .WithCacheInterceptor()
                .WithTransactionInterceptor()
                .WithActivityInterceptor()
                .WithInterceptor<UserActivityLogInterceptor>()
                .PostConfigureInterceptionOptions(opt =>
                {
                    opt.Response.GenerateMetadataFunc = StoreasyExtensions.GenerateMetadata;
                    opt.Cache.CacheAccessorType = typeof(IRedisAccessor);

                    // The [Cache] keys are hashes of the method and its arguments: the prefix goes into the hash.
                    opt.Cache.CacheKeyConfigurator = _ => cacheKeyOptions.Prefix;
                })
                .PostConfigureTransactionInterceptionOptions(opt =>
                {
                    opt.DbContextType = typeof(StoreasyDbContext);
                });

        return services;
    }

    /// <summary>
    /// Adds registration, e-mail and account mail services. The sender is chosen by <c>Storeasy:Email:Provider</c>; the
    /// startup (Api) refuses the log provider outside Development and a provider with missing settings.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    private static IServiceCollection AddAccountServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(RegistrationOptions.SectionName).Get<RegistrationOptions>() ?? new RegistrationOptions());
        services.AddSingleton(configurationManager.GetSection(AppOptions.SectionName).Get<AppOptions>() ?? new AppOptions());
        services.AddSingleton(configurationManager.GetSection(BuildOptions.SectionName).Get<BuildOptions>() ?? new BuildOptions());
        services.AddSingleton(configurationManager.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>() ?? new BootstrapOptions());
        services.AddScoped<IRegistrationModes, RegistrationModes>();

        // Storeasy:Mode: SingleTeam (default, one team on its own instance) or MultiTenant (hosted, many customers).
        services.AddSingleton(new DeploymentOptions { Mode = configurationManager.GetValue(DeploymentOptions.ConfigurationKey, DeploymentMode.SingleTeam) });
        services.AddSingleton<SetupCodeStore>();
        services.AddScoped<IFirstRunSetup, FirstRunSetup>();
        services.AddScoped<ISystemStatusProbe, SystemStatusProbe>();

        var emailOptions = configurationManager.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();

        services.AddSingleton(emailOptions);

        switch (emailOptions.Provider)
        {
            case EmailProvider.Notifike:
                services.AddScoped<IEmailSender, NotifikeEmailSender>();
                break;
            case EmailProvider.Log:
                services.AddScoped<IEmailSender, LogEmailSender>();
                break;
            default:
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
        }

        services.AddHttpClient(NotifikeEmailSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<IAccountMailer, AccountMailer>();

        // E-mails are queued on the request path and sent by the background sender (Storeasy:Email:Outbox).
        services.AddScoped<IEmailOutbox, EmailOutbox>();
        services.AddScoped<EmailOutboxSender>();
        services.AddHostedService<EmailOutboxRunner>();

        // Sign in with Google (Storeasy:Auth:Google); off until a client id and secret are set.
        services.AddSingleton(configurationManager.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>() ?? new GoogleAuthOptions());
        services.AddSingleton<IGoogleSignIn, GoogleSignInService>();
        services.AddHttpClient(GoogleSignInService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));

        // Web push with VAPID (Storeasy:WebPush); off until a key pair is set. Pushes go only to public addresses.
        services.AddSingleton(configurationManager.GetSection(WebPushOptions.SectionName).Get<WebPushOptions>() ?? new WebPushOptions());
        services.AddSingleton<WebPushQueue>();
        services.AddSingleton<IWebPushQueue>(provider => provider.GetRequiredService<WebPushQueue>());
        services.AddScoped<WebPushSender>();
        services.AddHttpClient(WebPushSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15))
                .ConfigurePrimaryHttpMessageHandler(provider => GuardedConnection.CreateHandler(provider.GetRequiredService<IWebhookUrlGuard>(), provider.GetRequiredService<IHostResolver>()));
        services.AddHostedService<WebPushRunner>();

        return services;
    }

    /// <summary>
    /// Adds the store job queue and its in-process runner. Handlers register as <see cref="IStoreJobHandler"/>; the live
    /// event publisher defaults to one that drops events (the API registers its SignalR publisher first).
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    private static IServiceCollection AddStoreJobServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        services.AddSingleton(configurationManager.GetSection(StoreJobOptions.SectionName).Get<StoreJobOptions>() ?? new StoreJobOptions());

        services.TryAddSingleton<IActivityPublisher, NullActivityPublisher>();
        services.TryAddSingleton<IRealtimeConnections, NullRealtimeConnections>();

        services.AddScoped<IStoreJobQueue, StoreJobQueue>();
        services.AddScoped<StoreJobExecutor>();
        services.AddScoped<IStoreJobHandler, SyncAppsJobHandler>();
        services.AddScoped<IStoreJobHandler, ApplyChangePlanJobHandler>();
        services.AddScoped<IStoreJobHandler, SyncMonetizationJobHandler>();
        services.AddScoped<IStoreJobHandler, SyncAsoExperimentsJobHandler>();

        // After MigrationHostedService (registered by the API before the infrastructure), so the tables exist.
        services.AddHostedService<StoreJobRunner>();

        return services;
    }

    /// <summary>
    /// Adds data access services.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configurationManager"></param>
    /// <returns></returns>
    private static IServiceCollection AddDataAccessServices(this IServiceCollection services, IConfigurationManager configurationManager)
    {
        var connectionString = configurationManager.GetConnectionString("DefaultConnectionString");

        services.ConfigureMilvaDataAccess(configurationManager)
                .PostConfigureMilvaDataAccess(opt =>
                {
                    opt.GetCurrentUserNameMethod = User.GetCurrentUser;
                })
                .AddInjectedDbContext<StoreasyDbContext>();

        var dataSource = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build();

        services.AddPooledDbContextFactory<StoreasyDbContext>((provider, options) =>
        {
            options.ConfigureWarnings(warnings => { warnings.Log(RelationalEventId.PendingModelChangesWarning); });

            options.UseNpgsql(dataSource, b => b.MigrationsHistoryTable(TableNames.EfMigrationHistory).MigrationsAssembly("Storeasy.Api").EnableRetryOnFailure())
                   .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
        });

        services.AddScoped<StoreasyDbContextScopedFactory>();
        services.AddScoped(sp => sp.GetRequiredService<StoreasyDbContextScopedFactory>().CreateDbContext());

        services.AddScoped(typeof(IStoreasyRepositoryBase<>), typeof(StoreasyRepositoryBase<>));
        services.AddScoped<IStoreasyDbContextAccessor, StoreasyDbContextAccessor>();

        return services;
    }

    /// <summary>
    /// Adds internal cron jobs.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    private static IServiceCollection AddInternalJobs(this IServiceCollection services)
    {
        services.AddMilvaCronJob<ActivityLogCleanerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 2 */30 * *"; // At 02:00 AM, every 30 days
            c.CronFormat = CronFormat.Standard;
        });

        services.AddMilvaCronJob<NotificationCleanerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 3 */5 * *"; // At 03:00 AM, every 5 days
            c.CronFormat = CronFormat.Standard;
        });

        services.AddMilvaCronJob<AuditLogCleanerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"30 3 * * *"; // At 03:30 AM, every day
            c.CronFormat = CronFormat.Standard;
        });

        services.AddMilvaCronJob<StoreMetricCleanerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"45 3 * * 0"; // At 03:45 AM, every Sunday
            c.CronFormat = CronFormat.Standard;
        });

        services.AddMilvaCronJob<IdempotencyKeyCleanerJob>(c =>
        {
            c.TimeZoneInfo = TimeZoneInfo.Utc;
            c.CronExpression = @"0 * * * *"; // At minute 0 of every hour
            c.CronFormat = CronFormat.Standard;
        });

        return services;
    }
}
