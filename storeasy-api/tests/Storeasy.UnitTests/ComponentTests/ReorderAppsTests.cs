using FluentAssertions;
using Milvasoft.Components.Rest.Enums;
using Milvasoft.Components.Rest.MilvaResponse;
using Milvasoft.Components.Rest.Request;
using Milvasoft.DataAccess.EfCore.Utils;
using Moq;
using Storeasy.Application.Dtos.AppDtos;
using Storeasy.Application.Features.Apps.GetAppList;
using Storeasy.Application.Features.Apps.ReorderApps;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.UnitTests.TestSupport;
using System.Linq.Expressions;

namespace Storeasy.UnitTests.ComponentTests;

[Trait("Component Unit Tests", "Reordering the apps list: the handler, the validator and the default order of the list.")]
public class ReorderAppsTests
{
    private const int _organizationId = 7;

    private sealed record Harness(ReorderAppsCommandHandler Handler, Mock<IStoreasyRepositoryBase<App>> Repository, Mock<IAuditTrail> AuditTrail);

    private static Harness Create(params App[] rows)
    {
        var repository = new Mock<IStoreasyRepositoryBase<App>>();

        repository.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<App, bool>>>(),
                                            It.IsAny<Expression<Func<App, App>>>(),
                                            It.IsAny<Expression<Func<App, bool>>>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<CancellationToken>()))
                  .Returns((Expression<Func<App, bool>> condition, Expression<Func<App, App>> projection, Expression<Func<App, bool>> _, bool _, bool _, CancellationToken _)
                      => Task.FromResult(rows.AsQueryable().Where(condition).Select(projection).ToList()));

        repository.Setup(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(1);

        var auditTrail = new Mock<IAuditTrail>();

        return new Harness(new ReorderAppsCommandHandler(repository.Object, auditTrail.Object), repository, auditTrail);
    }

    private static App Row(int id, int? displayOrder = null, int organizationId = _organizationId) => new() { Id = id, OrganizationId = organizationId, Name = $"App {id}", DisplayOrder = displayOrder };

    private static void VerifyUpdated(Harness harness, Times times)
        => harness.Repository.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()), times);

    private static void VerifyAudited(Harness harness, Times times)
        => harness.AuditTrail.Verify(a => a.WriteAsync(_organizationId, AuditAction.AppsReordered, _organizationId.ToString(), It.IsAny<string>(), null, It.IsAny<object>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Handle_ShouldWriteTheNewOrderOnceAndAuditIt()
    {
        // Arrange
        var harness = Create(Row(1), Row(2), Row(3));

        // Act
        var response = await harness.Handler.Handle(new ReorderAppsCommand { OrganizationId = _organizationId, AppIds = [3, 1, 2] }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Data.Should().Be(3);
        VerifyUpdated(harness, Times.Once());
        VerifyAudited(harness, Times.Once());
    }

    [Fact]
    public async Task Handle_WhenTheOrderIsAlreadyInPlace_ShouldWriteNothing()
    {
        // Arrange
        var harness = Create(Row(1, 1), Row(2, 2), Row(3, 0));

        // Act
        var response = await harness.Handler.Handle(new ReorderAppsCommand { OrganizationId = _organizationId, AppIds = [3, 1, 2] }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        VerifyUpdated(harness, Times.Never());
        VerifyAudited(harness, Times.Never());
    }

    [Fact]
    public async Task Handle_WhenAPlacedAppIsLeftOut_ShouldWriteToTakeItsPlaceAway()
    {
        // Arrange
        var harness = Create(Row(1, 0), Row(2, 1), Row(3, 2));

        // Act
        var response = await harness.Handler.Handle(new ReorderAppsCommand { OrganizationId = _organizationId, AppIds = [1, 2] }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        VerifyUpdated(harness, Times.Once());
        VerifyAudited(harness, Times.Once());
    }

    [Fact]
    public async Task Handle_WithAnAppOfAnotherOrganization_ShouldReportNotFoundAndWriteNothing()
    {
        // Arrange
        var harness = Create(Row(1), Row(2, organizationId: _organizationId + 1));

        // Act
        var response = await harness.Handler.Handle(new ReorderAppsCommand { OrganizationId = _organizationId, AppIds = [1, 2] }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Messages.Should().Contain(m => m.Message == MessageKey.AppNotFound);
        VerifyUpdated(harness, Times.Never());
        VerifyAudited(harness, Times.Never());
    }

    public static TheoryData<List<int>, bool> AppIdLists => new()
    {
        { null, false },
        { [], false },
        { [1, 0], false },
        { [1, -2], false },
        { [1, 2, 1], false },
        { [.. Enumerable.Range(1, ReorderAppsCommand.MaxAppCount + 1)], false },
        { [5], true },
        { [3, 1, 2], true },
        { [.. Enumerable.Range(1, ReorderAppsCommand.MaxAppCount)], true },
    };

    [Theory]
    [MemberData(nameof(AppIdLists))]
    public void Validator_ShouldRequireDistinctAppIdsWithinTheLimit(List<int> appIds, bool valid)
    {
        // Arrange
        var validator = new ReorderAppsCommandValidator(FakeLocalizer.Create().Object);

        // Act
        var result = validator.Validate(new ReorderAppsCommand { OrganizationId = _organizationId, AppIds = appIds });

        // Assert
        result.IsValid.Should().Be(valid);
    }

    private static Mock<IStoreasyRepositoryBase<App>> ListRepository(params AppListDto[] apps)
    {
        var repository = new Mock<IStoreasyRepositoryBase<App>>();

        repository.Setup(r => r.GetAllAsync(It.IsAny<ListRequest>(),
                                            It.IsAny<Expression<Func<App, bool>>>(),
                                            It.IsAny<Expression<Func<App, AppListDto>>>(),
                                            It.IsAny<Expression<Func<AppListDto, bool>>>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<int?>(),
                                            It.IsAny<CancellationToken>()))
                  .ReturnsAsync((ListRequest request, Expression<Func<App, bool>> _, Expression<Func<App, AppListDto>> _, Expression<Func<AppListDto, bool>> _, bool _, bool _, int? _, CancellationToken _)
                      => apps.AsQueryable().ToListResponse(request));

        repository.Setup(r => r.GetAllAsync(It.IsAny<Expression<Func<App, bool>>>(),
                                            It.IsAny<Expression<Func<App, AppListDto>>>(),
                                            It.IsAny<Expression<Func<AppListDto, bool>>>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<bool>(),
                                            It.IsAny<CancellationToken>()))
                  .Returns((Expression<Func<App, bool>> condition, Expression<Func<App, AppListDto>> _, Expression<Func<AppListDto, bool>> _, bool _, bool _, CancellationToken _)
                      => Task.FromResult(apps.Where(a => condition.Compile()(new App { Id = a.Id })).ToList()));

        return repository;
    }

    private static readonly AppListDto[] _apps =
    [
        new() { Id = 1, Name = "Zebra" },
        new() { Id = 2, Name = "Alpha", DisplayOrder = 1 },
        new() { Id = 3, Name = "Beta" },
        new() { Id = 4, Name = "Omega", DisplayOrder = 0 },
        new() { Id = 5, Name = "Delta" },
    ];

    [Fact]
    public async Task List_WithoutASort_ShouldPutThePlacedAppsFirstThenTheRestByName()
    {
        // Arrange
        var handler = new GetAppListQueryHandler(ListRepository(_apps).Object);

        // Act
        var first = await handler.Handle(new GetAppListQuery { OrganizationId = _organizationId, PageNumber = 1, RowCount = 3 }, TestContext.Current.CancellationToken);
        var second = await handler.Handle(new GetAppListQuery { OrganizationId = _organizationId, PageNumber = 2, RowCount = 3 }, TestContext.Current.CancellationToken);

        // Assert
        first.Data.Select(a => a.Id).Should().Equal(4, 2, 3);
        second.Data.Select(a => a.Id).Should().Equal(5, 1);
        first.TotalDataCount.Should().Be(5);
        first.TotalPageCount.Should().Be(2);
    }

    [Fact]
    public async Task List_WithASort_ShouldFollowTheSort()
    {
        // Arrange
        var repository = ListRepository(_apps);
        var handler = new GetAppListQueryHandler(repository.Object);

        // Act
        var response = await handler.Handle(new GetAppListQuery
        {
            OrganizationId = _organizationId,
            PageNumber = 1,
            RowCount = 10,
            Sorting = new SortRequest { SortBy = nameof(AppListDto.Name), Type = SortType.Desc },
        }, TestContext.Current.CancellationToken);

        // Assert
        response.Data.Select(a => a.Name).Should().Equal("Zebra", "Omega", "Delta", "Beta", "Alpha");
        repository.Verify(r => r.GetAllAsync(It.IsAny<Expression<Func<App, bool>>>(),
                                             It.IsAny<Expression<Func<App, AppListDto>>>(),
                                             It.IsAny<Expression<Func<AppListDto, bool>>>(),
                                             It.IsAny<bool>(),
                                             It.IsAny<bool>(),
                                             It.IsAny<CancellationToken>()), Times.Never);
    }
}
