using FluentAssertions;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using Storeasy.Domain.Rules;
using Storeasy.Infrastructure.Intake;
using Storeasy.Infrastructure.Intake.Packages;
using Storeasy.Infrastructure.Services;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Storeasy.UnitTests.IntakeTests;

[Trait("Intake Unit Tests", "Package reading without platform tools: binary and XML property lists, the App Bundle protocol buffer manifest, APK binary XML, MSIX manifests, and the catalog checks.")]
public class PackageReaderTests
{
    [Fact]
    public async Task Ipa_WithABinaryInfoPlist_ShouldBeRead()
    {
        var plist = BinaryPlist(new Dictionary<string, object>
        {
            ["CFBundleIdentifier"] = "com.kuzeylab.pusula",
            ["CFBundleShortVersionString"] = "2.4",
            ["CFBundleVersion"] = "119",
            ["MinimumOSVersion"] = "17.0",
            ["DTXcode"] = "2610",
            ["DTPlatformVersion"] = "26.1",
            ["DTPlatformName"] = "iphoneos",
            ["ITSAppUsesNonExemptEncryption"] = false,
            ["UIDeviceFamily"] = 1L,
        });

        using var zip = Zip(("Payload/Pusula.app/Info.plist", plist), ("Payload/Pusula.app/Pusula", [1, 2, 3]));

        var info = await PackageReader.ReadAsync(zip, PackageKind.Ipa, "pusula.ipa", CancellationToken.None);

        info.Identifier.Should().Be("com.kuzeylab.pusula");
        info.VersionName.Should().Be("2.4");
        info.VersionCode.Should().Be("119");
        info.MinOs.Should().Be("17.0");
        info.XcodeVersion.Should().Be("26.1");
        info.SdkVersion.Should().Be("26.1");
        info.ApplePlatform.Should().Be("IOS");
        info.UsesNonExemptEncryption.Should().BeFalse();
        info.Signature.Should().BeNull();
    }

    [Theory]
    [InlineData("2610", "26.1")]
    [InlineData("1640", "16.4")]
    [InlineData("2601", "26.0.1")]
    [InlineData("x", null)]
    public void XcodeVersion_ShouldBeDecoded(string dtXcode, string version) => PackageReader.XcodeVersionOf(dtXcode).Should().Be(version);

    [Fact]
    public void ProvisioningProfile_ShouldTellDistributionFromDevelopment()
    {
        static byte[] Profile(string extra) => Encoding.UTF8.GetBytes($"\u0001\u0002<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>TeamIdentifier</key><array><string>TEAM1</string></array>{extra}</dict></plist>\u0003");

        PackageReader.DescribeProvision(Profile(string.Empty)).Should().Be("app store profile, team TEAM1");
        PackageReader.DescribeProvision(Profile("<key>ProvisionedDevices</key><array><string>d</string></array>")).Should().StartWith("ad hoc");
        PackageReader.DescribeProvision(Profile("<key>Entitlements</key><dict><key>get-task-allow</key><true/></dict>")).Should().StartWith("development");
    }

    [Fact]
    public void BundleManifest_ShouldBeReadFromTheProtocolBufferForm()
    {
        var manifest = ProtoNode(ProtoElement("manifest",
                                              [ProtoAttribute("package", "com.kuzeylab.halka", null), ProtoAttribute("versionCode", null, 42), ProtoAttribute("versionName", "1.0.0", null)],
                                              [ProtoNode(ProtoElement("uses-sdk", [ProtoAttribute("minSdkVersion", "26", null), ProtoAttribute("targetSdkVersion", null, 36)], []))]));

        var values = AndroidManifestReader.ReadProto(manifest);

        values.Should().Be(new AndroidManifestReader.ManifestValues("com.kuzeylab.halka", "42", "1.0.0", "26", "36"));
    }

    [Fact]
    public void ApkManifest_ShouldBeReadFromBinaryXml()
    {
        var values = AndroidManifestReader.ReadBinaryXml(BinaryXml());

        values.Package.Should().Be("com.kuzeylab.halka");
        values.VersionCode.Should().Be("42");
        values.VersionName.Should().Be("1.0.0");
        values.TargetSdk.Should().Be("35");
    }

    [Fact]
    public void AppxManifest_ShouldGiveTheIdentity()
    {
        var xml = Encoding.UTF8.GetBytes("""
                                         <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                                           <Identity Name="KuzeyLab.Pusula" Publisher="CN=Kuzey" Version="2.4.0.0" />
                                           <Properties><DisplayName>Pusula</DisplayName></Properties>
                                           <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" /></Dependencies>
                                         </Package>
                                         """);

        var info = PackageReader.ReadAppxManifest(xml, signed: true);

        info.Identifier.Should().Be("KuzeyLab.Pusula");
        info.VersionName.Should().Be("2.4.0.0");
        info.MinOs.Should().Be("10.0.19041.0");
        info.DisplayName.Should().Be("Pusula");
    }

    [Fact]
    public void Checks_ShouldBlockALowTargetApiAndANonIncreasingVersionCode()
    {
        var catalog = RulesCatalog.Load(null).Current;
        var context = new ArtifactChecks.Context(catalog, new DateOnly(2026, 11, 18), [41], []);

        var ok = ArtifactChecks.Run(new ArtifactPackageInfo { Kind = PackageKind.Aab, Identifier = "a", VersionCode = "42", TargetSdk = "36", Signature = "signed" }, context);
        var low = ArtifactChecks.Run(new ArtifactPackageInfo { Kind = PackageKind.Aab, Identifier = "a", VersionCode = "41", TargetSdk = "33", Signature = "signed" }, context);
        var apple = ArtifactChecks.Run(new ArtifactPackageInfo { Kind = PackageKind.Ipa, VersionName = "1.0", VersionCode = "5", XcodeVersion = "16.4", SdkVersion = "18.5", MinOs = "15.0", Signature = "app store profile" },
                                       new ArtifactChecks.Context(catalog, new DateOnly(2026, 11, 18), [], [("1.0", "5")]));

        ok.Should().OnlyContain(c => c.Status == ReadinessStatus.Ok);
        low.Should().Contain(c => c.Key == "version-code" && c.Status == ReadinessStatus.Missing);
        low.Should().Contain(c => c.Group == "TargetApi" && c.Status == ReadinessStatus.Missing);
        apple.Should().Contain(c => c.Key == "build-number" && c.Status == ReadinessStatus.Missing);
        apple.Should().Contain(c => c.Group == "Sdk" && c.Status == ReadinessStatus.Missing);
        _ = typeof(RuleCatalog);
    }

    private static MemoryStream Zip(params (string Name, byte[] Content)[] entries)
    {
        var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name).Open();

                stream.Write(content);
            }
        }

        buffer.Position = 0;

        return buffer;
    }

    /// <summary>
    /// A bplist00 with one dictionary of strings, booleans and small integers.
    /// </summary>
    private static byte[] BinaryPlist(Dictionary<string, object> values)
    {
        var objects = new List<byte[]>();
        var keys = values.Keys.ToList();

        objects.Add([]);

        foreach (var key in keys)
        {
            objects.Add(AsciiObject(key));
        }

        foreach (var value in values.Values)
        {
            objects.Add(value switch
            {
                string text => AsciiObject(text),
                bool flag => [(byte)(flag ? 0x09 : 0x08)],
                long number => [0x10, (byte)number],
                _ => throw new NotSupportedException(),
            });
        }

        var count = keys.Count;
        objects[0] = [(byte)(0xD0 | count), .. Enumerable.Range(1, count).Select(i => (byte)i), .. Enumerable.Range(count + 1, count).Select(i => (byte)i)];

        var body = new List<byte>(Encoding.ASCII.GetBytes("bplist00"));
        var offsets = new List<int>();

        foreach (var item in objects)
        {
            offsets.Add(body.Count);
            body.AddRange(item);
        }

        var tableOffset = body.Count;

        foreach (var offset in offsets)
        {
            body.Add((byte)(offset >> 8));
            body.Add((byte)offset);
        }

        var trailer = new byte[32];

        trailer[6] = 2;
        trailer[7] = 1;
        BinaryPrimitives.WriteInt64BigEndian(trailer.AsSpan(8), objects.Count);
        BinaryPrimitives.WriteInt64BigEndian(trailer.AsSpan(16), 0);
        BinaryPrimitives.WriteInt64BigEndian(trailer.AsSpan(24), tableOffset);
        body.AddRange(trailer);

        return [.. body];
    }

    private static byte[] AsciiObject(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);

        return bytes.Length < 15 ? [(byte)(0x50 | bytes.Length), .. bytes] : [0x5F, 0x10, (byte)bytes.Length, .. bytes];
    }

    private static byte[] ProtoNode(byte[] element) => Field(1, element);

    private static byte[] ProtoElement(string name, IEnumerable<byte[]> attributes, IEnumerable<byte[]> children)
        => [.. Field(3, Encoding.UTF8.GetBytes(name)), .. attributes.SelectMany(a => Field(4, a)), .. children.SelectMany(c => Field(5, c))];

    private static byte[] ProtoAttribute(string name, string value, int? compiled)
    {
        var bytes = new List<byte>(Field(2, Encoding.UTF8.GetBytes(name)));

        if (value != null)
            bytes.AddRange(Field(3, Encoding.UTF8.GetBytes(value)));

        if (compiled != null)
            bytes.AddRange(Field(6, Field(7, [0x30, .. Varint((ulong)compiled.Value)])));

        return [.. bytes];
    }

    private static byte[] Field(int number, byte[] value) => [.. Varint((ulong)((number << 3) | 2)), .. Varint((ulong)value.Length), .. value];

    private static byte[] Varint(ulong value)
    {
        var bytes = new List<byte>();

        do
        {
            var b = (byte)(value & 0x7F);

            value >>= 7;
            bytes.Add(value == 0 ? b : (byte)(b | 0x80));
        }
        while (value != 0);

        return [.. bytes];
    }

    /// <summary>
    /// A minimal Android binary XML: a UTF-16 string pool and two start elements (manifest, uses-sdk).
    /// </summary>
    private static byte[] BinaryXml()
    {
        string[] strings = ["manifest", "package", "versionCode", "versionName", "com.kuzeylab.halka", "1.0.0", "uses-sdk", "targetSdkVersion"];
        var pool = new List<byte>();
        var stringData = new List<byte>();
        var offsets = new List<int>();

        foreach (var text in strings)
        {
            offsets.Add(stringData.Count);
            stringData.AddRange(BitConverter.GetBytes((ushort)text.Length));
            stringData.AddRange(Encoding.Unicode.GetBytes(text));
            stringData.AddRange(new byte[2]);
        }

        while (stringData.Count % 4 != 0)
        {
            stringData.Add(0);
        }

        var poolHeader = 28;
        var stringsStart = poolHeader + offsets.Count * 4;

        pool.AddRange(BitConverter.GetBytes((ushort)0x0001));
        pool.AddRange(BitConverter.GetBytes((ushort)poolHeader));
        pool.AddRange(BitConverter.GetBytes(stringsStart + stringData.Count));
        pool.AddRange(BitConverter.GetBytes(strings.Length));
        pool.AddRange(BitConverter.GetBytes(0));
        pool.AddRange(BitConverter.GetBytes(0));
        pool.AddRange(BitConverter.GetBytes(stringsStart));
        pool.AddRange(BitConverter.GetBytes(0));
        offsets.ForEach(o => pool.AddRange(BitConverter.GetBytes(o)));
        pool.AddRange(stringData);

        var manifest = StartElement(0, [(1, 0x03, 4), (2, 0x10, 42), (3, 0x03, 5)]);
        var usesSdk = StartElement(6, [(7, 0x10, 35)]);
        var body = new List<byte>();

        body.AddRange(pool);
        body.AddRange(manifest);
        body.AddRange(usesSdk);

        var document = new List<byte>();

        document.AddRange(BitConverter.GetBytes((ushort)0x0003));
        document.AddRange(BitConverter.GetBytes((ushort)8));
        document.AddRange(BitConverter.GetBytes(8 + body.Count));
        document.AddRange(body);

        return [.. document];
    }

    private static byte[] StartElement(int name, (int Name, byte Type, int Value)[] attributes)
    {
        var chunk = new List<byte>();
        var size = 16 + 20 + attributes.Length * 20;

        chunk.AddRange(BitConverter.GetBytes((ushort)0x0102));
        chunk.AddRange(BitConverter.GetBytes((ushort)16));
        chunk.AddRange(BitConverter.GetBytes(size));
        chunk.AddRange(BitConverter.GetBytes(1));
        chunk.AddRange(BitConverter.GetBytes(-1));
        chunk.AddRange(BitConverter.GetBytes(-1));
        chunk.AddRange(BitConverter.GetBytes(name));
        chunk.AddRange(BitConverter.GetBytes((ushort)20));
        chunk.AddRange(BitConverter.GetBytes((ushort)20));
        chunk.AddRange(BitConverter.GetBytes((ushort)attributes.Length));
        chunk.AddRange(new byte[6]);

        foreach (var (attributeName, type, value) in attributes)
        {
            chunk.AddRange(BitConverter.GetBytes(-1));
            chunk.AddRange(BitConverter.GetBytes(attributeName));
            chunk.AddRange(BitConverter.GetBytes(type == 0x03 ? value : -1));
            chunk.AddRange(BitConverter.GetBytes((ushort)8));
            chunk.Add(0);
            chunk.Add(type);
            chunk.AddRange(BitConverter.GetBytes(value));
        }

        return [.. chunk];
    }
}
