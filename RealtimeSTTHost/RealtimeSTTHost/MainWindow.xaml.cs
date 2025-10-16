using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace RealtimeSTTHost
{
    public partial class MainWindow : Window
    {
        private readonly string _wwwPath;
        private readonly string _token;
        private readonly string _termsPipe; // pipe-delimited
        private readonly BridgeServer _bridge;

        public MainWindow(string wwwPath, string token, string termsPipe, int port)
        {
            InitializeComponent();
            _wwwPath = wwwPath;
            _token = token;
            _termsPipe = termsPipe;
            _bridge = new BridgeServer(port);
            _bridge.Start(); // exposes /health, /pull, /push
            Loaded += MainWindow_Loaded;
            Closed += (_, __) => _bridge.Dispose();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Web.EnsureCoreWebView2Async();
            // host folder as https://local/
            Web.CoreWebView2.SetVirtualHostNameToFolderMapping(
              "local", _wwwPath, CoreWebView2HostResourceAccessKind.Allow);

            // inject init
            var init = new { token = _token, terms = _termsPipe };
            var js = $"window.__A2_INIT__={JsonSerializer.Serialize(init)};";
            await Web.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(js);

            // page→host messages (final/partial etc.)
            Web.CoreWebView2.WebMessageReceived += (_, ev) => {
                try
                {
                    var s = ev.TryGetWebMessageAsString();
                    _bridge.UpdateFromClient(s ?? "");
                }
                catch { }
            };

            Web.Source = new Uri("https://local/index.html");
        }
    }
}
