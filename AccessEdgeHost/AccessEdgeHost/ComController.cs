// ComController.cs
using System;
using System.IO;
using System.Reflection;                  // for BindingFlags
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

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
        private static readonly ManualResetEventSlim _ready = new ManualResetEventSlim(false);
        private static object _callback;   // any COM object with OnMessage(string)

        public Controller()
        {
            Logger.Init();
            EnsureUiThread();
        }

        public void Show(string url)             => SafeInvoke(() => _form.ShowWindow(url));
        public void Navigate(string url)         => SafeInvoke(() => _form.Navigate(url));
        public void SetHtml(string html)         => SafeInvoke(() => _form.SetHtml(html));
        public void Eval(string js)              => SafeInvoke(async () => await _form.Eval(js)); // async void via Action
        public void Hide()                       => SafeInvoke(() => _form.Hide());
        public void Close()                      => SafeInvoke(() => { if (!_form.IsDisposed) _form.Close(); });

        // Extra helpers exposed via IController
        public void SetCallback(object cb)       => _callback = cb;
        public void PostMessage(string text)     => SafeInvoke(() =>
        {
            if (_form?.Core != null) _form.Core.PostWebMessageAsString(text ?? "");
        });
        public void ShowDevTools()               => SafeInvoke(() => _form?.Core?.OpenDevToolsWindow());
        public void NavigateFile(string fullPath)=> SafeInvoke(() =>
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return;
            var uri = new Uri(fullPath, UriKind.Absolute).AbsoluteUri;
            _form.Navigate(uri);
        });

        internal static object CallbackObject => _callback;

        // ---------- UI thread plumbing ----------
        private static void EnsureUiThread()
        {
            if (_uiThread != null && _uiThread.IsAlive) return;

            _uiThread = new Thread(() =>
            {
                try
                {
                    Logger.Write("UI thread starting");
                    Application.EnableVisualStyles();
                    _form = new BrowserForm();

                    // Force handle so BeginInvoke is safe immediately
                    var _ = _form.Handle;

                    _ready.Set();                     // signal AFTER handle exists
                    Application.Run(_form);
                    Logger.Write("UI message loop ended");
                }
                catch (Exception ex)
                {
                    Logger.Write("Fatal in UI thread", ex);
                }
            });

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = false;
            _uiThread.Start();

            _ready.Wait();
        }

        private static void SafeInvoke(Action a)
        {
            EnsureUiThread();

            void Run() { try { a(); } catch (Exception ex) { Logger.Write("SafeInvoke", ex); } }

            if (_form.IsHandleCreated)
            {
                _form.BeginInvoke((Action)Run);
            }
            else
            {
                _form.HandleCreated += (_, __) => _form.BeginInvoke((Action)Run);
            }
        }
    }

    internal class BrowserForm : Form
    {
        private readonly WebView2 _wv = new Microsoft.Web.WebView2.WinForms.WebView2();
        private bool _coreReady;
        private CoreWebView2Environment _env;
        private readonly SemaphoreSlim _initGate = new SemaphoreSlim(1, 1);

        internal CoreWebView2 Core => _wv?.CoreWebView2;

        public BrowserForm()
        {
            Text = "Edge Host";
            Width = 1024; Height = 700;
            StartPosition = FormStartPosition.CenterScreen;

            _wv.Dock = DockStyle.Fill;
            Controls.Add(_wv);

            _wv.CoreWebView2InitializationCompleted += (_, e) =>
            {
                if (!e.IsSuccess)
                    Logger.Write("Core init failed", e.InitializationException);
                else
                    Logger.Write("Core init OK");
            };

            Shown += async (_, __) => await EnsureCoreAsync();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try { BeginInvoke(new Action(Application.ExitThread)); } catch { }
        }

        private void EmitToVba(string msg)
        {
            var cb = Controller.CallbackObject;
            if (cb == null) return;
            try
            {
                cb.GetType().InvokeMember(
                    "OnMessage",
                    BindingFlags.InvokeMethod,
                    null, cb, new object[] { msg });
            }
            catch (Exception ex) { Logger.Write("Callback invoke failed", ex); }
        }

        private string GetUserDataDir()
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessEdgeHost", "WebView2UserData");
            Directory.CreateDirectory(userData);
            return userData;
        }

        private async Task EnsureCoreAsync()
        {
            if (_coreReady) return;

            await _initGate.WaitAsync();
            try
            {
                if (_coreReady) return;

                var env = _env;
                if (env == null)
                {
                    env = await CoreWebView2Environment.CreateAsync(null, GetUserDataDir(), null);
                    _env = env; // reuse the same environment for all calls
                }

                await _wv.EnsureCoreWebView2Async(env);

                // Messaging VBA <-> page
                _wv.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _wv.CoreWebView2.WebMessageReceived += (_, e) =>
                    EmitToVba(e.TryGetWebMessageAsString());

                // Allow mic permission in-app
                _wv.CoreWebView2.PermissionRequested += (_, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.Microphone)
                        e.State = CoreWebView2PermissionState.Allow;
                };

                // Open external links in default browser
                _wv.CoreWebView2.NewWindowRequested += (_, e) =>
                {
                    e.Handled = true;
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = e.Uri,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                };

                _wv.CoreWebView2.ProcessFailed += (_, e) =>
                    Logger.Write("ProcessFailed: " + e.ProcessFailedKind);

                _coreReady = true;
                Logger.Write("EnsureCoreAsync complete");
            }
            catch (Exception ex)
            {
                Logger.Write("EnsureCoreAsync exception", ex);
                throw;
            }
            finally
            {
                _initGate.Release();
            }
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
