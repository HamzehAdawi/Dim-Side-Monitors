using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

class DimmerApp : Form
{
    private static Form[] overlays;
    private static bool dimmed = false;
    private static double opacity = 0.5;

    private NotifyIcon trayIcon;
    private ToolStripMenuItem startupItem;

    const int MOD_CONTROL = 0x2;

    const int HK_TOGGLE = 1;
    const int HK_DIM1 = 2;
    const int HK_DIM2 = 3;
    const int HK_DIM3 = 4;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public DimmerApp()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        Load += (s, e) => Hide();

        RegisterHotKey(Handle, HK_TOGGLE, MOD_CONTROL, (int)Keys.D0);
        RegisterHotKey(Handle, HK_DIM1, MOD_CONTROL, (int)Keys.D1);
        RegisterHotKey(Handle, HK_DIM2, MOD_CONTROL, (int)Keys.D2);
        RegisterHotKey(Handle, HK_DIM3, MOD_CONTROL, (int)Keys.D3);

         trayIcon = new NotifyIcon()
        {
            Icon = new Icon("dimmerIcon.ico"),
            Visible = true,
            Text = "Monitor Dimmer"
        };

        var menu = new ContextMenuStrip();

        startupItem = new ToolStripMenuItem("Start with Windows");
        startupItem.Checked = IsStartupEnabled();
        startupItem.Click += (s, e) => ToggleStartup();

        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("Exit", null, (s, e) => Application.Exit());

        trayIcon.ContextMenuStrip = menu;

        trayIcon.DoubleClick += (s, e) => ToggleDim();
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_HOTKEY = 0x0312;

        if (m.Msg == WM_HOTKEY)
        {
            switch (m.WParam.ToInt32())
            {
                case HK_TOGGLE:
                    ToggleDim();
                    break;
                case HK_DIM1:
                    SetDimLevel(0.3);
                    break;
                case HK_DIM2:
                    SetDimLevel(0.6);
                    break;
                case HK_DIM3:
                    SetDimLevel(0.85);
                    break;
            }
        }

        base.WndProc(ref m);
    }

    private void ToggleDim()
    {
        dimmed = !dimmed;

        if (dimmed)
            CreateOverlays();
        else
            RemoveOverlays();
    }

    private void SetDimLevel(double level)
    {
        opacity = level;

        if (dimmed)
        {
            RemoveOverlays();
            CreateOverlays();
        }
        else
        {
            dimmed = true;
            CreateOverlays();
        }
    }

    private void CreateOverlays()
    {
        var screens = Screen.AllScreens;
        overlays = new Form[screens.Length];

        for (int i = 0; i < screens.Length; i++)
        {
            if (screens[i].Primary)
                continue;

            Form overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = screens[i].Bounds,
                BackColor = Color.Black,
                Opacity = opacity,
                TopMost = true,
                ShowInTaskbar = false
            };

            overlay.Show();
            overlays[i] = overlay;
        }
    }

    private void RemoveOverlays()
    {
        if (overlays == null) return;

        foreach (var overlay in overlays)
            overlay?.Close();

        overlays = null;
    }


    private void ToggleStartup()
    {
        if (IsStartupEnabled())
            DisableStartup();
        else
            EnableStartup();

        startupItem.Checked = IsStartupEnabled();
    }

    private bool IsStartupEnabled()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
        {
            return key.GetValue("MonitorDimmer") != null;
        }
    }

    private void EnableStartup()
    {
        string exePath = Application.ExecutablePath;

        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
        {
            key.SetValue("MonitorDimmer", exePath);
        }
    }

    private void DisableStartup()
    {
        using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
        {
            key.DeleteValue("MonitorDimmer", false);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnregisterHotKey(Handle, HK_TOGGLE);
        UnregisterHotKey(Handle, HK_DIM1);
        UnregisterHotKey(Handle, HK_DIM2);
        UnregisterHotKey(Handle, HK_DIM3);

        trayIcon.Visible = false;

        base.OnFormClosed(e);
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new DimmerApp());
    }
}
