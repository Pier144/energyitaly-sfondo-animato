using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato.Core.Tests;

public class RegolePausaTests
{
    [Fact]
    public void NessunSegnale_NessunaPausa()
    {
        Assert.Equal(DecisionePausa.Nessuna, RegolePausa.Valuta(new SegnaliSistema(), true, false));
    }

    [Theory]
    [InlineData(true, false, false, false, MotivoPausa.DesktopCoperto)]
    [InlineData(false, true, false, false, MotivoPausa.SchermoIntero)]
    [InlineData(false, false, true, false, MotivoPausa.SessioneNonAttiva)]
    [InlineData(false, false, false, true, MotivoPausa.MonitorSpenti)]
    public void SegnaliGravi_PausaProfonda(bool coperto, bool schermoIntero, bool sessione, bool spenti, MotivoPausa motivo)
    {
        var d = RegolePausa.Valuta(new SegnaliSistema(coperto, schermoIntero, sessione, spenti), true, false);

        Assert.Equal(LivelloPausa.Profonda, d.Livello);
        Assert.Equal(motivo, d.Motivo);
    }

    [Fact]
    public void ABatteria_LeggeraSoloSeAttivaNelleImpostazioni()
    {
        var s = new SegnaliSistema(ABatteria: true);

        Assert.Equal(new DecisionePausa(LivelloPausa.Leggera, MotivoPausa.ABatteria), RegolePausa.Valuta(s, true, false));
        Assert.Equal(DecisionePausa.Nessuna, RegolePausa.Valuta(s, false, false));
    }

    [Fact]
    public void Manuale_Leggera()
    {
        Assert.Equal(new DecisionePausa(LivelloPausa.Leggera, MotivoPausa.Manuale), RegolePausa.Valuta(new SegnaliSistema(), true, true));
    }

    [Fact]
    public void PiuMotivi_VinceIlPiuGrave()
    {
        var tutti = new SegnaliSistema(true, true, true, true, true);
        Assert.Equal(MotivoPausa.SessioneNonAttiva, RegolePausa.Valuta(tutti, true, true).Motivo);
        Assert.Equal(MotivoPausa.MonitorSpenti, RegolePausa.Valuta(tutti with { SessioneNonAttiva = false }, true, true).Motivo);
        Assert.Equal(MotivoPausa.Manuale, RegolePausa.Valuta(new SegnaliSistema(ABatteria: true), true, true).Motivo);
    }

    [Theory]
    [InlineData(MotivoPausa.SessioneNonAttiva, "SESSIONE BLOCCATA")]
    [InlineData(MotivoPausa.MonitorSpenti, "MONITOR SPENTI")]
    [InlineData(MotivoPausa.SchermoIntero, "APP A SCHERMO INTERO")]
    [InlineData(MotivoPausa.DesktopCoperto, "DESKTOP COPERTO")]
    [InlineData(MotivoPausa.ABatteria, "A BATTERIA")]
    [InlineData(MotivoPausa.Manuale, "")]
    public void Testo_Motivi(MotivoPausa m, string atteso)
    {
        Assert.Equal(atteso, RegolePausa.Testo(m));
    }
}
