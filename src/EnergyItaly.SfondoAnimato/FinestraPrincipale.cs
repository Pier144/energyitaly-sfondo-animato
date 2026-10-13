using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Finestra galleria (design B). Chiuderla la nasconde: il programma resta nell'area di notifica (D11).</summary>
sealed class FinestraPrincipale : Form
{
    static readonly Color Fondo = Color.FromArgb(0x07, 0x11, 0x1f);

    readonly ILog log;
    readonly HostWebView web;
    readonly Action<string> daApp;
    readonly Action<IReadOnlyList<string>> importa;
    bool inizializzata;
    bool chiusuraDefinitiva;

    public FinestraPrincipale(ILog log, Icon icona, Action<string> daApp, Action<IReadOnlyList<string>> importa)
    {
        this.log = log;
        this.daApp = daApp;
        this.importa = importa;
        web = new HostWebView(log);
        web.Messaggio += MessaggioRicevuto;
        Text = "Energy Italy Sfondo Animato";
        Icon = icona;
        BackColor = Fondo;
        StartPosition = FormStartPosition.CenterScreen;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int scuro = 1;
        Win32.DwmSetWindowAttribute(Handle, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE, ref scuro, sizeof(int));
        float k = DeviceDpi / 96f;
        var lavoro = Screen.FromHandle(Handle).WorkingArea;
        MinimumSize = new Size(Math.Min((int)(960 * k), lavoro.Width), Math.Min((int)(640 * k), lavoro.Height));
        Size = new Size(Math.Min((int)(1280 * k), (int)(lavoro.Width * 0.92)), Math.Min((int)(860 * k), (int)(lavoro.Height * 0.92)));
        CenterToScreen();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (inizializzata) return;
        inizializzata = true;
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
                    ApriDialogoImport();
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
