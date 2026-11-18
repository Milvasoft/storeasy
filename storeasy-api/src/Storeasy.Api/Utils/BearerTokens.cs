namespace Storeasy.Api.Utils;

/// <summary>
/// Reads the access token a request carries.
/// </summary>
public static class BearerTokens
{
    /// <summary>
    /// The bearer token of the <c>Authorization</c> header, or the <c>access_token</c> query value (websockets of the
    /// activity hub); null when there is none.
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static string Read(HttpRequest request)
    {
        if (request == null)
            return null;

        var header = request.Headers.Authorization.ToString();

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header[7..].Trim();

        var query = request.Query["access_token"].ToString();

        return string.IsNullOrEmpty(query) ? null : query;
    }
}
