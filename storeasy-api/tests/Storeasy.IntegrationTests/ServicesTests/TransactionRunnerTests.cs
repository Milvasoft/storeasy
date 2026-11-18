using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storeasy.Application.Interfaces;
using Storeasy.Domain;
using Storeasy.IntegrationTests.TestBase;

namespace Storeasy.IntegrationTests.ServicesTests;

[Collection(nameof(StoreasyApiCollection))]
[Trait("Service Integration Tests", "Integration tests for the transaction runner.")]
public class TransactionRunnerTests(StoreasyApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task RunAsync_WhenTheWorkThrows_ShouldRollBackItsWrites()
    {
        // Arrange: the runner opens its transaction only when interception decorated it (registered before AddMilvaInterception).
        var userName = $"rolledback{TestApi.Unique()}";

        using var scope = _factory.CreateScope();

        var transactionRunner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IStoreasyRepositoryBase<User>>();

        // Act
        var act = () => transactionRunner.RunAsync(async () =>
        {
            await userRepository.AddAsync(new User { UserName = userName, NormalizedUserName = userName.ToUpperInvariant() }, TestContext.Current.CancellationToken);

            throw new InvalidOperationException("work failed after its write");
        });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        var userExists = await _factory.DbAsync(db => db.Users.AnyAsync(u => u.UserName == userName, TestContext.Current.CancellationToken));

        userExists.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_WhenTheWorkCompletes_ShouldCommitItsWrites()
    {
        // Arrange
        var userName = $"committed{TestApi.Unique()}";

        using var scope = _factory.CreateScope();

        var transactionRunner = scope.ServiceProvider.GetRequiredService<ITransactionRunner>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IStoreasyRepositoryBase<User>>();

        // Act
        await transactionRunner.RunAsync(async () =>
        {
            await userRepository.AddAsync(new User { UserName = userName, NormalizedUserName = userName.ToUpperInvariant() }, TestContext.Current.CancellationToken);
        });

        // Assert
        var userExists = await _factory.DbAsync(db => db.Users.AnyAsync(u => u.UserName == userName, TestContext.Current.CancellationToken));

        userExists.Should().BeTrue();
    }
}
