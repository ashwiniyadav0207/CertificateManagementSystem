using System.Reflection;
using PdfSharp.Fonts;

namespace CertificateEngine.Certificates;

/// <summary>Provides the embedded, redistributable Noto Sans faces on every supported OS.</summary>
public sealed class EmbeddedFontResolver : IFontResolver
{
    private const string RegularFace = "CertificateEngine-NotoSans-Regular";
    private const string BoldFace = "CertificateEngine-NotoSans-Bold";
    private static readonly Assembly Assembly = typeof(EmbeddedFontResolver).Assembly;

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) =>
        new FontResolverInfo(bold ? BoldFace : RegularFace, mustSimulateBold: false, mustSimulateItalic: italic);

    public byte[]? GetFont(string faceName) => faceName switch
    {
        RegularFace => ReadResource("CertificateEngine.Assets.Fonts.NotoSans-Regular.ttf"),
        BoldFace => ReadResource("CertificateEngine.Assets.Fonts.NotoSans-Bold.ttf"),
        _ => null
    };

    private static byte[] ReadResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded certificate font '{name}' was not found.");
        using var result = new MemoryStream();
        stream.CopyTo(result);
        return result.ToArray();
    }
}
