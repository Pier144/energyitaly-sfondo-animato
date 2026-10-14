using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Extensions.Time.Testing;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class MotoreRotazioneTests
{
    static readonly IReadOnlyList<Opzione> Nessuno = [];
    static Voce Anim(string id) => new(id, TipoVoce.Animazione, id.ToUpperInvariant(), true, id + ".html", Nessuno, null, true);
    static Voce Video(string id, int? durata) => new(id, TipoVoce.Video, id, false, id + ".mp4", Nessuno, durata, true);

    readonly FakeTimeProvider tempo = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));

    MotoreRotazione Motore(IReadOnlyList<Voce> voci, IReadOnlyList<string> playlist, TimeSpan? intervallo = null, bool fineVideo = true, Ordine ordine = Ordine.Sequenziale, int seme = 1)
    {
        var m = new MotoreRotazione(tempo, new Random(seme));
        m.AggiornaVoci(voci, playlist);
        m.AggiornaRegole(new RegoleRotazione(intervallo ?? TimeSpan.FromMinutes(30), fineVideo, ordine), riavvia: true);
        return m;
    }

    static readonly Voce[] Tre = [Anim("a"), Anim("b"), Anim("c")];

    [Fact]
    public void Avvia_SenzaUltima_PrimaDellaPlaylist()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);

        Assert.Equal("a", m.Corrente?.Id);
        Assert.Equal(TimeSpan.FromMinutes(30), m.Totale);
        Assert.True(m.StoricoVuoto);
    }

    [Theory]
    [InlineData("a", "b")]
    [InlineData("c", "a")]
    [InlineData("sparita", "a")]
    public void Avvia_DopoLUltimaMostrata(string ultima, string attesa)
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(ultima);
        Assert.Equal(attesa, m.Corrente?.Id);
    }

    [Fact]
    public void Prossima_CorrenteFuoriPlaylist_PrimaDellaPlaylist()
    {
        var m = Motore(Tre, ["b", "c"]);
        m.Avvia(null);
        m.Mostra("a");

        Assert.Equal("b", m.Prossima());
    }

    [Fact]
    public void Casuale_MaiLaStessaDueVolteDiFila()
    {
        var m = Motore(Tre, ["a", "b", "c"], ordine: Ordine.Casuale, seme: 42);
        m.Avvia(null);
        for (int i = 0; i < 200; i++)
        {
            var precedente = m.Corrente!.Id;
            var prossima = m.Prossima()!;
            Assert.NotEqual(precedente, prossima);
            m.Mostra(prossima);
        }
    }

    [Fact]
    public void Casuale_UnaSolaVoce_RestituisceQuella()
    {
        var m = Motore(Tre, ["b"], ordine: Ordine.Casuale);
        m.Avvia(null);
        Assert.Equal("b", m.Prossima());
    }

    [Fact]
    public void Scaduta_SoloDopoLIntervallo()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);

        tempo.Advance(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(59));
        Assert.False(m.Scaduta);
        Assert.Equal(TimeSpan.FromSeconds(1), m.Restante);
        tempo.Advance(TimeSpan.FromSeconds(1));
        Assert.True(m.Scaduta);
        Assert.Equal(TimeSpan.Zero, m.Restante);
    }

    [Fact]
    public void Mai_NessunaScadenza()
    {
        var m = new MotoreRotazione(tempo);
        m.AggiornaVoci(Tre, ["a", "b"]);
        m.AggiornaRegole(new RegoleRotazione(null, true, Ordine.Sequenziale), riavvia: true);
        m.Avvia(null);

        tempo.Advance(TimeSpan.FromDays(3));
        Assert.Null(m.Totale);
        Assert.Null(m.Restante);
        Assert.False(m.Scaduta);
    }

    [Fact]
    public void Video_ConDurataEFineVideo_RestaPerTuttoIlVideoPiuMargine()
    {
        var m = Motore([Anim("a"), Video("v", 160)], ["a", "v"], intervallo: TimeSpan.FromMinutes(1));
        m.Avvia("a");

        Assert.Equal("v", m.Corrente?.Id);
        Assert.Equal(TimeSpan.FromSeconds(160), m.Totale);
        Assert.True(m.AttendeFineVideo);
        Assert.True(m.DeveAvanzareDopoVideo("v"));
        tempo.Advance(TimeSpan.FromSeconds(169));
        Assert.False(m.Scaduta);
        tempo.Advance(TimeSpan.FromSeconds(1));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void Video_SenzaDurata_UsaLIntervallo()
    {
        var m = Motore([Video("v", null)], ["v"], intervallo: TimeSpan.FromMinutes(5));
        m.Avvia(null);

        Assert.Equal(TimeSpan.FromMinutes(5), m.Totale);
        Assert.False(m.AttendeFineVideo);
        Assert.True(m.DeveAvanzareDopoVideo("v"));
    }

    [Fact]
    public void Video_ConFineVideoSpenta_UsaLIntervallo()
    {
        var m = Motore([Video("v", 160)], ["v"], intervallo: TimeSpan.FromMinutes(5), fineVideo: false);
        m.Avvia(null);

        Assert.Equal(TimeSpan.FromMinutes(5), m.Totale);
        Assert.False(m.DeveAvanzareDopoVideo("v"));
    }

    [Fact]
    public void PausaManuale_FermaIlConteggio()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(10));

        m.PausaManuale();
        tempo.Advance(TimeSpan.FromHours(1));
        Assert.True(m.InPausaManuale);
        Assert.False(m.Scaduta);
        Assert.Equal(TimeSpan.FromMinutes(20), m.Restante);

        m.RiprendiManuale();
        tempo.Advance(TimeSpan.FromMinutes(19));
        Assert.False(m.Scaduta);
        tempo.Advance(TimeSpan.FromMinutes(1));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void PausaAutomatica_IlTempoContinuaAScorrere()
    {
        // Le pause automatiche non toccano il motore: alla ripresa il cambio risulta già scaduto (RF-23).
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(45));
        Assert.True(m.Scaduta);
    }

    [Fact]
    public void Storico_TornaIndietroNellOrdineMostrato()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        m.Mostra("c");
        m.Mostra("b");

        Assert.True(m.TornaIndietro());
        Assert.Equal("c", m.Corrente?.Id);
        Assert.True(m.TornaIndietro());
        Assert.Equal("a", m.Corrente?.Id);
        Assert.True(m.StoricoVuoto);
        Assert.True(m.TornaIndietro());
        Assert.Equal("c", m.Corrente?.Id);
    }

    [Fact]
    public void Storico_AlMassimoVentiVoci()
    {
        var voci = Enumerable.Range(0, 30).Select(i => Anim("v" + i)).ToList();
        var m = Motore(voci, voci.Select(v => v.Id).ToList());
        m.Avvia(null);
        foreach (var v in voci.Skip(1)) m.Mostra(v.Id);

        int passi = 0;
        while (!m.StoricoVuoto) { m.TornaIndietro(); passi++; }
        Assert.Equal(MotoreRotazione.MassimoStorico, passi);
    }

    [Fact]
    public void Mostra_VoceNonDisponibileOSconosciuta_Falso()
    {
        var rotta = Anim("x") with { Disponibile = false };
        var m = Motore([.. Tre, rotta], ["a", "b"]);
        m.Avvia(null);

        Assert.False(m.Mostra("x"));
        Assert.False(m.Mostra("sconosciuta"));
        Assert.Equal("a", m.Corrente?.Id);
    }

    [Fact]
    public void AggiornaVoci_EscludeNonDisponibili()
    {
        var m = Motore([Anim("a"), Anim("b") with { Disponibile = false }, Anim("c")], ["a", "b", "c"]);
        m.Avvia(null);

        Assert.Equal(new[] { "a", "c" }, m.Playlist.Select(v => v.Id));
        Assert.Equal("c", m.Prossima());
    }

    [Fact]
    public void AggiornaVoci_DurataNuovaDelVideoCorrente_AggiornaIlTotaleSenzaRipartire()
    {
        var m = Motore([Video("v", null)], ["v"], intervallo: TimeSpan.FromMinutes(5));
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromSeconds(30));

        m.AggiornaVoci([Video("v", 90)], ["v"]);

        Assert.Equal(TimeSpan.FromSeconds(90), m.Totale);
        Assert.Equal(TimeSpan.FromSeconds(60), m.Restante);
    }

    [Fact]
    public void AggiornaRegole_ConRiavvio_RipartIlConteggio()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(20));

        m.AggiornaRegole(new RegoleRotazione(TimeSpan.FromHours(2), true, Ordine.Sequenziale), riavvia: true);

        Assert.Equal(TimeSpan.FromHours(2), m.Restante);
    }

    [Fact]
    public void AggiornaRegole_SoloOrdine_NonRiparte()
    {
        var m = Motore(Tre, ["a", "b", "c"]);
        m.Avvia(null);
        tempo.Advance(TimeSpan.FromMinutes(20));

        m.AggiornaRegole(new RegoleRotazione(TimeSpan.FromMinutes(30), true, Ordine.Casuale), riavvia: false);

        Assert.Equal(TimeSpan.FromMinutes(10), m.Restante);
    }

    [Fact]
    public void PlaylistVuota_NessunaProssima()
    {
        var m = Motore(Tre, []);
        m.Avvia("b");

        Assert.Equal("b", m.Corrente?.Id);
        Assert.Null(m.Prossima());
    }
}
