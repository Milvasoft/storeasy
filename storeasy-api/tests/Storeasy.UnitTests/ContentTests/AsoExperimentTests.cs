using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Application.Utils.Experiments;
using Storeasy.Application.Utils.Content;
using Storeasy.Domain.Assets;
using Storeasy.Domain.Content;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Experiments;
using Storeasy.Domain.JsonModels;
using Storeasy.Domain.Rules;
using Storeasy.Infrastructure.Services;
using Storeasy.UnitTests.TestSupport;

namespace Storeasy.UnitTests.ContentTests;

[Trait("Content Unit Tests", "Store listing experiment rules, planner and store merge.")]
public class AsoExperimentTests
{
    private const string _ipad = "APP_IPAD_PRO_3GEN_129";

    private static readonly StoreAppContext _apple = new(1, 10, 5, "Pusula", StoreKind.Apple, "1234567890", 3, null);
    private static readonly Lazy<RuleCatalog> _catalog = new(() => RulesCatalog.Load(null).Current);

    [Theory]
    [InlineData(StoreKind.Apple, AsoExperimentKind.ProductPageOptimization, AsoStoreSupport.Api)]
    [InlineData(StoreKind.Apple, AsoExperimentKind.CustomProductPage, AsoStoreSupport.Api)]
    [InlineData(StoreKind.Google, AsoExperimentKind.PlayListingExperiment, AsoStoreSupport.Assisted)]
    [InlineData(StoreKind.Google, AsoExperimentKind.PlayCustomListing, AsoStoreSupport.Assisted)]
    [InlineData(StoreKind.Google, AsoExperimentKind.ProductPageOptimization, AsoStoreSupport.NotSupported)]
    [InlineData(StoreKind.Microsoft, AsoExperimentKind.PlayListingExperiment, AsoStoreSupport.NotSupported)]
    public void Support_ShouldFollowWhatEachStoreApiOffers(StoreKind store, AsoExperimentKind kind, AsoStoreSupport expected)
        => AsoExperimentRules.Support(store, kind).Should().Be(expected);

    [Fact]
    public void KindsOf_ShouldOfferNothingForTheMicrosoftStore()
    {
        AsoExperimentRules.KindsOf(StoreKind.Microsoft).Should().BeEmpty();
        AsoExperimentRules.KindsOf(StoreKind.Apple).Should().Equal(AsoExperimentKind.ProductPageOptimization, AsoExperimentKind.CustomProductPage);
    }

    [Theory]
    [InlineData("PREPARE_FOR_SUBMISSION", false, false, AsoExperimentStatus.Prepared)]
    [InlineData("READY_FOR_REVIEW", false, false, AsoExperimentStatus.Prepared)]
    [InlineData("WAITING_FOR_REVIEW", false, false, AsoExperimentStatus.InReview)]
    [InlineData("IN_REVIEW", false, false, AsoExperimentStatus.InReview)]
    [InlineData("ACCEPTED", false, false, AsoExperimentStatus.Approved)]
    [InlineData("APPROVED", true, false, AsoExperimentStatus.Running)]
    [InlineData("APPROVED", true, true, AsoExperimentStatus.Completed)]
    [InlineData("REJECTED", false, false, AsoExperimentStatus.Rejected)]
    [InlineData("STOPPED", true, true, AsoExperimentStatus.Stopped)]
    [InlineData("COMPLETED", true, true, AsoExperimentStatus.Completed)]
    public void StatusOfExperiment_ShouldMapAppleStates(string state, bool started, bool ended, AsoExperimentStatus expected)
        => AsoExperimentRules.StatusOfExperiment(state, started ? new DateTime(2026, 10, 1) : null, ended ? new DateTime(2026, 11, 1) : null).Should().Be(expected);

    [Theory]
    [InlineData("PREPARE_FOR_SUBMISSION", true, AsoExperimentStatus.Prepared)]
    [InlineData("IN_REVIEW", true, AsoExperimentStatus.InReview)]
    [InlineData("APPROVED", true, AsoExperimentStatus.Running)]
    [InlineData("APPROVED", false, AsoExperimentStatus.Approved)]
    [InlineData("REJECTED", true, AsoExperimentStatus.Rejected)]
    public void StatusOfCustomPage_ShouldTreatAnApprovedVisiblePageAsLive(string state, bool visible, AsoExperimentStatus expected)
        => AsoExperimentRules.StatusOfCustomPage(state, visible).Should().Be(expected);

    [Fact]
    public void Operations_ShouldFollowTheExperimentLifecycle()
    {
        var ppo = AsoExperimentKind.ProductPageOptimization;

        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Draft, false, null, false).Should().Equal(AsoExperimentOperation.Push);
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Prepared, true, true, false).Should().Equal(AsoExperimentOperation.Push, AsoExperimentOperation.Submit);
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Prepared, true, false, false).Should().Equal(AsoExperimentOperation.Push, AsoExperimentOperation.Start);
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.InReview, true, true, false).Should().BeEmpty();
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Approved, true, true, false).Should().Equal(AsoExperimentOperation.Start);
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Running, true, true, false).Should().Equal(AsoExperimentOperation.Stop);
        AsoExperimentRules.Operations(ppo, AsoExperimentStatus.Completed, true, true, true).Should().Equal(AsoExperimentOperation.ApplyWinner);
        AsoExperimentRules.Operations(AsoExperimentKind.CustomProductPage, AsoExperimentStatus.Running, true, null, false).Should().Equal(AsoExperimentOperation.Push);
        AsoExperimentRules.Operations(AsoExperimentKind.PlayListingExperiment, AsoExperimentStatus.Draft, false, null, false).Should().BeEmpty();
    }

    [Theory]
    [InlineData(AsoExperimentOperation.Push, OrgPermission.EditContent, false)]
    [InlineData(AsoExperimentOperation.Submit, OrgPermission.SubmitForReview, true)]
    [InlineData(AsoExperimentOperation.Start, OrgPermission.ReleaseProduction, true)]
    [InlineData(AsoExperimentOperation.Stop, OrgPermission.ReleaseProduction, true)]
    [InlineData(AsoExperimentOperation.ApplyWinner, OrgPermission.ReleaseProduction, true)]
    public void PermissionAndConfirmation_ShouldGuardLiveChanges(AsoExperimentOperation operation, OrgPermission permission, bool confirm)
    {
        AsoExperimentRules.PermissionOf(operation).Should().Be(permission);
        AsoExperimentRules.NeedsConfirmation(operation).Should().Be(confirm);
    }

    [Fact]
    public void Validate_ShouldCheckVariantCountTrafficAndTextLimits()
    {
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 30, []).Key.Should().Be(MessageKey.AsoVariantsRequired);
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 30, [Variant("A"), Variant("B"), Variant("C"), Variant("D")]).Should().Be((MessageKey.AsoTooManyVariants, "3"));
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 0, [Variant("A")]).Key.Should().Be(MessageKey.AsoTrafficInvalid);
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 30, [Variant("A"), Variant("A")]).Key.Should().Be(MessageKey.AsoVariantKeyInvalid);
        AsoExperimentPlanner.Validate(AsoExperimentKind.CustomProductPage, null, [Variant("A", promo: new string('x', 171))]).Key.Should().Be(MessageKey.AsoTextTooLong);
        AsoExperimentPlanner.Validate(AsoExperimentKind.PlayListingExperiment, 50, [new AsoExperimentVariant { Key = "A", Name = "A", Locales = [new AsoVariantLocale { Locale = "tr-TR", ShortDescription = new string('x', 81) }] }]).Key.Should().Be(MessageKey.AsoTextTooLong);
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 30, [Variant("A", classKey: "NOPE")]).Key.Should().Be(MessageKey.MediaClassUnknown);
        AsoExperimentPlanner.Validate(AsoExperimentKind.ProductPageOptimization, 30, [Variant("A"), Variant("B")]).Key.Should().BeNull();
    }

    [Fact]
    public void Build_Push_ShouldPlanANewExperimentWithTreatmentsAndCheckedMedia()
    {
        var experiment = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Draft, [Variant("A", icon: "AppIconDark"), Variant("B", assets: [1, 2])]);

        var result = AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.Push));

        result.IsSuccess.Should().BeTrue(result.ErrorDetail);

        var draft = result.Draft;
        draft.Kind.Should().Be(ChangePlanKind.AsoExperiment);
        draft.RequiredPermission.Should().Be(OrgPermission.EditContent);
        draft.ConfirmText.Should().BeNull();
        draft.BaseKeys.Should().BeEmpty();
        draft.Title.Should().Be("Create experiment 'Icon test'");
        draft.Items.Should().Contain(i => i.Group == AsoExperimentPlanner.ExperimentGroup && i.Field == "trafficProportion" && i.After == "30" && i.Action == ChangeAction.Create);
        draft.Items.Should().Contain(i => i.Group == "treatment A" && i.Field == "appIconName" && i.After == "AppIconDark");
        draft.Items.Should().Contain(i => i.Group == "treatment B" && i.Locale == "en-US" && i.Field == $"Screenshot:{_ipad}" && i.After == "2 files");
        draft.Calls.Should().Contain(c => c.Method == "POST" && c.Path == "/v2/appStoreVersionExperiments");
        draft.Files.Should().HaveCount(2).And.OnlyContain(f => f.Result == MediaPlanner.Accepted);

        var changes = draft.CustomChanges.Should().BeOfType<AsoExperimentChangeSet>().Subject;
        changes.ExternalId.Should().BeNull();
        changes.TrafficProportion.Should().Be(30);
        changes.Treatments.Select(t => t.Key).Should().Equal("A", "B");
        changes.Treatments[1].Locales.Single().Media.Single().Items.Select(i => i.Md5).Should().Equal("md5-1", "md5-2");
    }

    [Fact]
    public void Build_Push_ShouldRejectAVariantThatChangesNothingOrAWrongSizedFile()
    {
        var empty = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Draft, [Variant("A")]);

        AsoExperimentPlanner.Build(Input(empty, AsoExperimentOperation.Push)).ErrorKey.Should().Be(MessageKey.AsoVariantEmpty);

        var wrong = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Draft, [Variant("A", assets: [3])]);

        AsoExperimentPlanner.Build(Input(wrong, AsoExperimentOperation.Push)).ErrorKey.Should().Be(MessageKey.MediaRejected);
    }

    [Fact]
    public void Build_Push_ShouldUpdateAWrittenExperimentAndDeleteDroppedTreatments()
    {
        var experiment = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Prepared, [Variant("A", icon: "AppIconDark", externalId: "t1")], externalId: "e1");
        var read = new StoreAsoRead
        {
            Experiments = [new StoreExperimentState
            {
                Id = "e1",
                Name = "Icon test",
                State = "PREPARE_FOR_SUBMISSION",
                TrafficProportion = 20,
                Treatments = [new StoreTreatmentState { Id = "t1", Name = "A", AppIconName = "AppIconDark" }, new StoreTreatmentState { Id = "t2", Name = "Old" }],
            }],
        };

        var draft = AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.Push, read)).Draft;

        draft.Title.Should().StartWith("Update experiment");
        draft.Items.Should().Contain(i => i.Field == "trafficProportion" && i.Before == "20" && i.After == "30" && i.Action == ChangeAction.Update);
        draft.Items.Should().Contain(i => i.Field == "treatment" && i.Before == "Old" && i.Action == ChangeAction.Delete);
        draft.Items.Should().NotContain(i => i.Field == "appIconName");
        draft.BaseKeys.Should().Equal(AsoExperimentContent.KeyOf(AsoExperimentKind.ProductPageOptimization, "e1", "state"), AsoExperimentContent.KeyOf(AsoExperimentKind.ProductPageOptimization, "e1", "treatments"));
        draft.Calls.Should().Contain(c => c.Method == "DELETE" && c.Path == "/v1/appStoreVersionExperimentTreatments/t2");
    }

    [Fact]
    public void Build_Start_ShouldNeedTheReleasePermissionAndTheTypedConfirmation()
    {
        var experiment = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Approved, [Variant("A", icon: "AppIconDark")], externalId: "e1");

        var draft = AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.Start)).Draft;

        draft.RequiredPermission.Should().Be(OrgPermission.ReleaseProduction);
        draft.ConfirmText.Should().Be("Pusula Icon test");
        draft.Items.Should().ContainSingle(i => i.Field == "started" && i.Before == "false" && i.After == "true");
        ((AsoExperimentChangeSet)draft.CustomChanges).Operation.Should().Be(AsoExperimentOperation.Start);
    }

    [Fact]
    public void Build_ShouldRefuseAnOperationTheStateDoesNotAllowAndPlayRecords()
    {
        var draft = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Draft, [Variant("A", icon: "X")]);

        AsoExperimentPlanner.Build(Input(draft, AsoExperimentOperation.Start)).ErrorKey.Should().Be(MessageKey.AsoExperimentOperationNotAllowed);

        var play = Experiment(AsoExperimentKind.PlayListingExperiment, AsoExperimentStatus.Draft, [Variant("A")]);

        AsoExperimentPlanner.Build(Input(play, AsoExperimentOperation.Push)).ErrorKey.Should().Be(MessageKey.AsoExperimentNoApi);
    }

    [Fact]
    public void Build_Submit_ShouldGoWithTheVersionWhenAsked()
    {
        var experiment = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Prepared, [Variant("A", icon: "X")], externalId: "e1");
        experiment.ReviewRequired = true;

        var draft = AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.Submit, withVersion: true)).Draft;

        draft.RequiredPermission.Should().Be(OrgPermission.SubmitForReview);
        draft.Title.Should().EndWith("with the app version");
        draft.Calls.Should().ContainSingle(c => c.Path == "/v1/reviewSubmissionItems");
        ((AsoExperimentChangeSet)draft.CustomChanges).WithVersion.Should().BeTrue();
    }

    [Fact]
    public void Build_CustomPage_ShouldPlanPromotionalTextsAndMedia()
    {
        var page = Experiment(AsoExperimentKind.CustomProductPage, AsoExperimentStatus.Draft, [Variant("A", promo: "Study smarter.", assets: [1])]);
        page.TrafficProportion = null;

        var draft = AsoExperimentPlanner.Build(Input(page, AsoExperimentOperation.Push)).Draft;

        draft.Title.Should().Be("Create custom product page 'Icon test'");
        draft.Items.Should().Contain(i => i.Group == "page" && i.Field == "promotionalText" && i.After == "Study smarter." && i.Action == ChangeAction.Create);
        draft.Calls.Should().Contain(c => c.Path == "/v1/appCustomProductPages");

        var change = ((AsoExperimentChangeSet)draft.CustomChanges).Treatments.Single();
        change.Locales.Single().PromotionalText.Should().Be("Study smarter.");
        change.Locales.Single().Media.Should().ContainSingle();
    }

    [Fact]
    public void Build_ApplyWinner_ShouldWriteTheWinnersMediaToTheEditableVersion()
    {
        var experiment = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.Completed, [Variant("A", icon: "AppIconDark", assets: [1, 2])], externalId: "e1");
        experiment.WinnerVariantKey = "A";
        var version = new StoreContentSnapshot { Store = StoreKind.Apple, VersionId = "ver1", VersionString = "1.1", VersionEditable = true };
        version.Media.Add(new MediaSetState { Kind = MediaKind.Screenshot, Locale = "en-US", ClassKey = _ipad, Items = [new MediaItemState { Id = "s1", Checksum = "old" }] });

        var result = AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.ApplyWinner, version: version));

        result.IsSuccess.Should().BeTrue(result.ErrorDetail);
        result.Draft.VersionId.Should().Be("ver1");
        result.Draft.ConfirmText.Should().Be("Pusula Icon test");
        result.Draft.BaseKeys.Should().Equal(ContentSnapshots.MediaKey(MediaKind.Screenshot, "en-US", _ipad));
        result.Draft.Warnings.Should().ContainSingle(w => w.Key == MessageKey.AsoWinnerIconInBuild);
        result.Draft.Items.Should().ContainSingle(i => i.Before == "1 files" && i.After == "2 files");
        ((AsoExperimentChangeSet)result.Draft.CustomChanges).VersionMedia.Should().ContainSingle();

        version.VersionEditable = false;

        AsoExperimentPlanner.Build(Input(experiment, AsoExperimentOperation.ApplyWinner, version: version)).ErrorKey.Should().Be(MessageKey.VersionNotEditable);
    }

    [Fact]
    public void MergeAndDiscover_ShouldCopyTheStoreStateAndFindPagesMadeInAppStoreConnect()
    {
        var record = Experiment(AsoExperimentKind.ProductPageOptimization, AsoExperimentStatus.InReview, [Variant("A", icon: "X")], externalId: "e1");
        record.Variants[0].Name = "Dark icon";
        var read = new StoreAsoRead
        {
            Experiments = [new StoreExperimentState { Id = "e1", State = "APPROVED", StartDate = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), ReviewRequired = true, Treatments = [new StoreTreatmentState { Id = "t9", Name = "Dark icon" }] }],
            CustomPages = [new StoreCustomPageState { Id = "p1", Name = "Students", Url = "https://apps.apple.com/app/id1?ppid=p1", Visible = true, VersionId = "v2", VersionState = "APPROVED", Localizations = [new StoreCustomPageLocalization { Locale = "en-US", PromotionalText = "Hi" }] }],
        };

        AsoExperimentContent.Merge(record, read).Should().BeTrue();
        record.Status.Should().Be(AsoExperimentStatus.Running);
        record.StartDate.Should().Be(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc));
        record.Variants[0].ExternalId.Should().Be("t9");

        var found = AsoExperimentContent.Discover(_apple, read, new HashSet<string> { "e1" });

        found.Should().ContainSingle();
        found[0].Kind.Should().Be(AsoExperimentKind.CustomProductPage);
        found[0].Url.Should().Contain("ppid=p1");
        found[0].Status.Should().Be(AsoExperimentStatus.Running);
        found[0].Variants.Single().Locales.Single().PromotionalText.Should().Be("Hi");

        var snapshot = AsoExperimentContent.AsContent(read);
        snapshot.Values[AsoExperimentContent.KeyOf(AsoExperimentKind.CustomProductPage, "p1", "version")].Should().Be("v2");
    }

    private static AsoPlanInput Input(AsoExperiment experiment, AsoExperimentOperation operation, StoreAsoRead read = null, bool withVersion = false, StoreContentSnapshot version = null)
        => new(_apple, experiment, operation, withVersion, null, read ?? Read(experiment), Files(), version, _catalog.Value, FakeLocalizer.Create().Object);

    private static StoreAsoRead Read(AsoExperiment experiment)
    {
        if (experiment.ExternalId == null)
            return new StoreAsoRead();

        return new StoreAsoRead
        {
            Experiments = [new StoreExperimentState
            {
                Id = experiment.ExternalId,
                Name = experiment.Name,
                State = "PREPARE_FOR_SUBMISSION",
                TrafficProportion = experiment.TrafficProportion,
                Treatments = [.. experiment.Variants.Where(v => v.ExternalId != null).Select(v => new StoreTreatmentState { Id = v.ExternalId, Name = v.Name, AppIconName = v.AppIconName })],
            }],
        };
    }

    private static List<InspectedAsset> Files() =>
    [
        new(1, "org/1.png", "1.png", "image/png", 1000, "md5-1", "sha-1", new ImageHeader("png", 2064, 2752, false)),
        new(2, "org/2.png", "2.png", "image/png", 1000, "md5-2", "sha-2", new ImageHeader("png", 2064, 2752, false)),
        new(3, "org/3.png", "3.png", "image/png", 1000, "md5-3", "sha-3", new ImageHeader("png", 100, 100, false)),
    ];

    private static AsoExperiment Experiment(AsoExperimentKind kind, AsoExperimentStatus status, List<AsoExperimentVariant> variants, string externalId = null) => new()
    {
        Id = 7,
        OrganizationId = 1,
        AppId = 5,
        StoreAppId = 10,
        Store = AsoExperimentRules.IsApple(kind) ? StoreKind.Apple : StoreKind.Google,
        Kind = kind,
        Name = "Icon test",
        Status = status,
        TrafficProportion = 30,
        ExternalId = externalId,
        Variants = variants,
    };

    private static AsoExperimentVariant Variant(string key, string icon = null, string promo = null, List<long> assets = null, string classKey = _ipad, string externalId = null) => new()
    {
        Key = key,
        Name = key,
        AppIconName = icon,
        ExternalId = externalId,
        Locales = [new AsoVariantLocale
        {
            Locale = "en-US",
            PromotionalText = promo,
            Media = assets == null && classKey == _ipad ? [] : [new AsoVariantMedia { Kind = MediaKind.Screenshot, ClassKey = classKey, AssetIds = assets ?? [1] }],
        }],
    };
}
