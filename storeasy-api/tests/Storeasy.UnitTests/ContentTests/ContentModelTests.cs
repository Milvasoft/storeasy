using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Rules;
using Storeasy.Infrastructure.Services;
using Storeasy.Infrastructure.Stores.Content;
using Storeasy.UnitTests.TestSupport;
using System.Buffers.Binary;
using System.Net.Http.Headers;

namespace Storeasy.UnitTests.ContentTests;

/// <summary>
/// Pure parts of the content engine: diff and fingerprint, plan rules, planners, image headers, saved answer documents,
/// the Data safety CSV and the store retry delay.
/// </summary>
[Trait("Content Unit Tests", "Content model, planners and rules.")]
public class ContentModelTests
{
    private static readonly Lazy<RuleCatalog> _catalog = new(() => RulesCatalog.LoadFromFolder(Path.GetFullPath(Path.Combine(SourceCode.SolutionRoot, "..", "rules"))));

    [Fact]
    public void Diff_ShouldReturnOnlyChangedFieldsWithTheirAction()
    {
        var current = new Dictionary<string, string>
        {
            [ContentKey.Of("Version", "en-US", "description")] = "Old",
            [ContentKey.Of("Version", "en-US", "keywords")] = "a,b",
            [ContentKey.Of("Version", "en-US", "whatsNew")] = "Notes",
        };

        var items = ContentDiff.Compute(current, new Dictionary<string, string>
        {
            [ContentKey.Of("Version", "en-US", "description")] = "New",
            [ContentKey.Of("Version", "en-US", "keywords")] = "a,b",
            [ContentKey.Of("Version", "en-US", "whatsNew")] = "",
            [ContentKey.Of("Version", "de-DE", "description")] = "Neu",
        });

        items.Should().HaveCount(3);
        items.Should().ContainSingle(i => i.Locale == "en-US" && i.Field == "description" && i.Action == ChangeAction.Update && i.Before == "Old" && i.After == "New");
        items.Should().ContainSingle(i => i.Field == "whatsNew" && i.Action == ChangeAction.Delete && i.After == null);
        items.Should().ContainSingle(i => i.Locale == "de-DE" && i.Action == ChangeAction.Create);
    }

    [Fact]
    public void Hash_ShouldDependOnTheTouchedValuesOnly()
    {
        var a = new Dictionary<string, string> { ["k1"] = "1", ["k2"] = "2", ["other"] = "x" };
        var b = new Dictionary<string, string> { ["k1"] = "1", ["k2"] = "2", ["other"] = "y" };
        var c = new Dictionary<string, string> { ["k1"] = "1", ["k2"] = "3" };

        ContentDiff.Hash(a, ["k1", "k2"]).Should().Be(ContentDiff.Hash(b, ["k2", "k1"]));
        ContentDiff.Hash(a, ["k1", "k2"]).Should().NotBe(ContentDiff.Hash(c, ["k1", "k2"]));
    }

    [Fact]
    public void Counting_ShouldUseBytesForKeywordsAndTextElementsForCharacters()
    {
        ContentDiff.ByteCount("şğü").Should().Be(6);
        ContentDiff.CharCount("👍🏽ab").Should().Be(3);
    }

    [Theory]
    [InlineData(ChangePlanKind.Listing, StoreKind.Apple, null, OrgPermission.EditContent)]
    [InlineData(ChangePlanKind.SubmitForReview, StoreKind.Apple, null, OrgPermission.SubmitForReview)]
    [InlineData(ChangePlanKind.TrackRelease, StoreKind.Google, "internal", OrgPermission.ReleaseTest)]
    [InlineData(ChangePlanKind.TrackRelease, StoreKind.Google, "production", OrgPermission.ReleaseProduction)]
    [InlineData(ChangePlanKind.Rollout, StoreKind.Apple, null, OrgPermission.ReleaseProduction)]
    [InlineData(ChangePlanKind.Rollout, StoreKind.Google, "beta", OrgPermission.ReleaseTest)]
    [InlineData(ChangePlanKind.Testers, StoreKind.Google, "alpha", OrgPermission.ReleaseTest)]
    public void RequiredPermission_ShouldFollowTheRoleMatrix(ChangePlanKind kind, StoreKind store, string track, OrgPermission expected)
        => ChangePlanRules.RequiredPermission(kind, store, track).Should().Be(expected);

    [Fact]
    public void Confirmation_ShouldBeNeededForSubmissionsAndProductionOnly()
    {
        ChangePlanRules.NeedsConfirmation(ChangePlanKind.SubmitForReview, null).Should().BeTrue();
        ChangePlanRules.NeedsConfirmation(ChangePlanKind.BundleUpload, "production").Should().BeTrue();
        ChangePlanRules.NeedsConfirmation(ChangePlanKind.TrackRelease, "internal").Should().BeFalse();
        ChangePlanRules.ConfirmationText("Kumbara ", "1.0").Should().Be("Kumbara 1.0");
        ChangePlanRules.ConfirmationMatches("Kumbara 1.0", "  Kumbara   1.0 ").Should().BeTrue();
        ChangePlanRules.ConfirmationMatches("Kumbara 1.0", "kumbara 1.0").Should().BeFalse();
        ChangePlanRules.ConfirmationMatches(null, null).Should().BeTrue();
    }

    [Fact]
    public void Effective_ShouldDeriveExpiredAndFailed()
    {
        var now = new DateTime(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

        ChangePlanRules.Effective(ChangePlanStatus.Pending, now.AddMinutes(-1), null, now).Should().Be(ChangePlanStatus.Expired);
        ChangePlanRules.Effective(ChangePlanStatus.Pending, now.AddMinutes(1), null, now).Should().Be(ChangePlanStatus.Pending);
        ChangePlanRules.Effective(ChangePlanStatus.Approved, now, StoreJobStatus.Failed, now).Should().Be(ChangePlanStatus.Failed);
        ChangePlanRules.Effective(ChangePlanStatus.Approved, now, StoreJobStatus.Running, now).Should().Be(ChangePlanStatus.Approved);
    }

    [Fact]
    public void ListingPlanner_ShouldRefuseVersionFieldsOfALockedVersionButAllowPromotionalText()
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Apple, VersionId = "v1", VersionEditable = false, Locales = ["en-US"], PrimaryLocale = "en-US" };
        var localizer = FakeLocalizer.Create().Object;

        FieldPlanner.Listing(StoreKind.Apple, snapshot, [("en-US", "description", "x")], localizer, out var locked).Should().BeNull();
        locked.Should().Be("VersionNotEditable");

        var draft = FieldPlanner.Listing(StoreKind.Apple, snapshot, [("en-US", "promotionalText", "Now 50% off")], localizer, out var none);
        none.Should().BeNull();
        draft.Items.Should().ContainSingle(i => i.Field == "promotionalText" && i.Action == ChangeAction.Create);
        draft.Changes.Fields.Should().ContainSingle(f => f.Group == ContentGroups.Version && f.Value == "Now 50% off");
    }

    [Fact]
    public void ListingPlanner_ShouldWarnAboutKeywordsRepeatingTheName()
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Apple, VersionId = "v1", VersionEditable = true, Locales = ["en-US"], PrimaryLocale = "en-US" };
        snapshot.Values[ContentKey.Of(ContentGroups.AppInfo, "en-US", "name")] = "Pusula Compass";

        var draft = FieldPlanner.Listing(StoreKind.Apple, snapshot, [("en-US", "keywords", "compass,map")], FakeLocalizer.Create().Object, out _);

        draft.Warnings.Should().ContainSingle(w => w.Key == "Plan.KeywordInName" && w.Field == "keywords");
    }

    [Fact]
    public void LimitViolation_ShouldCountKeywordBytes()
    {
        var keywords = ListingFields.Find(StoreKind.Apple, "keywords");

        FieldPlanner.LimitViolation(keywords, new string('ş', 50), _catalog.Value).Should().BeNull();
        FieldPlanner.LimitViolation(keywords, new string('ş', 51), _catalog.Value).Should().Be(("FieldTooManyBytes", 102, 100));
        FieldPlanner.LimitViolation(ListingFields.Find(StoreKind.Google, "title"), new string('a', 31), _catalog.Value)?.Key.Should().Be("FieldTooLong");
    }

    [Fact]
    public void MediaPlanner_ShouldAcceptSkipAndRejectFiles()
    {
        var mediaClass = MediaClasses.Find(StoreKind.Apple, MediaKind.Screenshot, "APP_IPAD_PRO_3GEN_129");
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Apple, VersionId = "v1", VersionEditable = true };
        snapshot.Media.Add(new MediaSetState { Kind = MediaKind.Screenshot, Locale = "en-US", ClassKey = mediaClass.ClassKey, Id = "set1", Items = [new MediaItemState { Id = "s1", FileName = "old.png", Checksum = "aaa" }] });

        var files = new List<InspectedAsset>
        {
            Asset(1, "new.png", "bbb", new ImageHeader("png", 2064, 2752, false)),
            Asset(2, "same.png", "aaa", new ImageHeader("png", 2064, 2752, false)),
            Asset(3, "alpha.png", "ccc", new ImageHeader("png", 2064, 2752, true)),
            Asset(4, "iphone.png", "ddd", new ImageHeader("png", 1320, 2868, false)),
        };

        var draft = MediaPlanner.Build(StoreKind.Apple, mediaClass, "en-US", snapshot, [], [], files, null, _catalog.Value, FakeLocalizer.Create().Object);

        draft.Files.Select(f => f.Result).Should().Equal("Accepted", "Skipped", "Rejected", "Rejected");
        draft.Files[3].SuggestedClassKey.Should().Be("APP_IPHONE_67");
        draft.Changes.Media.Single().Items.Select(i => i.ExistingId ?? i.FileName).Should().Equal("s1", "new.png");
        draft.Items.Should().ContainSingle(i => i.Action == ChangeAction.Create && i.After == "new.png");
    }

    [Fact]
    public void MediaPlanner_ShouldReplaceASingleGraphic()
    {
        var icon = MediaClasses.Find(StoreKind.Google, MediaKind.Screenshot, "icon");
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Google };
        snapshot.Media.Add(new MediaSetState { Kind = MediaKind.Screenshot, Locale = "en-US", ClassKey = "icon", Items = [new MediaItemState { Id = "i1", Checksum = "old" }] });

        var draft = MediaPlanner.Build(StoreKind.Google, icon, "en-US", snapshot, [], [], [Asset(1, "icon.png", "new", new ImageHeader("png", 512, 512, true))], null, _catalog.Value, FakeLocalizer.Create().Object);

        draft.Changes.Media.Single().Items.Should().ContainSingle(i => i.AssetId == 1);
        draft.Items.Should().Contain(i => i.Action == ChangeAction.Delete);
    }

    [Fact]
    public void Rollout_ShouldOnlyGrowAStagedRelease()
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Google, ExternalId = "com.test" };
        snapshot.Tracks.Add(new TrackState { Track = "production", Releases = [new TrackReleaseState { VersionCodes = ["9"], Status = "inProgress", UserFraction = 0.2 }] });

        ReleasePlanner.Rollout(StoreKind.Google, snapshot, new RolloutChange { Action = RolloutAction.SetPercentage, UserFraction = 0.1 }, out var lower).Should().BeNull();
        lower.Should().Be("RolloutInvalid");
        ReleasePlanner.Rollout(StoreKind.Google, snapshot, new RolloutChange { Action = RolloutAction.Resume }, out _).Should().BeNull("only a halted rollout resumes");

        var draft = ReleasePlanner.Rollout(StoreKind.Google, snapshot, new RolloutChange { Action = RolloutAction.SetPercentage, UserFraction = 0.5 }, out var none);

        none.Should().BeNull();
        draft.Track.Should().Be("production");
        draft.Changes.Rollout.Track.Should().Be("production");
    }

    [Fact]
    public void Testers_WithTheSameGroups_ShouldPlanNothing()
    {
        var snapshot = new StoreContentSnapshot { Store = StoreKind.Google };
        snapshot.Tracks.Add(new TrackState { Track = "alpha", GoogleGroups = ["a@googlegroups.com"] });

        ReleasePlanner.Testers(snapshot, "alpha", ["A@googlegroups.com "]).IsEmpty.Should().BeTrue();
        ReleasePlanner.Testers(snapshot, "alpha", ["b@googlegroups.com"]).Changes.Testers.GoogleGroups.Should().Equal("b@googlegroups.com");
    }

    [Fact]
    public void ImageHeader_ShouldReadPngAndJpegSizesAndAlpha()
    {
        ImageHeader.Read(Png(2064, 2752, 2)).Should().Be(new ImageHeader("png", 2064, 2752, false));
        ImageHeader.Read(Png(512, 512, 6)).HasAlpha.Should().BeTrue();

        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00, 0xFF, 0xC0, 0x00, 0x11, 0x08, 0x0A, 0xFA, 0x07, 0xD0, 0x03, 0x00, 0x00, 0x00, 0x00];
        ImageHeader.Read(jpeg).Should().Be(new ImageHeader("jpeg", 2000, 2810, false));
        ImageHeader.Read([1, 2, 3, 4]).Should().BeNull();
    }

    [Fact]
    public void SavedAnswerDocuments_ShouldRefuseUnknownMembers()
    {
        SavedAnswerDocuments.Normalize(SavedAnswerKind.ReviewNotes, """{"notes":"Use the demo account."}""", out _).Should().Be("""{"notes":"Use the demo account."}""");
        SavedAnswerDocuments.Normalize(SavedAnswerKind.ReviewNotes, """{"note":"typo"}""", out var error).Should().BeNull();
        error.Should().NotBeNullOrEmpty();
        SavedAnswerDocuments.Normalize(SavedAnswerKind.ReviewLogin, """{"demoAccountPassword":"x"}""", out _).Should().BeNull("the password is never part of the document");
    }

    [Fact]
    public void DataSafetyCsv_ShouldQuoteFieldsThatNeedIt()
    {
        var csv = DataSafetyCsv.Generate(new DataSafetyAnswers
        {
            Rows =
            [
                new DataSafetyRow { QuestionId = "PSL_A", ResponseValue = "true" },
                new DataSafetyRow { QuestionId = "PSL_B", ResponseId = "R1", ResponseValue = "a, \"b\"", Label = "Label" },
            ],
        });

        csv.Split("\r\n").Should().Equal(DataSafetyCsv.Header, "PSL_A,,true,,", "PSL_B,R1,\"a, \"\"b\"\"\",,Label", "");
    }

    [Fact]
    public void RetryDelay_ShouldBackOffAndHonourRetryAfter()
    {
        var baseDelay = TimeSpan.FromMilliseconds(800);
        var max = TimeSpan.FromSeconds(60);
        var now = DateTimeOffset.UtcNow;

        StoreHttp.RetryDelay(1, null, baseDelay, max, now).Should().Be(TimeSpan.FromMilliseconds(800));
        StoreHttp.RetryDelay(3, null, baseDelay, max, now).Should().Be(TimeSpan.FromMilliseconds(3200));
        StoreHttp.RetryDelay(20, null, baseDelay, max, now).Should().Be(max);
        StoreHttp.RetryDelay(1, new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)), baseDelay, max, now).Should().Be(TimeSpan.FromSeconds(7));
    }

    [Fact]
    public void ApplePlanSet_ShouldKeepMatchingChecksumsAndRemoveTheRest()
    {
        MediaItemState[] existing = [new() { Id = "a", Checksum = "m1", State = "COMPLETE" }, new() { Id = "b", Checksum = "m2", State = "FAILED" }, new() { Id = "c", Checksum = "m3" }];
        MediaItemChange[] desired = [new() { AssetId = 1, Md5 = "m2" }, new() { ExistingId = "a" }, new() { AssetId = 2, Md5 = "m3" }];

        var (keep, upload, remove) = AppleContentGateway.PlanSet(existing, desired);

        keep.Values.Should().BeEquivalentTo(["a", "c"]);
        upload.Should().ContainSingle(u => u.Md5 == "m2", "a failed item never matches");
        remove.Select(r => r.Id).Should().Equal("b");
    }

    private static InspectedAsset Asset(long id, string name, string md5, ImageHeader image) => new(id, $"key/{name}", name, "image/png", 1000, md5, md5 + "-sha", image);

    private static byte[] Png(int width, int height, byte colorType)
    {
        var data = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        signature.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8), 13);
        "IHDR"u8.CopyTo(data.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20), (uint)height);
        data[24] = 8;
        data[25] = colorType;

        return data;
    }
}
