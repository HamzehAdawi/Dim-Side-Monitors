using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

class DimmerApp : Form
{
    private List<Form> overlays = new();
    private bool dimmed = false;
    private double opacity = 0.5;

    private Dictionary<string, (int modifier, Keys key)> hotkeyConfig = new();
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
        Load += OnLoaded;
    }

    private async void OnLoaded(object sender, EventArgs e)
    {
        await Task.Delay(1000);

       	string exeDir = System.IO.Path.GetDirectoryName(Application.ExecutablePath)!;

        string hotkeysPath = System.IO.Path.Combine(exeDir, "hotkeys.txt");
        if (!System.IO.File.Exists(hotkeysPath))
        {
            var defaults = new[]
            {
                "Toggle=2,D0",
                "Dim1=2,D1",
                "Dim2=2,D2",
                "Dim3=2,D3"
            };
            System.IO.File.WriteAllLines(hotkeysPath, defaults);
        }

        foreach (var line in System.IO.File.ReadAllLines(hotkeysPath))
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
            var parts = line.Split('=');
            var name = parts[0];
            var values = parts[1].Split(',');
            int modifier = int.Parse(values[0]);
            Keys key = (Keys)Enum.Parse(typeof(Keys), values[1]);
            hotkeyConfig[name] = (modifier, key);
        }

        RegisterAllHotkeys();

        trayIcon = new NotifyIcon
        {
            Icon = new Icon(System.IO.Path.Combine(exeDir, "dimmerIcon.ico")),
            Text = "Monitor Dimmer",
            ContextMenuStrip = BuildTrayMenu(),
            Visible = true
        };
        trayIcon.DoubleClick += (s, args) => ToggleDim();

        this.Hide();
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        startupItem = new ToolStripMenuItem("Start with Windows")
        {
            Checked = IsStartupEnabled()
        };
        startupItem.Click += (s, e) => ToggleStartup();

        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (s, e) => Application.Exit());

        return menu;
    }

    private void RegisterAllHotkeys()
    {
        if (hotkeyConfig.TryGetValue("Toggle", out var hk))
            RegisterHotKey(Handle, HK_TOGGLE, hk.modifier, (int)hk.key);
        if (hotkeyConfig.TryGetValue("Dim1", out hk))
            RegisterHotKey(Handle, HK_DIM1, hk.modifier, (int)hk.key);
        if (hotkeyConfig.TryGetValue("Dim2", out hk))
            RegisterHotKey(Handle, HK_DIM2, hk.modifier, (int)hk.key);
        if (hotkeyConfig.TryGetValue("Dim3", out hk))
            RegisterHotKey(Handle, HK_DIM3, hk.modifier, (int)hk.key);
    }

    private void LoadHotKeysFromFile()
    {
        string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hotkeys.txt");;
        if (!System.IO.File.Exists(path))
        {
            var defaults = new[]
            {
                "Toggle=2,D0",
                "Dim1=2,D1",
                "Dim2=2,D2",
                "Dim3=2,D3"
            };
            System.IO.File.WriteAllLines(path, defaults);
        }

        foreach (var line in System.IO.File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
            var parts = line.Split('=');
            var name = parts[0];
            var values = parts[1].Split(',');
            int modifier = int.Parse(values[0]);
            Keys key = (Keys)Enum.Parse(typeof(Keys), values[1]);
            hotkeyConfig[name] = (modifier, key);
        }
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
        RemoveOverlays();
        overlays = new List<Form>();

        foreach (var screen in Screen.AllScreens)
        {
            if (screen.Primary) continue;

            Form overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Bounds = screen.Bounds,
                BackColor = Color.Black,
                Opacity = opacity,
                TopMost = true,
                ShowInTaskbar = false
            };
            overlay.Show();
            overlays.Add(overlay);
        }
    }

    private void RemoveOverlays()
    {
        foreach (var overlay in overlays)
            overlay?.Close();
        overlays.Clear();
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
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
        return key?.GetValue("MonitorDimmer") != null;
    }

    private void EnableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        key.SetValue("MonitorDimmer", Application.ExecutablePath);
    }

    private void DisableStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
        key.DeleteValue("MonitorDimmer", false);
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
        bool createdNew;
        using var mutex = new System.Threading.Mutex(true, "MonitorDimmerSingleton", out createdNew);
        if (!createdNew) return;

        Application.EnableVisualStyles();
        Application.Run(new DimmerApp());
    }
}
