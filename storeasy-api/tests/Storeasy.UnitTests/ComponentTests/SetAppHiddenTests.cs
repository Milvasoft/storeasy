using FluentAssertions;
using Milvasoft.DataAccess.EfCore.Utils;
using Moq;
using Storeasy.Application.Features.Apps.SetAppHidden;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Constants;
using Storeasy.Domain.Apps;
using Storeasy.Domain.Enums;
using Storeasy.UnitTests.TestSupport;
using System.Linq.Expressions;

namespace Storeasy.UnitTests.ComponentTests;

[Trait("Component Unit Tests", "Hiding and showing an app: the handler and the validator.")]
public class SetAppHiddenTests
{
    private const int _organizationId = 7;
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed record Harness(SetAppHiddenCommandHandler Handler, Mock<IStoreasyRepositoryBase<App>> Repository, Mock<IAuditTrail> AuditTrail);

    private static Harness Create(params App[] rows)
    {
        var repository = new Mock<IStoreasyRepositoryBase<App>>();

        repository.Setup(r => r.GetFirstOrDefaultAsync(It.IsAny<Expression<Func<App, bool>>>(),
                                                       It.IsAny<Expression<Func<App, App>>>(),
                                                       It.IsAny<Expression<Func<App, bool>>>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<bool>(),
                                                       It.IsAny<CancellationToken>()))
                  .Returns((Expression<Func<App, bool>> condition, Expression<Func<App, App>> projection, Expression<Func<App, bool>> _, bool _, bool _, CancellationToken _)
                      => Task.FromResult(rows.AsQueryable().Where(condition).Select(projection).FirstOrDefault()));

        repository.Setup(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync(1);

        var auditTrail = new Mock<IAuditTrail>();

        return new Harness(new SetAppHiddenCommandHandler(repository.Object, auditTrail.Object, new FixedTime()), repository, auditTrail);
    }

    private static App Row(int id, DateTime? hiddenDate = null, int organizationId = _organizationId) => new() { Id = id, OrganizationId = organizationId, Name = $"App {id}", HiddenDate = hiddenDate };

    [Theory]
    [InlineData(true, AuditAction.AppHidden)]
    [InlineData(false, AuditAction.AppUnhidden)]
    public async Task Handle_ShouldChangeTheStateAndAuditIt(bool hidden, AuditAction action)
    {
        // Arrange
        var harness = Create(Row(1, hidden ? null : _now.UtcDateTime.AddDays(-3)));

        // Act
        var response = await harness.Handler.Handle(new SetAppHiddenCommand { OrganizationId = _organizationId, AppId = 1, Hidden = hidden }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        response.Data.Should().Be(1);
        harness.Repository.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()), Times.Once);
        harness.AuditTrail.Verify(a => a.WriteAsync(_organizationId, action, "1", It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_WhenTheAppIsAlreadyInTheAskedState_ShouldWriteNothing(bool hidden)
    {
        // Arrange
        var harness = Create(Row(1, hidden ? _now.UtcDateTime.AddDays(-3) : null));

        // Act
        var response = await harness.Handler.Handle(new SetAppHiddenCommand { OrganizationId = _organizationId, AppId = 1, Hidden = hidden }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeTrue();
        harness.Repository.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()), Times.Never);
        harness.AuditTrail.Verify(a => a.WriteAsync(It.IsAny<int>(), It.IsAny<AuditAction>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<StoreKind?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithAnAppOfAnotherOrganization_ShouldReportNotFound()
    {
        // Arrange
        var harness = Create(Row(1, organizationId: _organizationId + 1));

        // Act
        var response = await harness.Handler.Handle(new SetAppHiddenCommand { OrganizationId = _organizationId, AppId = 1, Hidden = true }, TestContext.Current.CancellationToken);

        // Assert
        response.IsSuccess.Should().BeFalse();
        response.Messages.Should().Contain(m => m.Message == MessageKey.AppNotFound);
        harness.Repository.Verify(r => r.ExecuteUpdateAsync(It.IsAny<Expression<Func<App, bool>>>(), It.IsAny<SetPropertyBuilder<App>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(5, true)]
    public void Validator_ShouldRequireAnAppId(int appId, bool valid)
    {
        // Arrange
        var validator = new SetAppHiddenCommandValidator(FakeLocalizer.Create().Object);

        // Act
        var result = validator.Validate(new SetAppHiddenCommand { OrganizationId = _organizationId, AppId = appId, Hidden = true });

        // Assert
        result.IsValid.Should().Be(valid);
    }
}
