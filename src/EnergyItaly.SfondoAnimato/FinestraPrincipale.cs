using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).</summary>
sealed class FinestraPrincipale : Form
{
    static readonly Color Fondo = Color.FromArgb(0x07, 0x11, 0x1f);
    static readonly TimeSpan IntervalloCrash = TimeSpan.FromMinutes(5);

    readonly ILog log;
    HostWebView web;
    readonly Action<string> daApp;
    readonly Action<IReadOnlyList<string>> importa;
    bool inizializzata;
    bool chiusuraDefinitiva;
    readonly Queue<DateTime> crash = new();

    public FinestraPrincipale(ILog log, Icon icona, Action<string> daApp, Action<IReadOnlyList<string>> importa)
    {
        this.log = log;
        this.daApp = daApp;
        this.importa = importa;
        web = CreaWebView();
        Text = "Energy Italy Sfondo Animato";
        Icon = icona;
        BackColor = Fondo;
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int scuro = 1;
        if (Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref scuro, sizeof(int)) != 0)
            Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1, ref scuro, sizeof(int));
        float k = DeviceDpi / 96f;
        var lavoro = Screen.FromHandle(Handle).WorkingArea;
        MinimumSize = new Size(Math.Min((int)(960 * k), lavoro.Width), Math.Min((int)(640 * k), lavoro.Height));
        Size = new Size(Math.Min((int)(1280 * k), (int)(lavoro.Width * 0.92)), Math.Min((int)(860 * k), (int)(lavoro.Height * 0.92)));
        CenterToScreen();
    }

    HostWebView CreaWebView()
    {
        var nuova = new HostWebView(log);
        nuova.Messaggio += MessaggioRicevuto;
        // Se il processo della WebView cade, la pagina si ricrea invece di restare bianca.
        nuova.ProcessoTerminato += () => BeginInvoke(new Action(RicreaWebView));
        return nuova;
    }

    void RicreaWebView()
    {
        if (chiusuraDefinitiva || IsDisposed) return;
        var adesso = DateTime.UtcNow;
        crash.Enqueue(adesso);
        while (crash.Count > 0 && adesso - crash.Peek() > IntervalloCrash) crash.Dequeue();
        if (crash.Count >= 3)
        {
            // Come per lo sfondo (spec 9): niente ricreazioni a ciclo continuo. La finestra si chiude
            // e la prossima apertura dal menu ne crea una nuova.
            log.Errore("Finestra principale: la WebView si è interrotta 3 volte in 5 minuti, la chiudo");
            ChiudiDefinitivamente();
            return;
        }
        log.Avviso("Finestra principale: ricreo la WebView dopo un errore del processo");
        web.Dispose();
        web = CreaWebView();
        InizializzaWeb();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (inizializzata) return;
        inizializzata = true;
        InizializzaWeb();
    }

    async void InizializzaWeb()
    {
        try
        {
            await web.InizializzaAsync(Handle, ClientRectangle, scalaFissa: false, Fondo, $"https://{Catalogo.HostApp}/app/index.html");
            AggiornaWebView();
        }
        catch (Exception ex)
        {
            log.Errore($"Finestra principale: WebView2 non avviata ({ex.Message})");
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        AggiornaWebView();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        AggiornaWebView();
    }

    void AggiornaWebView()
    {
        var controller = web.Controller;
        if (controller is null) return;
        controller.Bounds = ClientRectangle;
        bool visibile = Visible && WindowState != FormWindowState.Minimized;
        if (controller.IsVisible != visibile) controller.IsVisible = visibile;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!chiusuraDefinitiva && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    public void MostraInPrimoPiano()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    public void ChiudiDefinitivamente()
    {
        chiusuraDefinitiva = true;
        web.Dispose();
        Close();
    }

    public void Invia(string json)
    {
        try { web.Core?.PostWebMessageAsJson(json); }
        catch (Exception ex) { log.Avviso($"Messaggio alla finestra non inviato: {ex.Message}"); }
    }

    void MessaggioRicevuto(CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            switch (Messaggi.LeggiComandoApp(json)?.Tipo)
            {
                case "importaDialogo":
                    // Mai un dialogo modale dentro un evento della WebView2: lo si apre dopo.
                    BeginInvoke(new Action(ApriDialogoImport));
                    break;
                case "importaFile":
                    var percorsi = e.AdditionalObjects?.OfType<CoreWebView2File>().Select(f => f.Path).ToList() ?? [];
                    importa(percorsi);
                    break;
                default:
                    daApp(json);
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Errore($"Comando della finestra non gestito: {ex.Message}");
        }
    }

    void ApriDialogoImport()
    {
        using var dialogo = new OpenFileDialog
        {
            Title = "Importa sfondi",
            Multiselect = true,
            Filter = "Sfondi supportati|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp;*.mp4;*.webm;*.html;*.htm|" +
                     "Immagini|*.jpg;*.jpeg;*.png;*.webp;*.gif;*.bmp|Video|*.mp4;*.webm|Pagine web|*.html;*.htm"
        };
        if (dialogo.ShowDialog(this) == DialogResult.OK) importa(dialogo.FileNames);
    }
}
