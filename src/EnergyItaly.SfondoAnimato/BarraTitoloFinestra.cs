using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using EnergyItaly.SfondoAnimato.Core;

namespace EnergyItaly.SfondoAnimato;

/// <summary>
/// Striscia del titolo disegnata dal programma al posto di quella di Windows (spec 2026-10-14, sezione 3).
/// La finestra resta una finestra normale: qui si toglie la barra standard (WM_NCCALCSIZE), si dice a
/// Windows dove stanno pulsanti e area di trascinamento (WM_NCHITTEST) e si disegnano i pulsanti.
/// Così Snap, menu di sistema e doppio clic restano quelli di Windows.
/// </summary>
sealed class BarraTitoloFinestra : IDisposable
{
    public delegate void ProceduraStandard(ref Message m);

    static readonly Color Sfondo = Color.FromArgb(0x05, 0x0d, 0x18);
    static readonly Color Titolo = Color.FromArgb(0xa9, 0xbb, 0xcf);
    static readonly Color Spento = Color.FromArgb(0x5d, 0x75, 0x93);
    static readonly Color Glifo = Color.FromArgb(0xc6, 0xd4, 0xe3);
    static readonly Color Passaggio = Color.FromArgb(0x13, 0x24, 0x3c);
    static readonly Color Premuto = Color.FromArgb(0x1a, 0x2f, 0x4d);
    static readonly Color ChiudiPassaggio = Color.FromArgb(0xc4, 0x2b, 0x1c);
    static readonly Color ChiudiPremuto = Color.FromArgb(0xa1, 0x28, 0x1c);
    static readonly Color BordoWindows10 = Color.FromArgb(0x22, 0x39, 0x5a);
    static readonly bool Windows10 = Environment.OSVersion.Version.Build < 22000;

    readonly Form finestra;
    readonly Icon icona;
    readonly string testo;
    int dpi = 96;
    MisureBarra misure = BarraTitolo.Misure(96);
    Font carattere = new("Segoe UI", 12f, GraphicsUnit.Pixel);
    Icon iconaPiccola;
    ZonaBarra passaggio = ZonaBarra.Fuori;
    ZonaBarra premuto = ZonaBarra.Fuori;
    bool uscitaTracciata;

    public BarraTitoloFinestra(Form finestra, Icon icona, string testo)
    {
        this.finestra = finestra;
        this.icona = icona;
        this.testo = testo;
        iconaPiccola = new Icon(icona, 16, 16);
    }

    public int Altezza => misure.Altezza;

    int Px(int dip) => (int)Math.Round(dip * dpi / 96.0);

    /// <summary>Nuove misure, carattere e icona per una scala diversa (creazione e cambio di monitor).</summary>
    public void ImpostaDpi(int nuovo)
    {
        dpi = nuovo;
        misure = BarraTitolo.Misure(nuovo);
        carattere.Dispose();
        // In pixel e non in punti: un carattere in punti seguirebbe i DPI di sistema, non quelli del monitor.
        carattere = new Font("Segoe UI", 12f * nuovo / 96f, GraphicsUnit.Pixel);
        iconaPiccola.Dispose();
        iconaPiccola = new Icon(icona, Px(16), Px(16));
        Ridisegna();
    }

    public void Ridisegna()
    {
        if (finestra.IsHandleCreated) finestra.Invalidate(new Rectangle(0, 0, finestra.ClientSize.Width, misure.Altezza));
    }

    /// <summary>true se il messaggio è stato gestito qui e non va passato alla procedura standard.</summary>
    public bool Gestisci(ref Message m, ProceduraStandard standard)
    {
        switch (m.Msg)
        {
            case Win32.WM_NCCALCSIZE when m.WParam != IntPtr.Zero:
                CalcolaArea(ref m, standard);
                return true;

            case Win32.WM_NCHITTEST:
            {
                // Prima la cornice standard (bordi laterali e inferiore), poi la striscia.
                standard(ref m);
                if (m.Result != (IntPtr)Win32.HTCLIENT) return true;
                var p = finestra.PointToClient(PuntoSchermo(m.LParam));
                var zona = BarraTitolo.Zona(p.X, p.Y, finestra.ClientSize.Width, misure, Win32.IsZoomed(m.HWnd));
                if (zona != ZonaBarra.Fuori) m.Result = (IntPtr)CodiceHT(zona);
                return true;
            }

            // Sulle zone pulsante niente procedura standard: disegnerebbe i pulsanti classici sopra la
            // striscia e avvierebbe il suo tracciamento modale. Il riquadro Snap dipende solo da HTMAXBUTTON.
            case Win32.WM_NCMOUSEMOVE:
            {
                TracciaUscita();
                var zona = ZonaDaCodice((int)m.WParam);
                CambiaStato(EPulsante(zona) ? zona : ZonaBarra.Fuori, premuto);
                if (!EPulsante(zona)) return false;
                m.Result = IntPtr.Zero;
                return true;
            }

            // Il secondo clic rapido arriva come doppio clic anche senza CS_DBLCLKS: vale come pressione.
            case Win32.WM_NCLBUTTONDOWN:
            case Win32.WM_NCLBUTTONDBLCLK:
            {
                var zona = ZonaDaCodice((int)m.WParam);
                if (!EPulsante(zona)) return false;
                CambiaStato(zona, zona);
                m.Result = IntPtr.Zero;
                return true;
            }

            case Win32.WM_NCLBUTTONUP:
            {
                var zona = ZonaDaCodice((int)m.WParam);
                var eraPremuto = premuto;
                // "Premuto" si azzera a ogni rilascio: senza cattura del mouse un rilascio altrove non arriva.
                CambiaStato(passaggio, ZonaBarra.Fuori);
                if (!EPulsante(zona)) return false;
                if (zona == eraPremuto) Esegui(zona);
                m.Result = IntPtr.Zero;
                return true;
            }

            // Clic destro sulla striscia: menu di sistema. La procedura standard non lo apre, perché per il
            // suo calcolo della cornice qui c'è l'area client e non la barra del titolo.
            case Win32.WM_NCRBUTTONDOWN:
            case Win32.WM_NCRBUTTONUP:
            {
                var codice = (int)m.WParam;
                if (codice != Win32.HTCAPTION && !EPulsante(ZonaDaCodice(codice))) return false;
                if (m.Msg == Win32.WM_NCRBUTTONUP) ApriMenuDiSistema(PuntoSchermo(m.LParam));
                m.Result = IntPtr.Zero;
                return true;
            }

            case Win32.WM_NCMOUSELEAVE:
                uscitaTracciata = false;
                CambiaStato(ZonaBarra.Fuori, ZonaBarra.Fuori);
                return false;
        }
        return false;
    }

    // L'area client comincia dal bordo superiore della finestra: la barra standard sparisce, i bordi
    // laterali e inferiore (e il ridimensionamento da lì) restano quelli calcolati da Windows.
    void CalcolaArea(ref Message m, ProceduraStandard standard)
    {
        int alto = Marshal.PtrToStructure<Win32.NCCALCSIZE_PARAMS>(m.LParam).rgrc0.Top;
        standard(ref m);
        var p = Marshal.PtrToStructure<Win32.NCCALCSIZE_PARAMS>(m.LParam);
        p.rgrc0.Top = alto;
        if (Win32.IsZoomed(m.HWnd))
        {
            // Ingrandita, la finestra sborda dallo schermo dello spessore della cornice: senza questo
            // la striscia finirebbe in parte fuori.
            uint dpiFinestra = Win32.GetDpiForWindow(m.HWnd);
            p.rgrc0.Top += Win32.GetSystemMetricsForDpi(Win32.SM_CYFRAME, dpiFinestra)
                         + Win32.GetSystemMetricsForDpi(Win32.SM_CXPADDEDBORDER, dpiFinestra);
            LasciaSpazioBarraAScomparsa(ref p.rgrc0);
        }
        Marshal.StructureToPtr(p, m.LParam, false);
        m.Result = IntPtr.Zero;
    }

    // Se l'area client coprisse tutto il monitor Windows la tratterebbe come schermo intero e la barra
    // delle applicazioni a scomparsa non ricomparirebbe più (come Windows Terminal: 2 px liberi su quel lato).
    static void LasciaSpazioBarraAScomparsa(ref Win32.RECT area)
    {
        // Il monitor dell'area proposta, non quello attuale della finestra: con Win+Maiusc+freccia
        // la finestra si ingrandisce su un altro monitor prima di esserci arrivata.
        var monitor = Screen.FromRectangle(Rectangle.FromLTRB(area.Left, area.Top, area.Right, area.Bottom)).Bounds;
        bool aScomparsa(uint lato)
        {
            var dati = new Win32.APPBARDATA
            {
                cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(),
                uEdge = lato,
                rc = new Win32.RECT { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Bottom }
            };
            return Win32.SHAppBarMessage(Win32.ABM_GETAUTOHIDEBAREX, ref dati) != IntPtr.Zero;
        }
        if (aScomparsa(Win32.ABE_TOP)) area.Top += 2;
        if (aScomparsa(Win32.ABE_BOTTOM)) area.Bottom -= 2;
        if (aScomparsa(Win32.ABE_LEFT)) area.Left += 2;
        if (aScomparsa(Win32.ABE_RIGHT)) area.Right -= 2;
    }

    void TracciaUscita()
    {
        if (uscitaTracciata) return;
        var dati = new Win32.TRACKMOUSEEVENT
        {
            cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
            dwFlags = Win32.TME_LEAVE | Win32.TME_NONCLIENT,
            hwndTrack = finestra.Handle
        };
        uscitaTracciata = Win32.TrackMouseEvent(ref dati);
    }

    void CambiaStato(ZonaBarra nuovoPassaggio, ZonaBarra nuovoPremuto)
    {
        if (nuovoPassaggio == passaggio && nuovoPremuto == premuto) return;
        passaggio = nuovoPassaggio;
        premuto = nuovoPremuto;
        Ridisegna();
    }

    // Come i pulsanti veri: WM_SYSCOMMAND, così Windows applica animazioni e stato di Snap.
    // SC_CLOSE arriva a OnFormClosing come UserClosing: la finestra si nasconde (D11).
    void Esegui(ZonaBarra zona)
    {
        int comando = zona switch
        {
            ZonaBarra.Riduci => Win32.SC_MINIMIZE,
            ZonaBarra.Ingrandisci => Win32.IsZoomed(finestra.Handle) ? Win32.SC_RESTORE : Win32.SC_MAXIMIZE,
            _ => Win32.SC_CLOSE
        };
        Win32.PostMessage(finestra.Handle, Win32.WM_SYSCOMMAND, (IntPtr)comando, IntPtr.Zero);
    }

    // Come il menu di Windows: voci attive o grigie secondo lo stato della finestra, comando eseguito
    // con WM_SYSCOMMAND come se l'avesse scelto Windows.
    void ApriMenuDiSistema(Point punto)
    {
        var menu = Win32.GetSystemMenu(finestra.Handle, false);
        if (menu == IntPtr.Zero) return;
        bool ingrandita = Win32.IsZoomed(finestra.Handle);
        void voce(int comando, bool attiva) =>
            Win32.EnableMenuItem(menu, (uint)comando, Win32.MF_BYCOMMAND | (attiva ? Win32.MF_ENABLED : Win32.MF_GRAYED));
        voce(Win32.SC_RESTORE, ingrandita);
        voce(Win32.SC_MOVE, !ingrandita);
        voce(Win32.SC_SIZE, !ingrandita);
        voce(Win32.SC_MINIMIZE, true);
        voce(Win32.SC_MAXIMIZE, !ingrandita);
        voce(Win32.SC_CLOSE, true);
        int comandoScelto = Win32.TrackPopupMenu(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON, punto.X, punto.Y, 0, finestra.Handle, IntPtr.Zero);
        if (comandoScelto != 0) Win32.PostMessage(finestra.Handle, Win32.WM_SYSCOMMAND, (IntPtr)comandoScelto, IntPtr.Zero);
    }

    static bool EPulsante(ZonaBarra z) => z is ZonaBarra.Riduci or ZonaBarra.Ingrandisci or ZonaBarra.Chiudi;

    static Point PuntoSchermo(IntPtr lParam)
    {
        // Lette come short: sui monitor a sinistra o sopra il principale le coordinate sono negative.
        long v = lParam.ToInt64();
        return new Point((short)(v & 0xFFFF), (short)((v >> 16) & 0xFFFF));
    }

    static int CodiceHT(ZonaBarra z) => z switch
    {
        ZonaBarra.Riduci => Win32.HTMINBUTTON,
        ZonaBarra.Ingrandisci => Win32.HTMAXBUTTON,
        ZonaBarra.Chiudi => Win32.HTCLOSE,
        ZonaBarra.BordoAlto => Win32.HTTOP,
        ZonaBarra.AngoloAltoSinistra => Win32.HTTOPLEFT,
        ZonaBarra.AngoloAltoDestra => Win32.HTTOPRIGHT,
        _ => Win32.HTCAPTION
    };

    static ZonaBarra ZonaDaCodice(int codice) => codice switch
    {
        Win32.HTMINBUTTON => ZonaBarra.Riduci,
        Win32.HTMAXBUTTON => ZonaBarra.Ingrandisci,
        Win32.HTCLOSE => ZonaBarra.Chiudi,
        _ => ZonaBarra.Fuori
    };

    public void Disegna(Graphics g)
    {
        int larghezza = finestra.ClientSize.Width;
        bool contrasto = SystemInformation.HighContrast;
        bool attiva = Form.ActiveForm == finestra;
        bool ingrandita = Win32.IsZoomed(finestra.Handle);

        using (var fondo = new SolidBrush(contrasto ? SystemColors.ActiveCaption : Sfondo))
            g.FillRectangle(fondo, 0, 0, larghezza, misure.Altezza);
        if (Windows10 && !ingrandita)
            using (var linea = new Pen(BordoWindows10))
                g.DrawLine(linea, 0, 0, larghezza, 0);

        int lato = Px(16);
        int x = Px(12);
        g.DrawIcon(iconaPiccola, new Rectangle(x, (misure.Altezza - lato) / 2, lato, lato));
        int inizioTesto = x + lato + Px(8);
        var areaTesto = new Rectangle(inizioTesto, 0, Math.Max(0, larghezza - 3 * misure.LarghezzaPulsante - inizioTesto - Px(8)), misure.Altezza);
        var coloreTesto = contrasto ? SystemColors.ActiveCaptionText : attiva ? Titolo : Spento;
        TextRenderer.DrawText(g, testo, carattere, areaTesto, coloreTesto,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        foreach (var (zona, area) in BarraTitolo.Pulsanti(larghezza, misure))
        {
            var r = new Rectangle(area.X, area.Y, area.L, area.A);
            bool chiudi = zona == ZonaBarra.Chiudi;
            Color? evidenza = passaggio != zona ? null
                : premuto == zona ? (chiudi ? ChiudiPremuto : Premuto)
                : (chiudi ? ChiudiPassaggio : Passaggio);
            if (contrasto && evidenza is not null) evidenza = SystemColors.Highlight;
            if (evidenza is Color c)
                using (var pennello = new SolidBrush(c))
                    g.FillRectangle(pennello, r);
            var colore = contrasto ? (evidenza is null ? SystemColors.ActiveCaptionText : SystemColors.HighlightText)
                : chiudi && evidenza is not null ? Color.White
                : attiva ? Glifo : Spento;
            DisegnaGlifo(g, zona, r, colore, ingrandita);
        }
    }

    // Glifi a linee come quelli di Windows 11: 10 DIP, spessore 1 DIP.
    void DisegnaGlifo(Graphics g, ZonaBarra zona, Rectangle r, Color colore, bool ingrandita)
    {
        float s = Px(10);
        float x = r.X + (r.Width - s) / 2f;
        float y = r.Y + (r.Height - s) / 2f;
        using var penna = new Pen(colore, Math.Max(1f, dpi / 96f));
        var prima = g.SmoothingMode;
        g.SmoothingMode = zona == ZonaBarra.Chiudi ? SmoothingMode.AntiAlias : SmoothingMode.None;
        switch (zona)
        {
            case ZonaBarra.Riduci:
                g.DrawLine(penna, x, y + s / 2f, x + s, y + s / 2f);
                break;
            case ZonaBarra.Ingrandisci when ingrandita:
                // Ripristina: due quadrati sovrapposti.
                float d = MathF.Round(s * 0.2f);
                g.DrawRectangle(penna, x, y + d, s - d, s - d);
                g.DrawLines(penna, [new PointF(x + d, y + d), new PointF(x + d, y), new PointF(x + s, y), new PointF(x + s, y + s - d), new PointF(x + s - d, y + s - d)]);
                break;
            case ZonaBarra.Ingrandisci:
                g.DrawRectangle(penna, x, y, s, s);
                break;
            default:
                g.DrawLine(penna, x, y, x + s, y + s);
                g.DrawLine(penna, x + s, y, x, y + s);
                break;
        }
        g.SmoothingMode = prima;
    }

    public void Dispose()
    {
        carattere.Dispose();
        iconaPiccola.Dispose();
    }
}
