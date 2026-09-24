using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Windows Memory Cleaner - notification area (tray icon) and system power event handling.
    /// </summary>
    public partial class App
    {
        #region Methods

        /// <summary>
        /// Handles power mode changes (suspend/resume from hibernation).
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="PowerModeChangedEventArgs" /> instance containing the event data.</param>
        private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            try
            {
                switch (e.Mode)
                {
                    case PowerModes.Resume:
                        ThreadPool.QueueUserWorkItem(_ =>
                        {
                            try
                            {
                                // Let the system settle before reinitializing.
                                Thread.Sleep(5000);

                                // Retry logic for handling transient failures during system resume
                                const int maxRetries = 3;
                                var retryCount = 0;
                                Exception lastException = null;

                                while (retryCount < maxRetries)
                                {
                                    try
                                    {
                                        var mainViewModel = DependencyInjection.Container.Resolve<MainViewModel>();

                                        if (mainViewModel == null)
                                            throw new InvalidOperationException("MainViewModel could not be resolved from the DI container");

                                        mainViewModel.ReinitializeAfterHibernation();
                                        return;
                                    }
                                    catch (Exception ex)
                                    {
                                        lastException = ex;
                                        retryCount++;

                                        if (retryCount >= maxRetries)
                                        {
                                            var failureException = new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Failed to reinitialize after hibernation after {0} attempts", maxRetries), lastException);
                                            Logger.Error(failureException.Message + ": " + failureException.GetMessage());
                                            throw;
                                        }

                                        // Wait before retrying
                                        Thread.Sleep(5000);
                                    }
                                }
                            }
                            catch (Exception threadEx)
                            {
                                Logger.Error("Critical error in power mode resume handling: " + threadEx.GetMessage());
                            }
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error handling power mode change: " + ex.GetMessage());
            }
        }

        /// <summary>
        /// Called when [notify icon click].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        private void OnNotifyIconClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            lock (_showHidelock)
            {
                switch (e.Button)
                {
                    // Show/Hide
                    case MouseButtons.Left:
                        if (MainWindow == null)
                            return;

                        if (MainWindow.OwnedWindows.Cast<View>().Any(window => window != null && window.IsDialog))
                        {
                            MainWindow.Activate();
                            MainWindow.Topmost = true;
                            MainWindow.Topmost = Settings.AlwaysOnTop;

                            return;
                        }

                        switch (MainWindow.Visibility)
                        {
                            case Visibility.Collapsed:
                            case Visibility.Hidden:
                                MainWindow.Show();

                                MainWindow.WindowState = WindowState.Normal;

                                MainWindow.Activate();
                                MainWindow.Focus();

                                MainWindow.Topmost = true;
                                MainWindow.Topmost = Settings.AlwaysOnTop;
                                MainWindow.ShowInTaskbar = true;

                                // Focus the Optimize button when restoring from notification area
                                MainWindow.Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    var mainWindow = MainWindow as MainWindow;

                                    if (mainWindow != null)
                                    {
                                        var optimizeButton = mainWindow.FindName("Optimize") as UIElement;

                                        if (optimizeButton != null)
                                        {
                                            Keyboard.Focus(optimizeButton);
                                            FocusManager.SetFocusedElement(mainWindow, optimizeButton);
                                        }
                                    }
                                }), DispatcherPriority.ApplicationIdle);
                                break;

                            case Visibility.Visible:
                                MainWindow.Hide();

                                MainWindow.ShowInTaskbar = false;
                                break;
                        }

                        ReleaseMemory();
                        return;

                    // Optimize
                    case MouseButtons.Middle:
                        if (!Settings.TrayIconOptimizeOnMiddleMouseClick)
                            return;

                        var mainViewModel = DependencyInjection.Container.Resolve<MainViewModel>();

                        if (mainViewModel.OptimizeCommand.CanExecute(null))
                            mainViewModel.OptimizeCommand.Execute(null);
                        break;
                }
            }
        }


        #endregion
    }
}
