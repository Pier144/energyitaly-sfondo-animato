using System.Text.Json;
using EnergyItaly.SfondoAnimato.Core;
using Microsoft.Extensions.Time.Testing;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CoordinatoreTests
{
    const string Manifest = """
        { "versione": 1, "animazioni": [
          { "id": "a", "titolo": "A", "file": "a.html", "parametri": {} },
          { "id": "b", "titolo": "B", "file": "b.html", "parametri": {} },
          { "id": "c", "titolo": "C", "file": "c.html", "parametri": {} } ] }
        """;

    static readonly Monitor Principale = new("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true);
    static readonly Monitor Secondario = new("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false);

    readonly FakeTimeProvider tempo = new(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
    readonly PlayerFalso player = new();
    readonly AppFalsa app = new();
    readonly ArchivioFalso archivio = new();
    readonly SistemaFalso sistema = new();
    readonly LogFalso log = new();

    Coordinatore Avvia(string? predefinite = null, params Monitor[] monitor)
    {
        var c = new Coordinatore(Manifest, predefinite, player, app, archivio, sistema, log, tempo, new Random(7));
        c.Avvia(monitor.Length == 0 ? [Principale, Secondario] : monitor);
        c.DaPlayer("""{"tipo":"pronto"}""");
        return c;
    }

    static string Comando(object o) => JsonSerializer.Serialize(o);

    [Fact]
    public void Avvio_MostraLaPrimaQuandoIlPlayerEPronto()
    {
        var c = Avvia();

        var m = player.Ultimo("mostra");
        Assert.Equal("a", m.GetProperty("id").GetString());
        Assert.Equal(0, m.GetProperty("dissolvenzaMs").GetInt32());
        Assert.Equal(new Rettangolo(-1920, 0, 3840, 1087), player.Area);
        Assert.Contains("\"ultimaVoce\": \"a\"", archivio.Impostazioni);
        Assert.True(sistema.AvvioConWindows);
        Assert.True(sistema.AggiornamentiAutomatici);
        Assert.Equal(("A", false), sistema.Icona);
    }

    [Fact]
    public void AggiornamentiAutomatici_DallaFinestraArrivanoAlSistemaEAlloStato()
    {
        var c = Avvia();

        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { aggiornamentiAutomatici = false } }));
        Assert.False(sistema.AggiornamentiAutomatici);
        Assert.False(app.UltimoStato.GetProperty("impostazioni").GetProperty("aggiornamentiAutomatici").GetBoolean());
        Assert.Contains("\"aggiornamentiAutomatici\": false", archivio.Impostazioni);

        c.DaApp(Comando(new { tipo = "ripristina" }));
        Assert.True(sistema.AggiornamentiAutomatici);
    }

    [Fact]
    public void Avvio_LogDeiMonitorInCoordinateDelloSchermo()
    {
        Avvia();
        Assert.Contains(log.Righe, r => r.Contains("DISPLAY1 1920x1080 in (-1920,7), DISPLAY2 1920x1080 in (0,0) principale"));
    }

    [Fact]
    public void Avvio_RiprendeDopoLUltimaVoce()
    {
        archivio.Impostazioni = """{ "versione": 2, "ultimaVoce": "b" }""";
        var c = Avvia();
        Assert.Equal("c", c.Corrente?.Id);
    }

    [Fact]
    public void Avvio_ImpostazioniRotte_LogEValoriPredefiniti()
    {
        archivio.Impostazioni = "{ rotto";
        var c = Avvia();

        Assert.Equal("a", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.StartsWith("AVVISO impostazioni.json"));
    }

    [Fact]
    public void Tick_DopoLIntervallo_PassaAllaSuccessivaConDissolvenza()
    {
        var c = Avvia();
        tempo.Advance(TimeSpan.FromMinutes(29));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        tempo.Advance(TimeSpan.FromMinutes(1));
        c.Tick();

        Assert.Equal("b", c.Corrente?.Id);
        Assert.Equal(2000, player.Ultimo("mostra").GetProperty("dissolvenzaMs").GetInt32());
        Assert.Equal("b", app.UltimoStato.GetProperty("corrente").GetString());
    }

    [Fact]
    public void PausaAutomatica_BloccaIlCambioERiprendeSubitoSeScaduto()
    {
        var c = Avvia();
        c.AggiornaSegnali(new SegnaliSistema(SessioneNonAttiva: true));

        Assert.Equal("pausa", player.UltimoTipo);
        Assert.True(player.Sospeso);
        Assert.Equal("SESSIONE BLOCCATA", app.UltimoStato.GetProperty("motivoPausa").GetString());

        tempo.Advance(TimeSpan.FromMinutes(31));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        c.AggiornaSegnali(new SegnaliSistema());

        Assert.False(player.Sospeso);
        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains(player.Messaggi, m => m.Contains("\"riprendi\""));
    }

    [Fact]
    public void PausaManuale_FermaIlConteggio()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "pausa" }));

        Assert.Equal("pausa", player.UltimoTipo);
        Assert.True(app.UltimoStato.GetProperty("pausaManuale").GetBoolean());
        Assert.False(app.UltimoStato.GetProperty("contoAttivo").GetBoolean());
        Assert.Equal(("A", true), sistema.Icona);

        tempo.Advance(TimeSpan.FromHours(2));
        c.Tick();
        Assert.Equal("a", c.Corrente?.Id);

        c.DaApp(Comando(new { tipo = "riprendi" }));
        Assert.Equal("riprendi", player.UltimoTipo);
        tempo.Advance(TimeSpan.FromMinutes(30));
        c.Tick();
        Assert.Equal("b", c.Corrente?.Id);
    }

    [Fact]
    public void Successivo_EPrecedente()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "successivo" }));
        c.DaApp(Comando(new { tipo = "successivo" }));
        Assert.Equal("c", c.Corrente?.Id);

        c.DaApp(Comando(new { tipo = "precedente" }));
        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains("\"ultimaVoce\": \"b\"", archivio.Impostazioni);
    }

    [Fact]
    public void Impostazioni_ValoreNonValido_AvvisoESenzaSalvare()
    {
        var c = Avvia();
        var prima = archivio.Impostazioni;

        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 0 } }));

        Assert.Contains("Scrivi un numero intero da 1 a 1440 minuti", app.Avvisi);
        Assert.Equal(prima, archivio.Impostazioni);
        Assert.Equal(30, c.Impostazioni.IntervalloValore);
    }

    [Fact]
    public void Impostazioni_DueOre_CambiaPermanenzaESalva()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 2, intervalloUnita = "ore" } }));

        Assert.Equal(7_200_000, app.UltimoStato.GetProperty("totaleMs").GetInt64());
        Assert.Contains("\"unita\": \"ore\"", archivio.Impostazioni);
    }

    [Fact]
    public void Mai_ConVideo_IlPlayerLoMetteInLoop()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4", "durataSecondi": 60 } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        archivio.Impostazioni = """{ "versione": 2, "rotazioneAttiva": false, "playlist": ["u-v-aaaaaa"] }""";
        var c = Avvia();

        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
        Assert.False(player.Ultimo("mostra").GetProperty("fineVideo").GetBoolean());
        c.DaPlayer("""{"tipo":"videoFinito","id":"u-v-aaaaaa"}""");
        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
    }

    [Fact]
    public void Playlist_Vuota_Rifiutata()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = Array.Empty<string>() }));

        Assert.Contains("Lascia almeno uno sfondo nella playlist", app.Avvisi);
        Assert.Equal(new[] { "a", "b", "c" }, c.Impostazioni.Playlist);
    }

    [Fact]
    public void Playlist_Riordinata_DecideLaSuccessiva()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = new[] { "a", "c", "b", "sconosciuto" } }));
        c.Successivo();

        Assert.Equal(new[] { "a", "c", "b" }, c.Impostazioni.Playlist);
        Assert.Equal("c", c.Corrente?.Id);
    }

    [Fact]
    public void ErrorePlayer_SullaCorrente_PassaAllaSuccessiva()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"a","messaggio":"timeout"}""");

        Assert.Equal("b", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.StartsWith("ERRORE") && r.Contains("timeout"));
    }

    [Fact]
    public void ErroriDiFila_SuTutteLeVoci_SiFerma()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"a","messaggio":"x"}""");
        c.DaPlayer("""{"tipo":"errore","id":"b","messaggio":"x"}""");
        c.DaPlayer("""{"tipo":"errore","id":"c","messaggio":"x"}""");

        Assert.Equal("c", c.Corrente?.Id);
        Assert.Contains(log.Righe, r => r.Contains("Nessuno sfondo della playlist si carica"));
    }

    [Fact]
    public void ErroreDiUnaVoceVecchia_Ignorato()
    {
        var c = Avvia();
        c.DaPlayer("""{"tipo":"errore","id":"c","messaggio":"x"}""");
        Assert.Equal("a", c.Corrente?.Id);
    }

    [Fact]
    public async Task Import_FileValido_AggiuntoAllaLibreriaNonAllaPlaylist()
    {
        var c = Avvia();
        archivio.FileDaImportare[@"C:\Foto\Foto mare.JPG"] = 1000;

        await c.ImportaFileAsync([@"C:\Foto\Foto mare.JPG"]);

        var (origine, nome) = Assert.Single(archivio.Copie);
        Assert.Equal(@"C:\Foto\Foto mare.JPG", origine);
        Assert.StartsWith("foto-mare-", nome);
        Assert.EndsWith(".jpg", nome);
        Assert.Contains("Foto mare", archivio.Libreria);
        Assert.Contains("Aggiunto alla libreria: Foto mare", app.Avvisi);
        Assert.Equal(4, app.UltimoStato.GetProperty("voci").GetArrayLength());
        Assert.Equal(new[] { "a", "b", "c" }, c.Impostazioni.Playlist);
    }

    [Fact]
    public async Task Import_FormatoNonSupportatoEFileIllegibile_Avvisi()
    {
        var c = Avvia();
        archivio.FileDaImportare[@"C:\x\virus.exe"] = 10;

        await c.ImportaFileAsync([@"C:\x\virus.exe", @"C:\x\sparito.png"]);

        Assert.Empty(archivio.Copie);
        var avviso = Assert.Single(app.Avvisi);
        Assert.StartsWith("2 file non importati", avviso);
    }

    [Fact]
    public void Elimina_VoceImportataInOnda_CancellaFileEPassaAvanti()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-m-aaaaaa", "tipo": "immagine", "titolo": "M", "file": "m.jpg" } ] }""";
        archivio.FileLibreria.Add("m.jpg");
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "playlist", ids = new[] { "a", "u-m-aaaaaa" } }));
        c.DaApp(Comando(new { tipo = "mostra", id = "u-m-aaaaaa" }));

        c.DaApp(Comando(new { tipo = "elimina", id = "u-m-aaaaaa" }));

        Assert.Equal(new[] { "m.jpg" }, archivio.Cancellati);
        Assert.Equal("a", c.Corrente?.Id);
        Assert.Equal(new[] { "a" }, c.Impostazioni.Playlist);
        Assert.DoesNotContain("u-m-aaaaaa", archivio.Libreria);
        Assert.Contains("Eliminato dalla libreria: M", app.Avvisi);
    }

    [Fact]
    public async Task Import_FileGrande_AvvisaERestaReattivoDuranteLaCopia()
    {
        var c = Avvia();
        archivio.FileDaImportare[@"C:\Video\Clip.mp4"] = 300L * 1024 * 1024;
        archivio.CopiaInCorso = new TaskCompletionSource();

        var importazione = c.ImportaFileAsync([@"C:\Video\Clip.mp4"]);

        Assert.False(importazione.IsCompleted);
        Assert.Contains("Importazione in corso: 1 file", app.Avvisi);
        c.DaApp(Comando(new { tipo = "successivo" }));
        Assert.Equal("b", c.Corrente?.Id);

        archivio.CopiaInCorso.SetResult();
        await importazione;

        Assert.Contains("Aggiunto alla libreria: Clip", app.Avvisi);
        Assert.Equal(4, app.UltimoStato.GetProperty("voci").GetArrayLength());
    }

    [Fact]
    public void Elimina_FileInUso_RiprovaAlProssimoAvvio()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-m-aaaaaa", "tipo": "video", "titolo": "M", "file": "m.mp4" } ] }""";
        archivio.FileLibreria.Add("m.mp4");
        archivio.FileBloccati.Add("m.mp4");
        var c = Avvia();

        c.DaApp(Comando(new { tipo = "elimina", id = "u-m-aaaaaa" }));

        Assert.Empty(archivio.Cancellati);
        Assert.Contains("m.mp4", archivio.Libreria);
        Assert.DoesNotContain("u-m-aaaaaa", archivio.Libreria);

        archivio.FileBloccati.Clear();
        var nuovo = new Coordinatore(Manifest, null, player, app, archivio, sistema, log, tempo);
        nuovo.Avvia([Principale]);

        Assert.Equal(new[] { "m.mp4" }, archivio.Cancellati);
        Assert.DoesNotContain("m.mp4", archivio.Libreria);
    }

    [Fact]
    public void Avvio_LibreriaIlleggibile_ConservaUnaCopiaPrimaDiSovrascriverla()
    {
        archivio.Libreria = "{ rotto";
        Avvia();

        Assert.Equal(1, archivio.CopieDiSicurezza);
        Assert.Contains(log.Righe, r => r.StartsWith("ERRORE") && r.Contains("libreria.json"));
    }

    [Fact]
    public void Elimina_VoceAziendale_Rifiutata()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "elimina", id = "a" }));

        Assert.Empty(archivio.Cancellati);
        Assert.Contains("Si possono eliminare solo gli sfondi importati", app.Avvisi);
    }

    [Fact]
    public void VideoFinito_ConFineVideo_Avanza()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4", "durataSecondi": 60 } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        archivio.Impostazioni = """{ "versione": 2, "playlist": ["a", "u-v-aaaaaa"], "ultimaVoce": "a" }""";
        var c = Avvia();
        Assert.Equal("u-v-aaaaaa", c.Corrente?.Id);
        Assert.True(player.Ultimo("mostra").GetProperty("fineVideo").GetBoolean());

        c.DaPlayer("""{"tipo":"videoFinito","id":"u-v-aaaaaa"}""");

        Assert.Equal("a", c.Corrente?.Id);
    }

    [Fact]
    public void DurataVideo_SalvataInLibreria()
    {
        archivio.Libreria = """{ "versione": 1, "voci": [ { "id": "u-v-aaaaaa", "tipo": "video", "titolo": "V", "file": "v.mp4" } ] }""";
        archivio.FileLibreria.Add("v.mp4");
        var c = Avvia();

        c.DaPlayer("""{"tipo":"durataVideo","id":"u-v-aaaaaa","secondi":159.6}""");

        Assert.Contains("\"durataSecondi\": 160", archivio.Libreria);
    }

    [Fact]
    public void ModoDuplica_RimandaLoSfondoConUnaScenaPerMonitor()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { modoMonitor = "duplica" } }));

        var m = player.Ultimo("mostra");
        Assert.Equal("duplica", m.GetProperty("modo").GetString());
        Assert.Equal(2, m.GetProperty("scene").GetArrayLength());
        Assert.Equal("duplica", app.UltimoStato.GetProperty("modo").GetString());
    }

    [Fact]
    public void MonitorScollegato_RidimensionaEMandaIlLayout()
    {
        var c = Avvia();
        c.AggiornaMonitor([Principale]);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), player.Area);
        Assert.Equal("layout", player.UltimoTipo);
        Assert.Equal(1, app.UltimoStato.GetProperty("monitor").GetArrayLength());
    }

    [Fact]
    public void MonitorIdentici_NessunMessaggio()
    {
        var c = Avvia();
        var quanti = player.Messaggi.Count;
        c.AggiornaMonitor([Secondario, Principale]);
        Assert.Equal(quanti, player.Messaggi.Count);
    }

    [Fact]
    public void Ripristina_TornaAllePredefiniteAziendali()
    {
        const string aziendali = """{ "versione": 2, "intervallo": { "valore": 15, "unita": "minuti" } }""";
        var c = Avvia(aziendali);
        c.DaApp(Comando(new { tipo = "impostazioni", modifiche = new { intervalloValore = 3, intervalloUnita = "ore", ordine = "casuale" } }));

        c.DaApp(Comando(new { tipo = "ripristina" }));

        Assert.Equal(15, c.Impostazioni.IntervalloValore);
        Assert.Equal(Ordine.Sequenziale, c.Impostazioni.Ordine);
        Assert.Contains("Impostazioni aziendali ripristinate", app.Avvisi);
    }

    [Fact]
    public void ComandiVari()
    {
        var c = Avvia();
        c.DaApp(Comando(new { tipo = "apriLog" }));
        c.DaApp(Comando(new { tipo = "pronta" }));
        c.DaApp("non json");

        Assert.Equal(1, sistema.CartelleLogAperte);
        Assert.Contains(log.Righe, r => r.StartsWith("AVVISO") && r.Contains("non riconosciuto"));
    }

    [Fact]
    public void PlayerRicreato_AspettaPronto()
    {
        var c = Avvia();
        c.PlayerRicreato();
        var quanti = player.DiTipo("mostra").Count();

        c.Successivo();
        Assert.Equal(quanti, player.DiTipo("mostra").Count());

        c.DaPlayer("""{"tipo":"pronto"}""");
        Assert.Equal("b", player.Ultimo("mostra").GetProperty("id").GetString());
    }
}
