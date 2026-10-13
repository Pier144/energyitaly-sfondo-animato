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

    public void CopiaInLibreria(string percorsoOrigine, string nomeFile)
    {
        Directory.CreateDirectory(Percorsi.Libreria);
        File.Copy(percorsoOrigine, Path.Combine(Percorsi.Libreria, nomeFile), overwrite: false);
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
