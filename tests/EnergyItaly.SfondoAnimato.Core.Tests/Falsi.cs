using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

sealed class PlayerFalso : IVistaPlayer
{
    public List<string> Messaggi { get; } = [];
    public Rettangolo? Area { get; private set; }
    public bool Sospeso { get; private set; }
    public List<bool> Sospensioni { get; } = [];

    public void Invia(string json) => Messaggi.Add(json);
    public void ImpostaArea(Rettangolo area) => Area = area;
    public void ImpostaSospensione(bool sospesa) { Sospeso = sospesa; Sospensioni.Add(sospesa); }

    public IEnumerable<JsonElement> DiTipo(string tipo) =>
        Messaggi.Select(m => JsonDocument.Parse(m).RootElement).Where(e => e.GetProperty("tipo").GetString() == tipo);

    public JsonElement Ultimo(string tipo) => DiTipo(tipo).Last();
    public string? UltimoTipo => Messaggi.Count == 0 ? null : JsonDocument.Parse(Messaggi[^1]).RootElement.GetProperty("tipo").GetString();
}

sealed class AppFalsa : IVistaApp
{
    public List<string> Messaggi { get; } = [];
    public void Invia(string json) => Messaggi.Add(json);

    public JsonElement UltimoStato => Messaggi.Select(m => JsonDocument.Parse(m).RootElement).Last(e => e.GetProperty("tipo").GetString() == "stato");
    public IEnumerable<string> Avvisi => Messaggi.Select(m => JsonDocument.Parse(m).RootElement)
        .Where(e => e.GetProperty("tipo").GetString() == "avviso").Select(e => e.GetProperty("testo").GetString()!);
}

sealed class ArchivioFalso : IArchivio
{
    public string? Impostazioni { get; set; }
    public string? Libreria { get; set; }
    public HashSet<string> FileLibreria { get; } = [];
    public Dictionary<string, long> FileDaImportare { get; } = [];
    public List<(string Origine, string Nome)> Copie { get; } = [];
    public List<string> Cancellati { get; } = [];

    public string? LeggiImpostazioni() => Impostazioni;
    public void ScriviImpostazioni(string json) => Impostazioni = json;
    public string? LeggiLibreria() => Libreria;
    public void ScriviLibreria(string json) => Libreria = json;
    public bool EsisteInLibreria(string nomeFile) => FileLibreria.Contains(nomeFile);
    public long Dimensione(string percorso) => FileDaImportare.TryGetValue(percorso, out var d) ? d : throw new FileNotFoundException(percorso);
    public void CopiaInLibreria(string percorsoOrigine, string nomeFile) { Copie.Add((percorsoOrigine, nomeFile)); FileLibreria.Add(nomeFile); }
    public void CancellaDaLibreria(string nomeFile) { Cancellati.Add(nomeFile); FileLibreria.Remove(nomeFile); }
}

sealed class SistemaFalso : ISistema
{
    public bool? AvvioConWindows { get; private set; }
    public int CartelleLogAperte { get; private set; }
    public (string Titolo, bool InPausa) Icona { get; private set; }

    public void ImpostaAvvioConWindows(bool attivo) => AvvioConWindows = attivo;
    public void ApriCartellaLog() => CartelleLogAperte++;
    public void AggiornaIcona(string titolo, bool inPausa) => Icona = (titolo, inPausa);
}

sealed class LogFalso : ILog
{
    public List<string> Righe { get; } = [];
    public void Info(string testo) => Righe.Add("INFO " + testo);
    public void Avviso(string testo) => Righe.Add("AVVISO " + testo);
    public void Errore(string testo) => Righe.Add("ERRORE " + testo);
}
