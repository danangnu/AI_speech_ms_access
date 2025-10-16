using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace AccessEdgeHost
{
    [ComVisible(false)]
    internal class WebViewHost : UserControl
    {
        private WebView2 _wv;

        public WebViewHost()
        {
            this.Load += async (_, __) => await EnsureInitAsync();
        }

        private async Task EnsureInitAsync()
        {
            if (_wv != null) return;
            _wv = new WebView2 { Dock = DockStyle.Fill };
            this.Controls.Add(_wv);

            var userData = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessEdgeHost", "WebView2UserData");
            System.IO.Directory.CreateDirectory(userData);

            var env = await CoreWebView2Environment.CreateAsync(null, userData, null);
            await _wv.EnsureCoreWebView2Async(env);
        }

        public async void Navigate(string url)      { await EnsureInitAsync(); _wv.CoreWebView2.Navigate(url); }
        public async void SetHtml(string html)      { await EnsureInitAsync(); _wv.NavigateToString(html); }
        public async void Eval(string js)           { await EnsureInitAsync(); await _wv.ExecuteScriptAsync(js); }
    }
}
