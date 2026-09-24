using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using WpfApplication = System.Windows.Application;
using WinFormsColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Centralized theme manager that provides all theme resources with enhanced security
    /// </summary>
    public static partial class ThemeManager
    {
        private const int MAX_BRUSHES_COUNT = 500;
        private const int MAX_RESOURCE_KEY_LENGTH = 100;
        private const int MAX_HEX_COLOR_LENGTH = 9;
        private const int INITIALIZATION_TIMEOUT_MS = 30000;

        private static readonly Regex _hexColorPattern = new Regex(@"^#([A-Fa-f0-9]{3}|[A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})$", RegexOptions.Compiled);
        private static readonly Regex _resourceKeyPattern = new Regex(@"^[a-zA-Z0-9_.]+$", RegexOptions.Compiled);

        private static readonly object _lockObject = new object();
        private static readonly object _initializationLock = new object();
        private static volatile bool _isInitialized;
        private static volatile bool _isInitializing;
        private static readonly Timer _initializationTimer = InitializeTimer();

        private static Enums.Theme _theme = Enums.Theme.Dark;
        private static List<WpfBrush> _brushes;
        private static List<Enums.Theme> _themes;
        private static readonly Dictionary<string, WinFormsColor> _colorCache = new Dictionary<string, WinFormsColor>(StringComparer.Ordinal);
        private static DateTime _lastInitializationAttempt = DateTime.MinValue;

        private static Timer InitializeTimer()
        {
            try
            {
                return new Timer(OnInitializationTimeout, null, Timeout.Infinite, Timeout.Infinite);
            }
            catch (Exception e)
            {
                try
                {
                    Logger.Error("Failed to initialize timer: " + e.Message);
                }
                catch (Exception inner)
                {
                    // Logger itself failed; this runs during static initialization, so
                    // only the debugger output is still available.
                    System.Diagnostics.Debug.WriteLine(string.Format(Localizer.Culture, "Failed to log the timer initialization failure: {0}", inner.GetMessage()));
                }

                return null;
            }
        }

        private static void OnInitializationTimeout(object state)
        {
            try
            {
                Logger.Error("ThemeManager initialization timed out");

                lock (_initializationLock)
                {
                    if (_isInitializing)
                    {
                        _isInitializing = false;
                        InitializeMinimalDefaults();
                        _isInitialized = true;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Initialization timeout handler failed: " + e.Message);
            }
        }

        private static void InitializeMinimalDefaults()
        {
            try
            {
                _themes = new List<Enums.Theme> { Enums.Theme.Dark, Enums.Theme.Light };
                _brushes = new List<WpfBrush>();
                _theme = Enums.Theme.Dark;
            }
            catch (Exception e)
            {
                Logger.Error("Minimal defaults initialization failed: " + e.Message);
            }
        }

        private static void EnsureInitialized()
        {
            if (_isInitialized)
                return;

            var timeSinceLastAttempt = DateTime.UtcNow - _lastInitializationAttempt;
            if (timeSinceLastAttempt.TotalSeconds < 1 && _lastInitializationAttempt != DateTime.MinValue)
            {
                Thread.Sleep(100);
                if (_isInitialized)
                    return;
            }

            lock (_initializationLock)
            {
                if (_isInitialized)
                    return;

                if (_isInitializing)
                {
                    Monitor.Wait(_initializationLock, 5000);
                    return;
                }

                _isInitializing = true;
                _lastInitializationAttempt = DateTime.UtcNow;

                try
                {
                    if (_initializationTimer != null)
                        _initializationTimer.Change(INITIALIZATION_TIMEOUT_MS, Timeout.Infinite);

                    InitializeDefaults();
                    InitializeThemes();
                    InitializeBrushes();

                    _isInitialized = true;
                    _isInitializing = false;

                    if (_initializationTimer != null)
                        _initializationTimer.Change(Timeout.Infinite, Timeout.Infinite);

                    Monitor.PulseAll(_initializationLock);
                }
                catch (Exception e)
                {
                    Logger.Error("Initialization failed: " + e.Message);

                    InitializeMinimalDefaults();
                    _isInitialized = true;
                    _isInitializing = false;

                    Monitor.PulseAll(_initializationLock);
                }
            }
        }

        private static bool IsValidResourceKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            if (key.Length > MAX_RESOURCE_KEY_LENGTH)
                return false;

            try
            {
                return _resourceKeyPattern.IsMatch(key);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to match the resource key pattern.");

                return false;
            }
        }

        private static bool IsValidTheme(Enums.Theme theme)
        {
            try
            {
                return Enum.IsDefined(typeof(Enums.Theme), theme);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to validate the theme value.");

                return false;
            }
        }

        private static void InitializeDefaults()
        {
            try
            {
                var app = WpfApplication.Current;
                if (app == null || app.Resources == null)
                {
                    Logger.Warning("Application.Current or Resources is null during theme initialization");
                    return;
                }

                var defaultResources = new Dictionary<string, WpfColor>
                {
                    { "Accent", WpfColor.FromArgb(0xFF, 0x00, 0xAE, 0xF7) },
                    { "MemoryBarIndicatorBackground", WpfColor.FromArgb(0xFF, 0xE0, 0x36, 0x0A) },
                    { "MemoryBarTrackBackground", WpfColor.FromArgb(0xFF, 0x00, 0xAA, 0x41) },
                    { "PrimaryBackground", WpfColor.FromArgb(0xFF, 0x20, 0x20, 0x20) },
                    { "PrimaryBorder", WpfColor.FromArgb(0xFF, 0x30, 0x30, 0x30) },
                    { "SecondaryBackground", WpfColor.FromArgb(0xFF, 0x2C, 0x2C, 0x2C) },
                    { "SecondaryBorder", WpfColor.FromArgb(0xFF, 0x70, 0x70, 0x70) },
                    { "SecondaryDisabled", WpfColor.FromArgb(0xFF, 0x99, 0x99, 0x99) },
                    { "SecondaryForeground", WpfColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) }
                };

                foreach (var kvp in defaultResources)
                {
                    try
                    {
                        if (!IsValidResourceKey(kvp.Key))
                        {
                            Logger.Warning("Invalid resource key skipped: " + kvp.Key);
                            continue;
                        }

                        app.Resources[kvp.Key] = new WpfBrush(kvp.Value);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to set default resource " + kvp.Key + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Default resources initialization failed: " + e.Message);
            }
        }

        private static void InitializeThemes()
        {
            try
            {
                var enumValues = Enum.GetValues(typeof(Enums.Theme));
                if (enumValues == null || enumValues.Length == 0)
                {
                    _themes = new List<Enums.Theme> { Enums.Theme.Dark };
                    return;
                }

                _themes = new List<Enums.Theme>();

                foreach (Enums.Theme theme in enumValues)
                {
                    if (IsValidTheme(theme))
                    {
                        _themes.Add(theme);
                    }
                }

                if (_themes.Count == 0)
                {
                    _themes.Add(Enums.Theme.Dark);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Theme initialization failed: " + e.Message);
                _themes = new List<Enums.Theme> { Enums.Theme.Dark };
            }
        }

        private static void InitializeBrushes()
        {
            try
            {
                var colorType = typeof(WinFormsColor);
                if (colorType == null)
                {
                    _brushes = new List<WpfBrush>();
                    return;
                }

                var colorProperties = colorType.GetProperties(BindingFlags.Public | BindingFlags.Static);
                if (colorProperties == null || colorProperties.Length == 0)
                {
                    _brushes = new List<WpfBrush>();
                    return;
                }

                _brushes = new List<WpfBrush>();
                var processedCount = 0;

                foreach (var property in colorProperties)
                {
                    try
                    {
                        if (processedCount >= MAX_BRUSHES_COUNT)
                        {
                            Logger.Warning("Brush count limit reached: " + MAX_BRUSHES_COUNT);
                            break;
                        }

                        if (property == null || property.PropertyType != typeof(WinFormsColor))
                            continue;

                        var color = (WinFormsColor)property.GetValue(null, null);

                        if (color.A == 255)
                        {
                            var wpfColor = WpfColor.FromArgb(color.A, color.R, color.G, color.B);
                            _brushes.Add(new WpfBrush(wpfColor));
                            processedCount++;
                        }
                    }
                    catch (Exception e)
                    {
                        var propertyName = (property != null) ? property.Name : "unknown";
                        Logger.Error("Failed to process color property " + propertyName + ": " + e.Message);
                    }
                }

                try
                {
                    _brushes = _brushes
                        .OrderBy(GetColorHueSafe)
                        .ThenBy(GetColorSaturationSafe)
                        .ThenBy(GetColorBrightnessSafe)
                        .ToList();
                }
                catch (Exception e)
                {
                    Logger.Error("Brush sorting failed: " + e.Message);
                }
            }
            catch (Exception e)
            {
                Logger.Error("Brush initialization failed: " + e.Message);
                _brushes = new List<WpfBrush>();
            }
        }
    }
}
