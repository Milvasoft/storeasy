using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Milvasoft.Core.Abstractions;
using Moq;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Models;
using Storeasy.Domain;
using Storeasy.Domain.Enums;
using Storeasy.Infrastructure.Email;
using Storeasy.UnitTests.TestSupport;
using System.Net;
using System.Text.Json;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "E-mail senders, templates and settings (no network).")]
public class EmailTests
{
    private static readonly EmailMessage _message = new("ada@example.com", "Ada", "tr-TR", "Subject", "<p>Body</p>", "https://app.example.com/verify-email?token=evt_abc");

    [Fact]
    public async Task Notifike_ShouldPostTheDocumentedBodyWithTheApiKeyHeader()
    {
        // Arrange
        HttpRequestMessage sent = null;
        string sentBody = null;

        var handler = new FakeHandler(request =>
        {
            sent = request;
            sentBody = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var sender = new NotifikeEmailSender(CreateFactory(handler, NotifikeEmailSender.HttpClientName), new EmailOptions
        {
            Provider = EmailProvider.Notifike,
            Notifike = new NotifikeEmailOptions { BaseUrl = "https://notifike.example.com/api/", ApiKey = "test-key" },
        });

        // Act
        await sender.SendAsync(_message, TestContext.Current.CancellationToken);

        // Assert
        sent.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri.ToString().Should().Be("https://notifike.example.com/api/notifications/send");
        sent.Headers.GetValues("X-Api-Key").Should().ContainSingle().Which.Should().Be("test-key");

        using var document = JsonDocument.Parse(sentBody);
        var root = document.RootElement;

        root.GetProperty("Channel").GetInt32().Should().Be(1);
        root.GetProperty("Subject").GetString().Should().Be("Subject");
        root.GetProperty("Body").GetString().Should().Be("<p>Body</p>");

        var recipient = root.GetProperty("Recipients").EnumerateArray().Should().ContainSingle().Subject;

        recipient.GetProperty("To").GetString().Should().Be("ada@example.com");
        recipient.GetProperty("Name").GetString().Should().Be("Ada");
        recipient.GetProperty("Language").GetString().Should().Be("tr-TR");
    }

    [Fact]
    public async Task Notifike_WhenTheApiRefuses_ShouldThrowWithoutTheKey()
    {
        // Arrange
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sender = new NotifikeEmailSender(CreateFactory(handler, NotifikeEmailSender.HttpClientName), new EmailOptions
        {
            Notifike = new NotifikeEmailOptions { BaseUrl = "https://notifike.example.com", ApiKey = "secret-key" },
        });

        // Act
        var act = () => sender.SendAsync(_message, TestContext.Current.CancellationToken);

        // Assert
        (await act.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().NotContain("secret-key").And.Contain("401");
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task LogSender_ShouldWriteTheLinkOnlyInDevelopment(string environmentName, bool expectLink)
    {
        // Arrange
        var logger = new Mock<IMilvaLogger>();
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(e => e.EnvironmentName).Returns(environmentName);

        var sender = new LogEmailSender(logger.Object, environment.Object);

        // Act
        await sender.SendAsync(_message, TestContext.Current.CancellationToken);

        // Assert
        logger.Verify(l => l.Information(It.IsAny<string>(), It.Is<object[]>(args => args.Contains(_message.ActionUrl) == expectLink)), Times.Once);
    }

    [Fact]
    public void Templates_ShouldUseTheResxKeysAndEncodeValues()
    {
        // Arrange
        var localizer = FakeLocalizer.Create().Object;
        const string link = "https://app.example.com/verify-email?token=a&b=<x>";

        // Act
        var (subject, html) = EmailTemplates.EmailVerification(localizer, "<script>", link);
        var (invitationSubject, invitationHtml) = EmailTemplates.Invitation(localizer, "Acme", "owner@example.com", "Editor", link);

        // Assert
        subject.Should().Be(MessageKey.EmailVerifySubject);
        html.Should().Contain(MessageKey.EmailVerifyTitle).And.Contain(MessageKey.EmailVerifyAction);
        html.Should().NotContain("<script>").And.Contain("&lt;script&gt;");
        html.Should().Contain("token=a&amp;b=&lt;x&gt;");
        invitationSubject.Should().Be($"{MessageKey.EmailInvitationSubject}|Acme");
        invitationHtml.Should().Contain("owner@example.com,Acme,Editor");
    }

    [Theory]
    [InlineData(EmailProvider.Smtp, null, null, "Storeasy:Email:Smtp:Host")]
    [InlineData(EmailProvider.Smtp, "smtp.example.com", null, "Storeasy:Email:Smtp:From")]
    [InlineData(EmailProvider.Smtp, "smtp.example.com", "no-reply@example.com", null)]
    [InlineData(EmailProvider.Notifike, null, null, "Storeasy:Email:Notifike:BaseUrl")]
    [InlineData(EmailProvider.Log, null, null, null)]
    public void FindMissingSetting_ShouldNameTheFirstMissingSetting(EmailProvider provider, string host, string from, string expected)
    {
        // Arrange
        var options = new EmailOptions { Provider = provider, Smtp = new SmtpEmailOptions { Host = host, From = from } };

        // Act
        var missing = options.FindMissingSetting();

        // Assert
        missing.Should().Be(expected);
    }

    [Fact]
    public void BuildTokenLink_ShouldJoinTheBaseUrlAndEscapeTheToken()
    {
        // Arrange
        var options = new AppOptions { BaseUrl = "https://storeasy.example.com/" };

        // Act
        var link = options.BuildTokenLink(AppOptions.ResetPasswordPath, "pwr_a+b/c");

        // Assert
        link.Should().Be("https://storeasy.example.com/reset-password?token=pwr_a%2Bb%2Fc");
    }

    [Fact]
    public void AccountTokenLifetimes_ShouldMatchThePlan()
    {
        AccountToken.LifetimeOf(AccountTokenPurpose.EmailVerification).Should().Be(TimeSpan.FromHours(48));
        AccountToken.LifetimeOf(AccountTokenPurpose.PasswordReset).Should().Be(TimeSpan.FromHours(1));
    }

    private static IHttpClientFactory CreateFactory(HttpMessageHandler handler, string name)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(name)).Returns(() => new HttpClient(handler, disposeHandler: false));

        return factory.Object;
    }
}
