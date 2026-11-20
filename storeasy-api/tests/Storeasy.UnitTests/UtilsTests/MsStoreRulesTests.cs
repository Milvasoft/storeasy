using FluentAssertions;
using Storeasy.Application.Dtos.MsStoreDtos;
using Storeasy.Application.Features.MsStore.ApplyMsChange;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.MsStore;
using Storeasy.Domain.Enums;
using Storeasy.Domain.MsStore;
using Storeasy.Infrastructure.Stores.Microsoft;
using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Microsoft Store planning, submission JSON and installer rules.")]
public class MsStoreRulesTests
{
    private static JsonObject Submission(string status = "PendingCommit", string priceId = "Free") => JsonNode.Parse($$"""
    {
      "id": "1152921504621243540",
      "status": "{{status}}",
      "statusDetails": { "errors": [], "warnings": [], "certificationReports": [] },
      "fileUploadUrl": "https://blob.example.com/x?sig=abc",
      "pricing": { "priceId": "{{priceId}}", "trialPeriod": "NoFreeTrial" },
      "visibility": "Public",
      "listings": {
        "en-us": {
          "baseListing": {
            "title": "Pusula",
            "description": "Old description",
            "features": [ "One" ],
            "keywords": [ "maps" ],
            "images": [ { "fileName": "shot1.png", "fileStatus": "Uploaded", "id": "11", "imageType": "Screenshot" } ]
          },
          "platformOverrides": {}
        }
      },
      "applicationPackages": [ { "fileName": "app_1.0.0.0_x64.msix", "fileStatus": "Uploaded", "id": "21", "version": "1.0.0.0", "architecture": "x64" } ],
      "packageDeliveryOptions": { "packageRollout": { "isPackageRollout": false, "packageRolloutPercentage": 0, "packageRolloutStatus": "PackageRolloutNotStarted", "fallbackSubmissionId": "0" }, "isMandatoryUpdate": false }
    }
    """)!.AsObject();

    private static MsContentDto Content(JsonObject document, bool pending)
    {
        var content = new MsContentDto { Kind = MsProductKind.Msix, Target = MsSubmissionTarget.App, ProductId = "9NBLGGH4R315", SubmissionId = "1152921504621243540", SubmissionIsPending = pending };

        MsContentReader.Fill(content, document, MsSubmissionTarget.App);

        return content;
    }

    private static MsPlanContext Context(MsPendingSubmissionInfo pending = null, bool pricingV2 = false) => new()
    {
        Kind = MsProductKind.Msix,
        ProductId = "9NBLGGH4R315",
        FirstSubmissionDone = true,
        PricingV2 = pricingV2,
        Pending = pending,
    };

    private static MsChangeInputDto Listing(string description) => new()
    {
        Kind = MsChangeKind.Listings,
        Listings = [new MsListingDto { Locale = "en-US", Description = description, Features = ["One", "Two"] }],
    };

    [Fact]
    public void Plan_Listings_OnPublishedContent_ShouldDiffAndCreateADraft()
    {
        var content = Content(Submission("Published"), pending: false);

        var plan = MsChangePlanner.Plan(5, content, Listing("New description"), Context());

        plan.Blocked.Should().BeFalse();
        plan.CreatesSubmission.Should().BeTrue();
        plan.RequiredPermission.Should().Be(OrgPermission.EditContent);
        plan.Items.Select(i => i.Field).Should().BeEquivalentTo(["description", "features"]);
        plan.Items.Single(i => i.Field == "description").Should().Match<MsPlanItemDto>(i => i.Before == "Old description" && i.After == "New description" && i.Action == "update" && i.Locale == "en-us");
        plan.Calls.Select(c => c.Method).Should().Equal("POST", "PUT");
        plan.Warnings.Select(w => w.Key).Should().Contain(MessageKey.MsWarnCreatesSubmission);
    }

    [Fact]
    public void Plan_WhenTheActiveSubmissionIsInCertification_ShouldBeBlocked()
    {
        var plan = MsChangePlanner.Plan(5, Content(Submission("Certification"), pending: true), Listing("x"), Context(new MsPendingSubmissionInfo { CreatedByStoreasy = true }));

        plan.Blocked.Should().BeTrue();
        plan.BlockKey.Should().Be(MessageKey.MsSubmissionInProgress);
    }

    [Fact]
    public void Plan_WhenStoreasysDraftChangedInPartnerCenter_ShouldBeBlockedAsManualEdit()
    {
        var document = Submission();
        var content = Content(document, pending: true);

        var plan = MsChangePlanner.Plan(5, content, Listing("x"), Context(new MsPendingSubmissionInfo { CreatedByStoreasy = true, ContentHash = "an older fingerprint" }));

        plan.BlockKey.Should().Be(MessageKey.MsSubmissionModifiedOutsideApi);
        plan.AssistedReason.Should().Be(MsAssistedReason.ManualEdit);
    }

    [Fact]
    public void Plan_OnStoreasysUntouchedDraft_ShouldEditIt()
    {
        var document = Submission();
        var content = Content(document, pending: true);

        var plan = MsChangePlanner.Plan(5, content, Listing("x"), Context(new MsPendingSubmissionInfo { CreatedByStoreasy = true, ContentHash = MsSubmissionJson.ContentHash(document) }));

        plan.Blocked.Should().BeFalse();
        plan.CreatesSubmission.Should().BeFalse();
        plan.Calls.Select(c => c.Method).Should().Equal("PUT");
    }

    [Fact]
    public void Plan_OnADraftCreatedInPartnerCenter_ShouldBeBlocked()
    {
        var plan = MsChangePlanner.Plan(5, Content(Submission(), pending: true), Listing("x"), Context());

        plan.BlockKey.Should().Be(MessageKey.MsSubmissionNotOurs);
    }

    [Fact]
    public void Plan_Pricing_OnPricingVersion2_ShouldBeAssisted()
    {
        var content = Content(Submission("Published", priceId: "Base"), pending: false);
        var change = new MsChangeInputDto { Kind = MsChangeKind.Pricing, Pricing = new MsPricingDto { PriceId = "Tier2" } };

        var plan = MsChangePlanner.Plan(5, content, change, Context());

        content.PricingV2.Should().BeTrue();
        plan.BlockKey.Should().Be(MessageKey.MsPricingAssisted);
        plan.AssistedReason.Should().Be(MsAssistedReason.PricingV2);
    }

    [Fact]
    public void Plan_Images_OverTheDesktopLimit_ShouldBeBlocked()
    {
        var content = Content(Submission("Published"), pending: false);
        var change = new MsChangeInputDto
        {
            Kind = MsChangeKind.Images,
            Images = new MsImageChangeDto { Locale = "en-us", Add = [.. Enumerable.Range(1, 10).Select(i => new MsImageInputDto { AssetId = i, ImageType = "Screenshot" })] },
        };

        var plan = MsChangePlanner.Plan(5, content, change, Context());

        plan.BlockKey.Should().Be(MessageKey.MsTooManyImages, "1 existing + 10 new desktop screenshots exceed 10");
    }

    [Theory]
    [InlineData(null, OrgPermission.ReleaseProduction)]
    [InlineData("flight-1", OrgPermission.ReleaseTest)]
    public void RequiredPermission_OfARollout_ShouldDependOnTheAudience(string flightId, OrgPermission expected)
    {
        var change = new MsChangeInputDto { Kind = MsChangeKind.Rollout, FlightId = flightId, Rollout = new MsRolloutChangeDto { SubmissionId = "1", Action = MsRolloutAction.SetPercentage, Percentage = 50 } };

        MsChangePlanner.RequiredPermission(change, MsChangePlanner.TargetOf(MsProductKind.Msix, change)).Should().Be(expected);
    }

    [Fact]
    public void BaseHash_ShouldChange_WhenTheTouchedContentChanges()
    {
        var before = MsChangePlanner.BaseHash(Content(Submission("Published"), false), MsChangeKind.Listings);
        var document = Submission("Published");

        document["listings"]!["en-us"]!["baseListing"]!["description"] = "Edited elsewhere";

        MsChangePlanner.BaseHash(Content(document, false), MsChangeKind.Listings).Should().NotBe(before);
        MsChangePlanner.BaseHash(Content(document, false), MsChangeKind.Pricing).Should().Be(MsChangePlanner.BaseHash(Content(Submission("Published"), false), MsChangeKind.Pricing));
    }

    [Fact]
    public void ContentHash_ShouldIgnoreStatusUploadAddressAndFileStatus()
    {
        var a = Submission("PendingCommit");
        var b = Submission("CommitStarted");

        b["fileUploadUrl"] = "https://blob.example.com/y?sig=other";
        b["applicationPackages"]![0]!["fileStatus"] = "PendingUpload";

        MsSubmissionJson.ContentHash(a).Should().Be(MsSubmissionJson.ContentHash(b));

        b["visibility"] = "Hidden";

        MsSubmissionJson.ContentHash(a).Should().NotBe(MsSubmissionJson.ContentHash(b));
    }

    [Fact]
    public void WritePublishImmediately_ShouldSetImmediateAndDropTheDate_AndKeepEveryOtherField()
    {
        var document = Submission();

        document["targetPublishMode"] = "Manual";
        document["targetPublishDate"] = "2026-12-01T00:00:00Z";

        var changed = MsSubmissionJson.WritePublishImmediately(document);

        changed.Should().BeTrue();
        document["targetPublishMode"]!.GetValue<string>().Should().Be("Immediate");
        document.ContainsKey("targetPublishDate").Should().BeFalse();
        document["visibility"]!.GetValue<string>().Should().Be("Public");
        MsSubmissionJson.WritePublishImmediately(document).Should().BeFalse("it is already immediate");
    }

    [Fact]
    public void WritePublishImmediately_ShouldLeaveADocumentWithoutAPublishModeAlone()
    {
        var document = Submission();
        var before = document.ToJsonString();

        MsSubmissionJson.WritePublishImmediately(document).Should().BeFalse();
        MsSubmissionJson.WritePublishImmediately(null).Should().BeFalse();
        document.ToJsonString().Should().Be(before);
    }

    [Theory]
    [InlineData(false, MsSubmissionStatus.Published, BuildProcessingState.Valid)]
    [InlineData(true, MsSubmissionStatus.PendingPublication, BuildProcessingState.Valid)]
    [InlineData(true, MsSubmissionStatus.Publishing, BuildProcessingState.Valid)]
    [InlineData(true, MsSubmissionStatus.Certification, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.PreProcessing, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.PendingCommit, BuildProcessingState.Processing)]
    public void PackageState_ShouldNotCallACertifiedSubmissionProcessing(bool pending, MsSubmissionStatus status, BuildProcessingState expected)
    {
        MsSyncReader.PackageState(pending, status).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.Manual, BuildProcessingState.Valid)]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.Immediate, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.SpecificDate, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.Release, null, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.Certification, MsPublishMode.Manual, BuildProcessingState.Processing)]
    [InlineData(true, MsSubmissionStatus.PendingPublication, MsPublishMode.Immediate, BuildProcessingState.Valid)]
    public void PackageState_ShouldCallAManualSubmissionInReleaseValid(bool pending, MsSubmissionStatus status, MsPublishMode? mode, BuildProcessingState expected)
    {
        MsSyncReader.PackageState(pending, status, mode).Should().Be(expected);
    }

    [Theory]
    [InlineData("Immediate", MsPublishMode.Immediate)]
    [InlineData("Manual", MsPublishMode.Manual)]
    [InlineData("manual", MsPublishMode.Manual)]
    [InlineData("SpecificDate", MsPublishMode.SpecificDate)]
    [InlineData("SPECIFICDATE", MsPublishMode.SpecificDate)]
    [InlineData("Later", null)]
    [InlineData(null, null)]
    public void ReadPublishMode_ShouldParseCaseInsensitively_AndReturnNullForUnknownValues(string value, MsPublishMode? expected)
    {
        var document = Submission();

        if (value != null)
            document["targetPublishMode"] = value;

        MsSubmissionJson.ReadPublishMode(document).Should().Be(expected);
    }

    [Fact]
    public void ReadPublishMode_ShouldReturnNullForANonStringValueOrAMissingDocument()
    {
        var document = Submission();

        document["targetPublishMode"] = 2;

        MsSubmissionJson.ReadPublishMode(document).Should().BeNull();
        MsSubmissionJson.ReadPublishMode(null).Should().BeNull();
    }

    [Fact]
    public void ReadPublishDate_ShouldReadTheDateAsUtc()
    {
        var document = Submission();

        MsSubmissionJson.ReadPublishDate(document).Should().BeNull();

        document["targetPublishDate"] = "2026-12-01T09:30:00+03:00";

        var date = MsSubmissionJson.ReadPublishDate(document);

        date.Should().Be(new DateTime(2026, 12, 1, 6, 30, 0, DateTimeKind.Utc));
        date!.Value.Kind.Should().Be(DateTimeKind.Utc);

        document["targetPublishDate"] = "not a date";

        MsSubmissionJson.ReadPublishDate(document).Should().BeNull();
    }

    [Theory]
    [InlineData(true, MsSubmissionStatus.PendingPublication, null, true)]
    [InlineData(true, MsSubmissionStatus.PendingPublication, MsPublishMode.Immediate, true)]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.Manual, true)]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.Immediate, false)]
    [InlineData(true, MsSubmissionStatus.Release, MsPublishMode.SpecificDate, false)]
    [InlineData(true, MsSubmissionStatus.Release, null, false)]
    [InlineData(true, MsSubmissionStatus.Certification, MsPublishMode.Manual, false)]
    [InlineData(true, MsSubmissionStatus.Publishing, MsPublishMode.Manual, false)]
    [InlineData(false, MsSubmissionStatus.PendingPublication, MsPublishMode.Manual, false)]
    [InlineData(false, MsSubmissionStatus.Release, MsPublishMode.Manual, false)]
    public void AwaitsManualPublish_ShouldCatchPendingPublicationAndAManualSubmissionInRelease(bool pending, MsSubmissionStatus status, MsPublishMode? mode, bool expected)
    {
        MsSubmissionJson.AwaitsManualPublish(pending, status, mode).Should().Be(expected);
    }

    [Fact]
    public void ContentHash_ShouldMatchTheStoredDocumentAfterWritePublishImmediately()
    {
        // storeasy records the hash of the document it sent (with the mode it wrote), so a later read of the same document
        // is not taken for a Partner Center edit; a mode changed in Partner Center afterwards is one.
        var document = Submission();

        document["targetPublishMode"] = "Manual";
        MsSubmissionJson.WritePublishImmediately(document);

        var stored = JsonNode.Parse(document.ToJsonString())!.AsObject();

        MsSubmissionJson.ContentHash(stored).Should().Be(MsSubmissionJson.ContentHash(document));

        stored["targetPublishMode"] = "Manual";

        MsSubmissionJson.ContentHash(stored).Should().NotBe(MsSubmissionJson.ContentHash(document));
    }

    [Theory]
    [InlineData(MsSubmissionStatus.PendingCommit, true, "changed", true)]
    [InlineData(MsSubmissionStatus.PendingCommit, true, "written", false)]
    [InlineData(MsSubmissionStatus.Certification, true, "changed", false)]
    [InlineData(MsSubmissionStatus.Release, true, "changed", false)]
    [InlineData(MsSubmissionStatus.Published, false, "changed", false)]
    public void ObserveContent_ShouldFlagOnlyADraftChangedOutsideTheApi(MsSubmissionStatus status, bool pending, string hash, bool expected)
    {
        var row = new MsSubmission { CreatedByStoreasy = true, ContentHash = "written", ModifiedOutsideApi = status != MsSubmissionStatus.PendingCommit };

        MsSubmissionRows.ObserveContent(row, status, pending, hash);

        row.ModifiedOutsideApi.Should().Be(expected);
    }

    [Fact]
    public void WriteListing_ShouldKeepEveryOtherField_AndAddNewLanguages()
    {
        var document = Submission();

        MsSubmissionJson.WriteListing(document, new MsListingDto { Locale = "en-US", Description = "New", SearchTerms = ["a", "b"] });
        MsSubmissionJson.WriteListing(document, new MsListingDto { Locale = "tr-tr", Title = "Pusula", Description = "Yeni" });

        var listings = MsSubmissionJson.ReadListings(document);

        listings.Select(l => l.Locale).Should().Equal("en-us", "tr-tr");
        listings[0].Should().Match<MsListingDto>(l => l.Description == "New" && l.Title == "Pusula" && l.Features.Count == 1 && l.SearchTerms.Count == 2);
        document["pricing"]!["priceId"]!.GetValue<string>().Should().Be("Free");
        MsSubmissionJson.ReadImages(document).Should().ContainSingle();
    }

    [Fact]
    public void WritePackages_ShouldMarkRemovedAndAddPendingUploads()
    {
        var document = Submission();

        MsSubmissionJson.WritePackages(document, MsSubmissionTarget.App, ["app_1.0.0.0_x64.msix"], ["packages/7_app_1.1.0.0_x64.msix"]);
        MsSubmissionJson.WriteRollout(document, 25);

        var packages = document["applicationPackages"]!.AsArray();

        packages[0]!["fileStatus"]!.GetValue<string>().Should().Be("PendingDelete");
        packages[1]!["fileStatus"]!.GetValue<string>().Should().Be("PendingUpload");
        MsSubmissionJson.ReadPackages(document, MsSubmissionTarget.App).Select(p => p.FileName).Should().Equal("packages/7_app_1.1.0.0_x64.msix");
        MsSubmissionJson.ReadRollout(document).Should().Match<MsRolloutDto>(r => r.IsPackageRollout && r.Percentage == 25);
    }

    [Fact]
    public void WritePackages_OnARetry_ShouldNotDuplicate_AndReplaceTheUnsentEntryOfTheSameFile()
    {
        var document = Submission();

        MsSubmissionJson.WritePackages(document, MsSubmissionTarget.App, [], ["packages/7_Kabuk_1.0.15.0_x64.msix"]);
        MsSubmissionJson.WritePackages(document, MsSubmissionTarget.App, [], ["packages/7_Kabuk_1.0.15.0_x64.msix"]);
        MsSubmissionJson.WritePackages(document, MsSubmissionTarget.App, [], ["packages/9_Kabuk_1.0.15.0_x64.msix", "packages/9_Other_1.0.15.0_arm64.msix"]);

        var packages = document["applicationPackages"]!.AsArray().Select(p => (p!["fileName"]!.GetValue<string>(), p["fileStatus"]!.GetValue<string>())).ToList();

        packages.Should().Equal(("app_1.0.0.0_x64.msix", "Uploaded"), ("packages/9_Kabuk_1.0.15.0_x64.msix", "PendingUpload"), ("packages/9_Other_1.0.15.0_arm64.msix", "PendingUpload"));
    }

    [Theory]
    [InlineData("packages/12_Kabuk_1.0.15.0_x64.msix", "Kabuk_1.0.15.0_x64.msix")]
    [InlineData("packages/Kabuk_1.0.15.0_x64.msix", null)]
    [InlineData("images/12_shot.png", null)]
    [InlineData("app_1.0.0.0_x64.msix", null)]
    [InlineData(null, null)]
    public void OriginalName_ShouldReadOnlyStoreasysZipEntryNames(string entryName, string expected)
        => MsSubmissionJson.OriginalName(MsSubmissionJson.PackagesFolder, entryName).Should().Be(expected);

    [Fact]
    public void WriteReleaseNotes_ShouldFillEveryListingLanguage_AndKeepEverythingElse()
    {
        var document = Submission();

        MsSubmissionJson.WriteListing(document, new MsListingDto { Locale = "tr-tr", Title = "Pusula", Description = "Yeni" });

        var before = document.DeepClone().AsObject();
        var written = MsSubmissionJson.WriteReleaseNotes(document, "Faster start");

        written.Should().BeEquivalentTo("en-us", "tr-tr");
        MsSubmissionJson.ReadListings(document).Should().OnlyContain(l => l.WhatsNew == "Faster start");

        foreach (var listing in document["listings"]!.AsObject())
            listing.Value!["baseListing"]!.AsObject().Remove("releaseNotes");

        document.ToJsonString().Should().Be(before.ToJsonString());
        MsSubmissionJson.WriteReleaseNotes(document, null).Should().BeEmpty();
    }

    [Fact]
    public void Plan_PackagesWithReleaseNotes_ShouldShowTheNotesOfEveryListing()
    {
        var content = Content(Submission("Published"), pending: false);
        var change = new MsChangeInputDto { Kind = MsChangeKind.Packages, Packages = new MsPackageChangeDto { AddAssetIds = [7], ReleaseNotes = "Faster start" } };
        var context = new MsPlanContext
        {
            Kind = MsProductKind.Msix,
            ProductId = "9NBLGGH4R315",
            FirstSubmissionDone = true,
            AssetFileNames = new Dictionary<long, string> { [7] = "Kabuk_1.0.15.0_x64.msix" },
        };

        var plan = MsChangePlanner.Plan(5, content, change, context);

        plan.Blocked.Should().BeFalse();
        plan.CreatesSubmission.Should().BeTrue();
        plan.Items.Should().ContainSingle(i => i.Group == "listing" && i.Locale == "en-us" && i.Field == "whatsNew" && i.After == "Faster start");
        plan.Items.Should().ContainSingle(i => i.Group == "package" && i.Action == "add");
        plan.Calls.Select(c => c.Method).Should().Equal("POST", "PUT", "PUT");
    }

    [Theory]
    [InlineData("PendingCommit", MsSubmissionStatus.PendingCommit)]
    [InlineData("CertificationFailed", MsSubmissionStatus.CertificationFailed)]
    [InlineData("INPROGRESS", MsSubmissionStatus.InProgress)]
    [InlineData("PUBLISHED", MsSubmissionStatus.Published)]
    [InlineData("Something", MsSubmissionStatus.Unknown)]
    public void ParseStatus_ShouldMapBothApis(string status, MsSubmissionStatus expected) => MsSubmissionJson.ParseStatus(status).Should().Be(expected);

    [Fact]
    public void ListingLimits_ShouldFollowTheCatalog()
    {
        var listing = new MsListingDto
        {
            Locale = "en-us",
            Description = new string('a', 10001),
            ShortDescription = new string('a', 1000),
            WhatsNew = new string('a', 1501),
            Features = [.. Enumerable.Repeat("f", 21)],
            SearchTerms = ["ok", new string('t', 31)],
        };

        MsListingLimits.Default.Violations(listing).Select(v => v.Field).Should().BeEquivalentTo(["description", "whatsNew", "features", "searchTerms[]"]);
        MsListingLimits.Default.ScreenshotLimit("Screenshot").Should().Be(10);
        MsListingLimits.Default.ScreenshotLimit("XboxScreenshot").Should().Be(8);
        MsListingLimits.Default.ScreenshotLimit("StoreLogoSquare").Should().BeNull();
    }

    [Theory]
    [InlineData("https://downloads.example.com/setup-1.2.exe", MsInstallerType.Exe, "X64", true, null, "https://example.com/codes", true)]
    [InlineData("http://downloads.example.com/setup.exe", MsInstallerType.Exe, "X64", true, null, "https://example.com/codes", false)]
    [InlineData("https://downloads.example.com/setup.exe", MsInstallerType.Exe, "X64", true, null, null, false)]
    [InlineData("https://downloads.example.com/setup.msi", MsInstallerType.Msi, "Arm64", false, null, null, false)]
    [InlineData("https://downloads.example.com/setup.msi", MsInstallerType.Msi, "x64", false, "/quiet", null, false)]
    [InlineData("https://downloads.example.com/setup.msi", MsInstallerType.Msi, "Arm64", false, "/quiet", null, true)]
    public void InstallerValidation_ShouldFollowTheMsiExeApi(string url, MsInstallerType type, string architecture, bool silent, string parameters, string docUrl, bool valid)
    {
        var package = new MsInstallerPackageInputDto { PackageUrl = url, PackageType = type, Architecture = architecture, Languages = ["en-us"], IsSilentInstall = silent, InstallerParameters = parameters, GenericDocUrl = docUrl };

        ApplyMsChangeCommandValidator.IsValidInstaller(package).Should().Be(valid);
    }

    [Fact]
    public void SecretExpiry_ShouldBeInTheFutureAndAtMost24MonthsAway()
    {
        var now = new DateTime(2026, 11, 18, 0, 0, 0, DateTimeKind.Utc);

        MsSecretRules.IsValidExpiry(now.AddMonths(12), now).Should().BeTrue();
        MsSecretRules.IsValidExpiry(now.AddMonths(25), now).Should().BeFalse();
        MsSecretRules.IsValidExpiry(now.AddDays(-1), now).Should().BeFalse();
        MsSecretRules.IsValidExpiry(null, now).Should().BeFalse();
    }

    private static byte[] Pe(bool signed)
    {
        var file = new byte[8192];
        const int pe = 0x80;

        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(0x3C), pe);
        "PE\0\0"u8.CopyTo(file.AsSpan(pe));
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(pe + 24), 0x20B);

        if (signed)
        {
            var security = pe + 24 + 112 + (4 * 8);

            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(security), 6000);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(security + 4), 1000);
            BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(6000), 1000);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(6004), 0x0200);
            BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(6006), 0x0002);
        }

        return file;
    }

    /// <summary>
    /// A small signed PE (certificate table past the first read) used by the integration tests too.
    /// </summary>
    public static byte[] SignedPe() => Pe(true);

    [Fact]
    public async Task AuthenticodeProbe_ShouldFindThePeCertificateTable()
    {
        var signed = await AuthenticodeProbe.InspectAsync(new MemoryStream(Pe(true)), long.MaxValue, TestContext.Current.CancellationToken);
        var unsigned = await AuthenticodeProbe.InspectAsync(new MemoryStream(Pe(false)), long.MaxValue, TestContext.Current.CancellationToken);

        signed.Status.Should().Be(MsSignatureStatus.Signed);
        unsigned.Status.Should().Be(MsSignatureStatus.Unsigned);
    }

    [Fact]
    public async Task AuthenticodeProbe_ShouldFindTheMsiSignatureStream_AcrossReads()
    {
        var header = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        var signed = header.Concat(new byte[90000]).Concat(Encoding.Unicode.GetBytes("\u0005DigitalSignature")).Concat(new byte[100]).ToArray();
        var unsigned = header.Concat(new byte[90000]).ToArray();

        (await AuthenticodeProbe.InspectAsync(new MemoryStream(signed), long.MaxValue, TestContext.Current.CancellationToken)).Status.Should().Be(MsSignatureStatus.Signed);
        (await AuthenticodeProbe.InspectAsync(new MemoryStream(unsigned), long.MaxValue, TestContext.Current.CancellationToken)).Status.Should().Be(MsSignatureStatus.Unsigned);
        (await AuthenticodeProbe.InspectAsync(new MemoryStream(Encoding.ASCII.GetBytes("plain text")), long.MaxValue, TestContext.Current.CancellationToken)).Status.Should().Be(MsSignatureStatus.Unsigned);
    }

    [Theory]
    [InlineData("10.0.0.1", false)]
    [InlineData("192.168.1.20", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("20.42.65.92", true)]
    [InlineData("2603:1030::1", true)]
    public void DownloadGuard_ShouldOnlyAllowPublicAddresses(string address, bool expected) => DownloadGuard.IsPublic(IPAddress.Parse(address)).Should().Be(expected);

    [Fact]
    public void RankFlights_ShouldFollowTheRankHigherThanChain()
    {
        MsFlightRecord Flight(string id, string higherThan) => new(id, id, [], higherThan, null, null);

        var ranks = MsSyncJobHandler.RankFlights([Flight("low", null), Flight("top", "mid"), Flight("mid", "low")]);

        ranks.Should().BeEquivalentTo(new Dictionary<string, int> { ["top"] = 1, ["mid"] = 2, ["low"] = 3 });
    }

    [Fact]
    public void AssistedSteps_ShouldListTheFirstSubmissionAndPricing_WhenPending()
    {
        var product = new MsProductDto { ProductId = "9NBLGGH4R315", Kind = MsProductKind.Msix, FirstSubmissionDone = false, PricingV2 = true };

        var steps = MsAssistedSteps.For(product, assistedAddons: true, failedCertification: false, "https://partner.microsoft.com/dashboard/products/{productId}/overview", key => key);

        steps.Where(s => s.Pending).Select(s => s.Key).Should().BeEquivalentTo([MsAssistedSteps.FirstSubmission, MsAssistedSteps.Pricing, MsAssistedSteps.AddonUpdate]);
        steps.Should().Contain(s => s.Key == MsAssistedSteps.Reviews && !s.Pending);
        steps.Single(s => s.Key == MsAssistedSteps.FirstSubmission).Url.Should().Contain("9NBLGGH4R315");
    }

    [Fact]
    public void ListingLimits_ShouldComeFromTheRuleCatalogByRuleId()
    {
        var rule = new Storeasy.Domain.Rules.CatalogRule { Id = MsListingLimits.SearchTermsRule, Params = new Storeasy.Domain.Rules.RuleParams { MaxItems = 5, MaxCharsPerItem = 25 } };
        var catalog = new Storeasy.Domain.Rules.RuleCatalog("test", DateTime.UtcNow, [rule], "test");

        var limits = MsListingLimits.From(catalog);

        limits.SearchTermCount.Should().Be(5);
        limits.SearchTermLength.Should().Be(25);
        limits.Description.Should().Be(10000, "a rule missing from the catalog keeps the catalog v1 value");
        limits.Violations(new MsListingDto { Locale = "en-us", SearchTerms = ["a", "b", "c", "d", "e", "f"] }).Select(v => v.Field).Should().Equal("searchTerms");
    }

    [Fact]
    public void EncodedRead_ShouldRoundTrip()
    {
        var request = new MsContentReader.Request { Kind = MsProductKind.Msix, ProductId = "9NBLGGH4R315", Target = MsSubmissionTarget.Flight, FlightId = "f1", RolloutSubmissionId = "115" };

        var encoded = MsContentReader.Encode(request, MsChangeKind.Rollout);

        MsContentReader.TryDecode(encoded, "9NBLGGH4R315", out var decoded, out var kind).Should().BeTrue();
        kind.Should().Be(MsChangeKind.Rollout);
        decoded.Should().BeEquivalentTo(request);
        encoded.Length.Should().BeLessThanOrEqualTo(100, "it is kept in the change plan's version id column");
        MsContentReader.TryDecode("1.2.0", "x", out _, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(MsAssistedReason.PricingV2, MsAssistedSteps.Pricing)]
    [InlineData(MsAssistedReason.ManualEdit, MsAssistedSteps.ManualEdit)]
    [InlineData(MsAssistedReason.MandatoryUpdate, MsAssistedSteps.MandatoryUpdate)]
    [InlineData(MsAssistedReason.StoreManagedConsumable, MsAssistedSteps.AddonUpdate)]
    public void BlockedPlans_ShouldMapToPersistedAssistedSteps(MsAssistedReason reason, string key)
    {
        MsAssistedSteps.KeyOf(reason).Should().Be(key);
        Storeasy.Application.Utils.Content.AssistedStepCatalog.Find(new Storeasy.Domain.Rules.RuleCatalog("test", DateTime.UtcNow, [], "test"), StoreKind.Microsoft, key).Should().NotBeNull();
    }
}
