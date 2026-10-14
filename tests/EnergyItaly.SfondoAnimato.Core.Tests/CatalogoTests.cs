using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CatalogoTests
{
    const string Manifest = """
        {
          "versione": 2,
          "tavolozze": [
            { "id": "energy-italy", "titolo": "Energy Italy", "fondo": "#0F1E35", "accento": "#7ab52a", "secondario": "#1e5b8e" },
            { "id": "notte", "titolo": "Notte", "fondo": "#05070d", "accento": "#4a90c8", "secondario": "#2b3a55" },
            { "id": "rotta", "titolo": "Rotta", "fondo": "blu", "accento": "#4a90c8", "secondario": "#2b3a55" }
          ],
          "animazioni": [
            { "id": "onda-di-luce", "titolo": "Onda di luce", "file": "onda-di-luce.html", "opzioni": [
              { "id": "ampiezza", "titolo": "Ampiezza", "tipo": "scelta", "predefinito": "lieve",
                "valori": [ { "valore": "lieve", "titolo": "Lieve" }, { "valore": "forte", "titolo": "Forte" } ] },
              { "id": "durata", "titolo": "Durata dell'onda", "tipo": "numero", "min": 8, "max": 30, "passo": 1, "predefinito": 16, "unita": "s" }
            ] },
            { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "opzioni": [
              { "id": "veli", "titolo": "Veli di luce", "tipo": "interruttore", "predefinito": true }
            ] }
          ]
        }
        """;

    const string ManifestConErrori = """
        { "versione": 2, "animazioni": [
          { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "opzioni": [
            { "id": "veli", "titolo": "Veli di luce", "tipo": "interruttore", "predefinito": true },
            { "id": "veli", "titolo": "Doppia", "tipo": "interruttore", "predefinito": false },
            { "id": "rotta", "titolo": "Rotta", "tipo": "numero", "min": 5, "max": 1, "passo": 1, "predefinito": 2 },
            { "id": "scelta-corta", "titolo": "Corta", "tipo": "scelta", "predefinito": "a", "valori": [ { "valore": "a" } ] },
            { "id": "strana", "titolo": "Strana", "tipo": "colore", "predefinito": 1 }
          ] } ] }
        """;

    static DatiLibreria Libreria(params VoceLibreria[] voci) => new() { Voci = voci.ToList() };

    [Fact]
    public void Carica_Manifest2_VociAziendaliConOpzioni()
    {
        var (voci, avvisi) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Empty(avvisi);
        Assert.Equal(new[] { "onda-di-luce", "aurora" }, voci.Select(v => v.Id));
        var onda = voci[0];
        Assert.Equal(TipoVoce.Animazione, onda.Tipo);
        Assert.True(onda.Aziendale);
        Assert.True(onda.Disponibile);
        Assert.Equal(new[] { "ampiezza", "durata" }, onda.Opzioni.Select(o => o.Id));
        Assert.Equal(TipoOpzione.Scelta, onda.Opzioni[0].Tipo);
        Assert.Equal("lieve", onda.Opzioni[0].Predefinito.Testo);
        Assert.Equal(2, onda.Opzioni[0].Valori!.Count);
        Assert.Equal(16, onda.Opzioni[1].Predefinito.Numero);
        Assert.Equal("s", onda.Opzioni[1].Unita);
        Assert.True(voci[1].Opzioni[0].Predefinito.Booleano);
    }

    [Fact]
    public void Carica_OpzioniNonValide_ScartateConAvviso()
    {
        var (voci, avvisi) = Catalogo.Carica(ManifestConErrori, new DatiLibreria(), _ => true);

        Assert.Equal(new[] { "veli" }, Assert.Single(voci).Opzioni.Select(o => o.Id));
        Assert.Equal(4, avvisi.Count);
        Assert.All(avvisi, a => Assert.Contains("aurora", a));
    }

    [Fact]
    public void LeggiTavolozze_ScartaLeNonValideEMetteInMinuscolo()
    {
        var avvisi = new List<string>();
        var t = Catalogo.LeggiTavolozze(Manifest, avvisi);

        Assert.Equal(new[] { "energy-italy", "notte" }, t.Select(x => x.Id));
        Assert.Equal("#0f1e35", t[0].Fondo);
        Assert.Contains(avvisi, a => a.Contains("rotta"));
    }

    [Fact]
    public void LeggiTavolozze_NessunaValida_EnergyItalyDelCodice()
    {
        var t = Catalogo.LeggiTavolozze("""{ "versione": 2, "animazioni": [] }""", []);
        Assert.Equal(Personalizzazioni.EnergyItaly, Assert.Single(t));
    }

    [Fact]
    public void UrlFile_SoloIlFile()
    {
        var (voci, _) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);
        Assert.Equal("https://sfondo.energyitaly/animazioni/onda-di-luce.html", Catalogo.UrlFile(voci[0]));
    }

    [Fact]
    public void Carica_VociUtente_DopoLeAziendali()
    {
        var lib = Libreria(
            new VoceLibreria { Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare", File = "mare-abc123.jpg" },
            new VoceLibreria { Id = "u-clip-def456", Tipo = "video", Titolo = "Clip", File = "clip-def456.mp4", DurataSecondi = 160 });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.Empty(avvisi);
        Assert.Equal(4, voci.Count);
        Assert.Equal(TipoVoce.Immagine, voci[2].Tipo);
        Assert.False(voci[2].Aziendale);
        Assert.Equal(160, voci[3].DurataSecondi);
        Assert.Equal("https://libreria.energyitaly/clip-def456.mp4", Catalogo.UrlFile(voci[3]));
    }

    [Fact]
    public void UrlFile_CodificaIlNomeFile()
    {
        var v = new Voce("u-x-aaaaaa", TipoVoce.Html, "X", false, "pagina con spazi.html", [], null, true);
        Assert.Equal("https://pagine.energyitaly/pagina%20con%20spazi.html", Catalogo.UrlFile(v));
    }

    [Fact]
    public void Carica_FileMancante_VoceNonDisponibileEAvviso()
    {
        var lib = Libreria(new VoceLibreria { Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare", File = "mare-abc123.jpg" });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, nome => nome != "mare-abc123.jpg");

        Assert.False(voci.Single(v => v.Id == "u-mare-abc123").Disponibile);
        Assert.Contains(avvisi, a => a.Contains("mare-abc123.jpg"));
    }

    [Theory]
    [InlineData("..\\fuori.jpg")]
    [InlineData("cartella/file.jpg")]
    [InlineData("C:x.jpg")]
    [InlineData("")]
    public void Carica_NomeFilePericoloso_VoceIgnorata(string file)
    {
        var lib = Libreria(new VoceLibreria { Id = "u-x-aaaaaa", Tipo = "immagine", Titolo = "X", File = file });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.DoesNotContain(voci, v => v.Id == "u-x-aaaaaa");
        Assert.NotEmpty(avvisi);
    }

    [Fact]
    public void Carica_TipoSconosciutoOIdRipetuto_Ignorati()
    {
        var lib = Libreria(
            new VoceLibreria { Id = "u-a-aaaaaa", Tipo = "audio", Titolo = "A", File = "a.mp3" },
            new VoceLibreria { Id = "aurora", Tipo = "immagine", Titolo = "B", File = "b.jpg" },
            new VoceLibreria { Id = "u-c-cccccc", Tipo = "html", Titolo = "C", File = "c.html" },
            new VoceLibreria { Id = "u-c-cccccc", Tipo = "html", Titolo = "C2", File = "c2.html" });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.Equal(new[] { "onda-di-luce", "aurora", "u-c-cccccc" }, voci.Select(v => v.Id));
        Assert.Equal(3, avvisi.Count);
    }

    [Fact]
    public void Carica_ManifestRotto_NessunaVoceEAvviso()
    {
        var (voci, avvisi) = Catalogo.Carica("{ non è json", new DatiLibreria(), _ => true);

        Assert.Empty(voci);
        Assert.Single(avvisi);
    }

    [Fact]
    public void Libreria_AndataERitorno()
    {
        var dati = Libreria(new VoceLibreria
        {
            Id = "u-mare-abc123", Tipo = "immagine", Titolo = "Mare all'alba", File = "mare-abc123.jpg",
            ImportataIl = new DateTimeOffset(2026, 10, 13, 16, 0, 0, TimeSpan.Zero)
        });

        var json = Catalogo.ScriviLibreria(dati);
        var letta = Catalogo.LeggiLibreria(json, []);

        Assert.Contains("\"voci\"", json);
        var v = Assert.Single(letta.Voci);
        Assert.Equal("Mare all'alba", v.Titolo);
        Assert.Equal(dati.Voci[0].ImportataIl, v.ImportataIl);
    }

    [Fact]
    public void LeggiLibreria_JsonRottoOVersioneDiversa_LibreriaVuotaConAvviso()
    {
        var avvisi = new List<string>();

        Assert.Empty(Catalogo.LeggiLibreria("[1,2", avvisi).Voci);
        Assert.Empty(Catalogo.LeggiLibreria("""{ "versione": 7, "voci": [] }""", avvisi).Voci);
        Assert.Empty(Catalogo.LeggiLibreria(null, avvisi).Voci);
        Assert.Equal(2, avvisi.Count);
    }

    [Fact]
    public void LeggiLibreria_VociDifettose_ScartateLeAltreRestano()
    {
        const string json = """
            { "versione": 1, "voci": [
              { "id": "u-buona-aaaaaa", "tipo": "immagine", "titolo": "Buona", "file": "buona.jpg" },
              { "id": "u-data-bbbbbb", "tipo": "immagine", "titolo": "Data rotta", "file": "data.jpg", "importataIl": "boh" },
              { "id": null, "tipo": "immagine", "titolo": "Senza id", "file": "x.jpg" },
              7
            ], "daCancellare": ["vecchio.mp4", 3] }
            """;
        var avvisi = new List<string>();

        var dati = Catalogo.LeggiLibreria(json, avvisi, out var illeggibile);

        Assert.False(illeggibile);
        Assert.Equal("u-buona-aaaaaa", Assert.Single(dati.Voci).Id);
        Assert.Equal(new[] { "vecchio.mp4" }, dati.DaCancellare);
        Assert.Equal(4, avvisi.Count);
    }

    [Fact]
    public void LeggiLibreria_JsonRotto_SegnalatoComeIlleggibile()
    {
        Catalogo.LeggiLibreria("{ rotto", [], out var illeggibile);
        Assert.True(illeggibile);
    }

    [Fact]
    public void Carica_VoceConIdNullo_NonSolleva()
    {
        var lib = Libreria(new VoceLibreria { Id = null!, Tipo = "immagine", Titolo = "X", File = "x.jpg" });

        var (voci, avvisi) = Catalogo.Carica(Manifest, lib, _ => true);

        Assert.Equal(2, voci.Count);
        Assert.Single(avvisi);
    }

    [Theory]
    [InlineData(TipoVoce.Animazione, "animazione")]
    [InlineData(TipoVoce.Immagine, "immagine")]
    [InlineData(TipoVoce.Video, "video")]
    [InlineData(TipoVoce.Html, "html")]
    public void NomiJson_TipoAndataERitorno(TipoVoce tipo, string testo)
    {
        Assert.Equal(testo, NomiJson.Di(tipo));
        Assert.Equal(tipo, NomiJson.LeggiTipo(testo));
    }

    [Fact]
    public void NomiJson_ValoriSconosciuti_Null()
    {
        Assert.Null(NomiJson.LeggiTipo("Video"));
        Assert.Null(NomiJson.LeggiUnita("secondi"));
        Assert.Null(NomiJson.LeggiOrdine(null));
        Assert.Null(NomiJson.LeggiModo("specchio"));
        Assert.Equal(UnitaTempo.Ore, NomiJson.LeggiUnita("ore"));
        Assert.Equal(Ordine.Casuale, NomiJson.LeggiOrdine("casuale"));
        Assert.Equal(ModoMonitor.Duplica, NomiJson.LeggiModo("duplica"));
    }
}
