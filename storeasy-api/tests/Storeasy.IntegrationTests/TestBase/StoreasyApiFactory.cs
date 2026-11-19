using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Storeasy.Api.AppStartup;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Infrastructure.Stores.Microsoft;
using Storeasy.Infrastructure.Persistence;
using Storeasy.Infrastructure.Persistence.Context;
using Storeasy.IntegrationTests.TestBase.Fakes;
using Npgsql;
using System.Net;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Boots the real API (Program.cs, the real middleware pipeline and startup work) against a throw-away PostgreSQL and
/// Redis started with Testcontainers, initialized the way <c>database/init</c> initializes a new database (root user,
/// SuperAdmin role, seed protection triggers, permissions, languages). Authentication, authorization, transactions,
/// interceptors, rate limiting and the security stamp check run exactly as in production.
/// </summary>
/// <remarks>
/// <para> Only process boundaries that leave the machine are replaced, by recording fakes under <c>Fakes/</c>
///        registered in <see cref="ConfigureWebHost"/> (<c>RemoveAll</c> + <c>AddSingleton</c>). The template has no such
///        integration yet; when one is added (mail, SMS, a payment provider) its fake goes there and is exposed as a
///        property, and as a backstop every HttpClient of the process is proxied to a closed loopback port. </para>
/// <para> One factory (and therefore one database) is shared by every test class in the collection. The database is
///        never reset: tests create their own users and records with unique names (<see cref="TestApi.Unique"/>) and
///        never change the shared root user. Redis is flushed after every test (<see cref="IntegrationTestBase"/>). </para>
/// </remarks>
public class StoreasyApiFactory : WebApplicationFactory<IApiMarker>, IAsyncLifetime
{
    /// <summary>
    /// Configuration key of the Redis endpoint (read while the application registers its services).
    /// </summary>
    public const string RedisConnectionStringKey = "Milvasoft:Caching:Redis:ConnectionString";

    /// <summary>
    /// Configuration key of the database connection string (read while the application registers its services).
    /// </summary>
    public const string ConnectionStringKey = "ConnectionStrings:DefaultConnectionString";

    /// <summary>
    /// Configuration key that turns the store job runner of a host on or off.
    /// </summary>
    public const string StoreJobsEnabledKey = "Storeasy:Jobs:Enabled";

    /// <summary>
    /// Environment variable of the Storeasy environment (<c>dev</c>, <c>test</c>, <c>prod</c>). The shared host runs with
    /// <c>dev</c>; the documentation is mapped at startup from it, the developer endpoints read it on every call.
    /// </summary>
    public const string MilvaEnvironmentKey = "MILVA_ENV";

    /// <summary>
    /// Configuration key of the bootstrap init key that <c>developer/database/init</c> requires. The shared host has none.
    /// </summary>
    public const string BootstrapInitKeyKey = "Storeasy:Bootstrap:InitKey";

    /// <summary>
    /// Configuration key of the deployment mode. The shared host (and every host built from it unless overridden) runs
    /// in <c>MultiTenant</c>, the mode the shared database is initialized for (root user, open registration, many
    /// organizations); the single team tests build their own hosts with <c>SingleTeam</c>.
    /// </summary>
    public const string DeploymentModeKey = "Storeasy:Mode";

    /// <summary>
    /// Configuration key of the fixed first run setup code (single team mode).
    /// </summary>
    public const string BootstrapSetupCodeKey = "Storeasy:Bootstrap:SetupCode";

    /// <summary>
    /// Configuration key of the Redis key prefix (a separate host gets its own, so its keys never meet the shared host's).
    /// </summary>
    public const string CacheKeyPrefixKey = "CacheKeys:Prefix";

    /// <summary>
    /// Access token signing key of the test hosts. The appsettings files carry no key, so it is given through the
    /// environment like in a deployment (in Development the application would generate a random one per host).
    /// </summary>
    public const string TokenSigningKey = "integration-tests-signing-key-0123456789abcdef0123456789abcdef";

    /// <summary>
    /// Master key of the secret protector in the test hosts (base64 of 32 fixed bytes, test only).
    /// </summary>
    public const string EncryptionMasterKey = "c3RvcmVhc3ktaW50ZWdyYXRpb24tdGVzdHMta2V5MDE=";

    /// <summary>
    /// The store engine fake every test host uses (no App Store Connect or Google Play call is ever made).
    /// </summary>
    public FakeStoreGateway StoreGateway { get; } = new();

    /// <summary>
    /// The e-mail provider fake every test host uses (no e-mail ever leaves the test).
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// The bucket fake every test host uses.
    /// </summary>
    public FakeFileStorage FileStorage { get; } = new();

    /// <summary>
    /// App Store Connect and Google Play as the content engine sees them (the store HTTP client's primary handler).
    /// </summary>
    public FakeStoreServer Stores { get; } = new();

    /// <summary>
    /// The events phase store calls (webhooks, versions, reviews, TestFlight) every test host uses.
    /// </summary>
    public FakeStoreEventGateway StoreEvents { get; } = new();

    /// <summary>
    /// The official pages the rule watcher reads.
    /// </summary>
    public FakeRuleSourceFetcher RulePages { get; } = new();

    /// <summary>
    /// DNS of the outgoing webhook checks.
    /// </summary>
    public FakeHostResolver HostResolver { get; } = new();

    /// <summary>
    /// The receivers of outgoing webhooks.
    /// </summary>
    public FakeWebhookReceiver WebhookReceiver { get; } = new();

    /// <summary>
    /// The Microsoft every test host talks to (Entra, both Microsoft Store APIs, blob uploads, installer downloads).
    /// </summary>
    public FakeMicrosoftStore MicrosoftStore { get; } = new();
    /// The CI providers' OIDC issuers (discovery documents and key sets of locally generated keys).
    /// </summary>
    public FakeOidcIssuers OidcIssuers { get; } = new();

    /// <summary>
    /// Build services that pulled packages are downloaded from.
    /// </summary>
    public FakeDownloadServer Downloads { get; } = new();

    /// <summary>
    /// Google's sign in endpoints (OpenID Connect).
    /// </summary>
    public FakeGoogle Google { get; } = new();

    /// <summary>
    /// Browsers' push services.
    /// </summary>
    public FakePushService PushService { get; } = new();

    /// <summary>
    /// The Anthropic API of the AI reply drafts (the real SDK talks to it; no test reaches Anthropic).
    /// </summary>
    public FakeAnthropic Anthropic { get; } = new();

    /// <summary>
    /// VAPID key pair of the test hosts (generated once per run; base64url public point and private scalar).
    /// </summary>
    public static (string PublicKey, string PrivateKey) VapidKeys { get; } = CreateVapidKeys();

    private static int _databaseSequence;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").WithDatabase("app_tests")
                                                                                               .WithUsername("tests")
                                                                                               .WithPassword("tests")
                                                                                               .WithCleanUp(true)
                                                                                               .Build();

    // Redis backs the [Cache] interceptor, the token revocation stamps, the feature flag cache and the rate limit counters.
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").WithCleanUp(true).Build();

    /// <summary>
    /// Connection string of the shared test database.
    /// </summary>
    public string ConnectionString => $"{_postgres.GetConnectionString()};Include Error Detail=true";

    /// <summary>
    /// Redis endpoint (host:port) in the form <see cref="RedisConnectionStringKey"/> expects.
    /// </summary>
    public string RedisConnectionString => _redis.GetConnectionString();

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        // Program.cs reads these while REGISTERING services (NpgsqlDataSourceBuilder, Redis accessor, Serilog sinks), i.e.
        // before a WebApplicationFactory configuration callback gets a chance to run, so they must already be in the
        // environment when the host is created. One factory per test process, so process-wide is fine.
        SetEnvironment(Overrides());

        // Development: dev-only behaviour (missing resx key report, developer endpoints) as on a developer machine.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable(MilvaEnvironmentKey, "dev");

        // Hard guarantee that no test ever reaches the network: every HttpClient in this process is routed to a closed
        // loopback port and fails immediately instead of hitting a real provider. Postgres/Redis (Npgsql/StackExchange)
        // and the Docker socket used by Testcontainers do not go through HttpClient, so they are unaffected.
        HttpClient.DefaultProxy = new WebProxy("http://127.0.0.1:1");

        await CreateSchemaAsync(ConnectionString);

        // Force the host to build (startup migration + feature flag rows) so the first test does not pay for it.
        _ = Server;

        await SeedAsync();
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Also appended to the app configuration for everything read lazily, and so that a variable set by a CI agent
        // (e.g. CONNECTIONSTRINGS__DEFAULTCONNECTIONSTRING) can never point the tests at a real database.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(Overrides()));

        builder.ConfigureTestServices(services =>
        {
            // Under the test host the entry assembly is the test runner, so MVC does not discover the API's
            // controllers on its own (the OpenAPI document comes back with no paths). Register the part explicitly.
            services.AddControllers().AddApplicationPart(typeof(IApiMarker).Assembly);

            services.RemoveAll<IStoreGateway>();
            services.AddSingleton<IStoreGateway>(StoreGateway);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // The content engine talks HTTP to the stores; every call goes to the in-memory fakes.
            services.AddHttpClient(Storeasy.Infrastructure.Stores.StoreGateway.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Stores);

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(FileStorage);

            // Microsoft Store client: real code over a fake Microsoft, no waiting between retries or polls; the fake
            // download host resolves nowhere, so the public address guard is off.
            services.AddHttpClient(MicrosoftStoreClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => MicrosoftStore);
            services.RemoveAll<MicrosoftStoreOptions>();
            services.AddSingleton(new MicrosoftStoreOptions { RetryBaseDelayMilliseconds = 0, MaxRetryDelaySeconds = 0, StatusPollIntervalMilliseconds = 0, StatusPollAttempts = 3, BlockPrivateDownloadAddresses = false });

            // A job kind that exists only in the tests, so the queue and the runner are tested without a store.
            services.AddScoped<IStoreJobHandler, FakeStoreJobHandler>();

            // Events phase: store calls, rules catalog, official pages, DNS and the outgoing webhook receivers.
            services.RemoveAll<IStoreEventGateway>();
            services.AddSingleton<IStoreEventGateway>(StoreEvents);
            services.RemoveAll<IStoreReviewGateway>();
            services.AddSingleton<IStoreReviewGateway>(StoreEvents);
            services.RemoveAll<ITestFlightGateway>();
            services.AddSingleton<ITestFlightGateway>(StoreEvents);
            services.RemoveAll<IRuleSourceFetcher>();
            services.AddSingleton<IRuleSourceFetcher>(RulePages);
            services.RemoveAll<IHostResolver>();
            services.AddSingleton<IHostResolver>(HostResolver);
            services.AddHttpClient(Storeasy.Infrastructure.Notifications.OrgWebhookSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(WebhookReceiver.CreateHandler);

            // Intake phase: OIDC issuers, pulled package downloads and the bucket's ranged reads and part listings.
            services.AddHttpClient(Storeasy.Infrastructure.Intake.OidcTokenValidator.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => OidcIssuers);
            services.AddHttpClient(Storeasy.Infrastructure.Intake.ArtifactDownloader.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Downloads);

            // Phase 8: Google sign in and browser push services.
            services.AddHttpClient(Storeasy.Infrastructure.Services.GoogleSignInService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Google);
            services.AddHttpClient(Storeasy.Infrastructure.Events.GooglePushTokenValidator.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Google);
            services.AddHttpClient(Storeasy.Infrastructure.Notifications.WebPush.WebPushSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => PushService);
            services.RemoveAll<IArtifactStorage>();
            services.AddSingleton<IArtifactStorage>(FileStorage);

            // AI reply drafts: the SDK's HTTP client answers from the fake.
            services.AddHttpClient(Storeasy.Infrastructure.Ai.AnthropicReplyDraftWriter.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Anthropic);
        });
    }

    /// <summary>
    /// Creates a service scope on the running host (for direct database / service assertions).
    /// </summary>
    /// <returns></returns>
    public IServiceScope CreateScope() => Services.CreateScope();

    /// <summary>
    /// Deletes every Redis key (cached stamps and flags, rate limit counters). Runs after every test.
    /// </summary>
    /// <returns></returns>
    public Task FlushRedisAsync() => _redis.ExecAsync(["redis-cli", "FLUSHALL"]);

    /// <summary>
    /// Builds a separate host for a test that needs a different configuration (<paramref name="overrides"/>, e.g. an
    /// unreachable Redis or another database) or different services (<paramref name="configureServices"/>, e.g. a
    /// fake). The overrides are put into the environment only while the host is built, because the application reads
    /// them while registering its services. The caller disposes the host.
    /// </summary>
    /// <param name="overrides"></param>
    /// <param name="configureServices"></param>
    /// <returns></returns>
    public WebApplicationFactory<IApiMarker> CreateHost(Dictionary<string, string> overrides = null, Action<IServiceCollection> configureServices = null)
    {
        overrides ??= [];

        // Only the shared host runs store jobs: a second runner on the same database would take jobs whose handler or
        // fakes live in the shared host.
        overrides.TryAdd(StoreJobsEnabledKey, "false");
        overrides.TryAdd("Storeasy:Email:Outbox:Enabled", "false");

        var previous = overrides.Keys.ToDictionary(key => key, key => Environment.GetEnvironmentVariable(EnvironmentName(key)));

        SetEnvironment(overrides);

        try
        {
            var host = WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(overrides));

                builder.ConfigureTestServices(services => configureServices?.Invoke(services));
            });

            _ = host.Server;

            return host;
        }
        finally
        {
            SetEnvironment(previous);
        }
    }

    /// <summary>
    /// Sets <c>MILVA_ENV</c> to <paramref name="value"/> (empty removes it) until the returned scope is disposed, which
    /// restores <c>dev</c>. The tests of the collection run one after another, so the process-wide change is safe.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static IDisposable UseMilvaEnvironment(string value)
    {
        Environment.SetEnvironmentVariable(MilvaEnvironmentKey, value);

        return new MilvaEnvironmentScope();
    }

    /// <summary>
    /// Creates a new, empty database (schema only, no seed) in the test PostgreSQL and returns its connection string,
    /// for a test that needs a database nobody initialized yet.
    /// </summary>
    /// <returns></returns>
    public async Task<string> CreateEmptyDatabaseAsync()
    {
        var databaseName = $"app_tests_{Interlocked.Increment(ref _databaseSequence)}";

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);

            await command.ExecuteNonQueryAsync();
        }

        var connectionString = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName }.ConnectionString;

        await CreateSchemaAsync(connectionString);

        return connectionString;
    }

    /// <summary>
    /// Creates a new database without any table (not even the migrations), for a test of what the application does on
    /// its own with a database nobody prepared.
    /// </summary>
    /// <returns></returns>
    public async Task<string> CreateBlankDatabaseAsync()
    {
        var databaseName = $"app_tests_{Interlocked.Increment(ref _databaseSequence)}";

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{databaseName}\"", connection);

            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName }.ConnectionString;
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());

        GC.SuppressFinalize(this);
    }

    private Dictionary<string, string> Overrides() => new()
    {
        [ConnectionStringKey] = ConnectionString,
        [RedisConnectionStringKey] = RedisConnectionString,
        [StartupExtensions.TokenSigningKeyConfigurationKey] = TokenSigningKey,
        ["Storeasy:Encryption:MasterKey"] = EncryptionMasterKey,
        [StoreJobsEnabledKey] = "true",
        [DeploymentModeKey] = "MultiTenant",
        ["Storeasy:Jobs:PollIntervalMilliseconds"] = "100",
        ["Storeasy:Jobs:RetryBaseDelaySeconds"] = "0",
        ["Storeasy:Stores:RetryBaseDelayMilliseconds"] = "1",
        ["Storeasy:Stores:AssetPollIntervalMilliseconds"] = "10",
        ["Storeasy:Stores:UploadChunkBytes"] = "262144",
        ["Storeasy:Events:SchedulesEnabled"] = "false",
        ["Storeasy:OrgWebhooks:PollIntervalMilliseconds"] = "100",
        ["Storeasy:OrgWebhooks:RetryBaseDelaySeconds"] = "0",
        ["Storeasy:OrgWebhooks:MaxAttempts"] = "3",
        ["Storeasy:Email:Outbox:Enabled"] = "true",
        ["Storeasy:Email:Outbox:PollIntervalMilliseconds"] = "50",
        ["Storeasy:Email:Outbox:RetryBaseDelaySeconds"] = "0",
        ["Storeasy:Auth:Google:ClientId"] = FakeGoogle.ClientId,
        ["Storeasy:Auth:Google:ClientSecret"] = FakeGoogle.ClientSecret,
        ["Storeasy:Auth:Google:Authority"] = FakeGoogle.Authority,
        ["Storeasy:Webhooks:GooglePushIssuers:0"] = FakeGoogle.Authority,
        ["Storeasy:Webhooks:GooglePushKeysUrl"] = FakeGoogle.Authority + "/oauth2/v3/certs",
        ["Storeasy:WebPush:Subject"] = "mailto:tests@example.com",
        ["Storeasy:WebPush:PublicKey"] = VapidKeys.PublicKey,
        ["Storeasy:WebPush:PrivateKey"] = VapidKeys.PrivateKey,
        ["Serilog:MinimumLevel:Default"] = "Warning",
        ["Serilog:MinimumLevel:Override:Microsoft"] = "Warning",
        ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore"] = "Warning",
    };

    /// <summary>
    /// Creates the schema the way the application does: from the migrations once the project has any, from the model
    /// until then (the template ships without migrations; the first one is added in the generated project).
    /// </summary>
    private static async Task CreateSchemaAsync(string connectionString)
    {
        await using var dataSource = new NpgsqlDataSourceBuilder(connectionString).EnableDynamicJson().Build();

        var options = new DbContextOptionsBuilder<StoreasyDbContext>().UseNpgsql(dataSource, b => b.MigrationsHistoryTable(TableNames.EfMigrationHistory).MigrationsAssembly("Storeasy.Api"))
                                                                     .ConfigureWarnings(warnings => warnings.Log(RelationalEventId.PendingModelChangesWarning))
                                                                     .Options;

        await using var context = new StoreasyDbContext(options);

        if (context.Database.GetMigrations().Any())
            await context.Database.MigrateAsync();
        else
            await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Seeds the shared database like <c>database/init</c>: root user (<see cref="TestApi.RootPassword"/>) with the
    /// SuperAdmin role, the seed protection triggers (ids below 21 of the seed tables are read only), the permissions of
    /// the catalog and the languages.
    /// </summary>
    private async Task SeedAsync()
    {
        using var scope = CreateScope();

        var migrator = new DatabaseMigrator(scope.ServiceProvider);

        await migrator.SeedDefaultDataAsync(TestApi.RootPassword);

        await migrator.CreateTriggersAsync(CancellationToken.None);

        await DatabaseMigrator.MigratePermissionsAsync(scope.ServiceProvider.GetRequiredService<IPermissionManager>());

        await LanguageSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<StoreasyDbContext>());
    }

    /// <summary>
    /// Sets each key both in its conventional form (Section__Key) and upper case: a CI agent (e.g. Azure DevOps) exports
    /// its variables upper cased, and the configuration would pick either spelling.
    /// </summary>
    private static void SetEnvironment(Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            Environment.SetEnvironmentVariable(EnvironmentName(key), value);
            Environment.SetEnvironmentVariable(EnvironmentName(key).ToUpperInvariant(), value);
        }
    }

    private static string EnvironmentName(string key) => key.Replace(":", "__");

    private static (string PublicKey, string PrivateKey) CreateVapidKeys()
    {
        using var key = System.Security.Cryptography.ECDiffieHellman.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var parameters = key.ExportParameters(true);

        return (System.Buffers.Text.Base64Url.EncodeToString([0x04, .. parameters.Q.X, .. parameters.Q.Y]), System.Buffers.Text.Base64Url.EncodeToString(parameters.D));
    }
}

/// <summary>
/// Restores <c>MILVA_ENV=dev</c> when disposed (see <see cref="StoreasyApiFactory.UseMilvaEnvironment"/>).
/// </summary>
internal sealed class MilvaEnvironmentScope : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Environment.SetEnvironmentVariable(StoreasyApiFactory.MilvaEnvironmentKey, "dev");
}

/// <summary>
/// The single collection of the integration tests: one <see cref="StoreasyApiFactory"/> (one host, one database) for the
/// whole run. Tests of one collection run one after another.
/// </summary>
[CollectionDefinition(nameof(StoreasyApiCollection))]
public class StoreasyApiCollection : ICollectionFixture<StoreasyApiFactory>
{
}
