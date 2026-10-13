using Microsoft.Web.WebView2.Core;

namespace EnergyItaly.SfondoAnimato;

static class Program
{
    const string NomeMutex = @"Local\EnergyItaly.SfondoAnimato";
    const string NomeEventoApri = @"Local\EnergyItaly.SfondoAnimato.Apri";
    const string Titolo = "Energy Italy Sfondo Animato";

    [STAThread]
    static int Main(string[] args)
    {
        bool avvioAutomatico = args.Contains("--avvio", StringComparer.OrdinalIgnoreCase);
        using var mutex = new Mutex(true, NomeMutex, out bool primaIstanza);
        if (!primaIstanza)
        {
            // Una sola istanza (RF-42): la seconda chiede alla prima di aprire la finestra ed esce.
            if (!avvioAutomatico && EventWaitHandle.TryOpenExisting(NomeEventoApri, out var evento))
            {
                evento.Set();
                evento.Dispose();
            }
            return 0;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => RegistraErrore($"Errore non gestito: {e.Exception}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => RegistraErrore($"Errore non gestito: {e.ExceptionObject}");
        ApplicationConfiguration.Initialize();

        try
        {
            CoreWebView2Environment.GetAvailableBrowserVersionString();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            RegistraErrore("WebView2 Runtime non installato");
            MessageBox.Show("Per mostrare lo sfondo animato serve Microsoft Edge WebView2 Runtime, che su questo PC non è installato.\n\nContatta l'assistenza IT.",
                Titolo, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        using var eventoApri = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEventoApri);
        AppSfondo app;
        try
        {
            app = new AppSfondo(apriFinestra: !avvioAutomatico, eventoApri);
        }
        catch (Exception ex)
        {
            RegistraErrore($"Avvio non riuscito: {ex}");
            MessageBox.Show($"Il programma non è installato correttamente e non può partire.\n\n{ex.Message}\n\nContatta l'assistenza IT.",
                Titolo, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        Application.Run(app);
        return 0;
    }

    static void RegistraErrore(string testo)
    {
        try { new LogFile(Percorsi.Log).Errore(testo); }
        catch { /* niente log: non si può fare altro */ }
    }
}
