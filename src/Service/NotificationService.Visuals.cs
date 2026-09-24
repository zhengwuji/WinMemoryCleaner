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
    /// Notification Service - tray brush and tooltip text composition.
    /// </summary>
    public partial class NotificationService
    {
        /// <summary>
        /// Gets the background brush color based on memory usage and optimization state
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>A solid brush with the appropriate background color</returns>
        private Brush GetBackgroundBrush(Memory memory, bool isOptimizing)
        {
            try
            {
                if (Settings.TrayIconUseTransparentBackground)
                    return new SolidBrush(Color.Transparent);

                if (isOptimizing)
                {
                    var solidBrush = Settings.TrayIconOptimizingColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                if (memory.Physical.Used.Percentage >= Settings.TrayIconDangerLevel)
                {
                    var solidBrush = Settings.TrayIconDangerColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                if (memory.Physical.Used.Percentage >= Settings.TrayIconWarningLevel)
                {
                    var solidBrush = Settings.TrayIconWarningColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                var backgroundBrush = Settings.TrayIconBackgroundColor as SolidBrush;
                return new SolidBrush(backgroundBrush.Color);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the tray background brush; using black.");

                return new SolidBrush(Color.Black);
            }
        }


        /// <summary>
        /// Gets the tray icon tooltip text based on memory usage and optimization state
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>The formatted tooltip text</returns>
        private string GetText(Memory memory, bool isOptimizing)
        {
            try
            {
                string text;

                if (isOptimizing)
                    text = Localizer.String.Optimizing.ToUpper(Localizer.Culture);
                else
                {
                    text = Settings.ShowVirtualMemory
                        ? string.Format(Localizer.Culture, "{0}: {1}%{2}{3}: {4}%", Localizer.String.PhysicalMemory, memory.Physical.Used.Percentage, Environment.NewLine, Localizer.String.VirtualMemory, memory.Virtual.Used.Percentage)
                        : string.Format(Localizer.Culture, "{0}: {1}%", Localizer.String.PhysicalMemory, memory.Physical.Used.Percentage);
                }
                
                // Truncate to 63 characters
                if (text.Length > 63)
                    text = text.Substring(0, 63);

                return text;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the tray tooltip text; using the application title.");

                return Constants.App.Title;
            }
        }


        /// <summary>
        /// Gets the text brush color based on memory usage and optimization state
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>A solid brush with the appropriate text color</returns>
        private Brush GetTextBrush(Memory memory, bool isOptimizing)
        {
            try
            {
                if (!Settings.TrayIconUseTransparentBackground)
                {
                    var solidBrush = Settings.TrayIconTextColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                if (isOptimizing)
                {
                    var solidBrush = Settings.TrayIconOptimizingColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                if (memory.Physical.Used.Percentage >= Settings.TrayIconDangerLevel)
                {
                    var solidBrush = Settings.TrayIconDangerColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                if (memory.Physical.Used.Percentage >= Settings.TrayIconWarningLevel)
                {
                    var solidBrush = Settings.TrayIconWarningColor as SolidBrush;
                    return new SolidBrush(solidBrush.Color);
                }

                var textBrush = Settings.TrayIconTextColor as SolidBrush;
                return new SolidBrush(textBrush.Color);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the tray text brush; using white.");

                return new SolidBrush(Color.White);
            }
        }

    }
}
