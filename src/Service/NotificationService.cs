using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Cursors = System.Windows.Input.Cursors;
using WpfApplication = System.Windows.Application;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Notification Service
    /// </summary>
    public partial class NotificationService : INotificationService
    {
        #region Fields

        private int _currentRotationAngle;
        private Icon _currentIcon;
        private bool _disposed;
        private readonly Icon _imageIcon;
        private readonly NotifyIcon _notifyIcon;
        private readonly object _disposeLock = new object();
        private DispatcherTimer _rotationTimer;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="NotificationService" /> class.
        /// </summary>
        /// <param name="notifyIcon">Notify Icon</param>
        public NotificationService(NotifyIcon notifyIcon)
        {
            _currentRotationAngle = 0;
            _imageIcon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            _notifyIcon = notifyIcon;

            Initialize();
        }

        /// <summary>
        /// Initializes the notification service
        /// </summary>
        public void Initialize()
        {
            if (_notifyIcon == null)
                return;

            // Notification Areas (Menu)
            _notifyIcon.ContextMenuStrip = new TrayIconContextMenuControl();

            // Optimize
            _notifyIcon.ContextMenuStrip.Items.Add(Localizer.String.Optimize, null, (sender, args) =>
            {
                var mainViewModel = DependencyInjection.Container.Resolve<MainViewModel>();

                if (mainViewModel.OptimizeCommand.CanExecute(null))
                    mainViewModel.OptimizeCommand.Execute(null);
            });

            _notifyIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            // Exit
            _notifyIcon.ContextMenuStrip.Items.Add(Localizer.String.Exit, null, (sender, args) =>
            {
                App.Shutdown();
            });

            Update(new Memory());

            _notifyIcon.Visible = true;
        }

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
                lock (_disposeLock)
                {
                    if (_disposed)
                        return;

                    _disposed = true;
                }

                try
                {
                    if (_rotationTimer != null)
                    {
                        _rotationTimer.Stop();
                        _rotationTimer.Tick -= OnRotationTimerTick;
                        _rotationTimer = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }

                try
                {
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Icon = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }

                try
                {
                    if (_currentIcon != null && _currentIcon != _imageIcon)
                    {
                        _currentIcon.Dispose();
                        _currentIcon = null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }

                try
                {
                    if (_imageIcon != null)
                        _imageIcon.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }

                try
                {
                    if (_notifyIcon != null)
                        _notifyIcon.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Shows or hides the loading cursor and enables/disables the context menu
        /// </summary>
        /// <param name="running">if set to <c>true</c> shows loading cursor and disables menu</param>
        public void Loading(bool running)
        {
            if (WpfApplication.Current == null || WpfApplication.Current.Dispatcher == null)
                return;

            // Multi-threading trick
            WpfApplication.Current.Dispatcher.Invoke((Action)delegate
            {
                Mouse.OverrideCursor = running ? Cursors.Wait : null;

                if (_notifyIcon.ContextMenuStrip != null)
                    _notifyIcon.ContextMenuStrip.Enabled = !running;
            });
        }

        /// <summary>
        /// Displays a balloon tip notification from the system tray
        /// /// </summary>
        /// <param name="message">The notification message text</param>
        /// <param name="title">The notification title</param>
        /// <param name="timeout">The time period in seconds to display the notification</param>
        /// <param name="icon">The notification icon type</param>
        public void Notify(string message, string title = null, int timeout = 5, Enums.Icon.Notification icon = Enums.Icon.Notification.None)
        {
            if (_notifyIcon == null)
                return;

            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Visible = true;

                _notifyIcon.ShowBalloonTip(timeout * 1000, title, message, (ToolTipIcon)icon);
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
            }
        }

        /// <summary>
        /// Updates the tray icon and tooltip text based on current memory usage and optimization state
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <exception cref="ArgumentNullException">memory</exception>
        public void Update(Memory memory, bool isOptimizing = false)
        {
            if (memory == null)
                throw new ArgumentNullException("memory");

            lock (_disposeLock)
            {
                if (_disposed || _notifyIcon == null)
                    return;

                try
                {
                    _notifyIcon.Text = GetText(memory, isOptimizing);

                    var newIcon = GetIcon(memory, isOptimizing);
                    var oldIcon = _currentIcon;

                    _notifyIcon.Icon = newIcon;
                    _currentIcon = newIcon;

                    if (oldIcon != null && oldIcon != _imageIcon && oldIcon != newIcon)
                    {
                        try
                        {
                            oldIcon.Dispose();
                        }
                        catch (Exception disposeError)
                        {
                            // A failure to release the previous icon is not actionable.
                            Logger.Debug(disposeError);
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }
            }
        }

        #endregion
    }
}
