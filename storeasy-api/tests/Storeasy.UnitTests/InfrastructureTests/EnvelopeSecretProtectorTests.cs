using FluentAssertions;
using Storeasy.Application.Interfaces;
using Storeasy.Application.Utils.Models;
using Storeasy.Infrastructure.Security;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "Envelope secret protector unit tests.")]
public class EnvelopeSecretProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static readonly byte[] _secret = Encoding.UTF8.GetBytes("{\"issuerId\":\"x\",\"keyId\":\"y\"}");

    [Fact]
    public void ProtectThenUnprotect_ShouldRoundTrip()
    {
        var protector = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey() });

        var sealedSecret = protector.Protect(_secret, "StoreConnection:1:Apple");

        Convert.ToHexString(sealedSecret.EncryptedSecret).Should().NotContain(Convert.ToHexString(_secret[..8]));
        sealedSecret.KeyVersion.Should().Be(1);
        protector.Unprotect(sealedSecret, "StoreConnection:1:Apple").Should().Equal(_secret);
    }

    [Fact]
    public void Protect_ShouldUseAFreshDataKeyEachTime()
    {
        var protector = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey() });

        var first = protector.Protect(_secret, "a");
        var second = protector.Protect(_secret, "a");

        first.EncryptedSecret.Should().NotEqual(second.EncryptedSecret);
        first.EncryptedDataKey.Should().NotEqual(second.EncryptedDataKey);
    }

    [Fact]
    public void Unprotect_WithOtherAssociatedData_ShouldFail()
    {
        var protector = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey() });
        var sealedSecret = protector.Protect(_secret, "StoreConnection:1:Apple");

        var act = () => protector.Unprotect(sealedSecret, "StoreConnection:2:Apple");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WithTamperedCiphertext_ShouldFail()
    {
        var protector = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey() });
        var sealedSecret = protector.Protect(_secret, "a");

        sealedSecret.EncryptedSecret[^1] ^= 0x01;

        var act = () => protector.Unprotect(sealedSecret, "a");

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_AfterRotation_ShouldOpenOldRowsWithTheRetiredKey()
    {
        var oldKey = NewKey();
        var before = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = oldKey, KeyVersion = 1 });
        var sealedSecret = before.Protect(_secret, "a");

        var after = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey(), KeyVersion = 2, RetiredKeys = new() { [1] = oldKey } });

        after.CurrentKeyVersion.Should().Be(2);
        after.Unprotect(sealedSecret, "a").Should().Equal(_secret);
        after.Protect(_secret, "a").KeyVersion.Should().Be(2);
    }

    [Fact]
    public void Unprotect_WithUnknownKeyVersion_ShouldFail()
    {
        var protector = new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = NewKey() });
        var sealedSecret = protector.Protect(_secret, "a");

        var act = () => protector.Unprotect(sealedSecret with { KeyVersion = 9 }, "a");

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64")]
    [InlineData("c2hvcnQ=")]
    public void Constructor_WithInvalidMasterKey_ShouldNameTheVariable(string masterKey)
    {
        var act = () => new EnvelopeSecretProtector(new EncryptionOptions { MasterKey = masterKey });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Storeasy__Encryption__MasterKey*");
    }
}
