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
    /// Centralized theme manager - public theme and color API.
    /// </summary>
    public static partial class ThemeManager
    {
        /// <summary>
        /// Gets the accent color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor AccentColor
        {
            get { return GetColorSafe(nameof(AccentColor).Replace("Color", string.Empty), WinFormsColor.DeepSkyBlue); }
        }


        /// <summary>
        /// Gets the primary background color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor PrimaryBackgroundColor
        {
            get { return GetColorSafe(nameof(PrimaryBackgroundColor).Replace("Color", string.Empty), WinFormsColor.FromArgb(32, 32, 32)); }
        }


        /// <summary>
        /// Gets the secondary background color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryBackgroundColor
        {
            get { return GetColorSafe(nameof(SecondaryBackgroundColor).Replace("Color", string.Empty), WinFormsColor.DarkSlateGray); }
        }
        

        /// <summary>
        /// Gets the secondary border color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryBorderColor
        {
            get { return GetColorSafe(nameof(SecondaryBorderColor).Replace("Color", string.Empty), WinFormsColor.DimGray); }
        }


        /// <summary>
        /// Gets the secondary foreground color for Windows Forms controls.
        /// </summary>
        public static WinFormsColor SecondaryForegroundColor
        {
            get { return GetColorSafe(nameof(SecondaryForegroundColor).Replace("Color", string.Empty), WinFormsColor.White); }
        }


        /// <summary>
        /// Gets the brushes with defensive copying.
        /// </summary>
        public static List<WpfBrush> Brushes
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    var brushes = _brushes;
                    if (brushes == null || brushes.Count == 0)
                        return new List<WpfBrush>();

                    return new List<WpfBrush>(brushes);
                }
            }
        }


        /// <summary>
        /// Gets or sets the theme with enhanced validation and error handling.
        /// </summary>
        public static Enums.Theme Theme
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    return _theme;
                }
            }
            set
            {
                if (!IsValidTheme(value))
                {
                    Logger.Warning("Invalid theme value: " + value);
                    throw new ArgumentException("Invalid theme value: " + value, "value");
                }

                EnsureInitialized();

                lock (_lockObject)
                {
                    if (_theme == value)
                        return;

                    var oldTheme = _theme;

                    try
                    {
                        Load(value);
                        _theme = value;

                        lock (_colorCache)
                        {
                            _colorCache.Clear();
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to load theme " + value + ", reverting to " + oldTheme + ": " + e.Message);
                        _theme = oldTheme;
                        throw;
                    }
                }

                try
                {
                    App.ReleaseMemory();
                }
                catch (Exception e)
                {
                    Logger.Error("Failed to release memory after theme change: " + e.Message);
                }
            }
        }


        /// <summary>
        /// Gets the themes with defensive copying.
        /// </summary>
        public static List<Enums.Theme> Themes
        {
            get
            {
                EnsureInitialized();
                lock (_lockObject)
                {
                    var themes = _themes;
                    if (themes == null || themes.Count == 0)
                    {
                        return new List<Enums.Theme> { Enums.Theme.Dark };
                    }

                    return new List<Enums.Theme>(themes);
                }
            }
        }


        /// <summary>
        /// Cleanup method for proper resource disposal
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                lock (_initializationLock)
                {
                    if (_initializationTimer != null)
                        _initializationTimer.Dispose();
                }

                lock (_colorCache)
                {
                    _colorCache.Clear();
                }
            }
            catch (Exception e)
            {
                Logger.Error("Cleanup failed: " + e.Message);
            }
        }
    }
}
