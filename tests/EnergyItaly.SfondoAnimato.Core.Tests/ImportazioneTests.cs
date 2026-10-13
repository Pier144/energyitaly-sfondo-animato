using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class ImportazioneTests
{
    const long MB = 1024L * 1024;
    static string Fisso() => "k3x9qa";

    [Theory]
    [InlineData("foto.jpg", TipoVoce.Immagine)]
    [InlineData("FOTO.JPEG", TipoVoce.Immagine)]
    [InlineData("a.png", TipoVoce.Immagine)]
    [InlineData("a.webp", TipoVoce.Immagine)]
    [InlineData("a.gif", TipoVoce.Immagine)]
    [InlineData("a.bmp", TipoVoce.Immagine)]
    [InlineData("clip.mp4", TipoVoce.Video)]
    [InlineData("clip.WebM", TipoVoce.Video)]
    [InlineData("pagina.html", TipoVoce.Html)]
    [InlineData("pagina.htm", TipoVoce.Html)]
    public void Valuta_TipiRiconosciuti(string nome, TipoVoce atteso)
    {
        var e = Importazione.Valuta(nome, 1000, Fisso);
        Assert.True(e.Valido);
        Assert.Equal(atteso, e.Tipo);
    }

    [Theory]
    [InlineData("programma.exe")]
    [InlineData("senza-estensione")]
    [InlineData("film.mkv")]
    public void Valuta_FormatoNonSupportato(string nome)
    {
        var e = Importazione.Valuta(nome, 1000, Fisso);
        Assert.False(e.Valido);
        Assert.StartsWith("Formato non supportato", e.Motivo);
    }

    [Fact]
    public void Valuta_NomiEIdPuliti()
    {
        var e = Importazione.Valuta(@"C:\Utenti\Mario\Montagne all'alba (1).JPG", 1000, Fisso);

        Assert.Equal("Montagne all'alba (1)", e.Titolo);
        Assert.Equal("u-montagne-all-alba-1-k3x9qa", e.Id);
        Assert.Equal("montagne-all-alba-1-k3x9qa.jpg", e.NomeFile);
    }

    [Fact]
    public void Valuta_LimitiEsatti()
    {
        Assert.True(Importazione.Valuta("a.jpg", 50 * MB, Fisso).Valido);
        Assert.False(Importazione.Valuta("a.jpg", 50 * MB + 1, Fisso).Valido);
        Assert.True(Importazione.Valuta("a.mp4", 2048 * MB, Fisso).Valido);
        Assert.False(Importazione.Valuta("a.mp4", 2048 * MB + 1, Fisso).Valido);
        var html = Importazione.Valuta("a.html", 5 * MB + 1, Fisso);
        Assert.False(html.Valido);
        Assert.Equal("File troppo grande: il limite per le pagine HTML è 5 MB", html.Motivo);
    }

    [Fact]
    public void Valuta_FileVuoto()
    {
        var e = Importazione.Valuta("a.png", 0, Fisso);
        Assert.False(e.Valido);
        Assert.Equal("Il file è vuoto", e.Motivo);
    }

    [Theory]
    [InlineData("Città è bella", "citta-e-bella")]
    [InlineData("###", "sfondo")]
    [InlineData("  spazi   multipli  ", "spazi-multipli")]
    [InlineData("Ünïcödé_ÀÉÎ", "unicode-aei")]
    public void Slug_Casi(string testo, string atteso)
    {
        Assert.Equal(atteso, Importazione.Slug(testo));
    }

    [Fact]
    public void Slug_LungoTroncatoA40SenzaTrattinoFinale()
    {
        var s = Importazione.Slug(new string('a', 39) + " bbbbbb");
        Assert.True(s.Length <= 40);
        Assert.False(s.EndsWith('-'));
    }

    [Fact]
    public void Valuta_SoloEstensione_TitoloSfondo()
    {
        var e = Importazione.Valuta(".png", 10, Fisso);
        Assert.True(e.Valido);
        Assert.Equal("Sfondo", e.Titolo);
        Assert.Equal("sfondo-k3x9qa.png", e.NomeFile);
    }

    [Fact]
    public void SuffissoCasuale_SeiCaratteriMinuscoliOCifre()
    {
        var s = Importazione.SuffissoCasuale();
        Assert.Equal(6, s.Length);
        Assert.All(s, c => Assert.True(c is >= 'a' and <= 'z' or >= '0' and <= '9'));
    }
}
