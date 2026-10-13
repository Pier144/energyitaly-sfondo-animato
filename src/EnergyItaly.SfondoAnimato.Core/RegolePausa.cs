namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Segnali raccolti dal sistema operativo (senza la pausa manuale e le impostazioni).</summary>
public sealed record SegnaliSistema(
    bool DesktopCoperto = false,
    bool SchermoIntero = false,
    bool SessioneNonAttiva = false,
    bool MonitorSpenti = false,
    bool ABatteria = false,
    bool SfondoInterrotto = false);

public sealed record DecisionePausa(LivelloPausa Livello, MotivoPausa Motivo)
{
    public static readonly DecisionePausa Nessuna = new(LivelloPausa.Nessuna, MotivoPausa.Nessuno);
}

/// <summary>Tabella di pausa del PRD (sezione 5.4) più la pausa manuale. Vince il motivo più grave.</summary>
public static class RegolePausa
{
    public static DecisionePausa Valuta(SegnaliSistema s, bool pausaABatteria, bool pausaManuale)
    {
        if (s.SfondoInterrotto) return new(LivelloPausa.Profonda, MotivoPausa.SfondoInterrotto);
        if (s.SessioneNonAttiva) return new(LivelloPausa.Profonda, MotivoPausa.SessioneNonAttiva);
        if (s.MonitorSpenti) return new(LivelloPausa.Profonda, MotivoPausa.MonitorSpenti);
        // Leggera e non profonda: con la WebView nascosta, alla ripresa si vedrebbe lo sfondo di Windows
        // e il movimento ripartirebbe in ritardo. Ferma sull'ultimo fotogramma non consuma quasi nulla.
        if (s.SchermoIntero) return new(LivelloPausa.Leggera, MotivoPausa.SchermoIntero);
        if (s.DesktopCoperto) return new(LivelloPausa.Leggera, MotivoPausa.DesktopCoperto);
        if (pausaManuale) return new(LivelloPausa.Leggera, MotivoPausa.Manuale);
        if (s.ABatteria && pausaABatteria) return new(LivelloPausa.Leggera, MotivoPausa.ABatteria);
        return DecisionePausa.Nessuna;
    }

    /// <summary>Testo mostrato dopo "IN PAUSA: " nella finestra; vuoto per la pausa manuale.</summary>
    public static string Testo(MotivoPausa motivo) => motivo switch
    {
        MotivoPausa.SfondoInterrotto => "SFONDO INTERROTTO",
        MotivoPausa.SessioneNonAttiva => "SESSIONE BLOCCATA",
        MotivoPausa.MonitorSpenti => "MONITOR SPENTI",
        MotivoPausa.SchermoIntero => "APP A SCHERMO INTERO",
        MotivoPausa.DesktopCoperto => "DESKTOP COPERTO",
        MotivoPausa.ABatteria => "A BATTERIA",
        _ => ""
    };
}
