using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Windows Memory Cleaner
    /// </summary>
    public partial class App : IDisposable
    {
        #region Fields

        private static bool _isRunning;
        private static Mutex _mutex;
        private static NotifyIcon _notifyIcon;
        private static readonly List<string> _notifications = new List<string>();
        private static readonly object _showHidelock = new object();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="App" /> class.
        /// </summary>
        public App()
        {
            // Log
            Logger.Level = IsInDebugMode ? Enums.Log.Levels.Debug : Enums.Log.Levels.Information;

            // Events
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is in debug mode; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInDebugMode
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is in design mode.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is in design mode; otherwise, <c>false</c>.
        /// </value>
        public static bool IsInDesignMode
        {
            get
            {
                return DesignerProperties.GetIsInDesignMode(new DependencyObject());
            }
        }

        /// <summary>
        /// App path
        /// </summary>
        public static string Path { get; private set; }

        /// <summary>
        /// App version
        /// </summary>
        public static Version Version { get; private set; }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Logger.Dispose();

                try
                {
                    SystemEvents.PowerModeChanged -= OnPowerModeChanged;
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                }

                if (_mutex != null)
                {
                    try
                    {
                        _mutex.ReleaseMutex();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }

                    try
                    {
                        _mutex.Dispose();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }

                    _mutex = null;
                }

                try
                {
                    if (_notifyIcon != null)
                        _notifyIcon.Dispose();
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                }
            }
        }

        #endregion

        #region Methods

        private void Initialize()
        {
            // DI/IOC
            DependencyInjection.Container.Register<IComputerService, ComputerService>();
            DependencyInjection.Container.Register<IHotkeyService, HotkeyService>();
            DependencyInjection.Container.Register<INotificationService, NotificationService>();

            // App properties
            Path = Helper.GetExecutablePath();
            Version = Helper.GetVersion();

            // Check if app is already running
            bool createdNew;

            _mutex = new Mutex(true, Constants.App.Id, out createdNew);
            _isRunning = !createdNew;

            // App Migration
            if (!_isRunning)
                Migrator.Run();

            // App priority
            SetPriority(Settings.RunOnPriority);
        }

        /// <summary>
        /// Navigates the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        public static void Navigate(Uri uri)
        {
            if (uri == null)
                return;

            using (Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            })) { }
        }

        /// <summary>
        /// Called when [dispatcher unhandled exception].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DispatcherUnhandledExceptionEventArgs" /> instance containing the event data.</param>
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            Logger.Error(e.Exception);
        }

        /// <summary>
        /// Called when [process exit].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        private void OnProcessExit(object sender, EventArgs e)
        {
            Dispose();

            try
            {
                if (Updater.Process != null)
                    Process.Start(Updater.Process);
            }
            catch (Exception ex)
            {
                Logger.Error("Error starting update process." + ex);
            }
        }

        /// <summary>
        /// Raises the <see cref="E:Startup" /> event.
        /// </summary>
        /// <param name="startupEvent">The <see cref="StartupEventArgs" /> instance containing the event data.</param>
        protected override void OnStartup(StartupEventArgs startupEvent)
        {
            var startupType = Enums.StartupType.App;

            try
            {
                Initialize();

                var rawCommandLineArguments = startupEvent != null ? startupEvent.Args : null;

                // A staged update restarts this executable with /ApplyUpdate so it can
                // replace the installed file and relaunch it. Nothing else should run.
                if (Updater.ApplyUpdate(rawCommandLineArguments))
                {
                    Shutdown(true);

                    return;
                }

                var commandLineArguments = rawCommandLineArguments != null ? rawCommandLineArguments.Select(arg => arg.Replace("-", "/").Trim()).ToArray() : null;
                var memoryAreas = Enums.Memory.Areas.None;

                if (commandLineArguments != null)
                {
                    // Update to the latest version
                    Updater.Update(commandLineArguments);

                    // Process command‑line arguments
                    foreach (var argument in commandLineArguments.Select(arg => arg.Replace("/", string.Empty)))
                    {
                        // Memory areas to optimize
                        Enums.Memory.Areas area;

                        if (Enum.TryParse(argument, true, out area))
                            memoryAreas |= area;

                        // Startup Type
                        if (memoryAreas != Enums.Memory.Areas.None)
                            startupType = Enums.StartupType.Silent;

                        if (argument.Equals(Constants.App.CommandLineArgument.Install, StringComparison.OrdinalIgnoreCase))
                            startupType = Enums.StartupType.Installation;

                        if (argument.Equals(Constants.App.CommandLineArgument.Reset, StringComparison.OrdinalIgnoreCase))
                            startupType = Enums.StartupType.Reset;

                        if (argument.Equals(Constants.App.CommandLineArgument.Service, StringComparison.OrdinalIgnoreCase))
                            startupType = Enums.StartupType.Service;

                        if (argument.Equals(Constants.App.CommandLineArgument.Uninstall, StringComparison.OrdinalIgnoreCase))
                            startupType = Enums.StartupType.Uninstallation;

                        // Notify version update
                        if (argument.Equals(Version.ToString()))
                            _notifications.Add(string.Format(Localizer.Culture, Localizer.String.UpdatedToVersion, string.Format(Localizer.Culture, Constants.App.VersionFormat, Version.Major, Version.Minor, Version.Build)));
                    }
                }

                switch (startupType)
                {
                    case Enums.StartupType.App:
                        if (_isRunning)
                        {
                            try
                            {
                                var appHandle = NativeMethods.FindWindow(null, Constants.App.Title);

                                if (appHandle != IntPtr.Zero && NativeMethods.IsWindowVisible(appHandle))
                                {
                                    int appId;

                                    if (NativeMethods.GetWindowThreadProcessId(appHandle, out appId) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                                        NativeMethods.AllowSetForegroundWindow(appId);

                                    NativeMethods.ShowWindowAsync(appHandle, Constants.Windows.ShowWindow.Restore);
                                    NativeMethods.SetForegroundWindow(appHandle);
                                }
                            }
                            finally
                            {
                                Shutdown(true);
                            }
                        }

                        NativeMethods.AllowSetForegroundWindow(Process.GetCurrentProcess().Id);

                        // App startup shortcut
                        Helper.StartMenuShortcut(Settings.CreateStartMenuShortcut);

                        // Theme
                        ThemeManager.Theme = Enums.Theme.Dark;

                        // Notification Areas
                        _notifyIcon = new NotifyIcon();
                        _notifyIcon.MouseUp += OnNotifyIconClick;

                        // DI/IOC
                        DependencyInjection.Container.Register(_notifyIcon);

                        var mainWindow = new MainWindow();

                        if (!Settings.StartMinimized)
                        {
                            mainWindow.Show();
                            mainWindow.Activate();
                            mainWindow.Focus();
                            mainWindow.Topmost = true;
                            mainWindow.Topmost = Settings.AlwaysOnTop;
                        }

                        // Subscribe to power events
                        SystemEvents.PowerModeChanged += OnPowerModeChanged;

                        // Process notifications
                        foreach (var notification in _notifications)
                        {
                            Logger.Information(notification);
                            DependencyInjection.Container.Resolve<INotificationService>().Notify(notification);
                        }

                        RunOnStartup(Settings.RunOnStartup);
                        ReleaseMemory();
                        break;

                    case Enums.StartupType.Installation:
                        WinServiceInstaller.Install();

                        Shutdown();
                        break;

                    case Enums.StartupType.Reset:
                        try
                        {
                            Logger.EnableConsoleOutput();

                            // Kill all other running instances of the application to prevent conflicts and infinite loops
                            var currentProcess = Process.GetCurrentProcess();
                            var processes = Process.GetProcessesByName(currentProcess.ProcessName).Where(p => p.Id != currentProcess.Id);

                            foreach (var process in processes)
                            {
                                try
                                {
                                    process.Kill();
                                    process.WaitForExit(5000); // Wait up to 5 seconds for process to exit
                                }
                                catch (Exception e)
                                {
                                    // The other instance may have exited on its own already.
                                    Logger.Debug(e);
                                }
                            }

                            Settings.Reset(keepLanguage: true); // Reset settings to defaults while preserving language preference
                            Settings.AutoUpdate = false;        // Disable auto-update to prevent potential update-related issues
                            
                            Settings.Save();

                            Logger.Information(Localizer.String.ResetCommand);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(string.Format(Localizer.Culture, Localizer.String.ErrorResetCommand, ex.GetMessage()));
                        }
                        finally
                        {
                            Shutdown(true);
                        }

                        break;

                    case Enums.StartupType.Service:
                        using (var service = new WinService())
                        {
                            if (IsInDebugMode)
                                service.OnDebug(null);
                            else
                                ServiceBase.Run(service);
                        }
                        break;

                    case Enums.StartupType.Silent:
                        DependencyInjection.Container.Resolve<IComputerService>().Optimize(Enums.Memory.Optimization.Reason.Manual, memoryAreas);

                        Shutdown();
                        break;

                    case Enums.StartupType.Uninstallation:
                        WinServiceInstaller.Uninstall();

                        Shutdown();
                        break;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);

                if (startupType == Enums.StartupType.App)
                    ShowDialog(e);

                Shutdown(true);
            }
        }

        /// <summary>
        /// Called when [unhandled exception].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="UnhandledExceptionEventArgs" /> instance containing the event data.</param>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error((Exception)e.ExceptionObject);
        }

        /// <summary>
        /// Releases the app memory
        /// </summary>
        public static void ReleaseMemory()
        {
            // Optimize App Working Set.
            // The former forced full GC (two GC.Collect + WaitForPendingFinalizers)
            // has been removed: it stalls every thread for an unbounded time and
            // usually returns the memory to the runtime rather than to the OS.
            // Trimming the working set is what actually returns pages to Windows.
            try
            {
                NativeMethods.EmptyWorkingSet(Process.GetCurrentProcess().Handle);
            }
            catch (Exception e)
            {
                Logger.Debug(e);
            }
        }

        /// <summary>
        /// Shows a dialog
        /// </summary>
        /// <param name="exception">Exception</param>
        /// <param name="message">The message.</param>
        private void ShowDialog(Exception exception, string message = null)
        {
            ShowDialog(message ?? exception.GetMessage(), MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Shows the dialog.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="button">The button.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="defaultResult">The default result.</param>
        /// <param name="options">The options.</param>
        private void ShowDialog(string message, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None, MessageBoxResult defaultResult = MessageBoxResult.OK, System.Windows.MessageBoxOptions options = System.Windows.MessageBoxOptions.None)
        {
            try
            {
                System.Windows.MessageBox.Show(message, Constants.App.Title, button, icon, defaultResult, options);
            }
            catch (Exception e)
            {
                // No UI thread available; the caller already logs the message.
                Logger.Debug(e, "Failed to show the message box: " + message);
            }
        }

        /// <summary>
        /// Shuts down the app
        /// </summary>
        /// <param name="force">if set to <c>true</c> [force].</param>
        public static void Shutdown(bool force = false)
        {
            try
            {
                if (force)
                    Environment.Exit(Constants.Windows.SystemErrorCode.ErrorSuccess);
                else
                    Current.Shutdown();
            }
            catch (Exception e)
            {
                // The process is terminating, so Trace/EventLog may no longer flush;
                // the debugger output is the only reliable channel left.
                System.Diagnostics.Debug.WriteLine(string.Format(Localizer.Culture, "Graceful shutdown failed, forcing exit: {0}", e.GetMessage()));

                Environment.Exit(Constants.Windows.SystemErrorCode.ErrorSuccess);
            }
        }

        #endregion
    }
}
