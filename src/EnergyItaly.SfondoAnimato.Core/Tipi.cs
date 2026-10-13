namespace EnergyItaly.SfondoAnimato.Core;

/// <summary>Che cosa mostra uno sfondo.</summary>
public enum TipoVoce { Animazione, Immagine, Video, Html }

/// <summary>Unità dell'intervallo di cambio.</summary>
public enum UnitaTempo { Minuti, Ore }

/// <summary>Ordine di rotazione della playlist.</summary>
public enum Ordine { Sequenziale, Casuale }

/// <summary>Con più monitor: un'unica scena estesa o la stessa scena su ognuno.</summary>
public enum ModoMonitor { Estendi, Duplica }

/// <summary>Leggera: animazione congelata. Profonda: anche WebView nascosta e sospesa.</summary>
public enum LivelloPausa { Nessuna, Leggera, Profonda }

/// <summary>Perché lo sfondo è in pausa, in ordine crescente di gravità.</summary>
public enum MotivoPausa { Nessuno, ABatteria, Manuale, DesktopCoperto, SchermoIntero, MonitorSpenti, SessioneNonAttiva, SfondoInterrotto }
