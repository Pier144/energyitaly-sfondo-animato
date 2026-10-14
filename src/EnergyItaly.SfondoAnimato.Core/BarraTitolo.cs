namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Che cosa c'è in un punto della striscia del titolo disegnata dal programma.</summary>
public enum ZonaBarra { Fuori, Trascina, Riduci, Ingrandisci, Chiudi, BordoAlto, AngoloAltoSinistra, AngoloAltoDestra }

/// <summary>Misure della striscia in pixel fisici.</summary>
public sealed record MisureBarra(int Altezza, int LarghezzaPulsante, int BordoRidimensiona, int Angolo);

/// <summary>
/// Geometria della striscia del titolo (spec 2026-10-14, 3.4): dove stanno i pulsanti e che cosa c'è
/// sotto il mouse. Senza dipendenze da WinForms, così si prova a ogni scala e con la finestra ingrandita.
/// </summary>
public static class BarraTitolo
{
    // In DIP: altezza 32, pulsante 46, fascia per ridimensionare dall'alto 8 (come la cornice a 100%), angolo 16.
    public static MisureBarra Misure(int dpi) => new(Px(32, dpi), Px(46, dpi), Px(8, dpi), Px(16, dpi));

    static int Px(int dip, int dpi) => (int)Math.Round(dip * dpi / 96.0);

    /// <summary>Riduci, Ingrandisci, Chiudi affiancati al bordo destro. Se l'area è stretta si tagliano a sinistra.</summary>
    public static IReadOnlyList<(ZonaBarra Zona, Rettangolo Area)> Pulsanti(int larghezzaClient, MisureBarra m)
    {
        int l = m.LarghezzaPulsante;
        return
        [
            (ZonaBarra.Riduci, new Rettangolo(larghezzaClient - 3 * l, 0, l, m.Altezza)),
            (ZonaBarra.Ingrandisci, new Rettangolo(larghezzaClient - 2 * l, 0, l, m.Altezza)),
            (ZonaBarra.Chiudi, new Rettangolo(larghezzaClient - l, 0, l, m.Altezza))
        ];
    }

    /// <summary>Zona del punto (coordinate client). Fuori = sotto la striscia o fuori dalla finestra.</summary>
    public static ZonaBarra Zona(int x, int y, int larghezzaClient, MisureBarra m, bool ingrandita)
    {
        if (y < 0 || y >= m.Altezza || x < 0 || x >= larghezzaClient) return ZonaBarra.Fuori;
        // Come in Windows 11 la fascia per ridimensionare vale anche sopra i pulsanti.
        if (!ingrandita && y < m.BordoRidimensiona)
        {
            if (x < m.Angolo) return ZonaBarra.AngoloAltoSinistra;
            if (x >= larghezzaClient - m.Angolo) return ZonaBarra.AngoloAltoDestra;
            return ZonaBarra.BordoAlto;
        }
        foreach (var (zona, area) in Pulsanti(larghezzaClient, m))
            if (x >= area.X && x < area.Destra) return zona;
        return ZonaBarra.Trascina;
    }
}
