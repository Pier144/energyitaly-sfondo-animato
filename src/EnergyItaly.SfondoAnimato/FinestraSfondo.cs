using System.Drawing;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Finestra Win32 senza bordi, dietro le icone, che ospita il player (RF-01, RF-02, RF-14).</summary>
sealed class FinestraSfondo : NativeWindow
{
    public static readonly Color ColoreSfondo = Color.FromArgb(0x0f, 0x1e, 0x35);

    readonly ILog log;
    readonly DesktopHost desktop;
    readonly HostWebView web;
    Rettangolo area;
    bool chiusa;

    public event Action<string>? MessaggioDalPlayer;
    public event Action<bool>? Persa;

    public FinestraSfondo(ILog log, DesktopHost desktop)
    {
        this.log = log;
        this.desktop = desktop;
        web = new HostWebView(log);
        web.Messaggio += e =>
        {
            try { MessaggioDalPlayer?.Invoke(e.WebMessageAsJson); }
            catch (Exception ex) { log.Errore($"Messaggio del player non gestito: {ex.Message}"); }
        };
        web.ProcessoTerminato += () => Persa?.Invoke(true);
    }

    public bool Crea(Rettangolo area)
    {
        this.area = area;
        CreateHandle(new CreateParams
        {
            Caption = "Energy Italy Sfondo Animato",
            Style = unchecked((int)(Win32.WS_POPUP | Win32.WS_CLIPCHILDREN | Win32.WS_CLIPSIBLINGS)),
            ExStyle = Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE,
            X = area.X,
            Y = area.Y,
            Width = area.L,
            Height = area.A
        });
        return desktop.Aggancia(Handle, area);
    }

    public Task InizializzaWebAsync() =>
        web.InizializzaAsync(Handle, new Rectangle(0, 0, area.L, area.A), scalaFissa: true, ColoreSfondo,
            $"https://{Catalogo.HostApp}/player/player.html");

    public void Invia(string json)
    {
        try { web.Core?.PostWebMessageAsJson(json); }
        catch (Exception ex) { log.Avviso($"Messaggio al player non inviato: {ex.Message}"); }
    }

    public void ImpostaArea(Rettangolo nuova)
    {
        area = nuova;
        if (Handle == IntPtr.Zero) return;
        desktop.Posiziona(Handle, nuova);
        web.ImpostaArea(new Rectangle(0, 0, nuova.L, nuova.A));
    }

    public async void ImpostaSospensione(bool sospesa)
    {
        var controller = web.Controller;
        if (controller is null) return;
        try
        {
            if (sospesa)
            {
                controller.IsVisible = false;
                await controller.CoreWebView2.TrySuspendAsync();
            }
            else
            {
                controller.CoreWebView2.Resume();
                controller.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            log.Avviso($"Sospensione della WebView non riuscita: {ex.Message}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)Win32.MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
        if (m.Msg == Win32.WM_NCDESTROY && !chiusa)
        {
            log.Avviso("La finestra dello sfondo è stata distrutta (Explorer riavviato?)");
            Persa?.Invoke(false);
        }
    }

    public void Chiudi(bool ridisegna)
    {
        chiusa = true;
        web.Dispose();
        if (Handle != IntPtr.Zero)
        {
            desktop.Sgancia(Handle);
            DestroyHandle();
        }
        if (ridisegna) DesktopHost.RidisegnaSfondo();
    }
}
