using FloatingStatusWindowLibrary;
using HomeStatusWindow.Properties;
using System.Diagnostics;
using System.Windows;

namespace HomeStatusWindow
{
    public partial class App
    {
        private WindowSource _windowSource;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            StartManager.ManageAutoStart = true;
            StartManager.AutoStartEnabled = !Debugger.IsAttached && Settings.Default.AutoStart;
            StartManager.AutoStartChanged += (value =>
            {
                Settings.Default.AutoStart = value;
                Settings.Default.Save();
            });

            _windowSource = new WindowSource();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _windowSource.Dispose();

            base.OnExit(e);
        }
    }
}
