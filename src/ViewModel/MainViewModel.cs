using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using WpfApplication = System.Windows.Application;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Main View Model
    /// </summary>
    /// <remarks>
    /// Split across partial files: the bindable properties live in
    /// MainViewModel.Properties.cs.
    /// </remarks>
    public partial class MainViewModel : ViewModel, IDisposable
    {
        #region Fields

        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Computer _computer;
        private readonly IComputerService _computerService;
        private readonly IHotkeyService _hotKeyService;
        private bool _isOptimizationKeyValid;
        private bool _isOptimizationRunning;
        private bool _isReiniziliating;
        private DateTimeOffset _lastAutoOptimizationByInterval = DateTimeOffset.Now;
        private DateTimeOffset _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
        private readonly object _lockObject = new object();
        private byte _optimizationProgressPercentage;
        private string _optimizationProgressStep = Localizer.String.Optimize;
        private byte _optimizationProgressTotal = byte.MaxValue;
        private byte _optimizationProgressValue = byte.MinValue;
        private string _selectedProcess;
        private ObservableCollection<ObservableItem<bool>> _trayIconItems;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainViewModel" /> class.
        /// </summary>
        /// <param name="computerService">Computer service</param>
        /// <param name="hotKeyService">Hotkey service.</param>
        /// <param name="notificationService">Notification service</param>
        public MainViewModel(IComputerService computerService, IHotkeyService hotKeyService, INotificationService notificationService)
            : base(notificationService)
        {
            _computerService = computerService;
            _hotKeyService = hotKeyService;

            // Commands
            AddProcessToExclusionListCommand = new RelayCommand<string>(AddProcessToExclusionList, () => CanAddProcessToExclusionList);
            OptimizeCommand = new RelayCommand(() => OptimizeAsync(Enums.Memory.Optimization.Reason.Manual), () => CanOptimize);
            RemoveProcessFromExclusionListCommand = new RelayCommand<string>(RemoveProcessFromExclusionList);
            ResetSettingsToDefaultConfigurationCommand = new RelayCommand(ResetSettingsToDefaultConfiguration);

            // Properties
            FontSize = Settings.FontSize;
            MemoryUsageThresholds = Enumerable.Range(1, 99).Select(number => (byte)number).ToList();

            // Models
            Computer = new Computer();

            if (App.IsInDesignMode)
            {
                _computerService = new ComputerService();
                _hotKeyService = new HotkeyService();

                Settings.AutoUpdate = true;

                Computer.OperatingSystem.IsWindows81OrGreater = true;
                Computer.OperatingSystem.IsWindows8OrGreater = true;
                Computer.OperatingSystem.IsWindowsVistaOrGreater = true;
                Computer.OperatingSystem.IsWindowsXpOrGreater = true;
                IsOptimizationKeyValid = true;
            }
            else
            {
                _computerService.OnOptimizeProgressUpdate += OnOptimizeProgressUpdate;

                Computer.OperatingSystem = _computerService.OperatingSystem;
                UseHotkey = Settings.UseHotkey;

                MonitorAsync();
            }
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
                if (_cancellationTokenSource != null)
                {
                    try
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }

                    try
                    {
                        _cancellationTokenSource.Token.WaitHandle.WaitOne(100);
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }

                    try
                    {
                        _cancellationTokenSource.Dispose();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }
                }

                if (_hotKeyService != null)
                {
                    try
                    {
                        _hotKeyService.Dispose();
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e);
                    }
                }
            }
        }

        #endregion

        #region Commands

        /// <summary>
        /// Gets the add process to exclusion list.
        /// </summary>
        /// <value>
        /// The add process to exclusion list.
        /// </value>
        public ICommand AddProcessToExclusionListCommand { get; private set; }

        /// <summary>
        /// Gets the optimize command.
        /// </summary>
        /// <value>
        /// The optimize command.
        /// </value>
        public ICommand OptimizeCommand { get; private set; }

        /// <summary>
        /// Gets the remove process from exclusion list command.
        /// </summary>
        /// <value>
        /// The remove process from exclusion list command.
        /// </value>
        public ICommand RemoveProcessFromExclusionListCommand { get; private set; }

        /// <summary>
        /// Gets the reset settings to default configuration command.
        /// </summary>
        /// <value>
        /// The reset settings to default configuration command.
        /// </value>
        public ICommand ResetSettingsToDefaultConfigurationCommand { get; private set; }

        #endregion

        #region Actions

        /// <summary>
        /// Occurs when [on add process to exclusion list command completed].
        /// </summary>
        public event Action OnAddProcessToExclusionListCommandCompleted;

        /// <summary>
        /// Occurs when [on language change completed].
        /// </summary>
        public event Action OnLanguageChangeCompleted;

        /// <summary>
        /// Occurs when [on optimize command completed].
        /// </summary>
        public event Action OnOptimizeCommandCompleted;

        /// <summary>
        /// Occurs when [on remove process from exclusion list command completed].
        /// </summary>
        public event Action OnRemoveProcessFromExclusionListCommandCompleted;

        #endregion

        #region Methods

        /// <summary>
        /// Adds the process to exclusion list.
        /// </summary>
        /// <param name="process">The process.</param>
        private void AddProcessToExclusionList(string process)
        {
            try
            {
                IsBusy = true;

                if (!Settings.ProcessExclusionList.Contains(process, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(process))
                {
                    if (Settings.ProcessExclusionList.Add(process))
                    {
                        Settings.Save();

                        RaisePropertyChanged(() => Processes);
                        RaisePropertyChanged(() => ProcessExclusionList);

                if (OnAddProcessToExclusionListCommandCompleted != null)
                {
                    WpfApplication.Current.Dispatcher.Invoke((Action)delegate
                            {
                                OnAddProcessToExclusionListCommandCompleted();
                            });
                        }
                    }
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Monitor App Resources
        /// </summary>
        private void MonitorApp()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Check if it's busy
                    if (IsBusy)
                        continue;

                    // Delay
                    if (_cancellationTokenSource.Token.WaitHandle.WaitOne(60000))
                        break;

                    // Update app
                    Updater.Update();

                    // App priority
                    App.SetPriority(Settings.RunOnPriority);

                    // Auto Optimization
                    lock (_lockObject)
                    {
                        if (CanOptimize)
                        {
                            // Interval
                            if (Settings.AutoOptimizationInterval > 0 &&
                                DateTimeOffset.Now.Subtract(_lastAutoOptimizationByInterval).TotalHours >= Settings.AutoOptimizationInterval)
                            {
                                OptimizeAsync(Enums.Memory.Optimization.Reason.Schedule);

                                _lastAutoOptimizationByInterval = DateTimeOffset.Now;
                                continue;
                            }

                            // Memory usage
                            if (Settings.AutoOptimizationMemoryUsage > 0 &&
                                Computer.Memory.Physical.Free.Percentage < Settings.AutoOptimizationMemoryUsage &&
                                DateTimeOffset.Now.Subtract(_lastAutoOptimizationByMemoryUsage).TotalMinutes >= Constants.App.AutoOptimizationMemoryUsageInterval)
                            {
                                OptimizeAsync(Enums.Memory.Optimization.Reason.LowMemory);

                                _lastAutoOptimizationByMemoryUsage = DateTimeOffset.Now;
                                continue;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                }
            }
        }

        /// <summary>
        /// Monitor Background Tasks
        /// </summary>
        private void MonitorAsync()
        {
            // Monitor App Resources
            try
            {
                ThreadPool.QueueUserWorkItem(_ => MonitorApp());
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            // Monitor Computer Resources
            try
            {
                ThreadPool.QueueUserWorkItem(_ => MonitorComputer());
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        /// <summary>
        /// Monitor Computer Resources
        /// </summary>
        private void MonitorComputer()
        {
            // App priority
            App.SetPriority(Settings.RunOnPriority);

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    // Check if it's busy
                    if (IsBusy)
                        continue;

                    lock (_lockObject)
                    {
                        // Update memory info
                        Computer.Memory = _computerService.Memory;

                        RaisePropertyChanged(() => Computer);
                        RaisePropertyChanged(() => VirtualMemoryHeader);

                        NotificationService.Update(Computer.Memory, IsOptimizationRunning);
                    }

                    // Delay
                    if (_cancellationTokenSource.Token.WaitHandle.WaitOne(5000))
                        break;
                }
                catch (Exception e)
                {
                    Logger.Debug(e);
                }
            }
        }

        /// <summary>
        /// Called when [optimize progress is update].
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="step">The step.</param>
        private void OnOptimizeProgressUpdate(byte value, string step)
        {
            OptimizationProgressPercentage = (byte)(value * 100 / OptimizationProgressTotal);
            OptimizationProgressStep = step;
            OptimizationProgressValue = value;
        }

        /// <summary>
        /// Optimize
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        private void Optimize(Enums.Memory.Optimization.Reason reason)
        {
            lock (_lockObject)
            {
                try
                {
                    IsBusy = true;
                    IsOptimizationRunning = true;

                    NotificationService.Update(Computer.Memory, IsOptimizationRunning);

                    // App priority
                    App.SetPriority(Settings.RunOnPriority);

                    // Memory optimize
                    var tempPhysicalAvailable = Computer.Memory.Physical.Free.Bytes;
                    var tempVirtualAvailable = Computer.Memory.Virtual.Free.Bytes;

                    _computerService.Optimize(reason, Settings.MemoryAreas);

                    // Update memory info
                    Computer.Memory = _computerService.Memory;
                    RaisePropertyChanged(() => Computer);

                    // Notification
                    if (Settings.ShowOptimizationNotifications)
                    {
                        var physicalReleased = (Computer.Memory.Physical.Free.Bytes > tempPhysicalAvailable ? Computer.Memory.Physical.Free.Bytes - tempPhysicalAvailable : tempPhysicalAvailable - Computer.Memory.Physical.Free.Bytes).ToMemoryUnit();
                        var virtualReleased = (Computer.Memory.Virtual.Free.Bytes > tempVirtualAvailable ? Computer.Memory.Virtual.Free.Bytes - tempVirtualAvailable : tempVirtualAvailable - Computer.Memory.Virtual.Free.Bytes).ToMemoryUnit();

                        var message = Settings.ShowVirtualMemory
                            ? string.Format(Localizer.Culture, "{1}{0}{0}{2}: {3}{0}{4}: {5:0.#} {6}{0}{7}: {8:0.#} {9}", Environment.NewLine, Localizer.String.MemoryOptimized.ToUpper(Localizer.Culture), Localizer.String.Reason, reason.GetString(), Localizer.String.PhysicalMemory, physicalReleased.Key, physicalReleased.Value, Localizer.String.VirtualMemory, virtualReleased.Key, virtualReleased.Value)
                            : string.Format(Localizer.Culture, "{1}{0}{0}{2}: {3}{0}{4}: {5:0.#} {6}", Environment.NewLine, Localizer.String.MemoryOptimized.ToUpper(Localizer.Culture), Localizer.String.Reason, reason.GetString(), Localizer.String.PhysicalMemory, physicalReleased.Key, physicalReleased.Value);

                        Notify(message);
                    }
                }
                finally
                {
                    IsOptimizationRunning = false;
                    IsBusy = false;

                    NotificationService.Update(Computer.Memory, IsOptimizationRunning);

                    // Raise the event after IsOptimizationRunning is set to false
                    // Use BeginInvoke to ensure it runs after all property changes propagate
                    WpfApplication.Current.Dispatcher.BeginInvoke((Action)(() =>
                    {
                        // Force command manager to re-evaluate CanExecute on all commands
                        CommandManager.InvalidateRequerySuggested();
                    }), System.Windows.Threading.DispatcherPriority.Normal);

                    // Raise completion event with lower priority to ensure commands are refreshed first
                    if (OnOptimizeCommandCompleted != null)
                    {
                        WpfApplication.Current.Dispatcher.BeginInvoke((Action)(() =>
                         {
                             OnOptimizeCommandCompleted();
                         }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    }
                }
            }
        }

        /// <summary>
        /// Optimize
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        private void OptimizeAsync(Enums.Memory.Optimization.Reason reason)
        {
            try
            {
                if (IsOptimizationRunning)
                    return;

                OptimizationProgressStep = Localizer.String.Optimize;
                OptimizationProgressValue = 0;
                OptimizationProgressTotal = (byte)(new BitArray(new[] { (int)Settings.MemoryAreas }).OfType<bool>().Count(x => x) + 1);

                ThreadPool.QueueUserWorkItem(_ => Optimize(reason));
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        /// <summary>
        /// Registers the optimization hotkey.
        /// </summary>
        /// <param name="modifiers">The modifiers.</param>
        /// <param name="key">The key.</param>
        private void RegisterOptimizationHotkey(ModifierKeys modifiers, Key key)
        {
            UnregisterOptimizationHotkey();

            Settings.OptimizationKey = key;
            Settings.OptimizationModifiers = modifiers;

            var hotKey = new Hotkey(Settings.OptimizationModifiers, Settings.OptimizationKey);

            IsOptimizationKeyValid = _hotKeyService.Register(hotKey, () => OptimizeAsync(Enums.Memory.Optimization.Reason.Manual));

            if (!_isReiniziliating && !IsOptimizationKeyValid)
            {
                var message = string.Format(Localizer.Culture, Localizer.String.HotkeyIsInUseByOperatingSystem, hotKey);

                Logger.Warning(message);
                NotificationService.Notify(message);

                return;
            }

            Settings.Save();

            RaisePropertyChanged(() => OptimizationKey);
            RaisePropertyChanged(() => OptimizationModifiers);
        }

        /// <summary>
        /// Reinitializes app after system resume from hibernation.
        /// </summary>
        public void ReinitializeAfterHibernation()
        {
            try
            {
                lock (_lockObject)
                {
                    _isReiniziliating = true;

                    if (UseHotkey)
                        RegisterOptimizationHotkey(Settings.OptimizationModifiers, Settings.OptimizationKey);

                    Computer.Memory = _computerService.Memory;

                    NotificationService.Update(Computer.Memory, IsOptimizationRunning);

                    RaisePropertyChanged(string.Empty);

                    App.ReleaseMemory();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Error after system resume from hibernation: " + e.GetMessage());
            }
            finally
            {
                _isReiniziliating = false;
            }
        }

        /// <summary>
        /// Removes the process from exclusion list.
        /// </summary>
        /// <param name="process">The process.</param>
        private void RemoveProcessFromExclusionList(string process)
        {
            try
            {
                IsBusy = true;

                if (Settings.ProcessExclusionList.Remove(process))
                    Settings.Save();

                RaisePropertyChanged(() => Processes);
                RaisePropertyChanged(() => ProcessExclusionList);

                if (OnRemoveProcessFromExclusionListCommandCompleted != null)
                {
                    WpfApplication.Current.Dispatcher.Invoke((Action)delegate
                    {
                        OnRemoveProcessFromExclusionListCommandCompleted();
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Resets settings to the default configuration.
        /// </summary>
        private void ResetSettingsToDefaultConfiguration()
        {
            try
            {
                IsBusy = true;

                Settings.Reset(true);
                ThemeManager.Theme = Enums.Theme.Dark;

                FontSize = Settings.FontSize;
                OptimizationKey = Settings.OptimizationKey;
                OptimizationModifiers = Settings.OptimizationModifiers;

                _trayIconItems = null;

                NotificationService.Initialize();
                NotificationService.Update(Computer.Memory, IsOptimizationRunning);

                RaisePropertyChanged(string.Empty);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Unregisters the optimization hotkey.
        /// </summary>
        private void UnregisterOptimizationHotkey()
        {
            _hotKeyService.Unregister(new Hotkey(Settings.OptimizationModifiers, Settings.OptimizationKey));

            IsOptimizationKeyValid = true;
        }

        #endregion
    }
}
