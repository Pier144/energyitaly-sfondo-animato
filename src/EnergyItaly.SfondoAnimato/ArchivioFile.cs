using System.Text;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Impostazioni, libreria e file importati su disco. Le scritture passano da un file temporaneo.</summary>
sealed class ArchivioFile : IArchivio
{
    static readonly UTF8Encoding SenzaBom = new(false);

    public string? LeggiImpostazioni() => LeggiSeEsiste(Percorsi.Impostazioni);
    public void ScriviImpostazioni(string json) => ScriviSicuro(Percorsi.Impostazioni, json);
    public string? LeggiLibreria() => LeggiSeEsiste(Percorsi.FileLibreria);
    public void ScriviLibreria(string json) => ScriviSicuro(Percorsi.FileLibreria, json);

    public bool EsisteInLibreria(string nomeFile) => File.Exists(Path.Combine(Percorsi.Libreria, nomeFile));

    public long Dimensione(string percorso) => new FileInfo(percorso).Length;

    public void ConservaLibreriaIlleggibile()
    {
        if (File.Exists(Percorsi.FileLibreria))
            File.Copy(Percorsi.FileLibreria, $"{Percorsi.FileLibreria}.illeggibile-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: false);
    }

    public async Task CopiaInLibreriaAsync(string percorsoOrigine, string nomeFile)
    {
        Directory.CreateDirectory(Percorsi.Libreria);
        var destinazione = Path.Combine(Percorsi.Libreria, nomeFile);
        bool creato = false;
        try
        {
            await using var origine = new FileStream(percorsoOrigine, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
            await using var copia = new FileStream(destinazione, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);
            creato = true;
            await origine.CopyToAsync(copia);
        }
        catch
        {
            // Una copia a metà non deve restare nella libreria.
            if (creato) try { File.Delete(destinazione); } catch (IOException) { }
            throw;
        }
    }

    public void CancellaDaLibreria(string nomeFile)
    {
        var percorso = Path.Combine(Percorsi.Libreria, nomeFile);
        if (File.Exists(percorso)) File.Delete(percorso);
    }

    static string? LeggiSeEsiste(string percorso) => File.Exists(percorso) ? File.ReadAllText(percorso, Encoding.UTF8) : null;

    static void ScriviSicuro(string percorso, string testo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(percorso)!);
        var temporaneo = percorso + ".tmp";
        File.WriteAllText(temporaneo, testo, SenzaBom);
        File.Move(temporaneo, percorso, overwrite: true);
    }
}
