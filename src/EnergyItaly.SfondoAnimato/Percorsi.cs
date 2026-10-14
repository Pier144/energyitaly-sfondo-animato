namespace EnergyItaly.SfondoAnimato;

/// <summary>Percorsi su disco del programma (spec 3.1; libreria.json sta fuori dalla cartella servita).</summary>
static class Percorsi
{
    static string Dati => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EnergyItaly", "SfondoAnimato");
    static string Locali => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnergyItaly", "SfondoAnimato");

    public static string Impostazioni => Path.Combine(Dati, "impostazioni.json");
    public static string FileLibreria => Path.Combine(Dati, "libreria.json");
    public static string Libreria => Path.Combine(Dati, "libreria");
    public static string Log => Path.Combine(Locali, "log");
    public static string DatiWebView => Path.Combine(Locali, "WebView2");
    public static string Aggiornamenti => Path.Combine(Locali, "aggiornamenti");
    public static string Web => Path.Combine(AppContext.BaseDirectory, "web");
    public static string Manifest => Path.Combine(Web, "animazioni", "manifest.json");
    public static string PredefiniteAziendali => Path.Combine(AppContext.BaseDirectory, "impostazioni-predefinite.json");
}
