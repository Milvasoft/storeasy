using Microsoft.EntityFrameworkCore;
using Storeasy.Application.Dtos.ContentDtos;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Organizations;
using Storeasy.Domain.Stores;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Shared setup of the content engine tests: an organization with an App Store and a Play connection, store records
/// pointing at the fake stores, members of each role, assets in the fake storage and job waiting.
/// </summary>
public abstract class ContentTestBase(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    protected sealed record Org(TestUser Owner, int OrganizationId, int AppleConnectionId, int GoogleConnectionId, string Suffix);

    protected sealed record Record(int AppId, int StoreAppId, string ExternalId);

    protected async Task<Org> NewOrgAsync()
    {
        var owner = await _factory.CreateAndLoginUserAsync();
        var (_, organization) = await owner.Client.PostAsync<int>("organizations/organization", new { name = $"Org {TestApi.Unique()}" });

        using var appleKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var googleKey = RSA.Create(2048);

        var appleSecret = JsonSerializer.Serialize(new { issuerId = "57246542-96fe-1a63-e053-0824d011072a", keyId = "ABCDEFGHJK", privateKey = appleKey.ExportPkcs8PrivateKeyPem() });
        var googleSecret = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["type"] = "service_account",
            ["project_id"] = "demo",
            ["private_key"] = googleKey.ExportPkcs8PrivateKeyPem(),
            ["client_email"] = "store@demo.iam.gserviceaccount.com",
        });

        var (_, apple) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store = StoreKind.Apple, name = "ASC", secret = appleSecret });
        var (_, google) = await owner.Client.PostAsync<int>("storeconnections/connection", new { organizationId = organization.Data, store = StoreKind.Google, name = "Play", secret = googleSecret });

        return new Org(owner, organization.Data, apple.Data, google.Data, $"t{TestApi.Unique()}");
    }

    /// <summary>
    /// A storeasy app with an App Store record (and a Play record when <paramref name="package"/> is given) linked to the
    /// fake stores.
    /// </summary>
    protected async Task<(Record Apple, Record Google)> NewAppAsync(Org org, string appleId, string package = null, AppProfile profile = null)
    {
        var app = new App { OrganizationId = org.OrganizationId, Name = "Pusula", Profile = profile, StoreApps = [] };

        if (appleId != null)
            app.StoreApps.Add(new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Apple, ExternalId = appleId, BundleId = $"com.{org.Suffix}.pusula", Name = "Pusula", StoreConnectionId = org.AppleConnectionId, SyncedDate = DateTime.UtcNow });

        if (package != null)
            app.StoreApps.Add(new StoreApp { OrganizationId = org.OrganizationId, Store = StoreKind.Google, ExternalId = package, BundleId = package, Name = "Pusula", StoreConnectionId = org.GoogleConnectionId, SyncedDate = DateTime.UtcNow });

        await _factory.DbAsync(async db =>
        {
            db.Apps.Add(app);
            await db.SaveChangesAsync();
        });

        var apple = app.StoreApps.FirstOrDefault(s => s.Store == StoreKind.Apple);
        var google = app.StoreApps.FirstOrDefault(s => s.Store == StoreKind.Google);

        return (apple == null ? null : new Record(app.Id, apple.Id, apple.ExternalId), google == null ? null : new Record(app.Id, google.Id, google.ExternalId));
    }

    /// <summary>
    /// A logged in member of <paramref name="org"/> with <paramref name="role"/>.
    /// </summary>
    protected async Task<TestUser> MemberAsync(Org org, OrgRole role)
    {
        var user = await _factory.CreateAndLoginUserAsync();

        await _factory.DbAsync(async db =>
        {
            db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = org.OrganizationId, UserId = user.Id, Role = role });
            await db.SaveChangesAsync();
        });

        return user;
    }

    /// <summary>
    /// An uploaded asset of the organization with <paramref name="content"/> in the fake storage.
    /// </summary>
    protected async Task<long> AssetAsync(Org org, byte[] content, string fileName, string contentType, AssetPurpose purpose = AssetPurpose.Screenshot)
    {
        var asset = new Asset
        {
            OrganizationId = org.OrganizationId,
            Purpose = purpose,
            ObjectKey = $"org/{org.OrganizationId}/test/{Guid.NewGuid():N}/{fileName}",
            FileName = fileName,
            ContentType = contentType,
            Size = content.Length,
            Status = AssetStatus.Uploaded,
            UploadDate = DateTime.UtcNow,
        };

        await _factory.DbAsync(async db =>
        {
            db.Assets.Add(asset);
            await db.SaveChangesAsync();
        });

        _factory.FileStorage.Put(asset.ObjectKey, content, contentType);

        return asset.Id;
    }

    protected async Task<StoreJob> WaitForJobAsync(long jobId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var job = await _factory.DbAsync(db => db.StoreJobs.AsNoTracking().SingleAsync(j => j.Id == jobId));

            if (StoreJobRules.IsFinished(job.Status))
                return job;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Job {jobId} is still {job.Status}.");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    protected async Task<ChangePlanDto> ApproveAndWaitAsync(TestUser user, int organizationId, long planId, string confirm = null)
    {
        var (_, approved) = await user.Client.PostAsync<ChangePlanDto>("plans/approve", new { organizationId, planId, confirm });

        if (approved?.Data?.JobId == null)
            throw new InvalidOperationException($"Approval failed: {approved?.MessagesToString()}");

        var job = await WaitForJobAsync(approved.Data.JobId.Value);

        if (job.Status != StoreJobStatus.Succeeded)
            throw new InvalidOperationException($"Job {job.Id} {job.Status}: {job.Error}");

        var (_, plan) = await user.Client.GetAsync<ChangePlanDto>($"plans/plan?organizationId={organizationId}&planId={planId}");

        return plan.Data;
    }

    /// <summary>
    /// A PNG of <paramref name="width"/> x <paramref name="height"/> (header and a token data chunk; the engine reads the
    /// header only), with or without an alpha channel. <paramref name="salt"/> makes the bytes (and the checksums) unique.
    /// </summary>
    protected static byte[] Png(int width, int height, bool alpha = false, int salt = 0)
    {
        var bytes = new List<byte>([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];

        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height);
        header[8] = 8;
        header[9] = (byte)(alpha ? 6 : 2);

        Chunk(bytes, "IHDR", header);
        Chunk(bytes, "IDAT", [.. BitConverter.GetBytes(salt), .. RandomNumberGenerator.GetBytes(64)]);
        Chunk(bytes, "IEND", []);

        return [.. bytes];
    }

    private static void Chunk(List<byte> bytes, string type, byte[] data)
    {
        var length = new byte[4];

        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        bytes.AddRange(length);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(type));
        bytes.AddRange(data);
        bytes.AddRange(new byte[4]);
    }
}
