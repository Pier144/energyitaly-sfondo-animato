namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Decide se il desktop non è visibile su nessun monitor (RF-30).</summary>
public static class CoperturaDesktop
{
    /// <summary>Pixel di scarto ammessi tra il bordo della finestra e l'area del monitor.</summary>
    public const int Tolleranza = 2;

    /// <summary>Vero se ogni area di lavoro è coperta interamente da almeno una finestra.</summary>
    public static bool TuttiCoperti(IReadOnlyList<Rettangolo> areeMonitor, IReadOnlyList<Rettangolo> finestre) =>
        areeMonitor.Count > 0 && areeMonitor.All(a => finestre.Any(f => Copre(f, a)));

    public static bool Copre(Rettangolo finestra, Rettangolo area) =>
        finestra.X <= area.X + Tolleranza &&
        finestra.Y <= area.Y + Tolleranza &&
        finestra.Destra >= area.Destra - Tolleranza &&
        finestra.Basso >= area.Basso - Tolleranza;
}
