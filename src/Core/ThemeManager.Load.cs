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
    /// Centralized theme manager - theme resource loading.
    /// </summary>
    public static partial class ThemeManager
    {
        private static void Load(Enums.Theme theme)
        {
            Theme resource;

            try
            {
                if (string.IsNullOrEmpty(Constants.App.ThemesResourcePath) ||
                    string.IsNullOrEmpty(Constants.App.EmbeddedResourcePathExtension))
                {
                    Logger.Error("Theme resource path constants are null or empty");
                    return;
                }

                if (Constants.App.ThemesResourcePath.Contains("..") ||
                    Constants.App.EmbeddedResourcePathExtension.Contains(".."))
                {
                    Logger.Error("Theme resource path contains directory traversal characters");
                    return;
                }

                var resourcePath = string.Format(CultureInfo.InvariantCulture,
                    "{0}{1}Theme{2}",
                    Constants.App.ThemesResourcePath,
                    theme,
                    Constants.App.EmbeddedResourcePathExtension);

                if (resourcePath.Length > 260)
                {
                    Logger.Error("Theme resource path is too long");
                    return;
                }

                resource = Helper.ReadEmbeddedResource<Theme>(resourcePath);
            }
            catch (Exception e)
            {
                Logger.Error("Failed to read embedded theme resource for " + theme + ": " + e.Message);
                return;
            }

            if (resource == null)
            {
                Logger.Warning("Theme resource is null for " + theme);
                return;
            }

            try
            {
                var app = WpfApplication.Current;
                if (app == null || app.Resources == null)
                {
                    Logger.Warning("Application.Current or Resources is null during theme loading");
                    return;
                }

                var properties = typeof(Theme).GetProperties(BindingFlags.Public | BindingFlags.Instance);
                if (properties == null || properties.Length == 0)
                {
                    Logger.Warning("No properties found on Theme class");
                    return;
                }

                foreach (var property in properties)
                {
                    if (property == null)
                        continue;

                    if (!IsValidResourceKey(property.Name))
                    {
                        Logger.Warning("Invalid property name skipped: " + property.Name);
                        continue;
                    }

                    string hex;

                    try
                    {
                        hex = property.GetValue(resource, null) as string;
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to get property value for " + property.Name + ": " + e.Message);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(hex))
                        continue;

                    if (!IsValidHexColor(hex))
                    {
                        Logger.Warning("Invalid hex color '" + hex + "' for property " + property.Name);
                        continue;
                    }

                    try
                    {
                        var color = ParseHexColor(hex);
                        app.Resources[property.Name] = new WpfBrush(color);
                    }
                    catch (Exception e)
                    {
                        Logger.Error("Failed to parse or set color for property " + property.Name + ": " + e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error("Failed to apply theme " + theme + ": " + e.Message);
            }
        }

    }
}
