// ComController.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.IO;

namespace AccessEdgeHost
{
    [ComVisible(true)]
    [Guid("75B6CD1B-4CDE-4E3E-90B5-0E0D0B6052A1")]
    [ProgId("AccessEdgeHost.Controller")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(IController))]
    public class Controller : IController
    {
        private static Thread _uiThread;
        private static BrowserForm _form;
        private static ManualResetEventSlim _ready = new(false);

        public Controller() => EnsureUiThread();

        public void Show(string url) => SafeInvoke(() => _form.ShowWindow(url));
        public void Navigate(string url) => SafeInvoke(() => _form.Navigate(url));
        public void SetHtml(string html) => SafeInvoke(() => _form.SetHtml(html));
        public void Eval(string js) => SafeInvoke(async () => await _form.Eval(js));
        public void Hide() => SafeInvoke(() => _form.Hide());
        public void Close() => SafeInvoke(() => { if (!_form.IsDisposed) _form.Close(); });

        private static void EnsureUiThread()
        {
            if (_uiThread != null && _uiThread.IsAlive) return;

            _uiThread = new Thread(() =>
            {
                try
                {
                    Application.EnableVisualStyles();
                    _form = new BrowserForm();
                    _ready.Set();
                    Application.Run(_form);   // drives message loop
                }
                catch { /* swallow; keep Access alive */ }
            });
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = false;
            _uiThread.Start();
            _ready.Wait();
        }

        private static void SafeInvoke(Action a)
        {
            EnsureUiThread();
            if (_form.IsHandleCreated) _form.BeginInvoke((MethodInvoker)(() => { try { a(); } catch { } }));
            else a();
        }
    }

    internal class BrowserForm : Form
    {
        private readonly WebView2 _wv = new();
        private bool _coreReady;

        public BrowserForm()
        {
            Text = "Edge Host";
            Width = 1024; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;
            _wv.Dock = DockStyle.Fill;
            Controls.Add(_wv);
            Shown += async (_, __) => await EnsureCoreAsync();
        }

        private async Task EnsureCoreAsync()
        {
            if (_coreReady) return;

            // Force a writable user-data folder to avoid “can’t read/write data directory”
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessEdgeHost", "WebView2UserData");
            Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData, null);
            await _wv.EnsureCoreWebView2Async(env);
            _coreReady = true;
        }

        public async void ShowWindow(string url)
        {
            await EnsureCoreAsync();
            if (!Visible) Show();
            if (!string.IsNullOrWhiteSpace(url))
                _wv.CoreWebView2.Navigate(url);
            Activate();
        }

        public async void Navigate(string url)
        {
            await EnsureCoreAsync();
            _wv.CoreWebView2.Navigate(url);
        }

        public async void SetHtml(string html)
        {
            await EnsureCoreAsync();
            _wv.NavigateToString(html ?? "<html><body></body></html>");
        }

        public async Task Eval(string js)
        {
            await EnsureCoreAsync();
            if (!string.IsNullOrWhiteSpace(js))
                await _wv.ExecuteScriptAsync(js);
        }
    }
}
