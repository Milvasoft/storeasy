using FluentAssertions;
using Storeasy.Application.Utils.Helpers;
using Storeasy.Domain.Enums;
using Storeasy.Domain.JsonModels;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Events phase helpers: signatures, guideline parser, diff, version states, reply limits.")]
public class EventHelpersTests
{
    [Fact]
    public void AppleSignature_ShouldMatchTheRawBodyOnly()
    {
        // Arrange
        const string body = """{"data":{"id":"1"}}""";
        var header = WebhookSignatures.ComputeApple(body, "secret");

        // Act & Assert
        header.Should().StartWith("hmacsha256=").And.HaveLength("hmacsha256=".Length + 64);
        WebhookSignatures.VerifyApple(body, header, "secret").Should().BeTrue();
        WebhookSignatures.VerifyApple(body, header.ToUpperInvariant().Replace("HMACSHA256=", "hmacsha256="), "secret").Should().BeTrue();
        WebhookSignatures.VerifyApple(body + " ", header, "secret").Should().BeFalse();
        WebhookSignatures.VerifyApple(body, header, "other").Should().BeFalse();
        WebhookSignatures.VerifyApple(body, "hmacsha256=zz", "secret").Should().BeFalse();
        WebhookSignatures.VerifyApple(body, null, "secret").Should().BeFalse();
    }

    [Fact]
    public void StoreasySignature_ShouldBindTheTimestamp()
    {
        // Arrange
        var header = WebhookSignatures.ComputeStoreasy(1700000000, "{}", "secret");

        // Act & Assert
        WebhookSignatures.VerifyStoreasy(1700000000, "{}", header, "secret").Should().BeTrue();
        WebhookSignatures.VerifyStoreasy(1700000001, "{}", header, "secret").Should().BeFalse();
        WebhookSignatures.Mask(header).Should().StartWith("sha256=").And.Contain("****").And.EndWith(header[^4..]);
    }

    [Fact]
    public void GuidelineParser_ShouldReadNumbersAndTitlesOnce()
    {
        // Arrange
        const string text = """
            Guideline 2.1 - Performance - App Completeness
            We were unable to sign in.
            Guideline 5.1.1(v) - Legal - Privacy - Data Collection and Storage
            As noted under Guideline 2.1, the demo account failed.
            Guideline 4.3(a) - Design - Spam
            """;

        // Act
        var guidelines = GuidelineParser.Parse(text);

        // Assert
        guidelines.Select(g => g.Code).Should().Equal("2.1", "5.1.1", "4.3");
        guidelines[0].Title.Should().Be("Performance - App Completeness");
        GuidelineParser.Parse("There's an issue with your submission.").Should().BeEmpty();
    }

    [Fact]
    public void BuildChecklist_ShouldKeepManualItemsAndDropUncitedOnes()
    {
        // Arrange
        var existing = new List<RejectionChecklistItem>
        {
            new() { Id = "a", GuidelineCode = "2.1", Text = "Fix demo", Status = ChecklistItemStatus.Done },
            new() { Id = "b", GuidelineCode = "9.9", Text = "Old" },
            new() { Id = "c", Text = "Manual" },
        };

        // Act
        var checklist = GuidelineParser.BuildChecklist([new RejectionGuideline { Code = "2.1" }, new RejectionGuideline { Code = "5.1.1", Title = "Privacy" }], existing);

        // Assert
        checklist.Select(i => i.Text).Should().BeEquivalentTo(["Fix demo", "Manual", "Privacy"]);
        checklist.Single(i => i.Id == "a").Status.Should().Be(ChecklistItemStatus.Done);
    }

    [Fact]
    public void TextDiff_ShouldListRemovedAndAddedLines()
    {
        // Act
        var (removed, added) = TextDiff.Lines("a\nb\nc\nc", "a\nc\nd");

        // Assert
        removed.Should().Equal("b", "c");
        added.Should().Equal("d");
    }

    [Theory]
    [InlineData("REJECTED", RejectionKind.Rejected, false)]
    [InlineData("metadata_rejected", RejectionKind.MetadataRejected, false)]
    [InlineData("WAITING_FOR_REVIEW", null, true)]
    [InlineData("READY_FOR_SALE", null, true)]
    [InlineData("PREPARE_FOR_SUBMISSION", null, false)]
    public void VersionStates_ShouldClassify(string state, RejectionKind? rejection, bool submitted)
    {
        // Act & Assert
        AppStoreVersionStates.RejectionOf(state).Should().Be(rejection);
        AppStoreVersionStates.IsSubmitted(state).Should().Be(submitted);
    }

    [Theory]
    [InlineData("APP_STORE_VERSION_APP_VERSION_STATE_UPDATED")]
    [InlineData("APP_STORE_VERSIONS_APP_VERSION_STATE_UPDATED")]
    [InlineData("appStoreVersionAppVersionStateUpdated")]
    public void VersionStateEvent_ShouldAcceptBothSpellings(string eventType)
        => AppStoreVersionStates.NormalizeEventType(eventType).Should().Be(AppStoreVersionStates.StateUpdatedEvent);

    [Fact]
    public void ReplyLimits_ShouldCountUserPerceivedCharacters()
    {
        // Act & Assert
        ReviewRules.ReplyMaxLength(StoreKind.Google).Should().Be(350);
        ReviewRules.ReplyMaxLength(StoreKind.Apple).Should().Be(5970);
        ReviewRules.ReplyMaxLength(StoreKind.Microsoft).Should().Be(0);
        ReviewRules.Length("ab\U0001F600").Should().Be(3);
        RejectionCases.ReplyLength("a\r\nb").Should().Be(3);
    }

    [Fact]
    public void NotificationKinds_ShouldLockConnectionAlerts()
    {
        // Act & Assert
        NotificationKinds.Effective(NotificationType.ConnectionAlert, false, false).Should().Be((true, true));
        NotificationKinds.Default(NotificationType.NewReview).Should().Be((true, false));
        NotificationKinds.Default(NotificationType.Rejection).Should().Be((true, true));
    }

    [Fact]
    public void SavedReplies_ShouldFindUnknownVariables()
    {
        // Act & Assert
        SavedReplies.UnknownVariables("Hi {name}, {app} {version}").Should().BeEmpty();
        SavedReplies.UnknownVariables("Merhaba {isim}").Should().Equal("isim");
    }
}
