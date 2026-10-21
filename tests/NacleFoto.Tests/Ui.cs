using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Xunit;

namespace NacleFoto.Tests
{
    /// <summary>
    /// UI thread (STA) shared by tests: WPF windows can only be
    /// constructed and clicked on a thread with a Dispatcher.
    /// </summary>
    internal static class Ui
    {
        private static readonly object Lock = new object();
        private static Dispatcher _dispatcher;
        private static Thread _thread;

        public static void EnsureStarted() { Ensure(); }

        public static void Run(Action action)
        {
            Ensure();
            Exception error = null;
            _dispatcher.Invoke(() =>
            {
                try { action(); }
                catch (Exception ex) { error = ex; }
            });
            if (error != null)
                throw new InvalidOperationException("Falha na acao de interface: " + error.Message, error);
        }

        public static T Run<T>(Func<T> func)
        {
            Ensure();
            T result = default(T);
            Exception error = null;
            _dispatcher.Invoke(() =>
            {
                try { result = func(); }
                catch (Exception ex) { error = ex; }
            });
            if (error != null)
                throw new InvalidOperationException("Falha na acao de interface: " + error.Message, error);
            return result;
        }

        /// <summary>Lets the dispatcher process queued items (async, timers, etc).</summary>
        public static void Idle(int ms = 150)
        {
            Ensure();
            _dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(ms);
        }

        /// <summary>Waits until a condition becomes true while processing the UI.</summary>
        public static bool WaitUntil(Func<bool> condicao, int timeoutMs = 8000)
        {
            Ensure();
            var fim = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < fim)
            {
                if (condicao()) return true;
                Idle(40);
            }
            return condicao();
        }

        private static void Ensure()
        {
            if (_dispatcher != null) return;
            lock (Lock)
            {
                if (_dispatcher != null) return;
                var ready = new ManualResetEventSlim();
                _thread = new Thread(() =>
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(_dispatcher));

                    // application must exist so that App.xaml styles are found
                    if (Application.Current == null)
                    {
                        var app = new NacleFoto.App.App();
                        app.InitializeComponent();
                    }

                    ready.Set();
                    Dispatcher.Run();
                });
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.IsBackground = true;
                _thread.Name = "ui-tests";
                _thread.Start();
                ready.Wait();
            }
        }

        /// <summary>
        /// Shuts down the UI thread. Without this, the test process hangs on
        /// exit (the UI thread stays alive).
        ///
        /// We do not shut down WPF Application: it is a singleton and, once
        /// shut down, other test classes can no longer create windows.
        /// </summary>
        public static void Shutdown()
        {
            var dispatcher = _dispatcher;
            var thread = _thread;
            if (dispatcher == null) return;

            try
            {
                dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(() => dispatcher.InvokeShutdown()));
            }
            catch (Exception) { }

            if (thread != null && !thread.Join(3000))
            {
                thread.Join(1000);
            }
            _dispatcher = null;
            _thread = null;
        }
    }

    /// <summary>
    /// Fixture that starts and tears down the UI thread when the test class completes.
    /// </summary>
    public sealed class UiFixture : IDisposable
    {
        public UiFixture() { Ui.EnsureStarted(); }
        public void Dispose() { Ui.Shutdown(); }
    }

    /// <summary>
    /// Collection for UI tests: runs sequentially because tests share the
    /// UI thread and static App.Settings (running in parallel causes accidental test collisions).
    /// </summary>
    [CollectionDefinition("ui", DisableParallelization = true)]
    public class UiCollection { }
}
