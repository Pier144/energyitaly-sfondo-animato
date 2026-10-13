using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Un solo ambiente WebView2 per sfondo e finestra principale: un solo processo browser.</summary>
static class AmbienteWebView
{
    static Task<CoreWebView2Environment>? ambiente;

    public static Task<CoreWebView2Environment> OttieniAsync()
    {
        // Un tentativo fallito non resta in memoria: al prossimo uso si riprova.
        if (ambiente is null || ambiente.IsFaulted || ambiente.IsCanceled)
        {
            ambiente = CoreWebView2Environment.CreateAsync(null, Percorsi.DatiWebView,
                new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
        }
        return ambiente;
    }

    /// <summary>Dopo la caduta del processo browser l'ambiente non è più usabile: se ne crea uno nuovo.</summary>
    public static void Azzera() => ambiente = null;
}
