namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>La finestra di sfondo con il player.</summary>
public interface IVistaPlayer
{
    void Invia(string json);
    void ImpostaArea(Rettangolo area);
    void ImpostaSospensione(bool sospesa);
}

/// <summary>La finestra principale (galleria).</summary>
public interface IVistaApp
{
    void Invia(string json);
}

/// <summary>File su disco: impostazioni, libreria e file importati.</summary>
public interface IArchivio
{
    string? LeggiImpostazioni();
    void ScriviImpostazioni(string json);
    string? LeggiLibreria();
    void ScriviLibreria(string json);
    /// <summary>Salva da parte un libreria.json illeggibile prima che venga sovrascritto.</summary>
    void ConservaLibreriaIlleggibile();
    bool EsisteInLibreria(string nomeFile);
    long Dimensione(string percorso);
    /// <summary>Copia senza bloccare l'interfaccia: i video possono arrivare a 2 GB.</summary>
    Task CopiaInLibreriaAsync(string percorsoOrigine, string nomeFile);
    void CancellaDaLibreria(string nomeFile);
}

/// <summary>Servizi di Windows: avvio automatico, cartella log, icona nell'area di notifica.</summary>
public interface ISistema
{
    void ImpostaAvvioConWindows(bool attivo);
    /// <summary>Accende o spegne gli aggiornamenti automatici (impostazione "Aggiornamenti automatici").</summary>
    void ImpostaAggiornamentiAutomatici(bool attivo);
    void ApriCartellaLog();
    void AggiornaIcona(string titolo, bool inPausa);
}

public interface ILog
{
    void Info(string testo);
    void Avviso(string testo);
    void Errore(string testo);
}
