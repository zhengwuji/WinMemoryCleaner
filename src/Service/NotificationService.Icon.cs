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
    /// Notification Service - tray icon rendering and rotation animation.
    /// </summary>
    public partial class NotificationService
    {
        /// <summary>
        /// Cleans up the rotation timer resources and resets the rotation angle
        /// </summary>
        private void CleanupRotationTimer()
        {
            try
            {
                _currentRotationAngle = 0;

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
        }


        /// <summary>
        /// Gets the appropriate tray icon based on settings and current state
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>The tray icon to display</returns>
        private Icon GetIcon(Memory memory, bool isOptimizing)
        {
            try
            {
                return Settings.TrayIconShowMemoryUsage ? GetMemoryUsageIcon(memory, isOptimizing) : GetImageIcon(isOptimizing);
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the tray icon; using the static icon.");

                return _imageIcon;
            }
        }


        /// <summary>
        /// Gets the static application icon with optional rotation animation during optimization
        /// </summary>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>The application icon, optionally rotated</returns>
        private Icon GetImageIcon(bool isOptimizing)
        {
            try
            {
                if (isOptimizing)
                {
                    StartRotationAnimation();

                    if (_currentRotationAngle > 0)
                        return GetRotatedIcon(_imageIcon, _currentRotationAngle);
                }
                else
                {
                    StopRotationAnimation();
                }

                return _imageIcon;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the image icon; using the static icon.");

                return _imageIcon;
            }
        }


        /// <summary>
        /// Gets a custom icon displaying the current memory usage percentage
        /// </summary>
        /// <param name="memory">The memory information</param>
        /// <param name="isOptimizing">if set to <c>true</c> the system is optimizing</param>
        /// <returns>An icon with rendered memory percentage text</returns>
        private Icon GetMemoryUsageIcon(Memory memory, bool isOptimizing)
        {
            try
            {
                using (var image = new Bitmap(16, 16))
                using (var graphics = Graphics.FromImage(image))
                using (var font = new Font("Consolas", 14F, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var format = new StringFormat())
                using (var backgroundBrush = GetBackgroundBrush(memory, isOptimizing))
                using (var textBrush = GetTextBrush(memory, isOptimizing))
                {
                    // Configure format
                    format.Alignment = StringAlignment.Center;
                    format.LineAlignment = StringAlignment.Center;

                    // Configure graphics quality
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

                    // Draw background
                    if (!Settings.TrayIconUseTransparentBackground)
                    {
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(0, 0, 10, 10, 180, 90);
                            path.AddArc(5, 0, 10, 10, 270, 90);
                            path.AddArc(5, 5, 10, 10, 0, 90);
                            path.AddArc(0, 5, 10, 10, 90, 90);
                            path.CloseFigure();

                            graphics.FillPath(backgroundBrush, path);
                        }
                    }

                    // Draw text
                    graphics.DrawString(string.Format(CultureInfo.InvariantCulture, "{0:00}", memory.Physical.Used.Percentage == 100 ? 99 : memory.Physical.Used.Percentage), font, textBrush, 8F, 9F, format);

                    var handle = image.GetHicon();

                    using (var icon = Icon.FromHandle(handle))
                    {
                        var clonedIcon = (Icon)icon.Clone();

                        NativeMethods.DestroyIcon(handle);

                        return clonedIcon;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to build the memory usage icon; using the static icon.");

                return _imageIcon;
            }
        }


        /// <summary>
        /// Gets a rotated version of the specified icon
        /// </summary>
        /// <param name="icon">The icon to rotate</param>
        /// <param name="angle">The rotation angle in degrees</param>
        /// <returns>A new icon rotated by the specified angle</returns>
        private Icon GetRotatedIcon(Icon icon, float angle)
        {
            if (icon == null || angle == 0)
                return icon;

            try
            {
                using (var image = icon.ToBitmap())
                using (var rotatedImage = new Bitmap(image.Width, image.Height))
                using (var graphics = Graphics.FromImage(rotatedImage))
                {
                    // Configure graphics quality
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;

                    // Rotate around center point
                    var centerX = image.Width / 2f;
                    var centerY = image.Height / 2f;

                    graphics.TranslateTransform(centerX, centerY);
                    graphics.RotateTransform(angle);
                    graphics.TranslateTransform(-centerX, -centerY);

                    graphics.DrawImage(image, new Point(0, 0));

                    var handle = rotatedImage.GetHicon();

                    using (var tempIcon = Icon.FromHandle(handle))
                    {
                        var clonedIcon = (Icon)tempIcon.Clone();

                        NativeMethods.DestroyIcon(handle);

                        return clonedIcon;
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to rotate the icon; using it unrotated.");

                return icon;
            }
        }


        /// <summary>
        /// Handles the rotation timer tick event to animate the icon rotation
        /// </summary>
        /// <param name="sender">The event sender</param>
        /// <param name="e">The event arguments</param>
        private void OnRotationTimerTick(object sender, EventArgs e)
        {
            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                try
                {
                    _currentRotationAngle = (_currentRotationAngle + 90) % 360;

                    var newIcon = GetRotatedIcon(_imageIcon, _currentRotationAngle);
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


        /// <summary>
        /// Starts the icon rotation animation for the optimization state
        /// </summary>
        private void StartRotationAnimation()
        {
            if (_rotationTimer != null)
                return;

            if (WpfApplication.Current == null || WpfApplication.Current.Dispatcher == null)
                return;

            try
            {
                WpfApplication.Current.Dispatcher.Invoke((Action)delegate
                {
                    try
                    {
                        _currentRotationAngle = 0;

                        _rotationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                        _rotationTimer.Tick += OnRotationTimerTick;
                        _rotationTimer.Start();
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
            }
        }


        /// <summary>
        /// Stops the icon rotation animation
        /// </summary>
        private void StopRotationAnimation()
        {
            if (_rotationTimer == null)
                return;

            try
            {
                if (WpfApplication.Current == null || WpfApplication.Current.Dispatcher == null)
                {
                    CleanupRotationTimer();
                    return;
                }

                WpfApplication.Current.Dispatcher.Invoke((Action)delegate
                {
                    CleanupRotationTimer();
                });
            }
            catch (Exception ex)
            {
                Logger.Debug(ex);
            }
        }

    }
}
