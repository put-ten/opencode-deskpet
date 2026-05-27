using System.Windows;
using System.Windows.Threading;

namespace DeskPet;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "deskpet_crash.log"),
                $"Crash: {args.Exception.Message}\n{args.Exception.StackTrace}"
            );
            args.Handled = true;
        };
    }
}
