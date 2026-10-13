using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// WebView2 ospitata direttamente su una finestra Win32. Serve solo i due host virtuali,
/// blocca ogni altra richiesta di rete e consegna le pagine HTML importate con una
/// Content-Security-Policy senza rete (spec 3.1).
/// </summary>
sealed class HostWebView : IDisposable
{
    const string PoliticaPagineImportate =
        "default-src 'none'; script-src 'unsafe-inline' 'unsafe-eval' https://libreria.energyitaly; " +
        "style-src 'unsafe-inline' https://libreria.energyitaly; img-src data: blob: https://libreria.energyitaly; " +
        "media-src data: blob: https://libreria.energyitaly; font-src data: https://libreria.energyitaly; " +
        "connect-src 'none'; frame-src 'none'; form-action 'none'; base-uri 'none'";

    readonly ILog log;
    static readonly HashSet<string> hostBloccati = new(StringComparer.OrdinalIgnoreCase);
    CoreWebView2Environment? ambiente;

    public HostWebView(ILog log) => this.log = log;

    public CoreWebView2Controller? Controller { get; private set; }
    public CoreWebView2? Core => Controller?.CoreWebView2;

    public event Action<CoreWebView2WebMessageReceivedEventArgs>? Messaggio;
    public event Action? ProcessoTerminato;

    public async Task InizializzaAsync(IntPtr finestra, Rectangle area, bool scalaFissa, Color sfondo, string url)
    {
        ambiente = await AmbienteWebView.OttieniAsync();
        var controller = await ambiente.CreateCoreWebView2ControllerAsync(finestra);
        Controller = controller;
        controller.DefaultBackgroundColor = sfondo;
        if (scalaFissa)
        {
            // RF-11: 1 px CSS = 1 px fisico anche con monitor a DPI diversi.
            controller.ShouldDetectMonitorScaleChanges = false;
            controller.RasterizationScale = 1.0;
        }
        controller.Bounds = area;

        var core = controller.CoreWebView2;
        var s = core.Settings;
        s.AreDefaultContextMenusEnabled = false;
        s.AreDefaultScriptDialogsEnabled = false;
        s.IsStatusBarEnabled = false;
        s.IsZoomControlEnabled = false;
        s.IsPinchZoomEnabled = false;
        s.IsSwipeNavigationEnabled = false;
        s.IsGeneralAutofillEnabled = false;
        s.IsPasswordAutosaveEnabled = false;
        s.AreBrowserAcceleratorKeysEnabled = false;
#if DEBUG
        s.AreDevToolsEnabled = true;
#else
        s.AreDevToolsEnabled = false;
#endif
        Directory.CreateDirectory(Percorsi.Libreria);
        core.SetVirtualHostNameToFolderMapping(Catalogo.HostApp, Percorsi.Web, CoreWebView2HostResourceAccessKind.Allow);
        core.SetVirtualHostNameToFolderMapping(Catalogo.HostLibreria, Percorsi.Libreria, CoreWebView2HostResourceAccessKind.Allow);
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
        core.WebResourceRequested += FiltraRete;
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
        core.NavigationStarting += (_, e) =>
        {
            if (Consentito(e.Uri)) return;
            e.Cancel = true;
            log.Avviso($"Navigazione bloccata: {e.Uri}");
        };
        core.WebMessageReceived += (_, e) => Messaggio?.Invoke(e);
        core.ProcessFailed += (_, e) =>
        {
            log.Errore($"WebView2: processo terminato ({e.ProcessFailedKind}, {e.Reason})");
            // GPU, utility e iframe vengono ripristinati da WebView2 da sola: si ricrea solo se la pagina o il browser sono persi.
            switch (e.ProcessFailedKind)
            {
                case CoreWebView2ProcessFailedKind.BrowserProcessExited:
                    AmbienteWebView.Azzera();
                    ProcessoTerminato?.Invoke();
                    break;
                case CoreWebView2ProcessFailedKind.RenderProcessExited:
                case CoreWebView2ProcessFailedKind.RenderProcessUnresponsive:
                    ProcessoTerminato?.Invoke();
                    break;
            }
        };
        core.Navigate(url);
    }

    public void ImpostaArea(Rectangle area)
    {
        if (Controller is not null) Controller.Bounds = area;
    }

    void FiltraRete(object? mittente, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme is "data" or "blob" or "about") return;
        bool https = uri.Scheme == Uri.UriSchemeHttps;
        if (https && uri.Host.Equals(Catalogo.HostApp, StringComparison.OrdinalIgnoreCase)) return;
        if (https && uri.Host.Equals(Catalogo.HostLibreria, StringComparison.OrdinalIgnoreCase)) return;
        if (https && uri.Host.Equals(Catalogo.HostPagine, StringComparison.OrdinalIgnoreCase))
        {
            // Host non mappato: le pagine importate arrivano solo da qui, sempre con la CSP senza rete.
            e.Response = PaginaImportataIsolata(uri);
            return;
        }
        lock (hostBloccati)
        {
            if (hostBloccati.Add(uri.Host)) log.Avviso($"Richiesta di rete bloccata verso {uri.Scheme}://{uri.Host}");
        }
        e.Response = ambiente!.CreateWebResourceResponse(null, 403, "Bloccato", "Content-Type: text/plain");
    }

    // Il player controlla con fetch che la pagina esista prima di mostrarla (un iframe segnala "load"
    // anche per un 404): solo la sua origine può leggere la risposta.
    const string LetturaDalPlayer = $"Access-Control-Allow-Origin: https://{Catalogo.HostApp}";

    CoreWebView2WebResourceResponse PaginaImportataIsolata(Uri uri)
    {
        var nome = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        var percorso = Path.Combine(Percorsi.Libreria, nome);
        var estensione = Path.GetExtension(nome).ToLowerInvariant();
        if (!Catalogo.NomeFileSicuro(nome) || estensione is not (".html" or ".htm") || !File.Exists(percorso))
            return ambiente!.CreateWebResourceResponse(null, 404, "Non trovato", $"Content-Type: text/plain\n{LetturaDalPlayer}");
        log.Info($"Pagina importata servita senza accesso alla rete: {nome}");
        var contenuto = new MemoryStream(File.ReadAllBytes(percorso));
        return ambiente!.CreateWebResourceResponse(contenuto, 200, "OK",
            $"Content-Type: text/html\nContent-Security-Policy: {PoliticaPagineImportate}\nCache-Control: no-store\n{LetturaDalPlayer}");
    }

    static bool Consentito(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == "about" ||
         (u.Scheme == Uri.UriSchemeHttps && (u.Host == Catalogo.HostApp || u.Host == Catalogo.HostLibreria || u.Host == Catalogo.HostPagine)));

    public void Dispose()
    {
        try { Controller?.Close(); }
        catch (Exception ex) { log.Avviso($"Chiusura WebView2: {ex.Message}"); }
        Controller = null;
    }
}
