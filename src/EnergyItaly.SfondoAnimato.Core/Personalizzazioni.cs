namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Regole pure della personalizzazione degli sfondi (spec personalizzazione 4.2).</summary>
public static class Personalizzazioni
{
    public const string Personalizzata = "personalizzata";

    /// <summary>Tavolozza di riserva se il manifest non ne ha di valide.</summary>
    public static readonly Tavolozza EnergyItaly = new("energy-italy", "Energy Italy", "#0f1e35", "#7ab52a", "#1e5b8e");

    /// <summary>"#rrggbb" (maiuscole o minuscole).</summary>
    public static bool ColoreValido(string? c) =>
        c is { Length: 7 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);
}
