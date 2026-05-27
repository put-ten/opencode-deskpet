using System.Windows;

namespace DeskPet.Tray;

public class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly Window _window;

    public TrayIcon(Window window)
    {
        _window = window;
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "DeskPet",
            Visible = true
        };
        _icon.Click += (_, _) =>
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
        };
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
