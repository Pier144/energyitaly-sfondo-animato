using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).
/// In cima la striscia del titolo Energy Italy (D12); la WebView occupa il resto.
/// </summary>
sealed class FinestraPrincipale : Form
{
    static readonly Color Fondo = Color.FromArgb(0x07, 0x11, 0x1f);
    static readonly TimeSpan IntervalloCrash = TimeSpan.FromMinutes(5);

    readonly ILog log;
    readonly BarraTitoloFinestra barra;
    // Creato una volta: WndProc passa da qui per ogni messaggio della finestra.
    readonly BarraTitoloFinestra.ProceduraStandard standard;
    HostWebView web;
    readonly Action<string> daApp;
    readonly Action<IReadOnlyList<string>> importa;
    readonly Action<string> aggiornamenti;
    bool inizializzata;
    bool chiusuraDefinitiva;
    readonly Queue<DateTime> crash = new();

    /// <param name="aggiornamenti">Comandi per l'aggiornatore: "controlla", "installa", "pronta".</param>
    public FinestraPrincipale(ILog log, Icon icona, Action<string> daApp, Action<IReadOnlyList<string>> importa, Action<string> aggiornamenti)
    {
        this.log = log;
        this.daApp = daApp;
        this.importa = importa;
        this.aggiornamenti = aggiornamenti;
        web = CreaWebView();
        Text = "Energy Italy Sfondo Animato";
        Icon = icona;
        BackColor = Fondo;
        StartPosition = FormStartPosition.CenterScreen;
        standard = ProceduraStandard;
        barra = new BarraTitoloFinestra(this, icona, Text);
        DoubleBuffered = true;
        SystemEvents.UserPreferenceChanged += PreferenzeCambiate;
    }

    protected override void WndProc(ref Message m)
    {
        // barra è null solo se Windows manda messaggi prima della fine del costruttore.
        if (barra is not null && barra.Gestisci(ref m, standard)) return;
        base.WndProc(ref m);
    }

    void ProceduraStandard(ref Message m) => base.WndProc(ref m);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int scuro = 1;
        if (Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref scuro, sizeof(int)) != 0)
            Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE_PRIMA_20H1, ref scuro, sizeof(int));
        barra.ImpostaDpi(DeviceDpi);
        // Ricalcolo della cornice subito: la barra standard non compare nemmeno al primo disegno.
        Win32.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE | Win32.SWP_FRAMECHANGED);
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
            await web.InizializzaAsync(Handle, AreaWebView(), scalaFissa: false, Fondo, $"https://{Catalogo.HostApp}/app/index.html");
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
        // I pulsanti seguono il bordo destro: si ridisegna solo la striscia.
        barra.Ridisegna();
        AggiornaWebView();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        // Riaperta dall'area di notifica, nessun pulsante deve restare evidenziato.
        if (!Visible) barra.Azzera();
        AggiornaWebView();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        barra.ImpostaDpi(e.DeviceDpiNew);
        AggiornaWebView();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        barra.Disegna(e.Graphics);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        barra.Ridisegna();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        barra.Ridisegna();
    }

    // Il contrasto alto può cambiare a programma aperto: la striscia passa ai colori di sistema.
    void PreferenzeCambiate(object? mittente, UserPreferenceChangedEventArgs e)
    {
        // Solo le categorie che toccano colori e contrasto: i cambi di sfondo del programma stesso no.
        if (e.Category is not (UserPreferenceCategory.Accessibility or UserPreferenceCategory.Color or UserPreferenceCategory.General)) return;
        if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(barra.Ridisegna));
    }

    /// <summary>L'area della WebView: tutto sotto la striscia del titolo.</summary>
    Rectangle AreaWebView()
    {
        int alto = barra.Altezza;
        return new Rectangle(0, alto, ClientSize.Width, Math.Max(0, ClientSize.Height - alto));
    }

    void AggiornaWebView()
    {
        var controller = web.Controller;
        if (controller is null) return;
        bool visibile = Visible && WindowState != FormWindowState.Minimized;
        // Ridotta a icona l'area client è 0 x 0: i limiti restano quelli di prima.
        if (visibile) web.ImpostaArea(AreaWebView());
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

    /// <summary>L'utente la sta guardando: visibile e non ridotta a icona.</summary>
    public bool Vista => Visible && WindowState != FormWindowState.Minimized;

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

    protected override void Dispose(bool disposing)
    {
        if (disposing) SystemEvents.UserPreferenceChanged -= PreferenzeCambiate;
        base.Dispose(disposing);
        // Dopo: durante la distruzione della finestra WndProc riceve ancora messaggi.
        if (disposing) barra.Dispose();
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
                case "controllaAggiornamenti":
                    aggiornamenti("controlla");
                    break;
                case "installaAggiornamento":
                    aggiornamenti("installa");
                    break;
                case "pronta":
                    // Lo stato lo manda il coordinatore, la riga "Versione" l'aggiornatore.
                    daApp(json);
                    aggiornamenti("pronta");
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
