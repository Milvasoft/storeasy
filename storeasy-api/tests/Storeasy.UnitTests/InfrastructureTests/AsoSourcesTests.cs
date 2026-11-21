using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Aso;
using Storeasy.UnitTests.TestSupport;
using System.Net;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "ASO store sources: the response readers on recorded answers, and the polite HTTP access (pacing, cache, User-Agent, failures).")]
public class AsoSourcesTests
{
    [Fact]
    public void ITunesSearch_ShouldReadTheSoftwareResultsInOrder()
    {
        var result = AsoResponseParsers.ParseITunesSearch(Fixture("aso-itunes-search.json"));

        result.ResultCount.Should().Be(3, "the audiobook is not an app");
        result.Apps.Select(a => a.Id).Should().Equal("1067456176", "778415974", "900837187");
        result.Apps[0].Should().Match<Domain.JsonModels.AsoCompetitor>(a => a.Rank == 1
                                                                            && a.Name == "Compass"
                                                                            && a.Developer == "Apple Distribution International"
                                                                            && a.RatingCount == 139
                                                                            && a.Rating > 3.09 && a.Rating < 3.1
                                                                            && a.IconUrl.EndsWith("100x100bb.jpg"));
        result.Apps[2].Rank.Should().Be(3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>busy</html>")]
    [InlineData("{\"errorMessage\": \"Invalid value(s) for key(s): [country]\"}")]
    public void ITunesSearch_ShouldNotReadAnUnexpectedAnswer(string body) => AsoResponseParsers.ParseITunesSearch(body).Should().BeNull();

    [Fact]
    public void AppleHints_ShouldReadThePropertyListWithoutResolvingItsDoctype()
        => AsoResponseParsers.ParseAppleHints(Fixture("aso-apple-hints.xml")).Should().Equal("compass", "compass app", "Compass Pro");

    [Fact]
    public void AppleHints_ShouldReadTheJsonForm()
        => AsoResponseParsers.ParseAppleHints(Fixture("aso-apple-hints.json")).Should().Equal("pusula", "pusula kıble", "pusula pro");

    [Theory]
    [InlineData("")]
    [InlineData("<plist><dict><key>hints</key><string>x</string></dict></plist>")]
    [InlineData("<broken")]
    public void AppleHints_ShouldBeEmptyForAnUnexpectedAnswer(string body) => AsoResponseParsers.ParseAppleHints(body).Should().BeEmpty();

    [Fact]
    public void GooglePlaySuggest_ShouldKeepTheQuerySuggestionsOnly()
        => AsoResponseParsers.ParseGooglePlaySuggest(Fixture("aso-google-suggest.json")).Should().Equal("compass", "compass app", "compass for android");

    [Fact]
    public void GooglePlaySuggest_ShouldReadTheQueryAndTermsForm()
        => AsoResponseParsers.ParseGooglePlaySuggest("[\"comp\", [\"compass\", \"compass app\"]]").Should().Equal("compass", "compass app");

    [Fact]
    public void GooglePlaySearch_ShouldListTheDistinctPackagesInOrder()
    {
        var result = AsoResponseParsers.ParseGooglePlaySearch(Fixture("aso-google-search.html"));

        result.Apps.Select(a => (a.Rank, a.Id)).Should().Equal((1, "com.example.compass"), (2, "org.maps.compass.pro"), (3, "net.tools.qibla_finder"));
        AsoResponseParsers.ParseGooglePlaySearch("<html>Before you continue to Google</html>").Should().BeNull("a page without app links is not an empty search");
    }

    [Fact]
    public async Task Http_ShouldPaceRequestsPerHost_CacheAnswers_AndSendTheProductUserAgent()
    {
        var requests = new List<(DateTimeOffset At, HttpRequestMessage Request)>();
        var http = Http(new AsoOptions { MinRequestIntervalMilliseconds = 300 }, TimeProvider.System, request =>
        {
            requests.Add((DateTimeOffset.UtcNow, request));

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"answer {requests.Count}") };
        });

        var first = await http.GetStringAsync(new Uri("https://itunes.apple.com/search?term=a"), null, TestContext.Current.CancellationToken);
        var cached = await http.GetStringAsync(new Uri("https://itunes.apple.com/search?term=a"), null, TestContext.Current.CancellationToken);
        var second = await http.GetStringAsync(new Uri("https://itunes.apple.com/search?term=b"), new Dictionary<string, string> { ["X-Apple-Store-Front"] = "143480-1,29" }, TestContext.Current.CancellationToken);
        var otherHost = await http.GetStringAsync(new Uri("https://search.itunes.apple.com/hints?term=b"), null, TestContext.Current.CancellationToken);

        first.Should().Be("answer 1");
        cached.Should().Be("answer 1", "the same URL is answered from the cache");
        second.Should().Be("answer 2");
        otherHost.Should().Be("answer 3");
        requests.Should().HaveCount(3);
        (requests[1].At - requests[0].At).Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(250), "the same host waits for the interval");
        (requests[2].At - requests[1].At).Should().BeLessThan(TimeSpan.FromMilliseconds(250), "another host does not wait");
        requests[0].Request.Headers.UserAgent.ToString().Should().Be(AsoOptions.DefaultUserAgent);
        requests[1].Request.Headers.GetValues("X-Apple-Store-Front").Should().Equal("143480-1,29");
    }

    [Fact]
    public async Task Http_ShouldAnswerNullForAFailureAndNotCacheIt()
    {
        var calls = 0;
        var http = Http(new AsoOptions { MinRequestIntervalMilliseconds = 0 }, TimeProvider.System, _ =>
        {
            calls++;

            if (calls == 2)
                throw new HttpRequestException("offline (test)");

            return new HttpResponseMessage(calls == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent("ok") };
        });
        var url = new Uri("https://search.itunes.apple.com/WebObjects/MZSearchHints.woa/wa/hints?term=x");

        (await http.GetStringAsync(url, null, TestContext.Current.CancellationToken)).Should().BeNull();
        (await http.GetStringAsync(url, null, TestContext.Current.CancellationToken)).Should().BeNull();
        (await http.GetStringAsync(url, null, TestContext.Current.CancellationToken)).Should().Be("ok");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task GooglePlaySearch_ShouldNotBeCalledWhileItsSwitchIsOff()
    {
        var calls = 0;
        var options = new AsoOptions { MinRequestIntervalMilliseconds = 0 };
        var source = new GooglePlaySearchSource(Http(options, TimeProvider.System, _ =>
        {
            calls++;

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Fixture("aso-google-search.html")) };
        }), options);

        source.IsEnabled.Should().BeFalse("reading Play search pages is off by default");
        (await source.SearchAsync("compass", "us", "en", 200, TestContext.Current.CancellationToken)).Should().BeNull();
        calls.Should().Be(0);

        options.GooglePlayRanksEnabled = true;

        (await source.SearchAsync("compass", "us", "en", 2, TestContext.Current.CancellationToken)).Apps.Should().HaveCount(2);
        calls.Should().Be(1);
    }

    private static AsoHttp Http(AsoOptions options, TimeProvider time, Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new(new SingleClientFactory(new FakeHandler(respond)), options, time, NullLogger<AsoHttp>.Instance);

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "InfrastructureTests", "Fixtures", name));

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
