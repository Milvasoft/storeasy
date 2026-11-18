using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Storeasy.IntegrationTests.TestBase;

/// <summary>
/// Builds small but real packages for the intake tests: an App Bundle with an aapt2 protocol buffer manifest, an IPA with
/// an XML Info.plist and an App Store provisioning profile, and an MSIX with an AppxManifest.xml. Random padding makes
/// every package's SHA-256 unique.
/// </summary>
public static class PackageFactory
{
    /// <summary>
    /// An App Bundle of <paramref name="package"/>.
    /// </summary>
    public static byte[] Aab(string package, long versionCode, string versionName = "1.0.0", int? targetSdk = 36, int minSdk = 26, bool signed = true)
    {
        var usesSdk = new List<byte[]> { Attribute("minSdkVersion", minSdk.ToString()) };

        if (targetSdk != null)
            usesSdk.Add(Attribute("targetSdkVersion", targetSdk.ToString()));

        var manifest = Node(Element("manifest",
                                    [Attribute("package", package, android: false), Attribute("versionCode", versionCode.ToString()), Attribute("versionName", versionName)],
                                    [Node(Element("uses-sdk", usesSdk, []))]));

        var entries = new Dictionary<string, byte[]>
        {
            ["base/manifest/AndroidManifest.xml"] = manifest,
            ["base/dex/classes.dex"] = RandomNumberGenerator.GetBytes(4096),
        };

        if (signed)
            entries["META-INF/UPLOAD.RSA"] = RandomNumberGenerator.GetBytes(256);

        return Zip(entries);
    }

    /// <summary>
    /// An IPA of <paramref name="bundleId"/> signed with an App Store distribution profile (or a development one).
    /// </summary>
    public static byte[] Ipa(string bundleId, string version, string build, string xcode = "2610", string sdk = "26.1", string minimumOs = "17.0", bool development = false, bool? encryption = false)
    {
        var plist = $"""
                     <?xml version="1.0" encoding="UTF-8"?>
                     <plist version="1.0"><dict>
                     <key>CFBundleIdentifier</key><string>{bundleId}</string>
                     <key>CFBundleShortVersionString</key><string>{version}</string>
                     <key>CFBundleVersion</key><string>{build}</string>
                     <key>MinimumOSVersion</key><string>{minimumOs}</string>
                     <key>DTXcode</key><string>{xcode}</string>
                     <key>DTPlatformVersion</key><string>{sdk}</string>
                     <key>DTPlatformName</key><string>iphoneos</string>
                     <key>CFBundleName</key><string>Pusula</string>
                     {(encryption == null ? string.Empty : $"<key>ITSAppUsesNonExemptEncryption</key><{(encryption.Value ? "true" : "false")}/>")}
                     </dict></plist>
                     """;

        var profile = $"""
                       <?xml version="1.0" encoding="UTF-8"?>
                       <plist version="1.0"><dict>
                       <key>TeamIdentifier</key><array><string>TEAM123456</string></array>
                       <key>Entitlements</key><dict><key>get-task-allow</key><{(development ? "true" : "false")}/></dict>
                       {(development ? "<key>ProvisionedDevices</key><array><string>device</string></array>" : string.Empty)}
                       </dict></plist>
                       """;

        return Zip(new Dictionary<string, byte[]>
        {
            ["Payload/Pusula.app/Info.plist"] = Encoding.UTF8.GetBytes(plist),
            ["Payload/Pusula.app/embedded.mobileprovision"] = [.. RandomNumberGenerator.GetBytes(64), .. Encoding.UTF8.GetBytes(profile), .. RandomNumberGenerator.GetBytes(64)],
            ["Payload/Pusula.app/Pusula"] = RandomNumberGenerator.GetBytes(8192),
        });
    }

    /// <summary>
    /// An MSIX of <paramref name="identity"/>.
    /// </summary>
    public static byte[] Msix(string identity, string version = "2.4.0.0")
    {
        var manifest = $"""
                        <?xml version="1.0" encoding="utf-8"?>
                        <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10">
                          <Identity Name="{identity}" Publisher="CN=Kuzey" Version="{version}" />
                          <Properties><DisplayName>Pusula</DisplayName></Properties>
                          <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.19041.0" MaxVersionTested="10.0.22621.0" /></Dependencies>
                        </Package>
                        """;

        return Zip(new Dictionary<string, byte[]> { ["AppxManifest.xml"] = Encoding.UTF8.GetBytes(manifest), ["AppxSignature.p7x"] = RandomNumberGenerator.GetBytes(128) });
    }

    /// <summary>
    /// Lower case hex SHA-256.
    /// </summary>
    public static string Sha256(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));

    private static byte[] Zip(Dictionary<string, byte[]> entries)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var stream = archive.CreateEntry(name, CompressionLevel.Fastest).Open();

                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    private static byte[] Node(byte[] element) => Field(1, element);

    private static byte[] Element(string name, IEnumerable<byte[]> attributes, IEnumerable<byte[]> children)
        => [.. Field(3, Encoding.UTF8.GetBytes(name)), .. attributes.SelectMany(a => Field(4, a)), .. children.SelectMany(c => Field(5, c))];

    private static byte[] Attribute(string name, string value, bool android = true)
        => [.. android ? Field(1, Encoding.UTF8.GetBytes("http://schemas.android.com/apk/res/android")) : [], .. Field(2, Encoding.UTF8.GetBytes(name)), .. Field(3, Encoding.UTF8.GetBytes(value))];

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
}
