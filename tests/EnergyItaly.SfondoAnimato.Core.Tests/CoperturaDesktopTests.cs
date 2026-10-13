using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class CoperturaDesktopTests
{
    // Aree di lavoro (senza barra delle applicazioni) del PC di riferimento.
    static readonly Rettangolo Sinistro = new(-1920, 7, 1920, 1040);
    static readonly Rettangolo Destro = new(0, 0, 1920, 1040);

    [Fact]
    public void FinestreIngrandite_SuEntrambiIMonitor_DesktopCoperto()
    {
        Assert.True(CoperturaDesktop.TuttiCoperti([Sinistro, Destro], [Destro, Sinistro]));
    }

    [Fact]
    public void FinestraIngrandita_SoloSuUnMonitor_DesktopVisibile()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([Sinistro, Destro], [Destro]));
    }

    [Fact]
    public void SchermoIntero_CopreAncheLAreaDiLavoro()
    {
        var schermoIntero = new Rettangolo(0, 0, 1920, 1080);
        Assert.True(CoperturaDesktop.TuttiCoperti([Destro], [schermoIntero]));
    }

    [Fact]
    public void FinestraPiuPiccola_NonCopre()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([Destro], [new Rettangolo(100, 100, 800, 600)]));
    }

    [Fact]
    public void ScartoDiDuePixel_Tollerato()
    {
        Assert.True(CoperturaDesktop.Copre(new Rettangolo(2, 2, 1916, 1036), Destro));
        Assert.False(CoperturaDesktop.Copre(new Rettangolo(3, 0, 1917, 1040), Destro));
    }

    [Fact]
    public void NessunMonitor_NonConsideratoCoperto()
    {
        Assert.False(CoperturaDesktop.TuttiCoperti([], [Destro]));
    }
}
