using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class LayoutMonitorTests
{
    // PC di riferimento: principale (DISPLAY2) a destra in (0,0), secondario (DISPLAY1) a sinistra sfalsato di 7 px.
    static readonly Monitor Principale = new("DISPLAY2", new Rettangolo(0, 0, 1920, 1080), true);
    static readonly Monitor Secondario = new("DISPLAY1", new Rettangolo(-1920, 7, 1920, 1080), false);

    [Fact]
    public void Estendi_PcDiRiferimento_FinestraSulloSchermoVirtuale()
    {
        var d = LayoutMonitor.Calcola([Principale, Secondario], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(-1920, 0, 3840, 1087), d.Finestra);
        Assert.Equal(ModoMonitor.Estendi, d.ModoEffettivo);
        Assert.Equal(new[] { "DISPLAY1", "DISPLAY2" }, d.Monitor.Select(m => m.Nome));
        Assert.Equal(new Rettangolo(0, 7, 1920, 1080), d.Monitor[0].Area);
        Assert.Equal(new Rettangolo(1920, 0, 1920, 1080), d.Monitor[1].Area);
        Assert.True(d.Monitor[1].Principale);
        var scena = Assert.Single(d.Scene);
        Assert.Equal(new Rettangolo(0, 0, 3840, 1087), scena.Area);
        Assert.Equal("0,7,1920,1080;1920,0,1920,1080", scena.ParametroMonitor());
    }

    [Fact]
    public void Duplica_PcDiRiferimento_UnaScenaPerMonitor()
    {
        var d = LayoutMonitor.Calcola([Principale, Secondario], ModoMonitor.Duplica);

        Assert.Equal(ModoMonitor.Duplica, d.ModoEffettivo);
        Assert.Equal(2, d.Scene.Count);
        Assert.Equal(new Rettangolo(0, 7, 1920, 1080), d.Scene[0].Area);
        Assert.Equal(new Rettangolo(1920, 0, 1920, 1080), d.Scene[1].Area);
        Assert.Equal("0,0,1920,1080", d.Scene[0].ParametroMonitor());
        Assert.Equal("0,0,1920,1080", d.Scene[1].ParametroMonitor());
    }

    [Fact]
    public void Duplica_ConUnSoloMonitor_DiventaEstendi()
    {
        var d = LayoutMonitor.Calcola([Principale], ModoMonitor.Duplica);

        Assert.Equal(ModoMonitor.Estendi, d.ModoEffettivo);
        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
        Assert.Equal("0,0,1920,1080", Assert.Single(d.Scene).ParametroMonitor());
    }

    [Fact]
    public void NessunMonitor_UsaUnMonitorDiRiserva()
    {
        var d = LayoutMonitor.Calcola([], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
        Assert.Single(d.Monitor);
    }

    [Fact]
    public void TreMonitorDiversiConUnoVerticale()
    {
        var verticale = new Monitor("DISPLAY3", new Rettangolo(-1080, -300, 1080, 1920), false);
        var grande = new Monitor("DISPLAY1", new Rettangolo(0, 0, 2560, 1440), true);
        var destro = new Monitor("DISPLAY2", new Rettangolo(2560, 200, 1920, 1080), false);

        var d = LayoutMonitor.Calcola([grande, destro, verticale], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(-1080, -300, 5560, 1920), d.Finestra);
        Assert.Equal("0,0,1080,1920;1080,300,2560,1440;3640,500,1920,1080", Assert.Single(d.Scene).ParametroMonitor());
    }

    [Fact]
    public void MonitorConAreaVuota_Ignorato()
    {
        var vuoto = new Monitor("DISPLAY9", new Rettangolo(5000, 0, 0, 0), false);

        var d = LayoutMonitor.Calcola([Principale, vuoto], ModoMonitor.Estendi);

        Assert.Equal(new Rettangolo(0, 0, 1920, 1080), d.Finestra);
    }

    [Fact]
    public void Rettangolo_DestraEBasso()
    {
        var r = new Rettangolo(-10, 5, 30, 40);
        Assert.Equal(20, r.Destra);
        Assert.Equal(45, r.Basso);
    }
}
