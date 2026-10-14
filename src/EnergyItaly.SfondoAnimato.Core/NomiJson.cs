namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Nomi testuali dei valori enumerati nei file JSON e nei messaggi.</summary>
public static class NomiJson
{
    public static string Di(TipoVoce t) => t switch
    {
        TipoVoce.Animazione => "animazione",
        TipoVoce.Immagine => "immagine",
        TipoVoce.Video => "video",
        _ => "html"
    };

    public static string Di(UnitaTempo u) => u == UnitaTempo.Ore ? "ore" : "minuti";

    public static string Di(Ordine o) => o == Ordine.Casuale ? "casuale" : "sequenziale";

    public static string Di(ModoMonitor m) => m == ModoMonitor.Duplica ? "duplica" : "estendi";

    public static TipoVoce? LeggiTipo(string? s) => s switch
    {
        "animazione" => TipoVoce.Animazione,
        "immagine" => TipoVoce.Immagine,
        "video" => TipoVoce.Video,
        "html" => TipoVoce.Html,
        _ => null
    };

    public static UnitaTempo? LeggiUnita(string? s) => s switch
    {
        "minuti" => UnitaTempo.Minuti,
        "ore" => UnitaTempo.Ore,
        _ => null
    };

    public static Ordine? LeggiOrdine(string? s) => s switch
    {
        "sequenziale" => Ordine.Sequenziale,
        "casuale" => Ordine.Casuale,
        _ => null
    };

    public static ModoMonitor? LeggiModo(string? s) => s switch
    {
        "estendi" => ModoMonitor.Estendi,
        "duplica" => ModoMonitor.Duplica,
        _ => null
    };

    public static string Di(Riempimento r) => r == Riempimento.Adatta ? "adatta" : "riempi";

    public static string Di(TipoOpzione t) => t switch
    {
        TipoOpzione.Numero => "numero",
        TipoOpzione.Scelta => "scelta",
        _ => "interruttore"
    };

    public static Riempimento? LeggiRiempimento(string? s) => s switch
    {
        "riempi" => Riempimento.Riempi,
        "adatta" => Riempimento.Adatta,
        _ => null
    };

    public static TipoOpzione? LeggiTipoOpzione(string? s) => s switch
    {
        "numero" => TipoOpzione.Numero,
        "scelta" => TipoOpzione.Scelta,
        "interruttore" => TipoOpzione.Interruttore,
        _ => null
    };
}
