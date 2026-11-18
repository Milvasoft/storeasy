using Microsoft.EntityFrameworkCore;
using Milvasoft.Components.Rest.MilvaResponse;
using Storeasy.Application.Dtos.IntakeDtos;
using Storeasy.Application.Dtos.TokenDtos;
using Storeasy.Domain.Intake;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Shared setup of the token, intake and MCP tests: personal access tokens, CI tokens, uploading a package through the
/// session API into the fake bucket and waiting for a session to become final.
/// </summary>
public abstract class IntakeTestBase(StoreasyApiFactory factory) : ContentTestBase(factory)
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Creates a personal access token of <paramref name="user"/> in <paramref name="organizationId"/>.
    /// </summary>
    protected static async Task<string> PatAsync(TestUser user, int organizationId, bool readOnly = false)
    {
        var (_, created) = await user.Client.PostAsync<PersonalAccessTokenCreatedDto>("tokens/token", new { organizationId, name = $"pat {TestApi.Unique()}", readOnly, expiryDays = 30 });

        return created?.Data?.Token ?? throw new InvalidOperationException($"PAT creation failed: {created?.MessagesToString()}");
    }

    /// <summary>
    /// Creates a CI token for <paramref name="appIds"/>.
    /// </summary>
    protected static async Task<string> CiTokenAsync(TestUser owner, int organizationId, params int[] appIds)
    {
        var (_, created) = await owner.Client.PostAsync<CiTokenCreatedDto>("artifacts/ci-token", new { organizationId, name = $"ci {TestApi.Unique()}", appIds, allApps = appIds.Length == 0, expiryDays = 30 });

        return created?.Data?.Token ?? throw new InvalidOperationException($"CI token creation failed: {created?.MessagesToString()}");
    }

    /// <summary>
    /// A client that sends <paramref name="token"/>.
    /// </summary>
    protected HttpClient TokenClient(string token) => _factory.CreateClientWithAddress().UseAccessToken(token);

    /// <summary>
    /// Opens a session for <paramref name="content"/>, puts every missing part into the fake bucket and completes it.
    /// </summary>
    protected async Task<(long SessionId, ArtifactUploadDto Open, Response<ArtifactSessionListDto> Complete)> UploadAsync(HttpClient client, int appId, string fileName, byte[] content, object delivery = null, int? skipPart = null)
    {
        var sha256 = PackageFactory.Sha256(content);
        var (openHttp, open) = await client.PostAsync<ArtifactUploadDto>($"apps/{appId}/artifacts", new { fileName, size = content.Length, sha256, source = 4, delivery });

        if (open?.IsSuccess != true)
            throw new InvalidOperationException($"Open failed ({(int)openHttp.StatusCode}): {open?.MessagesToString()}");

        if (open.Data.Duplicate)
            return (open.Data.SessionId, open.Data, null);

        var parts = PutParts(open.Data, content, skipPart);
        var (_, complete) = await client.PutAsync<ArtifactSessionListDto>($"artifacts/{open.Data.SessionId}/complete", new { parts });

        return (open.Data.SessionId, open.Data, complete);
    }

    /// <summary>
    /// Puts the parts of <paramref name="open"/> into the fake bucket and returns every part with its ETag.
    /// </summary>
    protected List<object> PutParts(ArtifactUploadDto open, byte[] content, int? skipPart = null)
    {
        var partSize = (int)open.PartSize.Value;
        var parts = open.UploadedParts.Select(p => (object)new { partNumber = p.PartNumber, eTag = p.ETag }).ToList();

        foreach (var part in open.Parts)
        {
            if (part.PartNumber == skipPart)
                continue;

            var uploadId = HttpUtility.ParseQueryString(new Uri(part.Url).Query)["uploadId"];
            var offset = (part.PartNumber - 1) * partSize;
            var bytes = content.AsSpan(offset, Math.Min(partSize, content.Length - offset)).ToArray();
            var etag = _factory.FileStorage.PutPart(uploadId, part.PartNumber, bytes);

            parts.Add(new { partNumber = part.PartNumber, eTag = etag });
        }

        return parts;
    }

    /// <summary>
    /// Waits until the session is final and returns it.
    /// </summary>
    protected async Task<ArtifactSession> WaitForSessionAsync(long sessionId)
    {
        var deadline = DateTime.UtcNow + _timeout;

        while (true)
        {
            var session = await _factory.DbAsync(db => db.ArtifactSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId));

            if (IntakeRules.IsFinal(session.Status) && session.CiExitCode != null)
                return session;

            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Session {sessionId} is still {session.Status}: {session.StatusDescription}");

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Sends <paramref name="method"/> with a JSON body (DELETE with a body).
    /// </summary>
    protected static async Task<(HttpResponseMessage Http, Response<T> Body)> SendAsync<T>(HttpClient client, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, $"{TestApi.Base}/{path}") { Content = JsonContent.Create(body) };

        var http = await client.SendAsync(request);
        var text = await http.Content.ReadAsStringAsync();

        return (http, string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<Response<T>>(text, TestApi.Json));
    }

    /// <summary>
    /// The message keys of an envelope.
    /// </summary>
    protected static IEnumerable<string> Keys(IResponse response) => response?.Messages?.Select(m => m.Key) ?? [];
}
