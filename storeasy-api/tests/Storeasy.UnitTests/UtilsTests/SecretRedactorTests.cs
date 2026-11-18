using FluentAssertions;
using Storeasy.Application.Utils.Helpers;

namespace Storeasy.UnitTests.UtilsTests;

[Trait("Utils Unit Tests", "Secret redactor unit tests.")]
public class SecretRedactorTests
{
    [Fact]
    public void Redact_ShouldStripEveryCredentialShape()
    {
        var input = "Bearer abc.def.ghi ya29.secret eyJhbGciOiJFUzI1NiJ9.eyJpc3MiOiJ4In0.sig "
                    + "-----BEGIN PRIVATE KEY-----\nMIGsecret\n-----END PRIVATE KEY----- "
                    + "https://u.example/p?X-Amz-Signature=zz x-apple-signature: hmacsha256=deadbeef";

        var output = SecretRedactor.Redact(input);

        output.Should().NotContainAny("abc.def", "ya29.secret", "eyJhbGci", "MIGsecret", "zz", "deadbeef");
        output.Should().Contain("https://u.example/p?[redacted-query]");
    }

    [Fact]
    public void Redact_ShouldKeepOrdinaryText()
    {
        SecretRedactor.Redact("Upload of 01-takvim.png failed (500)").Should().Be("Upload of 01-takvim.png failed (500)");
        SecretRedactor.Redact(null).Should().BeNull();
    }

    [Fact]
    public void RedactAndTrim_ShouldCut()
    {
        SecretRedactor.RedactAndTrim(new string('x', 50), 10).Should().HaveLength(10);
    }

    [Fact]
    public void SecureTokens_ShouldBeRandomAndHashStable()
    {
        var first = SecureTokens.Create("inv_");
        var second = SecureTokens.Create("inv_");

        first.Should().StartWith("inv_").And.NotBe(second);
        first.Length.Should().BeGreaterThan(40);
        SecureTokens.Hash(first).Should().Be(SecureTokens.Hash(first)).And.HaveLength(64).And.NotBe(SecureTokens.Hash(second));
    }
}
