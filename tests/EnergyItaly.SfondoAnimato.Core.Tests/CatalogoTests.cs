using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CatalogoTests
{
    const string Manifest = """
        {
          "versione": 1,
          "animazioni": [
            { "id": "onda-di-luce", "titolo": "Onda di luce", "file": "onda-di-luce.html", "parametri": { "ampiezza": "lieve", "durata": 16 } },
            { "id": "aurora", "titolo": "Aurora", "file": "aurora.html", "parametri": { "velocita": 1, "veli": 1 } }
          ]
        }
        """;

    static DatiLibreria Libreria(params VoceLibreria[] voci) => new() { Voci = voci.ToList() };

    [Fact]
    public void Carica_SoloManifest_VociAziendaliConParametriTestuali()
    {
        var (voci, avvisi) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Empty(avvisi);
        Assert.Equal(new[] { "onda-di-luce", "aurora" }, voci.Select(v => v.Id));
        var onda = voci[0];
        Assert.Equal(TipoVoce.Animazione, onda.Tipo);
        Assert.True(onda.Aziendale);
        Assert.True(onda.Disponibile);
        Assert.Equal("lieve", onda.Parametri["ampiezza"]);
        Assert.Equal("16", onda.Parametri["durata"]);
    }

    [Fact]
    public void UrlBase_AnimazioneConParametri()
    {
        var (voci, _) = Catalogo.Carica(Manifest, new DatiLibreria(), _ => true);

        Assert.Equal("https://sfondo.energyitaly/animazioni/onda-di-luce.html?ampiezza=lieve&durata=16", Catalogo.UrlBase(voci[0]));
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
        Assert.Equal("https://libreria.energyitaly/clip-def456.mp4", Catalogo.UrlBase(voci[3]));
    }

    [Fact]
    public void UrlBase_CodificaIlNomeFile()
    {
        var v = new Voce("u-x-aaaaaa", TipoVoce.Html, "X", false, "pagina con spazi.html", new Dictionary<string, string>(), null, true);
        Assert.Equal("https://pagine.energyitaly/pagina%20con%20spazi.html", Catalogo.UrlBase(v));
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
