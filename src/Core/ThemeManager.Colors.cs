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
    /// Centralized theme manager - private color resolution helpers.
    /// </summary>
    public static partial class ThemeManager
    {
        private static float GetColorHueSafe(WpfBrush brush)
        {
            try
            {
                if (brush == null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var hue = winFormsColor.GetHue();

                if (float.IsNaN(hue) || float.IsInfinity(hue))
                    return 0f;

                return Math.Max(0f, Math.Min(360f, hue));
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to read the brush hue.");

                return 0f;
            }
        }


        private static float GetColorSaturationSafe(WpfBrush brush)
        {
            try
            {
                if (brush == null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var saturation = winFormsColor.GetSaturation();

                if (float.IsNaN(saturation) || float.IsInfinity(saturation))
                    return 0f;

                return Math.Max(0f, Math.Min(1f, saturation));
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to read the brush saturation.");

                return 0f;
            }
        }


        private static float GetColorBrightnessSafe(WpfBrush brush)
        {
            try
            {
                if (brush == null)
                    return 0f;

                var color = brush.Color;
                var winFormsColor = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);
                var brightness = winFormsColor.GetBrightness();

                if (float.IsNaN(brightness) || float.IsInfinity(brightness))
                    return 0f;

                return Math.Max(0f, Math.Min(1f, brightness));
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to read the brush brightness.");

                return 0f;
            }
        }


        private static WinFormsColor GetColorSafe(string resourceKey, WinFormsColor fallback)
        {
            if (!IsValidResourceKey(resourceKey))
            {
                Logger.Warning("Invalid resource key: " + resourceKey);
                return fallback;
            }

            try
            {
                lock (_colorCache)
                {
                    WinFormsColor cachedColor;
                    if (_colorCache.TryGetValue(resourceKey, out cachedColor))
                    {
                        return cachedColor;
                    }
                }

                EnsureInitialized();

                var app = WpfApplication.Current;
                if (app == null || app.Resources == null)
                    return fallback;

                var resource = app.Resources[resourceKey] as WpfBrush;
                if (resource == null)
                    return fallback;

                var color = resource.Color;
                var result = WinFormsColor.FromArgb(color.A, color.R, color.G, color.B);

                lock (_colorCache)
                {
                    if (!_colorCache.ContainsKey(resourceKey))
                    {
                        _colorCache[resourceKey] = result;
                    }
                }

                return result;
            }
            catch (Exception e)
            {
                Logger.Error("Failed to get color for resource " + resourceKey + ": " + e.Message);
                return fallback;
            }
        }


        private static bool IsValidHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return false;

            if (hex.Length > MAX_HEX_COLOR_LENGTH)
                return false;

            try
            {
                return _hexColorPattern.IsMatch(hex);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to match the hex color pattern.");

                return false;
            }
        }


        private static WpfColor ParseHexColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor))
                throw new ArgumentException("Hex color cannot be null or empty", "hexColor");

            if (!IsValidHexColor(hexColor))
                throw new ArgumentException("Invalid hex color format: " + hexColor, "hexColor");

            try
            {
                var color = System.Drawing.ColorTranslator.FromHtml(hexColor);
                return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to parse hex color '" + hexColor + "': " + e.Message);
                throw new ArgumentException("Invalid hex color format: " + hexColor, "hexColor", e);
            }
        }

    }
}
