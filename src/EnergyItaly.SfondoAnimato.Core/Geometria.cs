namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Rettangolo in pixel fisici: origine (X, Y), larghezza L, altezza A.</summary>
public readonly record struct Rettangolo(int X, int Y, int L, int A)
{
    public int Destra => X + L;
    public int Basso => Y + A;
}

/// <summary>Un monitor collegato, in coordinate dello schermo virtuale.</summary>
public sealed record Monitor(string Nome, Rettangolo Area, bool Principale);

/// <summary>Un monitor in coordinate relative alla finestra di sfondo.</summary>
public sealed record MonitorRelativo(string Nome, Rettangolo Area, bool Principale);

/// <summary>
/// Un'area della finestra di sfondo in cui gira un'animazione intera.
/// <paramref name="Monitor"/> sono i rettangoli dei monitor relativi all'area della scena.
/// </summary>
public sealed record Scena(Rettangolo Area, IReadOnlyList<Rettangolo> Monitor)
{
    /// <summary>Valore del parametro <c>monitor=</c> del contratto delle animazioni.</summary>
    public string ParametroMonitor() => string.Join(';', Monitor.Select(m => $"{m.X},{m.Y},{m.L},{m.A}"));
}

/// <summary>Dove sta la finestra di sfondo e quali scene contiene.</summary>
public sealed record Disposizione(
    Rettangolo Finestra,
    ModoMonitor ModoEffettivo,
    IReadOnlyList<MonitorRelativo> Monitor,
    IReadOnlyList<Scena> Scene);
