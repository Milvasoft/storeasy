using Storeasy.Application.Interfaces;
using Storeasy.Domain.Enums;
using Storeasy.Domain.Intake;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Storeasy.Infrastructure.Intake.Packages;

/// <summary>
/// Reads the identity and version of a stored package (docs/urun-plani.md 11.8 step 2) with ranged reads: IPA
/// (<c>Payload/*.app/Info.plist</c>, binary or XML, and the embedded provisioning profile), AAB (protocol buffer manifest),
/// APK (binary XML manifest) and MSIX/APPX packages, bundles and upload files (<c>AppxManifest.xml</c>,
/// <c>AppxMetadata/AppxBundleManifest.xml</c>). Throws <see cref="InvalidDataException"/> or <see cref="FormatException"/>
/// for a file that is not what its extension says.
/// </summary>
/// <param name="storage"></param>
public sealed partial class PackageReader(IArtifactStorage storage)
{
    private const long _maxEntryBytes = 16 * 1024 * 1024;
    private const long _maxNestedPackageBytes = 2L * 1024 * 1024 * 1024;

    private readonly IArtifactStorage _storage = storage;

    [GeneratedRegex(@"^Payload/[^/]+\.app/Info\.plist$")]
    private static partial Regex InfoPlistRegex();

    [GeneratedRegex(@"^Payload/[^/]+\.app/embedded\.mobileprovision$")]
    private static partial Regex ProvisionRegex();

    /// <summary>
    /// Reads the package stored at <paramref name="objectKey"/>.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="size"></param>
    /// <param name="kind"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<ArtifactPackageInfo> ReadAsync(string objectKey, long size, PackageKind kind, string fileName, CancellationToken cancellationToken)
    {
        await using var stream = new RangeReadStream(_storage, objectKey, size);

        return await ReadAsync(stream, kind, fileName, cancellationToken);
    }

    /// <summary>
    /// Reads a package from a seekable stream.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="kind"></param>
    /// <param name="fileName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task<ArtifactPackageInfo> ReadAsync(Stream stream, PackageKind kind, string fileName, CancellationToken cancellationToken)
    {
        await using var archive = await ZipArchive.CreateAsync(stream, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken);

        return kind switch
        {
            PackageKind.Ipa => await ReadIpaAsync(archive, cancellationToken),
            PackageKind.Aab => await ReadAabAsync(archive, cancellationToken),
            PackageKind.Apk => await ReadApkAsync(archive, cancellationToken),
            PackageKind.Xpi => (await Storeasy.Application.Utils.Firefox.FirefoxManifest.ReadAsync(archive, cancellationToken)).ToPackageInfo(),
            _ => await ReadMsixAsync(archive, fileName, 0, cancellationToken),
        };
    }

    /// <summary>
    /// Xcode version from <c>DTXcode</c> (<c>2610</c> is 26.1, <c>1640</c> is 16.4).
    /// </summary>
    /// <param name="dtXcode"></param>
    /// <returns></returns>
    public static string XcodeVersionOf(string dtXcode)
    {
        if (string.IsNullOrWhiteSpace(dtXcode) || dtXcode.Length < 3 || !dtXcode.All(char.IsAsciiDigit))
            return null;

        var major = int.Parse(dtXcode[..^2], CultureInfo.InvariantCulture);
        var minor = dtXcode[^2] - '0';
        var patch = dtXcode[^1] - '0';

        return patch == 0 ? $"{major}.{minor}" : $"{major}.{minor}.{patch}";
    }

    private static async Task<ArtifactPackageInfo> ReadIpaAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var plistEntry = archive.Entries.FirstOrDefault(e => InfoPlistRegex().IsMatch(e.FullName)) ?? throw new InvalidDataException("The IPA has no Payload/*.app/Info.plist.");
        var plist = PropertyListReader.ParseDictionary(await ReadEntryAsync(plistEntry, cancellationToken));

        var platformName = Str(plist, "DTPlatformName");

        var info = new ArtifactPackageInfo
        {
            Kind = PackageKind.Ipa,
            Identifier = Str(plist, "CFBundleIdentifier"),
            VersionName = Str(plist, "CFBundleShortVersionString"),
            VersionCode = Str(plist, "CFBundleVersion"),
            MinOs = Str(plist, "MinimumOSVersion"),
            XcodeVersion = XcodeVersionOf(Str(plist, "DTXcode")),
            SdkVersion = Str(plist, "DTPlatformVersion"),
            ApplePlatform = platformName switch
            {
                "appletvos" => "TV_OS",
                "xros" => "VISION_OS",
                "macosx" => "MAC_OS",
                _ => "IOS",
            },
            UsesNonExemptEncryption = plist.TryGetValue("ITSAppUsesNonExemptEncryption", out var encryption) && encryption is bool uses ? uses : null,
            DisplayName = Str(plist, "CFBundleDisplayName") ?? Str(plist, "CFBundleName"),
        };

        var provisionEntry = archive.Entries.FirstOrDefault(e => ProvisionRegex().IsMatch(e.FullName));

        info.Signature = provisionEntry == null ? null : DescribeProvision(await ReadEntryAsync(provisionEntry, cancellationToken));

        return info;
    }

    /// <summary>
    /// Summary of an embedded provisioning profile: distribution (App Store) or development / ad hoc, and the team.
    /// </summary>
    /// <param name="cms"></param>
    /// <returns></returns>
    public static string DescribeProvision(byte[] cms)
    {
        var text = Encoding.UTF8.GetString(cms);
        var start = text.IndexOf("<?xml", StringComparison.Ordinal);
        var end = text.IndexOf("</plist>", StringComparison.Ordinal);

        if (start < 0 || end < start)
            return null;

        Dictionary<string, object> profile;

        try
        {
            profile = PropertyListReader.ParseDictionary(Encoding.UTF8.GetBytes(text[start..(end + "</plist>".Length)]));
        }
        catch (FormatException)
        {
            return null;
        }

        var team = profile.TryGetValue("TeamIdentifier", out var teams) && teams is List<object> list ? list.OfType<string>().FirstOrDefault() : null;
        var hasDevices = profile.ContainsKey("ProvisionedDevices");
        var allDevices = profile.TryGetValue("ProvisionsAllDevices", out var all) && all is true;
        var development = profile.TryGetValue("Entitlements", out var entitlements) && entitlements is Dictionary<string, object> e && e.TryGetValue("get-task-allow", out var debug) && debug is true;

        var kind = development ? "development" : hasDevices ? "ad hoc" : allDevices ? "enterprise" : "app store";

        return team == null ? $"{kind} profile" : $"{kind} profile, team {team}";
    }

    private static async Task<ArtifactPackageInfo> ReadAabAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("base/manifest/AndroidManifest.xml") ?? throw new InvalidDataException("The bundle has no base/manifest/AndroidManifest.xml.");
        var manifest = AndroidManifestReader.ReadProto(await ReadEntryAsync(entry, cancellationToken));

        return Android(PackageKind.Aab, manifest, archive);
    }

    private static async Task<ArtifactPackageInfo> ReadApkAsync(ZipArchive archive, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry("AndroidManifest.xml") ?? throw new InvalidDataException("The APK has no AndroidManifest.xml.");
        var manifest = AndroidManifestReader.ReadBinaryXml(await ReadEntryAsync(entry, cancellationToken));

        return Android(PackageKind.Apk, manifest, archive);
    }

    private static ArtifactPackageInfo Android(PackageKind kind, AndroidManifestReader.ManifestValues manifest, ZipArchive archive)
    {
        var signed = archive.Entries.Any(e => e.FullName.StartsWith("META-INF/", StringComparison.Ordinal) && (e.FullName.EndsWith(".RSA", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".EC", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".DSA", StringComparison.OrdinalIgnoreCase)));

        return new ArtifactPackageInfo
        {
            Kind = kind,
            Identifier = manifest.Package,
            VersionCode = manifest.VersionCode,
            VersionName = manifest.VersionName,
            MinOs = manifest.MinSdk,
            TargetSdk = manifest.TargetSdk,
            Signature = signed ? "signed (JAR signature)" : kind == PackageKind.Apk ? "APK signature scheme v2+ or unsigned" : "unsigned",
        };
    }

    private static async Task<ArtifactPackageInfo> ReadMsixAsync(ZipArchive archive, string fileName, int depth, CancellationToken cancellationToken)
    {
        var manifestEntry = archive.GetEntry("AppxManifest.xml") ?? archive.GetEntry("AppxMetadata/AppxBundleManifest.xml");

        if (manifestEntry != null)
            return ReadAppxManifest(await ReadEntryAsync(manifestEntry, cancellationToken), archive.GetEntry("AppxSignature.p7x") != null);

        var nested = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".msixbundle", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".appxbundle", StringComparison.OrdinalIgnoreCase))
                     ?? archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase) || e.FullName.EndsWith(".appx", StringComparison.OrdinalIgnoreCase));

        if (nested == null || depth > 0 || nested.Length > _maxNestedPackageBytes)
            throw new InvalidDataException($"{fileName} has no AppxManifest.xml or bundle manifest.");

        var temp = Path.Combine(Path.GetTempPath(), $"storeasy-{Guid.NewGuid():N}.zip");

        try
        {
            await using (var source = await nested.OpenAsync(cancellationToken))
            await using (var target = File.Create(temp))
            {
                await source.CopyToAsync(target, cancellationToken);
            }

            await using var file = File.OpenRead(temp);
            await using var inner = await ZipArchive.CreateAsync(file, ZipArchiveMode.Read, leaveOpen: false, entryNameEncoding: null, cancellationToken);

            return await ReadMsixAsync(inner, nested.Name, depth + 1, cancellationToken);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    /// <summary>
    /// Reads the identity of an <c>AppxManifest.xml</c> or <c>AppxBundleManifest.xml</c>.
    /// </summary>
    /// <param name="xml"></param>
    /// <param name="signed"></param>
    /// <returns></returns>
    public static ArtifactPackageInfo ReadAppxManifest(byte[] xml, bool signed)
    {
        var document = XDocument.Parse(Encoding.UTF8.GetString(xml));
        var identity = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "Identity") ?? throw new InvalidDataException("The manifest has no Identity.");
        var minVersion = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "TargetDeviceFamily")?.Attribute("MinVersion")?.Value;
        var displayName = document.Descendants().FirstOrDefault(e => e.Name.LocalName == "DisplayName" && e.Parent?.Name.LocalName == "Properties")?.Value;

        return new ArtifactPackageInfo
        {
            Kind = PackageKind.Msix,
            Identifier = identity.Attribute("Name")?.Value,
            VersionName = identity.Attribute("Version")?.Value,
            VersionCode = identity.Attribute("Version")?.Value,
            Publisher = identity.Attribute("Publisher")?.Value,
            MinOs = minVersion,
            DisplayName = displayName,
            Signature = signed ? "signed (AppxSignature.p7x)" : "unsigned",
        };
    }

    private static async Task<byte[]> ReadEntryAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Length > _maxEntryBytes)
            throw new InvalidDataException($"{entry.FullName} is too large.");

        await using var stream = await entry.OpenAsync(cancellationToken);
        using var buffer = new MemoryStream((int)entry.Length);

        await stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private static string Str(Dictionary<string, object> plist, string key) => plist.TryGetValue(key, out var value) ? value?.ToString() : null;
}
