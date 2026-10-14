using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class BarraTitoloTests
{
    static readonly MisureBarra M100 = BarraTitolo.Misure(96);

    [Theory]
    [InlineData(96, 32, 46, 8, 16)]
    [InlineData(120, 40, 58, 10, 20)]
    [InlineData(144, 48, 69, 12, 24)]
    public void Misure_SeguonoLaScala(int dpi, int altezza, int pulsante, int bordo, int angolo)
    {
        Assert.Equal(new MisureBarra(altezza, pulsante, bordo, angolo), BarraTitolo.Misure(dpi));
    }

    [Fact]
    public void Pulsanti_AllineatiADestraSenzaSpazi()
    {
        var p = BarraTitolo.Pulsanti(1000, M100);
        Assert.Equal(
            [
                (ZonaBarra.Riduci, new Rettangolo(862, 0, 46, 32)),
                (ZonaBarra.Ingrandisci, new Rettangolo(908, 0, 46, 32)),
                (ZonaBarra.Chiudi, new Rettangolo(954, 0, 46, 32))
            ],
            p);
    }

    [Fact]
    public void Pulsanti_AreaStretta_SiTaglianoASinistra()
    {
        var p = BarraTitolo.Pulsanti(100, M100);
        Assert.Equal(new Rettangolo(-38, 0, 46, 32), p[0].Area);
        Assert.Equal(new Rettangolo(54, 0, 46, 32), p[2].Area);
    }

    [Theory]
    [InlineData(500, 20, ZonaBarra.Trascina)]
    [InlineData(862, 20, ZonaBarra.Riduci)]
    [InlineData(861, 20, ZonaBarra.Trascina)]
    [InlineData(907, 20, ZonaBarra.Riduci)]
    [InlineData(908, 20, ZonaBarra.Ingrandisci)]
    [InlineData(953, 20, ZonaBarra.Ingrandisci)]
    [InlineData(954, 20, ZonaBarra.Chiudi)]
    [InlineData(999, 31, ZonaBarra.Chiudi)]
    [InlineData(500, 32, ZonaBarra.Fuori)]
    [InlineData(500, -1, ZonaBarra.Fuori)]
    [InlineData(-1, 20, ZonaBarra.Fuori)]
    [InlineData(1000, 20, ZonaBarra.Fuori)]
    [InlineData(500, 0, ZonaBarra.BordoAlto)]
    [InlineData(500, 7, ZonaBarra.BordoAlto)]
    [InlineData(500, 8, ZonaBarra.Trascina)]
    [InlineData(960, 3, ZonaBarra.BordoAlto)]
    [InlineData(15, 3, ZonaBarra.AngoloAltoSinistra)]
    [InlineData(16, 3, ZonaBarra.BordoAlto)]
    [InlineData(984, 3, ZonaBarra.AngoloAltoDestra)]
    [InlineData(983, 3, ZonaBarra.BordoAlto)]
    public void Zona_FinestraNormale(int x, int y, ZonaBarra attesa)
    {
        Assert.Equal(attesa, BarraTitolo.Zona(x, y, 1000, M100, ingrandita: false));
    }

    [Theory]
    [InlineData(500, 3, ZonaBarra.Trascina)]
    [InlineData(5, 3, ZonaBarra.Trascina)]
    [InlineData(990, 3, ZonaBarra.Chiudi)]
    [InlineData(870, 0, ZonaBarra.Riduci)]
    public void Zona_FinestraIngrandita_NienteBordo(int x, int y, ZonaBarra attesa)
    {
        Assert.Equal(attesa, BarraTitolo.Zona(x, y, 1000, M100, ingrandita: true));
    }

    [Fact]
    public void Zona_A150PerCento_UsaLeMisureScalate()
    {
        var m = BarraTitolo.Misure(144);
        Assert.Equal(ZonaBarra.Chiudi, BarraTitolo.Zona(1500 - 69, 47, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.Ingrandisci, BarraTitolo.Zona(1500 - 70, 47, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.Fuori, BarraTitolo.Zona(700, 48, 1500, m, ingrandita: false));
        Assert.Equal(ZonaBarra.BordoAlto, BarraTitolo.Zona(700, 11, 1500, m, ingrandita: false));
    }
}
