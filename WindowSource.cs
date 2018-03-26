using FloatingStatusWindowLibrary;
using HomeStatusWindow.Properties;
using Newtonsoft.Json;
using Quobject.SocketIoClientDotNet.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HomeStatusWindow
{
    public class WindowSource : IWindowSource, IDisposable
    {
        private readonly FloatingStatusWindow _floatingStatusWindow;
        private readonly Dispatcher _dispatcher;

        private Socket _socket;

        internal WindowSource()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            _floatingStatusWindow = new FloatingStatusWindow(this);
            _floatingStatusWindow.SetText(Resources.Loading);

            Task.Factory.StartNew(Initialize);
        }

        public void Dispose()
        {
            Terminate();

            _floatingStatusWindow.Save();
            _floatingStatusWindow.Dispose();
        }

        public void ShowSettings()
        {
        }

        public void Refresh()
        {
        }

        public void ShowAbout()
        {
        }

        public string Name => Resources.Name;

        public bool HasSettingsMenu => false;
        public bool HasRefreshMenu => false;
        public bool HasAboutMenu => false;

        public System.Drawing.Icon Icon => Resources.ApplicationIcon;

        public string WindowSettings
        {
            get => Settings.Default.WindowSettings;
            set
            {
                Settings.Default.WindowSettings = value;
                Settings.Default.Save();
            }
        }

        private readonly Status _fullStatus = new Status();

        private void Initialize()
        {
            // Create the socket
            _socket = IO.Socket(Settings.Default.ServerAddress);

            // Setup for status events
            _socket.On("status", UpdateText);

            _socket.On(Socket.EVENT_CONNECT, () => _socket.Emit("getStatus"));
            _socket.On(Socket.EVENT_DISCONNECT, () => SetText(Resources.Disconnected));
        }

        private void Terminate()
        {
            _socket?.Disconnect();
        }

        private void UpdateText(object data)
        {
            var json = (string)data;

            var status = JsonConvert.DeserializeObject<Status>(json);

            if (status.Dryer.HasValue)
                _fullStatus.Dryer = status.Dryer;

            if (status.Washer.HasValue)
                _fullStatus.Washer = status.Washer;

            var text = GetText(_fullStatus);

            SetText(text);
        }

        private void SetText(string text)
        {
            // Update the window on the main thread
            _dispatcher.Invoke(() => _floatingStatusWindow.SetText(text));
        }

        private static string GetText(Status status)
        {
            try
            {
                var output = new StringBuilder();

                output.AppendFormat(Resources.DryerStatus, status.Dryer.GetValueOrDefault() ? Resources.On : Resources.Off);
                output.AppendLine();
                output.AppendFormat(Resources.WasherStatus, status.Washer.GetValueOrDefault() ? Resources.On : Resources.Off);

                return output.ToString();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
    }
}
