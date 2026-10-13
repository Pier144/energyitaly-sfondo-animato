using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Un solo ambiente WebView2 per sfondo e finestra principale: un solo processo browser.</summary>
static class AmbienteWebView
{
    static Task<CoreWebView2Environment>? ambiente;

    public static Task<CoreWebView2Environment> OttieniAsync() =>
        ambiente ??= CoreWebView2Environment.CreateAsync(null, Percorsi.DatiWebView,
            new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
}
