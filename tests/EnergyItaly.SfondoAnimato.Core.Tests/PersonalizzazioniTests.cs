using System.Globalization;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class PersonalizzazioniTests
{
    [Fact]
    public void PerUrl_CulturaInvarianteEInterruttori()
    {
        var prima = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try
        {
            Assert.Equal("1.5", ValoreOpzione.DiNumero(1.5).PerUrl());
            Assert.Equal("2", ValoreOpzione.DiNumero(2).PerUrl());
            Assert.Equal("1", ValoreOpzione.DiBooleano(true).PerUrl());
            Assert.Equal("0", ValoreOpzione.DiBooleano(false).PerUrl());
            Assert.Equal("media", ValoreOpzione.DiTesto("media").PerUrl());
        }
        finally { CultureInfo.CurrentCulture = prima; }
    }

    [Fact]
    public void Riempimento_NomiJson()
    {
        Assert.Equal("adatta", NomiJson.Di(Riempimento.Adatta));
        Assert.Equal(Riempimento.Riempi, NomiJson.LeggiRiempimento("riempi"));
        Assert.Null(NomiJson.LeggiRiempimento("allunga"));
        Assert.Equal("interruttore", NomiJson.Di(TipoOpzione.Interruttore));
        Assert.Equal(TipoOpzione.Scelta, NomiJson.LeggiTipoOpzione("scelta"));
    }

    [Fact]
    public void Personalizzazione_Vuota()
    {
        Assert.True(new Personalizzazione().Vuota);
        Assert.False(new Personalizzazione { ModoMonitor = ModoMonitor.Duplica }.Vuota);
    }

    static readonly Opzione Velocita = new("velocita", "Velocità", TipoOpzione.Numero, ValoreOpzione.DiNumero(1), 0.5, 2, 0.25, "×");
    static readonly Opzione Ampiezza = new("ampiezza", "Ampiezza", TipoOpzione.Scelta, ValoreOpzione.DiTesto("lieve"),
        Valori: [new("lieve", "Lieve"), new("forte", "Forte")]);
    static readonly Opzione Veli = new("veli", "Veli di luce", TipoOpzione.Interruttore, ValoreOpzione.DiBooleano(true));
    static readonly Voce Anim = new("segnale", TipoVoce.Animazione, "Segnale", true, "segnale.html", [Velocita, Ampiezza, Veli], null, true);
    static readonly Voce Foto = new("u-foto-aaaaaa", TipoVoce.Immagine, "Foto", false, "foto.jpg", [], null, true);
    static readonly Voce Pagina = new("u-pag-bbbbbb", TipoVoce.Html, "Pagina", false, "p.html", [], null, true);
    static readonly Tavolozza Notte = new("notte", "Notte", "#05070d", "#4a90c8", "#2b3a55");
    static readonly IReadOnlyList<Tavolozza> Tav = [Personalizzazioni.EnergyItaly, Notte];

    static Dictionary<string, ValoreOpzione> Op(params (string Id, ValoreOpzione V)[] x) => x.ToDictionary(p => p.Id, p => p.V);

    [Fact]
    public void Normalizza_NumeroAlLimiteEAlPasso()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1.6))) }, Anim, Tav, avvisi);
        Assert.Equal(1.5, p.Opzioni["velocita"].Numero);
        Assert.Single(avvisi);

        var altri = new List<string>();
        p = Personalizzazioni.Normalizza(new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(9))) }, Anim, Tav, altri);
        Assert.Equal(2, p.Opzioni["velocita"].Numero);
        Assert.Contains(altri, a => a.Contains("velocita"));
    }

    [Fact]
    public void Normalizza_ValoriUgualiAlPredefinitoETavolozzaPredefinitaTolti()
    {
        var p = Personalizzazioni.Normalizza(new()
        {
            Tavolozza = "energy-italy",
            Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1)), ("veli", ValoreOpzione.DiBooleano(true))),
            Riempimento = Riempimento.Riempi
        }, Anim, Tav, []);
        Assert.True(p.Vuota);
    }

    [Fact]
    public void Normalizza_OpzioniSconosciuteTipoSbagliatoSceltaNonAmmessaTolte()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new()
        {
            Opzioni = Op(("sparita", ValoreOpzione.DiNumero(3)), ("velocita", ValoreOpzione.DiTesto("tanta")),
                ("ampiezza", ValoreOpzione.DiTesto("media")))
        }, Anim, Tav, avvisi);
        Assert.Empty(p.Opzioni);
        Assert.Equal(3, avvisi.Count);
    }

    [Fact]
    public void Normalizza_PersonalizzataSenzaColori_TornaAllaPredefinita()
    {
        var avvisi = new List<string>();
        var p = Personalizzazioni.Normalizza(new() { Tavolozza = "personalizzata", Fondo = "#102030" }, Anim, Tav, avvisi);
        Assert.True(p.Vuota);
        Assert.NotEmpty(avvisi);
    }

    [Fact]
    public void Normalizza_ColoriFuoriDaPersonalizzataTolti_ColoriInMinuscolo()
    {
        var p = Personalizzazioni.Normalizza(new() { Tavolozza = "notte", Fondo = "#102030" }, Anim, Tav, []);
        Assert.Equal("notte", p.Tavolozza);
        Assert.Null(p.Fondo);

        p = Personalizzazioni.Normalizza(new() { Tavolozza = "personalizzata", Fondo = "#A0B0C0", Accento = "#E0A040" }, Anim, Tav, []);
        Assert.Equal("#a0b0c0", p.Fondo);
        Assert.Equal("#e0a040", p.Accento);
    }

    [Fact]
    public void Normalizza_CampiNonAmmessiPerTipoDiVoce()
    {
        var avvisi = new List<string>();
        var foto = Personalizzazioni.Normalizza(new() { Tavolozza = "notte", Riempimento = Riempimento.Adatta }, Foto, Tav, avvisi);
        Assert.Null(foto.Tavolozza);
        Assert.Equal(Riempimento.Adatta, foto.Riempimento);

        var pagina = Personalizzazioni.Normalizza(new() { Riempimento = Riempimento.Adatta, ModoMonitor = ModoMonitor.Duplica }, Pagina, Tav, avvisi);
        Assert.Null(pagina.Riempimento);
        Assert.Equal(ModoMonitor.Duplica, pagina.ModoMonitor);
    }

    [Fact]
    public void Applica_ErroriPerCampiNonValidi()
    {
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "notte"), Foto, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Riempimento: "adatta"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "sconosciuta"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Tavolozza: "personalizzata", Fondo: "blu"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("nessuna", ValoreOpzione.DiNumero(1)))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("veli", ValoreOpzione.DiNumero(1)))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Opzioni: Op(("ampiezza", ValoreOpzione.DiTesto("media")))), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(ModoMonitor: "ruota"), Anim, Tav).Errore);
        Assert.NotNull(Personalizzazioni.Applica(null, new(Fondo: "#102030"), Anim, Tav).Errore);
    }

    [Fact]
    public void Applica_UnisceENormalizza()
    {
        var (p, e) = Personalizzazioni.Applica(null, new(Opzioni: Op(("velocita", ValoreOpzione.DiNumero(1.4))), ModoMonitor: "duplica"), Anim, Tav);
        Assert.Null(e);
        Assert.Equal(1.5, p.Opzioni["velocita"].Numero);
        Assert.Equal(ModoMonitor.Duplica, p.ModoMonitor);

        (p, e) = Personalizzazioni.Applica(p, new(Opzioni: Op(("veli", ValoreOpzione.DiBooleano(false))), ModoMonitor: "generale"), Anim, Tav);
        Assert.Null(e);
        Assert.Equal(2, p.Opzioni.Count);
        Assert.Null(p.ModoMonitor);
    }

    [Fact]
    public void Applica_PassaggioAPersonalizzataPartendoDallaTavolozzaScelta()
    {
        var (p, _) = Personalizzazioni.Applica(new() { Tavolozza = "notte" }, new(Tavolozza: "personalizzata"), Anim, Tav);
        Assert.Equal("personalizzata", p.Tavolozza);
        Assert.Equal("#05070d", p.Fondo);
        Assert.Equal("#4a90c8", p.Accento);

        (p, _) = Personalizzazioni.Applica(p, new(Accento: "#E0A040"), Anim, Tav);
        Assert.Equal("#e0a040", p.Accento);

        (p, _) = Personalizzazioni.Applica(p, new(Tavolozza: "notte"), Anim, Tav);
        Assert.Equal("notte", p.Tavolozza);
        Assert.Null(p.Fondo);
    }

    [Fact]
    public void Riconcilia_TogliSfondiSconosciutiEVociVuote()
    {
        var avvisi = new List<string>();
        var dati = new Dictionary<string, Personalizzazione>
        {
            ["segnale"] = new() { Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1))), ModoMonitor = ModoMonitor.Estendi },
            ["aurora"] = new() { Tavolozza = "notte" },
            ["u-foto-aaaaaa"] = new() { Tavolozza = "notte" }
        };
        var r = Personalizzazioni.Riconcilia(dati, [Anim, Foto], Tav, avvisi);

        Assert.Equal(new[] { "segnale" }, r.Keys);
        Assert.Empty(r["segnale"].Opzioni);
        Assert.Contains(avvisi, a => a.Contains("aurora"));
    }

    [Fact]
    public void Url_TavolozzaEOpzioniEffettive()
    {
        var p = new Personalizzazione { Tavolozza = "notte", Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1.5)), ("veli", ValoreOpzione.DiBooleano(false))) };
        Assert.Equal("https://sfondo.energyitaly/animazioni/segnale.html?fondo=05070d&accento=4a90c8&secondario=2b3a55&velocita=1.5&ampiezza=lieve&veli=0",
            Personalizzazioni.Url(Anim, p, Tav));
        Assert.Equal("https://sfondo.energyitaly/animazioni/segnale.html?fondo=0f1e35&accento=7ab52a&secondario=1e5b8e&velocita=1&ampiezza=lieve&veli=1",
            Personalizzazioni.Url(Anim, null, Tav));
    }

    [Fact]
    public void Url_PersonalizzataSenzaSecondario_AltriTipiSenzaParametri()
    {
        var p = new Personalizzazione { Tavolozza = "personalizzata", Fondo = "#102030", Accento = "#e0a040" };
        Assert.StartsWith("https://sfondo.energyitaly/animazioni/segnale.html?fondo=102030&accento=e0a040&velocita=",
            Personalizzazioni.Url(Anim, p, Tav));
        Assert.Equal("https://libreria.energyitaly/foto.jpg",
            Personalizzazioni.Url(Foto, new() { Riempimento = Riempimento.Adatta }, Tav));
    }

    [Fact]
    public void ModoERiempimentoEffettivi()
    {
        Assert.Equal(ModoMonitor.Estendi, Personalizzazioni.ModoEffettivo(null, ModoMonitor.Estendi));
        Assert.Equal(ModoMonitor.Duplica, Personalizzazioni.ModoEffettivo(new() { ModoMonitor = ModoMonitor.Duplica }, ModoMonitor.Estendi));
        Assert.Equal(Riempimento.Riempi, Personalizzazioni.RiempimentoEffettivo(null));
        Assert.Equal(Riempimento.Adatta, Personalizzazioni.RiempimentoEffettivo(new() { Riempimento = Riempimento.Adatta }));
    }

    [Fact]
    public void Scrivi_ValoriConIlLoroTipoECulturaInvariante()
    {
        var prima = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("it-IT");
        try
        {
            using var flusso = new MemoryStream();
            using (var w = new System.Text.Json.Utf8JsonWriter(flusso))
            {
                Personalizzazioni.Scrivi(w, new Personalizzazione
                {
                    Tavolozza = "notte",
                    Opzioni = Op(("velocita", ValoreOpzione.DiNumero(1.5)), ("veli", ValoreOpzione.DiBooleano(false))),
                    ModoMonitor = ModoMonitor.Duplica
                });
            }
            Assert.Equal("""{"tavolozza":"notte","opzioni":{"velocita":1.5,"veli":false},"modoMonitor":"duplica"}""",
                System.Text.Encoding.UTF8.GetString(flusso.ToArray()));
        }
        finally { CultureInfo.CurrentCulture = prima; }
    }
}
