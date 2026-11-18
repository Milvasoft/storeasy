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
    /// The Microsoft every test host talks to (Entra, both Microsoft Store APIs, blob uploads, installer downloads).
    /// </summary>
    public FakeMicrosoftStore MicrosoftStore { get; } = new();

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
        Environment.SetEnvironmentVariable("MILVA_ENV", "dev");

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

            services.RemoveAll<IFileStorage>();
            services.AddSingleton<IFileStorage>(FileStorage);

            // Microsoft Store client: real code over a fake Microsoft, no waiting between retries or polls; the fake
            // download host resolves nowhere, so the public address guard is off.
            services.AddHttpClient(MicrosoftStoreClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => MicrosoftStore);
            services.RemoveAll<MicrosoftStoreOptions>();
            services.AddSingleton(new MicrosoftStoreOptions { RetryBaseDelayMilliseconds = 0, MaxRetryDelaySeconds = 0, StatusPollIntervalMilliseconds = 0, StatusPollAttempts = 3, BlockPrivateDownloadAddresses = false });

            // A job kind that exists only in the tests, so the queue and the runner are tested without a store.
            services.AddScoped<IStoreJobHandler, FakeStoreJobHandler>();
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
        ["Storeasy:Jobs:PollIntervalMilliseconds"] = "100",
        ["Storeasy:Jobs:RetryBaseDelaySeconds"] = "0",
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
}

/// <summary>
/// The single collection of the integration tests: one <see cref="StoreasyApiFactory"/> (one host, one database) for the
/// whole run. Tests of one collection run one after another.
/// </summary>
[CollectionDefinition(nameof(StoreasyApiCollection))]
public class StoreasyApiCollection : ICollectionFixture<StoreasyApiFactory>
{
}
