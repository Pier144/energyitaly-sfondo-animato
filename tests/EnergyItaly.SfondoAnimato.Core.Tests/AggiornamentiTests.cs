using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class AggiornamentiTests
{
    const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    static readonly DateTimeOffset Avvio = new(2026, 10, 14, 9, 0, 0, TimeSpan.FromHours(2));

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("V10.0.1", "10.0.1")]
    [InlineData("1.0.0+6d27f24a1be31360dc1db4a54dddb6ffd77dcf5d", "1.0.0")]
    [InlineData(" 1.1.0 ", "1.1.0")]
    public void VersioneDa_Valida(string testo, string attesa)
    {
        Assert.Equal(Version.Parse(attesa), Aggiornamenti.VersioneDa(testo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2.3-beta")]
    [InlineData("uno.due.tre")]
    [InlineData("v")]
    [InlineData("999999.0.0")]
    public void VersioneDa_NonValida(string? testo)
    {
        Assert.Null(Aggiornamenti.VersioneDa(testo));
    }

    [Fact]
    public void Leggi_FileValido()
    {
        var info = Aggiornamenti.Leggi($$"""{ "versione": "1.1.0", "file": "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", "sha256": "{{Sha.ToUpperInvariant()}}" }""");

        Assert.Equal(new InfoVersione(new Version(1, 1, 0), "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", Sha), info);
    }

    [Theory]
    [InlineData("{ rotto")]
    [InlineData("[]")]
    [InlineData("""{ "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1", "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "v1.1.0", "file": "a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "..\\a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "cartella/a.exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.msi", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": ".exe", "sha256": "SHA" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe", "sha256": "abc" }""")]
    [InlineData("""{ "versione": "1.1.0", "file": "a.exe", "sha256": "zz23456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef" }""")]
    [InlineData("""{ "versione": 110, "file": "a.exe", "sha256": "SHA" }""")]
    public void Leggi_NonValido(string json)
    {
        Assert.Null(Aggiornamenti.Leggi(json.Replace("SHA", Sha)));
    }

    [Fact]
    public void PiuNuova_ConfrontaLeVersioni()
    {
        var info = new InfoVersione(new Version(1, 1, 0), "a.exe", Sha);
        Assert.True(Aggiornamenti.PiuNuova(info, new Version(1, 0, 0)));
        Assert.True(Aggiornamenti.PiuNuova(info, new Version(1, 0, 9)));
        Assert.False(Aggiornamenti.PiuNuova(info, new Version(1, 1, 0)));
        Assert.False(Aggiornamenti.PiuNuova(info, new Version(1, 2, 0)));
        Assert.False(Aggiornamenti.PiuNuova(null, new Version(1, 0, 0)));
    }

    [Fact]
    public void Indirizzi_DelleRelease()
    {
        Assert.Equal("https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", Aggiornamenti.IndirizzoInfo.ToString());
        var info = new InfoVersione(new Version(1, 1, 0), "EnergyItaly-SfondoAnimato-1.1.0-setup.exe", Sha);
        Assert.Equal("https://github.com/Pier144/energyitaly-sfondo-animato/releases/download/v1.1.0/EnergyItaly-SfondoAnimato-1.1.0-setup.exe",
            Aggiornamenti.IndirizzoInstaller(info).ToString());
    }

    [Theory]
    [InlineData("https://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", true)]
    [InlineData("https://github.com/pier144/Energyitaly-Sfondo-Animato/releases/download/v1.1.0/a.exe", true)]
    [InlineData("https://objects.githubusercontent.com/github-production-release-asset-2e65be/123/abc?X=1", true)]
    [InlineData("https://release-assets.githubusercontent.com/github-production-release-asset/123/abc", true)]
    [InlineData("http://github.com/Pier144/energyitaly-sfondo-animato/releases/latest/download/versione.json", false)]
    [InlineData("https://github.com/altro/progetto/releases/download/v1.1.0/a.exe", false)]
    [InlineData("https://github.com/Pier144/energyitaly-sfondo-animato/archive/main.zip", false)]
    [InlineData("https://githubusercontent.com.esempio.it/a.exe", false)]
    [InlineData("https://raw.githubusercontent.com/Pier144/energyitaly-sfondo-animato/main/a.exe", false)]
    public void IndirizzoAffidabile(string indirizzo, bool atteso)
    {
        Assert.Equal(atteso, Aggiornamenti.IndirizzoAffidabile(new Uri(indirizzo)));
    }

    [Fact]
    public void RitardoPrimoControllo_TraDueETrentaMinuti()
    {
        var caso = new Random(3);
        for (int i = 0; i < 1000; i++)
        {
            var r = Aggiornamenti.RitardoPrimoControllo(caso);
            Assert.InRange(r, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(30));
        }
    }

    [Fact]
    public void ProssimoControllo_IlPrimoPossibileSenzaControlliPrecedenti()
    {
        var primo = Avvio.AddMinutes(7);
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, null));
    }

    [Fact]
    public void ProssimoControllo_SeiOreDopoLUltimo()
    {
        Assert.Equal(Avvio.AddHours(5), Aggiornamenti.ProssimoControllo(Avvio.AddMinutes(7), Avvio.AddHours(-1)));
    }

    [Fact]
    public void ProssimoControllo_MaiPrimaDelPrimoPossibile()
    {
        var primo = Avvio.AddMinutes(7);
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, Avvio.AddHours(-7)));
        Assert.Equal(primo, Aggiornamenti.ProssimoControllo(primo, Avvio.AddHours(-6).AddMinutes(1)));
    }

    [Theory]
    [InlineData(true, false, false, false, false, true)]
    [InlineData(false, true, false, false, false, true)]
    [InlineData(false, false, true, false, false, true)]
    [InlineData(false, false, false, true, false, true)]
    [InlineData(false, false, false, false, false, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(false, false, true, false, true, false)]
    public void MomentoBuono(bool coperto, bool schermoIntero, bool sessione, bool monitorSpenti, bool finestraVisibile, bool atteso)
    {
        var s = new SegnaliSistema(DesktopCoperto: coperto, SchermoIntero: schermoIntero, SessioneNonAttiva: sessione, MonitorSpenti: monitorSpenti);
        Assert.Equal(atteso, Aggiornamenti.MomentoBuono(s, finestraVisibile));
    }

    [Fact]
    public void MomentoBuono_BatteriaEPausaManualeNonBastano()
    {
        Assert.False(Aggiornamenti.MomentoBuono(new SegnaliSistema(ABatteria: true), finestraVisibile: false));
    }

    [Fact]
    public void Stato_ScrittoERiletto()
    {
        var stato = new StatoAggiornamenti(Avvio, new InfoVersione(new Version(1, 2, 0), "a.exe", Sha), 1);

        Assert.Equal(stato, Aggiornamenti.LeggiStato(Aggiornamenti.ScriviStato(stato)));
        var vuoto = new StatoAggiornamenti(null, null, 0);
        Assert.Equal(vuoto, Aggiornamenti.LeggiStato(Aggiornamenti.ScriviStato(vuoto)));
    }

    [Fact]
    public void Stato_TentativiSenzaAggiornamentoProntoValgonoZero()
    {
        Assert.Equal(0, Aggiornamenti.LeggiStato("""{ "ultimoControllo": null, "pronta": null, "tentativi": 3 }""").Tentativi);
        Assert.Equal(0, Aggiornamenti.LeggiStato($$"""{ "pronta": { "versione": "1.2.0", "file": "a.exe", "sha256": "{{Sha}}" }, "tentativi": -1 }""").Tentativi);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{ rotto")]
    [InlineData("[1]")]
    [InlineData("""{ "ultimoControllo": 5, "pronta": { "versione": "x" } }""")]
    public void Stato_IlleggibileDiventaVuoto(string? json)
    {
        Assert.Equal(new StatoAggiornamenti(null, null, 0), Aggiornamenti.LeggiStato(json));
    }

    [Fact]
    public void ScriviInfo_StessoFormatoDiVersioneJson()
    {
        var info = new InfoVersione(new Version(1, 1, 0), "a.exe", Sha);
        Assert.Equal(info, Aggiornamenti.Leggi(Aggiornamenti.ScriviInfo(info)));
    }

    [Fact]
    public void MessaggioVista_CampiPerLaFinestra()
    {
        var json = Aggiornamenti.MessaggioVista(new Version(1, 1, 0), "pronto", Avvio, new Version(1, 2, 0), null);

        var r = JsonDocument.Parse(json).RootElement;
        Assert.Equal("aggiornamenti", r.GetProperty("tipo").GetString());
        Assert.Equal("1.1.0", r.GetProperty("versione").GetString());
        Assert.Equal("pronto", r.GetProperty("stato").GetString());
        Assert.Equal(Avvio, r.GetProperty("ultimoControllo").GetDateTimeOffset());
        Assert.Equal("1.2.0", r.GetProperty("versioneNuova").GetString());
        Assert.Equal(JsonValueKind.Null, r.GetProperty("errore").ValueKind);
    }
}
