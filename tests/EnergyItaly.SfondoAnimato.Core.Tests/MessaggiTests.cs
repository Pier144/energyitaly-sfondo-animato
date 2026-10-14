using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class MessaggiTests
{
    static readonly Opzione Durata = new("durata", "Durata", TipoOpzione.Numero, ValoreOpzione.DiNumero(16), 8, 30, 1);
    static readonly Voce Onda = new("onda-di-luce", TipoVoce.Animazione, "Onda di luce", true, "onda-di-luce.html", [Durata], null, true);
    static readonly Disposizione PcRiferimento = LayoutMonitor.Calcola(
        [new Monitor("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true), new Monitor("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false)],
        ModoMonitor.Estendi);

    static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void LeggiComandoApp_Semplice()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "mostra", "id": "aurora" }""");
        Assert.Equal(new ComandoApp("mostra", "aurora", null, null), c);
    }

    [Fact]
    public void LeggiComandoApp_PlaylistIgnoraNonStringhe()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "playlist", "ids": ["a", 3, "b", null] }""");
        Assert.Equal(new[] { "a", "b" }, c!.Ids);
    }

    [Fact]
    public void LeggiComandoApp_Modifiche()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "impostazioni", "modifiche": { "intervalloValore": 2, "intervalloUnita": "ore", "fineVideo": false, "ordine": 5 } }""");

        Assert.Equal(new ModificheImpostazioni(IntervalloValore: 2, IntervalloUnita: "ore", FineVideo: false), c!.Modifiche);
    }

    [Fact]
    public void LeggiComandoApp_NumeroNonIntero_DiventaZero()
    {
        var c = Messaggi.LeggiComandoApp("""{ "tipo": "impostazioni", "modifiche": { "intervalloValore": 1.5 } }""");
        Assert.Equal(0, c!.Modifiche!.IntervalloValore);
    }

    [Theory]
    [InlineData("non json")]
    [InlineData("[]")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "tipo": 4 }""")]
    public void LeggiComandoApp_NonValido_Null(string json)
    {
        Assert.Null(Messaggi.LeggiComandoApp(json));
    }

    [Fact]
    public void LeggiEventoPlayer()
    {
        Assert.Equal(new EventoPlayer("durataVideo", "v", null, 160.4), Messaggi.LeggiEventoPlayer("""{ "tipo": "durataVideo", "id": "v", "secondi": 160.4 }"""));
        Assert.Equal(new EventoPlayer("errore", "v", "boh", null), Messaggi.LeggiEventoPlayer("""{ "tipo": "errore", "id": "v", "messaggio": "boh" }"""));
        Assert.Null(Messaggi.LeggiEventoPlayer("{"));
    }

    [Fact]
    public void Mostra_ContieneVoceEScene()
    {
        var m = Json(Messaggi.Mostra(Onda, "https://x/onda.html?durata=16", Riempimento.Riempi, PcRiferimento, 2000, true));

        Assert.Equal("mostra", m.GetProperty("tipo").GetString());
        Assert.Equal("onda-di-luce", m.GetProperty("id").GetString());
        Assert.Equal("animazione", m.GetProperty("tipoVoce").GetString());
        Assert.Equal("https://x/onda.html?durata=16", m.GetProperty("url").GetString());
        Assert.Equal("riempi", m.GetProperty("riempimento").GetString());
        Assert.Equal("estendi", m.GetProperty("modo").GetString());
        Assert.Equal(2000, m.GetProperty("dissolvenzaMs").GetInt32());
        Assert.True(m.GetProperty("fineVideo").GetBoolean());
        var scena = Assert.Single(m.GetProperty("scene").EnumerateArray());
        Assert.Equal(3840, scena.GetProperty("area").GetProperty("w").GetInt32());
        Assert.Equal(7, scena.GetProperty("monitor")[0].GetProperty("y").GetInt32());
    }

    [Fact]
    public void LeggiComandoApp_Personalizza()
    {
        var c = Messaggi.LeggiComandoApp("""
            { "tipo": "personalizza", "id": "segnale", "modifiche": { "tavolozza": "personalizzata", "fondo": "#102030",
              "opzioni": { "velocita": 1.5, "veli": false, "ampiezza": "forte" }, "modoMonitor": "generale", "riempimento": "adatta" } }
            """)!;

        Assert.Equal("personalizza", c.Tipo);
        Assert.Equal("segnale", c.Id);
        Assert.Null(c.Modifiche);
        var p = c.Personalizzazione!;
        Assert.Equal("personalizzata", p.Tavolozza);
        Assert.Equal("#102030", p.Fondo);
        Assert.Null(p.Accento);
        Assert.Equal(1.5, p.Opzioni!["velocita"].Numero);
        Assert.False(p.Opzioni["veli"].Booleano);
        Assert.Equal("forte", p.Opzioni["ampiezza"].Testo);
        Assert.Equal("generale", p.ModoMonitor);
        Assert.Equal("adatta", p.Riempimento);
    }

    [Fact]
    public void Stato_VociConUrlPersonalizzatoOpzioniETavolozze()
    {
        var imp = new Impostazioni
        {
            Playlist = ["onda-di-luce"],
            Personalizzazioni = new Dictionary<string, Personalizzazione>
            {
                ["onda-di-luce"] = new()
                {
                    Opzioni = new Dictionary<string, ValoreOpzione> { ["durata"] = ValoreOpzione.DiNumero(20) },
                    ModoMonitor = ModoMonitor.Duplica
                }
            }
        };
        var s = new StatoVista([Onda], imp, "onda-di-luce", true, false, false, null, 60000, 1800000, true, false, PcRiferimento,
            [Personalizzazioni.EnergyItaly]);

        var j = Json(Messaggi.Stato(s));
        var v = j.GetProperty("voci")[0];

        Assert.EndsWith("onda-di-luce.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&durata=20", v.GetProperty("url").GetString());
        Assert.Equal("durata", v.GetProperty("opzioni")[0].GetProperty("id").GetString());
        Assert.Equal("numero", v.GetProperty("opzioni")[0].GetProperty("tipo").GetString());
        Assert.Equal(16, v.GetProperty("opzioni")[0].GetProperty("predefinito").GetDouble());
        Assert.Equal(20, v.GetProperty("personalizzazione").GetProperty("opzioni").GetProperty("durata").GetDouble());
        Assert.Equal("duplica", v.GetProperty("modoMonitor").GetString());
        Assert.Equal("riempi", v.GetProperty("riempimento").GetString());
        Assert.Equal("energy-italy", j.GetProperty("tavolozze")[0].GetProperty("id").GetString());
    }

    [Fact]
    public void Semplice_EAvviso()
    {
        Assert.Equal("""{"tipo":"pausa"}""", Messaggi.Semplice("pausa"));
        Assert.Equal("""{"tipo":"avviso","testo":"Aggiunto alla libreria: Città"}""", Messaggi.Avviso("Aggiunto alla libreria: Città"));
    }

    [Fact]
    public void Stato_CampiPrincipali()
    {
        var imp = new Impostazioni { Playlist = ["onda-di-luce"] };
        var s = new StatoVista([Onda], imp, "onda-di-luce", true, false, false, null, 60000, 1800000, true, false, PcRiferimento);

        var j = Json(Messaggi.Stato(s));

        Assert.Equal("stato", j.GetProperty("tipo").GetString());
        Assert.Equal("Onda di luce", j.GetProperty("voci")[0].GetProperty("titolo").GetString());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("voci")[0].GetProperty("durataSecondi").ValueKind);
        Assert.Equal(JsonValueKind.Null, j.GetProperty("voci")[0].GetProperty("personalizzazione").ValueKind);
        Assert.Equal("onda-di-luce", j.GetProperty("playlist")[0].GetString());
        Assert.Equal("minuti", j.GetProperty("impostazioni").GetProperty("intervalloUnita").GetString());
        Assert.Equal("onda-di-luce", j.GetProperty("corrente").GetString());
        Assert.Equal(JsonValueKind.Null, j.GetProperty("motivoPausa").ValueKind);
        Assert.Equal(60000, j.GetProperty("restanteMs").GetInt64());
        Assert.Equal(3840, j.GetProperty("finestra").GetProperty("w").GetInt32());
        Assert.Equal("DISPLAY1", j.GetProperty("monitor")[0].GetProperty("nome").GetString());
        Assert.False(j.GetProperty("monitor")[0].GetProperty("principale").GetBoolean());
        Assert.Equal("estendi", j.GetProperty("modo").GetString());
        Assert.Single(j.GetProperty("scene").EnumerateArray());
    }
}
