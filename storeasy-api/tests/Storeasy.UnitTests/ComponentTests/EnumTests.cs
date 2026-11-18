using FluentAssertions;
using Storeasy.Domain.Enums;
using System.ComponentModel;

namespace Storeasy.UnitTests.ComponentTests;

[Trait("Enum Unit Tests", "Enums unit tests.")]
public class EnumTests
{
    [Theory]
    [InlineData(UserActivity.CreateUser, "User added")]
    [InlineData(UserActivity.UpdateUser, "User updated")]
    [InlineData(UserActivity.DeleteUser, "User deleted")]
    [InlineData(UserActivity.CreateRole, "Role added")]
    [InlineData(UserActivity.UpdateRole, "Role updated")]
    [InlineData(UserActivity.DeleteRole, "Role deleted")]
    [InlineData(UserActivity.UpdateLanguages, "Languages updated")]
    [InlineData(UserActivity.UpdateFeatureFlag, "Feature flag updated")]
    public void UserActivity_DescriptionAttribute_ShouldMatch(UserActivity activity, string expectedDescription)
    {
        // Arrange
        var type = typeof(UserActivity);
        var memberInfo = type.GetMember(activity.ToString());
        var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
        var descriptionAttribute = (DescriptionAttribute)attributes[0];

        // Act
        var description = descriptionAttribute.Description;

        // Assert
        description.Should().Be(expectedDescription);
    }

    [Theory]
    [InlineData(UserActivity.CreateUser, 0)]
    [InlineData(UserActivity.UpdateUser, 1)]
    [InlineData(UserActivity.DeleteUser, 2)]
    [InlineData(UserActivity.CreateRole, 3)]
    [InlineData(UserActivity.UpdateRole, 4)]
    [InlineData(UserActivity.DeleteRole, 5)]
    [InlineData(UserActivity.UpdateLanguages, 15)]
    [InlineData(UserActivity.UpdateFeatureFlag, 16)]
    public void UserActivity_Value_ShouldMatch(UserActivity activity, byte expectedValue)
    {
        // Act
        var value = (byte)activity;

        // Assert
        value.Should().Be(expectedValue);
    }

    [Theory]
    [InlineData(UserType.Manager, "Manager user type")]
    [InlineData(UserType.AppUser, "Application user type")]
    public void UserType_DescriptionAttribute_ShouldMatch(UserType userType, string expectedDescription)
    {
        // Arrange
        var type = typeof(UserType);
        var memberInfo = type.GetMember(userType.ToString());
        var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
        var descriptionAttribute = (DescriptionAttribute)attributes[0];

        // Act
        var description = descriptionAttribute.Description;

        // Assert
        description.Should().Be(expectedDescription);
    }

    [Theory]
    [InlineData(UserType.Manager, 1)]
    [InlineData(UserType.AppUser, 2)]
    public void UserType_Value_ShouldMatch(UserType type, byte expectedValue)
    {
        // Act
        var value = (byte)type;

        // Assert
        value.Should().Be(expectedValue);
    }
}