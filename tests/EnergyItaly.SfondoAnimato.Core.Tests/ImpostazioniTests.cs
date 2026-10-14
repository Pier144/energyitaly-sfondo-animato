using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class ImpostazioniTests
{
    static readonly string[] Validi = ["onda-di-luce", "onde-di-energia", "aurora", "u-mare-abc123"];
    static readonly string[] Base = ["onda-di-luce", "onde-di-energia", "aurora"];

    static Impostazioni Codice() => ArchivioImpostazioni.Predefinite(null, Validi, Base, []);

    [Fact]
    public void Predefinite_SenzaFileAziendale_ValoriDelCodice()
    {
        var i = Codice();

        Assert.Equal(30, i.IntervalloValore);
        Assert.Equal(UnitaTempo.Minuti, i.IntervalloUnita);
        Assert.Equal(TimeSpan.FromMinutes(30), i.Intervallo);
        Assert.True(i.FineVideo);
        Assert.Equal(Ordine.Sequenziale, i.Ordine);
        Assert.Equal(Base, i.Playlist);
        Assert.Equal(2, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Estendi, i.ModoMonitor);
        Assert.True(i.PausaABatteria);
        Assert.True(i.AvvioConWindows);
        Assert.True(i.AggiornamentiAutomatici);
        Assert.Null(i.UltimaVoce);
    }

    [Fact]
    public void Predefinite_FileAziendale_SovrascriveIlCodice()
    {
        const string aziendale = """{ "versione": 2, "intervallo": { "valore": 15, "unita": "minuti" }, "ordine": "casuale", "playlist": ["aurora"] }""";

        var i = ArchivioImpostazioni.Predefinite(aziendale, Validi, Base, []);

        Assert.Equal(15, i.IntervalloValore);
        Assert.Equal(Ordine.Casuale, i.Ordine);
        Assert.Equal(new[] { "aurora" }, i.Playlist);
        Assert.Equal(2, i.DissolvenzaSecondi);
    }

    [Fact]
    public void Carica_FileUtenteValido_Letto()
    {
        const string utente = """
            {
              "versione": 2,
              "intervallo": { "valore": 2, "unita": "ore" },
              "rotazioneAttiva": true, "fineVideo": false, "ordine": "casuale",
              "playlist": ["u-mare-abc123", "aurora"], "dissolvenzaSecondi": 4,
              "modoMonitor": "duplica", "pausaABatteria": false, "avvioConWindows": false,
              "aggiornamentiAutomatici": false,
              "ultimaVoce": "aurora"
            }
            """;
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica(utente, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Equal(TimeSpan.FromHours(2), i.Intervallo);
        Assert.False(i.FineVideo);
        Assert.Equal(Ordine.Casuale, i.Ordine);
        Assert.Equal(new[] { "u-mare-abc123", "aurora" }, i.Playlist);
        Assert.Equal(4, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Duplica, i.ModoMonitor);
        Assert.False(i.PausaABatteria);
        Assert.False(i.AvvioConWindows);
        Assert.False(i.AggiornamentiAutomatici);
        Assert.Equal("aurora", i.UltimaVoce);
    }

    [Fact]
    public void AggiornamentiAutomatici_ValoreAziendaleEModifica()
    {
        var aziendale = ArchivioImpostazioni.Predefinite("""{ "versione": 2, "aggiornamentiAutomatici": false }""", Validi, Base, []);
        Assert.False(aziendale.AggiornamentiAutomatici);

        var (r, e) = Codice().Applica(new ModificheImpostazioni(AggiornamentiAutomatici: false));
        Assert.Null(e);
        Assert.False(r.AggiornamentiAutomatici);
        Assert.Contains("\"aggiornamentiAutomatici\": false", ArchivioImpostazioni.Scrivi(r));
        Assert.False(ArchivioImpostazioni.Carica(ArchivioImpostazioni.Scrivi(r), Codice(), Validi, []).AggiornamentiAutomatici);
    }

    [Theory]
    [InlineData("{ rotto")]
    [InlineData("[]")]
    [InlineData("""{ "versione": 1, "intervalloMinuti": 5 }""")]
    [InlineData("""{ "intervallo": { "valore": 5, "unita": "minuti" } }""")]
    public void Carica_FileIllegibileOVersioneDiversa_PredefiniteConAvviso(string json)
    {
        var avvisi = new List<string>();
        var predefinite = Codice();

        var i = ArchivioImpostazioni.Carica(json, predefinite, Validi, avvisi);

        Assert.Same(predefinite, i);
        Assert.Single(avvisi);
    }

    [Fact]
    public void Carica_CampiSbagliati_CorrettiUnoPerUno()
    {
        const string utente = """
            {
              "versione": 2,
              "intervallo": { "valore": 0, "unita": "ore" },
              "rotazioneAttiva": "si", "fineVideo": 1, "ordine": "boh",
              "dissolvenzaSecondi": 3, "modoMonitor": 2, "pausaABatteria": false
            }
            """;
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica(utente, Codice(), Validi, avvisi);

        Assert.Equal(30, i.IntervalloValore);
        Assert.Equal(UnitaTempo.Minuti, i.IntervalloUnita);
        Assert.True(i.RotazioneAttiva);
        Assert.True(i.FineVideo);
        Assert.Equal(Ordine.Sequenziale, i.Ordine);
        Assert.Equal(2, i.DissolvenzaSecondi);
        Assert.Equal(ModoMonitor.Estendi, i.ModoMonitor);
        Assert.False(i.PausaABatteria);
        Assert.Equal(6, avvisi.Count);
    }

    [Fact]
    public void Carica_SoloUnita_ValoreRidottoAlMassimo()
    {
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "intervallo": { "unita": "ore" } }""", Codice(), Validi, []);

        Assert.Equal(UnitaTempo.Ore, i.IntervalloUnita);
        Assert.Equal(24, i.IntervalloValore);
    }

    [Fact]
    public void Carica_PlaylistConSconosciutiEDoppioni_Filtrata()
    {
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "playlist": ["aurora", "sparito", "aurora", 5, "onda-di-luce"] }""", Codice(), Validi, avvisi);

        Assert.Equal(new[] { "aurora", "onda-di-luce" }, i.Playlist);
        Assert.Equal(2, avvisi.Count);
    }

    [Fact]
    public void Carica_PlaylistTuttaSconosciuta_PredefinitaConAvviso()
    {
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "playlist": ["sparito"] }""", Codice(), Validi, avvisi);

        Assert.Equal(Base, i.Playlist);
        Assert.Contains(avvisi, a => a.Contains("playlist vuota"));
    }

    [Fact]
    public void Carica_UltimaVoceSconosciuta_Ignorata()
    {
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "ultimaVoce": "sparito" }""", Codice(), Validi, []);
        Assert.Null(i.UltimaVoce);
    }

    [Fact]
    public void Scrivi_AndataERitorno()
    {
        var originale = Codice() with
        {
            IntervalloValore = 3, IntervalloUnita = UnitaTempo.Ore, Ordine = Ordine.Casuale,
            Playlist = ["aurora", "u-mare-abc123"], ModoMonitor = ModoMonitor.Duplica, UltimaVoce = "aurora", FineVideo = false
        };

        var json = ArchivioImpostazioni.Scrivi(originale);
        var avvisi = new List<string>();
        var riletta = ArchivioImpostazioni.Carica(json, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Contains("\"versione\": 2", json);
        Assert.Equal(originale.Intervallo, riletta.Intervallo);
        Assert.Equal(originale.Ordine, riletta.Ordine);
        Assert.Equal(originale.Playlist, riletta.Playlist);
        Assert.Equal(originale.ModoMonitor, riletta.ModoMonitor);
        Assert.Equal(originale.UltimaVoce, riletta.UltimaVoce);
        Assert.False(riletta.FineVideo);
    }

    [Fact]
    public void SfondiAziendaliVisti_AndataERitorno()
    {
        var originale = Codice() with { SfondiAziendaliVisti = ["onda-di-luce", "aurora"] };

        var riletta = ArchivioImpostazioni.Carica(ArchivioImpostazioni.Scrivi(originale), Codice(), Validi, []);

        Assert.Equal(new[] { "onda-di-luce", "aurora" }, riletta.SfondiAziendaliVisti);
    }

    [Fact]
    public void SfondiAziendaliVisti_FileUtenteSenzaCampo_QuelliDellaVersione110()
    {
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "playlist": ["aurora"] }""", Codice(), Validi, []);

        Assert.Equal(new[] { "onda-di-luce", "onde-di-energia", "paesaggio-rinnovabile", "circuito", "aurora" }, i.SfondiAziendaliVisti);
    }

    [Fact]
    public void SfondiAziendaliVisti_SenzaFileUtente_Nessuno()
    {
        Assert.Null(ArchivioImpostazioni.Carica(null, Codice(), Validi, []).SfondiAziendaliVisti);
    }

    [Fact]
    public void SfondiAziendaliVisti_NonValido_AvvisoEQuelliDellaVersione110()
    {
        var avvisi = new List<string>();

        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "sfondiAziendaliVisti": "aurora" }""", Codice(), Validi, avvisi);

        Assert.Equal(5, i.SfondiAziendaliVisti?.Count);
        Assert.Contains(avvisi, a => a.Contains("sfondiAziendaliVisti"));
    }

    [Fact]
    public void AccogliNuoviSfondi_AggiungeInCodaSoloINuoviDellaPlaylistPredefinita()
    {
        var i = Codice() with { Playlist = ["aurora"], SfondiAziendaliVisti = ["onda-di-luce", "aurora"] };
        string[] aziendali = ["onda-di-luce", "aurora", "segnale", "trama", "nascosto"];
        string[] predefinita = ["segnale", "onda-di-luce", "aurora", "trama"];

        var (r, aggiunti) = i.AccogliNuoviSfondi(aziendali, predefinita);

        // onda-di-luce era già stata vista e l'utente l'ha tolta: non torna. "nascosto" non è nella playlist predefinita.
        Assert.Equal(new[] { "segnale", "trama" }, aggiunti);
        Assert.Equal(new[] { "aurora", "segnale", "trama" }, r.Playlist);
        Assert.Equal(aziendali, r.SfondiAziendaliVisti);
    }

    [Fact]
    public void AccogliNuoviSfondi_GiaInPlaylist_NonDuplicato()
    {
        var i = Codice() with { Playlist = ["segnale", "aurora"], SfondiAziendaliVisti = ["aurora"] };

        var (r, aggiunti) = i.AccogliNuoviSfondi(["aurora", "segnale"], ["aurora", "segnale"]);

        Assert.Empty(aggiunti);
        Assert.Equal(new[] { "segnale", "aurora" }, r.Playlist);
    }

    [Fact]
    public void AccogliNuoviSfondi_SenzaElencoVisti_NonAggiungeNiente()
    {
        var i = Codice() with { Playlist = ["aurora"] };

        var (r, aggiunti) = i.AccogliNuoviSfondi(["aurora", "segnale"], ["aurora", "segnale"]);

        Assert.Empty(aggiunti);
        Assert.Equal(new[] { "aurora" }, r.Playlist);
        Assert.Equal(new[] { "aurora", "segnale" }, r.SfondiAziendaliVisti);
    }

    [Fact]
    public void Personalizzazioni_AndataERitorno()
    {
        var originale = Codice() with
        {
            Personalizzazioni = new Dictionary<string, Personalizzazione>
            {
                ["aurora"] = new()
                {
                    Tavolozza = "personalizzata", Fondo = "#102030", Accento = "#e0a040",
                    Opzioni = new Dictionary<string, ValoreOpzione>
                    {
                        ["velocita"] = ValoreOpzione.DiNumero(1.5),
                        ["veli"] = ValoreOpzione.DiBooleano(false),
                        ["ampiezza"] = ValoreOpzione.DiTesto("forte")
                    },
                    ModoMonitor = ModoMonitor.Duplica
                },
                ["u-mare-abc123"] = new() { Riempimento = Riempimento.Adatta }
            }
        };

        var json = ArchivioImpostazioni.Scrivi(originale);
        var avvisi = new List<string>();
        var riletta = ArchivioImpostazioni.Carica(json, Codice(), Validi, avvisi);

        Assert.Empty(avvisi);
        Assert.Contains("\"velocita\": 1.5", json);
        Assert.Contains("\"veli\": false", json);
        var a = riletta.Personalizzazioni["aurora"];
        Assert.Equal("personalizzata", a.Tavolozza);
        Assert.Equal("#e0a040", a.Accento);
        Assert.Equal(1.5, a.Opzioni["velocita"].Numero);
        Assert.False(a.Opzioni["veli"].Booleano);
        Assert.Equal("forte", a.Opzioni["ampiezza"].Testo);
        Assert.Equal(ModoMonitor.Duplica, a.ModoMonitor);
        Assert.Equal(Riempimento.Adatta, riletta.Personalizzazioni["u-mare-abc123"].Riempimento);
    }

    [Fact]
    public void Personalizzazioni_CampiDelTipoSbagliatoTolti()
    {
        var avvisi = new List<string>();
        var i = ArchivioImpostazioni.Carica("""
            { "versione": 2, "personalizzazioni": {
                "aurora": { "tavolozza": 3, "opzioni": { "velocita": [1] }, "modoMonitor": "ruota" },
                "onda-di-luce": "notte",
                "onde-di-energia": { "modoMonitor": "duplica" } } }
            """, Codice(), Validi, avvisi);

        Assert.Equal(new[] { "onde-di-energia" }, i.Personalizzazioni.Keys);
        Assert.Equal(4, avvisi.Count);
    }

    [Fact]
    public void Personalizzazioni_NumeroNonRappresentabile_AvvisoENessunErrore()
    {
        var avvisi = new List<string>();
        var i = ArchivioImpostazioni.Carica("""{ "versione": 2, "personalizzazioni": { "aurora": { "opzioni": { "velocita": 1e400 } } } }""",
            Codice(), Validi, avvisi);

        Assert.Empty(i.Personalizzazioni);
        Assert.Contains(avvisi, a => a.Contains("velocita"));
    }

    [Fact]
    public void Personalizzazioni_IgnorateNelFileAziendale()
    {
        var i = ArchivioImpostazioni.Predefinite("""{ "versione": 2, "personalizzazioni": { "aurora": { "tavolozza": "notte" } } }""", Validi, Base, []);
        Assert.Empty(i.Personalizzazioni);
    }

    [Fact]
    public void Applica_ValoreFuoriIntervallo_Errore()
    {
        var i = Codice();

        var (r, errore) = i.Applica(new ModificheImpostazioni(IntervalloValore: 0));

        Assert.Same(i, r);
        Assert.Equal("Scrivi un numero intero da 1 a 1440 minuti", errore);
        Assert.Equal("Scrivi un numero intero da 1 a 24 ore", i.Applica(new ModificheImpostazioni(IntervalloValore: 30, IntervalloUnita: "ore")).Errore);
    }

    [Fact]
    public void Applica_CambioUnitaSenzaValore_ValoreRidotto()
    {
        var (r, errore) = Codice().Applica(new ModificheImpostazioni(IntervalloUnita: "ore"));

        Assert.Null(errore);
        Assert.Equal(UnitaTempo.Ore, r.IntervalloUnita);
        Assert.Equal(24, r.IntervalloValore);
        Assert.Equal(TimeSpan.FromHours(24), r.Intervallo);
    }

    [Fact]
    public void Applica_DueOre()
    {
        var (r, _) = Codice().Applica(new ModificheImpostazioni(IntervalloValore: 2, IntervalloUnita: "ore"));
        Assert.Equal(TimeSpan.FromHours(2), r.Intervallo);
    }

    [Fact]
    public void Applica_Mai_IntervalloNullo()
    {
        var (r, _) = Codice().Applica(new ModificheImpostazioni(RotazioneAttiva: false));
        Assert.Null(r.Intervallo);
        Assert.Equal(30, r.IntervalloValore);
    }

    [Fact]
    public void Applica_ValoriEnumerati()
    {
        var (r, e) = Codice().Applica(new ModificheImpostazioni(Ordine: "casuale", ModoMonitor: "duplica", DissolvenzaSecondi: 4, FineVideo: false, PausaABatteria: false, AvvioConWindows: false));

        Assert.Null(e);
        Assert.Equal(Ordine.Casuale, r.Ordine);
        Assert.Equal(ModoMonitor.Duplica, r.ModoMonitor);
        Assert.Equal(4, r.DissolvenzaSecondi);
        Assert.False(r.FineVideo);
        Assert.False(r.PausaABatteria);
        Assert.False(r.AvvioConWindows);
    }

    [Theory]
    [InlineData(null, null, null, 3, "Dissolvenza non valida: scegli 1, 2 o 4 secondi")]
    [InlineData("secondi", null, null, null, "Unità di tempo non valida")]
    [InlineData(null, "boh", null, null, "Ordine non valido")]
    [InlineData(null, null, "specchio", null, "Modo monitor non valido")]
    public void Applica_ValoriNonValidi_Errore(string? unita, string? ordine, string? modo, int? dissolvenza, string atteso)
    {
        var (_, errore) = Codice().Applica(new ModificheImpostazioni(IntervalloUnita: unita, Ordine: ordine, ModoMonitor: modo, DissolvenzaSecondi: dissolvenza));
        Assert.Equal(atteso, errore);
    }
}
