using FluentAssertions;
using Milvasoft.DataAccess.EfCore.Utils;
using Moq;
using Storeasy.Application.Dtos.StoreDtos;
using Storeasy.Application.Features.StoreConnections.AddStoreConnection;
using Storeasy.Application.Features.StoreConnections.UpdateStoreConnection;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Stores;
using Storeasy.UnitTests.TestSupport;
using System.Linq.Expressions;

namespace Storeasy.UnitTests.ComponentTests;

[Trait("Component Unit Tests", "Store connection icons: the allow list, the add and update validators and the update handler.")]
public class StoreConnectionIconTests
{
    private const int _organizationId = 7;

    private sealed record Harness(UpdateStoreConnectionCommandHandler Handler, Mock<IStoreasyRepositoryBase<StoreConnection>> Repository, Mock<IAuditTrail> AuditTrail);

    private static Harness Create(params StoreConnection[] rows)
    {
        var repository = new Mock<IStoreasyRepositoryBase<StoreConnection>>();

        repository.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<Expression<Func<StoreConnection, bool>>>(),
                                                       It.IsAny<Expression<Func<StoreConnection, StoreConnection>>>(),
                                                       It.IsAny<Expression<Func<StoreConnection, bool>>>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<CancellationToken>()))
                  .Returns((Expression<Func<StoreConnection, bool>> condition, Expression<Func<StoreConnection, StoreConnection>> projection, Expression<Func<StoreConnection, bool>> _, bool _, bool _, CancellationToken _)
                      => Task.FromResult(rows.AsQueryable().Where(condition).Select(projection).FirstOrDefault()));

        repository.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<Expression<Func<StoreConnection, bool>>>(),
                                                       It.IsAny<Expression<Func<StoreConnection, StoreConnectionDto>>>(),
                                                       It.IsAny<Expression<Func<StoreConnectionDto, bool>>>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<CancellationToken>()))
                  .Returns((Expression<Func<StoreConnection, bool>> condition, Expression<Func<StoreConnection, StoreConnectionDto>> projection, Expression<Func<StoreConnectionDto, bool>> _, bool _, bool _, CancellationToken _)
                      => Task.FromResult(rows.AsQueryable().Where(condition).Select(projection).FirstOrDefault()));

        repository.Setup(r => r.ExecuteUpdateAsync(It.IsAny<int>(), It.IsAny<SetPropertyBuilder<StoreConnection>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(1);

        var auditTrail = new Mock<IAuditTrail>();

        return new Harness(new UpdateStoreConnectionCommandHandler(repository.Object, auditTrail.Object), repository, auditTrail);
    }

    private static StoreConnection Row(int id, StoreKind store, string icon = null, int organizationId = _organizationId)
        => new() { Id = id, OrganizationId = organizationId, Store = store, Name = $"Key {id}", Icon = icon };

    private static void VerifyUpdated(Harness harness, Times times)
        => harness.Repository.Verify(r => r.ExecuteUpdateAsync(It.IsAny<int>(), It.IsAny<SetPropertyBuilder<StoreConnection>>(), It.IsAny<CancellationToken>()), times);

    private static void VerifyAudited(Harness harness, Times times)
        => harness.AuditTrail.Verify(a => a.WriteAsync(_organizationId, AuditAction.ConnectionUpdated, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoreKind?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), times);

    [Theory]
    [InlineData(StoreKind.Apple, null, true)]
    [InlineData(StoreKind.Apple, "", true)]
    [InlineData(StoreKind.Apple, "appstore", true)]
    [InlineData(StoreKind.Google, "googleplay", true)]
    [InlineData(StoreKind.Microsoft, "msstore", true)]
    [InlineData(StoreKind.Google, "phone", true)]
    [InlineData(StoreKind.Microsoft, "rocket", true)]
    [InlineData(StoreKind.Apple, "googleplay", false)]
    [InlineData(StoreKind.Google, "msstore", false)]
    [InlineData(StoreKind.Microsoft, "appstore", false)]
    [InlineData(StoreKind.Apple, "Phone", false)]
    [InlineData(StoreKind.Apple, "https://example.com/logo.png", false)]
    public void IsAllowed_ShouldTakeGenericIconsAnywhereAndALogoOnlyOnItsStore(StoreKind store, string icon, bool allowed)
        => StoreConnectionIcons.IsAllowed(store, icon).Should().Be(allowed);

    [Fact]
    public void Keys_ShouldFitTheColumn()
    {
        string[] keys = [StoreConnectionIcons.AppStore, StoreConnectionIcons.GooglePlay, StoreConnectionIcons.MsStore, .. StoreConnectionIcons.Generic];

        keys.Should().OnlyHaveUniqueItems().And.OnlyContain(key => key.Length <= StoreConnectionIcons.MaxLength && key == key.ToLowerInvariant());
    }

    [Theory]
    [InlineData(StoreKind.Apple, null, true)]
    [InlineData(StoreKind.Apple, "appstore", true)]
    [InlineData(StoreKind.Google, " globe ", true)]
    [InlineData(StoreKind.Google, "appstore", false)]
    [InlineData(StoreKind.Apple, "unicorn", false)]
    public void AddValidator_ShouldCheckTheIconAgainstTheStore(StoreKind store, string icon, bool valid)
    {
        // Arrange
        var validator = new AddStoreConnectionCommandValidator(FakeLocalizer.Create().Object, TimeProvider.System);

        // Act
        var result = validator.Validate(new AddStoreConnectionCommand { OrganizationId = _organizationId, Store = store, Name = "Key", Secret = "{}", Icon = icon });

        // Assert
        result.Errors.Any(e => e.PropertyName == nameof(AddStoreConnectionCommand.Icon)).Should().Be(!valid);
    }

    [Theory]
    [InlineData(1, "Key", null, true)]
    [InlineData(1, "Key", "box", true)]
    [InlineData(1, "Key", "msstore", true)]
    [InlineData(1, "Key", "unicorn", false)]
    [InlineData(1, "", "box", false)]
    [InlineData(0, "Key", "box", false)]
    public void UpdateValidator_ShouldRequireANameAndAKnownIcon(int connectionId, string name, string icon, bool valid)
    {
        // Arrange
        var validator = new UpdateStoreConnectionCommandValidator(FakeLocalizer.Create().Object);

        // Act
        var result = validator.Validate(new UpdateStoreConnectionCommand { OrganizationId = _organizationId, ConnectionId = connectionId, Name = name, Icon = icon });

        // Assert
        result.IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task Handle_ShouldWriteTheIconAndTheNameAndAuditIt()
    {
        // Arrange
        var harness = Create(Row(1, StoreKind.Google));

        // Act
        var response = await harness.Handler.Handle(new UpdateStoreConnectionCommand { OrganizationId = _organizationId, ConnectionId = 1, Name = " Play team ", Icon = "googleplay" }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Data.Id.Should().Be(1);
        VerifyUpdated(harness, Times.Once());
        VerifyAudited(harness, Times.Once());
    }

    [Fact]
    public async Task Handle_WhenNothingChanges_ShouldWriteAndAuditNothing()
    {
        // Arrange
        var harness = Create(Row(1, StoreKind.Apple, "phone"));

        // Act
        var response = await harness.Handler.Handle(new UpdateStoreConnectionCommand { OrganizationId = _organizationId, ConnectionId = 1, Name = "Key 1", Icon = "phone" }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Data.Icon.Should().Be("phone");
        VerifyUpdated(harness, Times.Never());
        VerifyAudited(harness, Times.Never());
    }

    [Fact]
    public async Task Handle_WithAnotherStoresLogo_ShouldRefuseAndWriteNothing()
    {
        // Arrange
        var harness = Create(Row(1, StoreKind.Apple));

        // Act
        var response = await harness.Handler.Handle(new UpdateStoreConnectionCommand { OrganizationId = _organizationId, ConnectionId = 1, Name = "Key 1", Icon = "googleplay" }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Messages.Should().Contain(m => m.Message == MessageKey.StoreConnectionIconInvalid);
        VerifyUpdated(harness, Times.Never());
    }

    [Fact]
    public async Task Handle_WithAConnectionOfAnotherOrganization_ShouldReportNotFound()
    {
        // Arrange
        var harness = Create(Row(1, StoreKind.Apple, organizationId: _organizationId + 1));

        // Act
        var response = await harness.Handler.Handle(new UpdateStoreConnectionCommand { OrganizationId = _organizationId, ConnectionId = 1, Name = "Key 1", Icon = "box" }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Messages.Should().Contain(m => m.Message == MessageKey.StoreConnectionNotFound);
        VerifyUpdated(harness, Times.Never());
    }
}
