using FloatingStatusWindowLibrary;
using HomeStatusWindow.Properties;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Threading;

namespace HomeStatusWindow
{
    public class WindowSource : IWindowSource, IDisposable
    {
        private readonly FloatingStatusWindow _floatingStatusWindow;
        private readonly Dispatcher _dispatcher;

        private HubConnection _hubConnection;
        private DateTime _lastUpdate;
        private Timer _reconnectTimer;

        private readonly Status _lastStatus = new Status();

        internal WindowSource()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;

            _floatingStatusWindow = new FloatingStatusWindow(this);
            UpdateText(Resources.Loading);

            // Initialize the connection
            Task.Factory.StartNew(InitializeConnection);
        }

        public async void Dispose()
        {
            // Stop the reconnection timer (if any)
            StopReconnectionTimer();

            // Terminate the connection
            await TerminateConnection();

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

        public System.Drawing.Icon Icon => Resources.ApplicationIcon;

        public bool HasSettingsMenu => false;

        public bool HasRefreshMenu => false;

        public bool HasAboutMenu => false;

        public string WindowSettings
        {
            get => Settings.Default.WindowSettings;
            set
            {
                Settings.Default.WindowSettings = value;
                Settings.Default.Save();
            }
        }

        private void StartReconnectionTimer()
        {
            // Stop the current timer (if any)
            StopReconnectionTimer();

            // Create and start the reconnection timer
            _reconnectTimer = new Timer(Settings.Default.ReconnectTimerInterval.TotalMilliseconds);
            _reconnectTimer.Elapsed += HandleReconnectTimerElapsed;
            _reconnectTimer.Start();
        }

        private void StopReconnectionTimer()
        {
            // Get rid of the reconnection timer
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }

        private void HandleReconnectTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // See if we haven't heard from the server within the timeout and reconnect if needed
            if (DateTime.Now - _lastUpdate >= Settings.Default.ReconnectTimeout)
                InitializeConnection();
        }

        private void InitializeConnection()
        {
            try
            {
                // Stop the reconnection timer (if any)
                StopReconnectionTimer();

                // Create the URI for the server
                var serverUri = string.Format(Settings.Default.ServerUri, Settings.Default.ServerName, Settings.Default.ServerPort);

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(serverUri)
                    .Build();

                _hubConnection.Closed += error =>
                {
                    StartReconnectionTimer();

                    return Task.CompletedTask;
                };

                _hubConnection.On<string>("LatestStatus", (message) =>
                {
                    try
                    {
                        var statusMessage = JsonConvert.DeserializeObject<StatusMessage>(message);

                        switch (statusMessage?.Name)
                        {
                            case "washer":
                                _lastStatus.Washer = statusMessage.Status;
                                break;
                            case "dryer":
                                _lastStatus.Dryer = statusMessage.Status;
                                break;
                        }

                        UpdateDisplay(_lastStatus);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                });

                // Open the connection
                _hubConnection.StartAsync().Wait();

                _hubConnection.InvokeAsync("RequestLatestStatus").Wait();
            }
            catch (Exception exception)
            {
                UpdateText($"Connection error: {exception.Message}");
            }
            finally
            {
                // Start the reconnection check timer
                StartReconnectionTimer();
            }
        }

        private async Task TerminateConnection()
        {
            // If the client doesn't exist or isn't open then there's nothing to do
            if (_hubConnection == null || _hubConnection.State != HubConnectionState.Connected)
                return;

            // Close the connection
            await _hubConnection.DisposeAsync();
        }

        private void UpdateDisplay(Status status)
        {
            // Last update was now
            _lastUpdate = DateTime.Now;

            // Create a string builder
            var text = new StringBuilder();

            text.AppendFormat(Resources.DryerStatus, status.Dryer ? Resources.On : Resources.Off);
            text.AppendLine();
            text.AppendFormat(Resources.WasherStatus, status.Washer ? Resources.On : Resources.Off);

            // Set the text
            UpdateText(text.ToString());
        }

        private void UpdateText(string text)
        {
            _dispatcher.InvokeAsync(() => _floatingStatusWindow.SetText(text));
        }
    }
}