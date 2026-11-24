using System.Reflection;
using MigraDoc;
using PdfSharp.Fonts;

namespace Sorteo360.Events.Infrastructure.NotarialRecords;

/// <summary>
/// La única tipografía del acta, Noto Serif regular y negrita (SIL OFL, la licencia está al lado
/// de los archivos), servida desde el propio ensamblado. En Linux PDFsharp no encuentra fuentes del
/// sistema, y así macOS, CI y la imagen dibujan el mismo documento (ADR-0024).
/// <para>
/// <b>Sin fuente de respaldo.</b> Una familia que no sea esta no se resuelve y PDFsharp falla en voz
/// alta: un acta con otra letra es un cambio que alguien tiene que decidir, no un accidente.
/// </para>
/// </summary>
internal sealed class NotoSerifFontResolver : IFontResolver
{
    /// <summary>El nombre de familia con que la maquetación pide la fuente.</summary>
    public const string FamilyName = "Noto Serif";

    private const string RegularFace = "NotoSerif-Regular";
    private const string BoldFace = "NotoSerif-Bold";

    private static readonly Lock RegistrationLock = new();

    /// <summary>
    /// Registra la fuente en PDFsharp, una sola vez por proceso: <c>GlobalFontSettings</c> es global
    /// y no admite una segunda asignación, así que dos renderizadores —o dos tests— no la repiten.
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (RegistrationLock)
        {
            if (GlobalFontSettings.FontResolver is NotoSerifFontResolver)
                return;

            GlobalFontSettings.FontResolver = new NotoSerifFontResolver();

            // MigraDoc resuelve de antemano una fuente para sus mensajes de error, Courier New por
            // omisión; sin esto pediría una familia que acá no existe antes de dibujar nada.
            PredefinedFontsAndChars.ErrorFontName = FamilyName;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        // La cursiva no viaja en el ensamblado, y tampoco se finge: pedirla falla como otra familia.
        if (italic || !string.Equals(familyName, FamilyName, StringComparison.OrdinalIgnoreCase))
            return null;

        return new FontResolverInfo(bold ? BoldFace : RegularFace);
    }

    public byte[]? GetFont(string faceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith($".{faceName}.ttf", StringComparison.Ordinal));
        if (resource is null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
