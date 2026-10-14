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
}
