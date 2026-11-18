using FluentAssertions;
using Storeasy.Domain.JsonModels;

namespace Storeasy.UnitTests.ComponentTests;

[Trait("Json Model's Property Getter Setters Unit Tests", "Json models property getter setter unit tests.")]
public class JsonModelTests
{
    [Fact]
    public void RequestInfo_PropertyGetterSetter_ShouldWorkCorrectly()
    {
        // Arrange
        var method = "GET";
        var absoluteUri = "https://example.com";
        var queryString = "?param=value";
        var headers = new { Header1 = "value1", Header2 = "value2" };
        var contentLength = 12345L;
        var body = new { Key = "value" };

        // Act
        var requestInfo = new RequestInfo
        {
            Method = method,
            AbsoluteUri = absoluteUri,
            QueryString = queryString,
            Headers = headers,
            ContentLength = contentLength,
            Body = body,
        };

        // Assert
        requestInfo.Method.Should().Be(method);
        requestInfo.AbsoluteUri.Should().Be(absoluteUri);
        requestInfo.QueryString.Should().Be(queryString);
        requestInfo.Headers.Should().Be(headers);
        requestInfo.ContentLength.Should().Be(contentLength);
        requestInfo.Body.Should().Be(body);
    }
}