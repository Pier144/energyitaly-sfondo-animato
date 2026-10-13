namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Calcola la finestra di sfondo e le scene a partire dai monitor collegati.</summary>
public static class LayoutMonitor
{
    /// <summary>Usato quando Windows non restituisce nessun monitor valido.</summary>
    public static readonly Monitor MonitorDiRiserva = new("DISPLAY1", new Rettangolo(0, 0, 1920, 1080), true);

    public static Disposizione Calcola(IReadOnlyList<Monitor> monitor, ModoMonitor modo)
    {
        var elenco = monitor
            .Where(m => m.Area.L > 0 && m.Area.A > 0)
            .OrderBy(m => m.Area.X).ThenBy(m => m.Area.Y)
            .ToList();
        if (elenco.Count == 0) elenco.Add(MonitorDiRiserva);

        int sinistra = elenco.Min(m => m.Area.X);
        int alto = elenco.Min(m => m.Area.Y);
        int destra = elenco.Max(m => m.Area.Destra);
        int basso = elenco.Max(m => m.Area.Basso);
        var finestra = new Rettangolo(sinistra, alto, destra - sinistra, basso - alto);

        var relativi = elenco
            .Select(m => new MonitorRelativo(m.Nome,
                new Rettangolo(m.Area.X - sinistra, m.Area.Y - alto, m.Area.L, m.Area.A), m.Principale))
            .ToList();

        var effettivo = relativi.Count == 1 ? ModoMonitor.Estendi : modo;
        IReadOnlyList<Scena> scene = effettivo == ModoMonitor.Estendi
            ? [new Scena(new Rettangolo(0, 0, finestra.L, finestra.A), relativi.Select(r => r.Area).ToList())]
            : relativi.Select(r => new Scena(r.Area, [new Rettangolo(0, 0, r.Area.L, r.Area.A)])).ToList();

        return new Disposizione(finestra, effettivo, relativi, scene);
    }
}
