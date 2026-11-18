using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Storeasy.Application.Utils.Models;
using Storeasy.UnitTests.TestSupport;
using System.Text.RegularExpressions;

namespace Storeasy.UnitTests.ArchitectureTests;

/// <summary>
/// Links the API hands to the web app (notification action links, the e-mail links of <see cref="AppOptions"/>) must
/// open a page of the web app. The routes are read from the web app's source (storeasy-app/src) next to this solution;
/// a link to a path the router does not know lands on "page not found".
/// </summary>
[Trait("Architecture Tests", "Links into the web app.")]
public partial class WebLinkTests
{
    [Fact]
    public void ActionLinks_ShouldOpenAWebRoute()
    {
        // Arrange
        var routes = WebRoutes();

        // Act
        var violations = SourceCode.Files.SelectMany(file => file.Root.DescendantNodes()
                                                                   .OfType<AssignmentExpressionSyntax>()
                                                                   .Where(assignment => assignment.Left is IdentifierNameSyntax { Identifier.Text: "ActionLink" })
                                                                   .Select(assignment => (Location: file.Location(assignment), Path: LinkPath(assignment.Right)))
                                                                   .Where(link => link.Path != null && !routes.Any(route => route.IsMatch(link.Path)))
                                                                   .Select(link => $"{link.Location}: {link.Path}"));

        // Assert
        violations.ShouldBeEmpty("A notification's ActionLink must be a route of storeasy-app (App.tsx, features/routes.tsx)");
    }

    [Theory]
    [InlineData(AppOptions.VerifyEmailPath)]
    [InlineData(AppOptions.ResetPasswordPath)]
    [InlineData(AppOptions.InvitationPath)]
    [InlineData(AppOptions.GoogleCallbackPath)]
    public void EmailLinkPaths_ShouldOpenAWebRoute(string path)
    {
        // Arrange
        var routes = WebRoutes();

        // Act
        var opens = routes.Any(route => route.IsMatch("/" + path));

        // Assert
        Assert.True(opens, $"/{path} (AppOptions) is not a route of storeasy-app");
    }

    /// <summary>
    /// The path of an ActionLink expression: interpolations become one segment, the query is dropped. Null when the
    /// value is not a string written in place (then it cannot be checked here).
    /// </summary>
    private static string LinkPath(ExpressionSyntax expression)
    {
        var text = expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => string.Concat(interpolated.Contents.Select(content => content is InterpolatedStringTextSyntax part ? part.TextToken.ValueText : "x")),
            _ => null,
        };

        return text?.Split('?', '#')[0];
    }

    private static List<Regex> WebRoutes()
    {
        var webSource = Path.Combine(SourceCode.SolutionRoot, "..", "storeasy-app", "src");

        if (!Directory.Exists(webSource))
            Assert.Skip($"{webSource} not found, the web app's routes are not available.");

        return [.. Directory.EnumerateFiles(webSource, "*.tsx", SearchOption.AllDirectories)
                            .SelectMany(file => RouteRegex().Matches(File.ReadAllText(file)))
                            .Select(match => match.Groups["path"].Value)
                            .Distinct()
                            .Select(ToRegex)];
    }

    private static Regex ToRegex(string route)
    {
        var pattern = string.Join("/", route.Split('/').Select(segment => segment switch
        {
            "*" => ".*",
            _ when segment.StartsWith(':') => "[^/]+",
            _ => Regex.Escape(segment),
        }));

        return new Regex($"^{pattern}/?$", RegexOptions.IgnoreCase);
    }

    [GeneratedRegex("""path(?:=|:\s*)["'](?<path>/[^"']*)["']""")]
    private static partial Regex RouteRegex();
}
