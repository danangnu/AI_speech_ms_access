using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AccessEdgeHost
{
    internal static class Logger
    {
        private static readonly object Gate = new();
        private static string _file;
        private static bool _init;

        public static void Init()
        {
            if (_init) return;
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AccessEdgeHost");
            Directory.CreateDirectory(dir);
            _file = Path.Combine(dir, "edgehost.log");
            _init = true;

            // Catch *everything* so errors don’t vanish
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => Write("Application.ThreadException", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Write("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Write("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };

            Write("Logger initialized");
        }

        public static void Write(string message, Exception ex = null)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(_file,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}" +
                        (ex == null ? "" : $" | {ex.GetType().Name}: {ex.Message}\r\n{ex.StackTrace}") +
                        Environment.NewLine);
                }
            }
            catch { /* ignore */ }
        }

        public static void Try(Action action, string context)
        {
            try { action(); }
            catch (Exception ex) { Write("ERR " + context, ex); }
        }
    }
}
