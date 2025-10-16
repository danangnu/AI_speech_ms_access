using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace RealtimeSTTHost
{
    public static class Cli
    {
        public static (string www, string token, string terms, int port) Parse(string[] args)
        {
            string Get(string name, string def = "")
            {
                var i = Array.IndexOf(args, name);
                return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : def;
            }
            var www = Get("--ui", Path.Combine(AppContext.BaseDirectory, "www"));
            var token = Get("--token");
            var terms = Get("--terms");
            var portStr = Get("--port", "51337");
            int.TryParse(portStr, out var port);
            if (port == 0) port = 51337;
            return (www, token, terms, port);
        }

        [STAThread]
        public static void Run(string[] args)
        {
            var (www, token, terms, port) = Parse(args);
            var app = new Application();
            var win = new MainWindow(www, token, terms, port);
            app.Run(win);
        }
    }
}
