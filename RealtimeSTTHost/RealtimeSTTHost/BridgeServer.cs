using System;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RealtimeSTTHost
{
    public sealed class BridgeServer : IDisposable
    {
        private readonly HttpListener _http = new HttpListener();
        private volatile string _latestJson = "{}";   // last message from page
        private volatile string _commandJson = "{}";  // last command from Access

        public BridgeServer(int port)
        {
            _http.Prefixes.Add($"http://127.0.0.1:{port}/");
        }

        public void Start()
        {
            _http.Start();
            _ = Loop();
        }

        public void UpdateFromClient(string json)
        {
            _latestJson = json;
        }

        public void SetCommand(object obj)
        {
            _commandJson = JsonSerializer.Serialize(obj);
        }

        private async System.Threading.Tasks.Task Loop()
        {
            while (_http.IsListening)
            {
                var ctx = await _http.GetContextAsync();
                try
                {
                    var path = ctx.Request.Url!.AbsolutePath;
                    if (path == "/health")
                    {
                        Reply(ctx, 200, "OK");
                    }
                    else if (path == "/pull")
                    {
                        // Access pulls latest page message (partial/final/progress)
                        Reply(ctx, 200, _latestJson, "application/json");
                    }
                    else if (path == "/push" && ctx.Request.HttpMethod == "POST")
                    {
                        using var sr = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
                        var body = await sr.ReadToEndAsync();
                        _commandJson = body;
                        Reply(ctx, 200, "{\"ok\":true}", "application/json");
                    }
                    else if (path == "/command")
                    {
                        // page can poll for new commands if desired
                        Reply(ctx, 200, _commandJson, "application/json");
                    }
                    else
                    {
                        Reply(ctx, 404, "Not found");
                    }
                }
                catch
                {
                    try { Reply(ctx, 500, "err"); } catch { }
                }
            }
        }

        private static void Reply(HttpListenerContext ctx, int code, string text, string ct = "text/plain")
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            ctx.Response.StatusCode = code;
            ctx.Response.ContentType = ct;
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.Close();
        }

        public void Dispose()
        {
            try { _http.Stop(); } catch { }
            try { _http.Close(); } catch { }
        }
    }
}
