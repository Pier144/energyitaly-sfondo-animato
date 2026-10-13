using System.Runtime.InteropServices;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Finestra nascosta di primo livello: riceve TaskbarCreated (Explorer riavviato, RF-04),
/// lo stato dei monitor (RF-33) e del risparmio energia (RF-34).
/// </summary>
sealed class FinestraMessaggi : NativeWindow, IDisposable
{
    static readonly int WmTaskbarCreated = (int)Win32.RegisterWindowMessage("TaskbarCreated");
    readonly List<IntPtr> registrazioni = [];

    public event Action? ExplorerRiavviato;
    public event Action<bool>? MonitorAccesi;
    public event Action<bool>? RisparmioEnergetico;

    /// <summary>Windows o un installer (Restart Manager) chiedono di chiudere il programma.</summary>
    public event Action? ChiusuraRichiesta;

    public FinestraMessaggi()
    {
        CreateHandle(new CreateParams { Caption = "EnergyItaly.SfondoAnimato.Messaggi", ExStyle = Win32.WS_EX_TOOLWINDOW });
        foreach (var impostazione in new[] { Win32.GUID_CONSOLE_DISPLAY_STATE, Win32.GUID_POWER_SAVING_STATUS })
        {
            var guid = impostazione;
            var h = Win32.RegisterPowerSettingNotification(Handle, ref guid, 0);
            if (h != IntPtr.Zero) registrazioni.Add(h);
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmTaskbarCreated)
        {
            ExplorerRiavviato?.Invoke();
        }
        else if (m.Msg == Win32.WM_QUERYENDSESSION)
        {
            // Si accetta sempre: lo stato è già salvato su disco a ogni cambiamento.
            m.Result = (IntPtr)1;
            return;
        }
        else if (m.Msg == Win32.WM_ENDSESSION && m.WParam != IntPtr.Zero)
        {
            ChiusuraRichiesta?.Invoke();
        }
        else if (m.Msg == Win32.WM_POWERBROADCAST && m.WParam.ToInt64() == Win32.PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
        {
            var dati = Marshal.PtrToStructure<Win32.POWERBROADCAST_SETTING>(m.LParam);
            if (dati.PowerSetting == Win32.GUID_CONSOLE_DISPLAY_STATE) MonitorAccesi?.Invoke(dati.Data != 0);
            else if (dati.PowerSetting == Win32.GUID_POWER_SAVING_STATUS) RisparmioEnergetico?.Invoke(dati.Data != 0);
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        foreach (var h in registrazioni) Win32.UnregisterPowerSettingNotification(h);
        registrazioni.Clear();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
