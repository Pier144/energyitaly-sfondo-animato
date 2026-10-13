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
        // L'evento esiste da subito: anche una seconda istanza lanciata durante l'avvio della prima la trova.
        using var eventoApri = new EventWaitHandle(false, EventResetMode.AutoReset, NomeEventoApri);
        if (!primaIstanza)
        {
            // Una sola istanza (RF-42): la seconda chiede alla prima di aprire la finestra ed esce.
            // Prima le cede il diritto di andare in primo piano, che Windows dà solo al processo lanciato.
            if (!avvioAutomatico)
            {
                Win32.AllowSetForegroundWindow(Win32.ASFW_ANY);
                eventoApri.Set();
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
