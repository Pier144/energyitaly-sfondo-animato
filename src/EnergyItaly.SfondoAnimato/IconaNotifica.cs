using System.Drawing;

namespace EnergyItaly.SfondoAnimato;

/// <summary>Icona e menu nell'area di notifica (RF-40, RF-41).</summary>
sealed class IconaNotifica : IDisposable
{
    const string NomeProgramma = "Energy Italy Sfondo Animato";
    readonly NotifyIcon icona;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem vocePausa;

    public event Action? Apri;
    public event Action? Successivo;
    public event Action? AlternaPausa;
    public event Action? ApriLog;
    public event Action? Esci;

    public IconaNotifica(Icon immagine)
    {
        menu = new ContextMenuStrip();
        var voceApri = new ToolStripMenuItem("Apri " + NomeProgramma, null, (_, _) => Apri?.Invoke());
        voceApri.Font = new Font(menu.Font, FontStyle.Bold);
        vocePausa = new ToolStripMenuItem("Pausa", null, (_, _) => AlternaPausa?.Invoke());
        menu.Items.AddRange(new ToolStripItem[]
        {
            voceApri,
            new ToolStripMenuItem("Sfondo successivo", null, (_, _) => Successivo?.Invoke()),
            vocePausa,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Apri cartella log", null, (_, _) => ApriLog?.Invoke()),
            new ToolStripMenuItem("Esci", null, (_, _) => Esci?.Invoke())
        });
        icona = new NotifyIcon
        {
            Icon = new Icon(immagine, SystemInformation.SmallIconSize),
            Text = NomeProgramma,
            ContextMenuStrip = menu,
            Visible = true
        };
        icona.MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) Apri?.Invoke(); };
    }

    public void Aggiorna(string titolo, bool inPausa)
    {
        var testo = $"{NomeProgramma}\n{titolo}{(inPausa ? " (in pausa)" : "")}";
        icona.Text = testo.Length > 127 ? testo[..124] + "..." : testo;
        vocePausa.Text = inPausa ? "Riprendi" : "Pausa";
    }

    public void Dispose()
    {
        icona.Visible = false;
        icona.Dispose();
        menu.Dispose();
    }
}
