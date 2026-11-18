using Milvasoft.Components.Rest.MilvaResponse;
using System.Net.Http.Json;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Requests with a JSON body for any method (DELETE with a body, as the API's delete endpoints take).
/// </summary>
public static class HttpExtensions
{
    /// <summary>Sends <paramref name="body"/> with <paramref name="method"/> and reads the envelope.</summary>
    public static async Task<(HttpResponseMessage Http, Response<T> Body)> SendAsync<T>(this HttpClient client, HttpMethod method, string path, object body)
    {
        using var request = new HttpRequestMessage(method, $"{TestApi.Base}/{path}") { Content = JsonContent.Create(body, options: TestApi.Json) };

        var http = await client.SendAsync(request);

        return (http, await TestApi.ReadAsync<Response<T>>(http));
    }
}
