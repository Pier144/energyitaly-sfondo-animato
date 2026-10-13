using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Log in italiano a rotazione: sfondo.log più 4 file precedenti da 1 MB (RF-53).</summary>
sealed class LogFile : ILog
{
    const long DimensioneMassima = 1024 * 1024;
    const int NumeroFile = 5;
    static readonly UTF8Encoding SenzaBom = new(false);

    readonly string cartella;
    readonly string percorso;
    readonly object blocco = new();

    public LogFile(string cartella)
    {
        this.cartella = cartella;
        Directory.CreateDirectory(cartella);
        percorso = Path.Combine(cartella, "sfondo.log");
    }

    public void Info(string testo) => Scrivi("INFO", testo);
    public void Avviso(string testo) => Scrivi("AVVISO", testo);
    public void Errore(string testo) => Scrivi("ERRORE", testo);

    void Scrivi(string livello, string testo)
    {
        var riga = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{livello}] {testo}{Environment.NewLine}";
        lock (blocco)
        {
            try
            {
                var info = new FileInfo(percorso);
                if (info.Exists && info.Length + riga.Length > DimensioneMassima) Ruota();
                File.AppendAllText(percorso, riga, SenzaBom);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        System.Diagnostics.Debug.Write(riga);
    }

    void Ruota()
    {
        string Nome(int i) => i == 0 ? percorso : Path.Combine(cartella, $"sfondo.{i}.log");
        File.Delete(Nome(NumeroFile - 1));
        for (int i = NumeroFile - 2; i >= 0; i--)
        {
            if (File.Exists(Nome(i))) File.Move(Nome(i), Nome(i + 1));
        }
    }
}
