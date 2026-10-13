using EnergyItaly.SfondoAnimato.Core;
using Monitor = EnergyItaly.SfondoAnimato.Core.Monitor;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Monitor collegati in pixel fisici (il processo è PerMonitorV2).</summary>
static class LettoreMonitor
{
    public static IReadOnlyList<Monitor> Leggi() =>
        Screen.AllScreens
            .Select(s => new Monitor(
                s.DeviceName.Replace(@"\\.\", "", StringComparison.Ordinal),
                new Rettangolo(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height),
                s.Primary))
            .ToList();
}
