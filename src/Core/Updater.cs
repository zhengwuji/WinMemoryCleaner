using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Updater
    /// </summary>
    public static class Updater
    {
        #region Fields

        private const int ApplyUpdateTimeoutMilliseconds = 60000;
        private const int MoveRetryCount = 30;
        private const int MoveRetryDelayMilliseconds = 500;

        private static WebClient _client;
        private static DateTimeOffset _lastCheck = DateTimeOffset.MinValue;

        internal static ProcessStartInfo Process;

        #endregion Fields

        #region Methods (Apply)

        /// <summary>
        /// Applies a pending update from the staged copy of the executable.
        /// This runs inside the newly downloaded process: it waits for the previous
        /// instance to terminate, replaces the installed executable and restarts it.
        /// </summary>
        /// <param name="arguments">The raw command-line arguments.</param>
        /// <returns>True when this process was started as an update applier.</returns>
        public static bool ApplyUpdate(string[] arguments)
        {
            // WPF's StartupEventArgs.Args does NOT include the executable path, so the
            // layout here is: [/ApplyUpdate] [<base64 target path>] [<previous pid>] [<base64 forwarded args>].
            // The switch is located by name rather than by a fixed index so the same
            // code also tolerates a raw command line that does include the exe path.
            if (arguments == null)
                return false;

            var switchIndex = -1;

            for (var index = 0; index < arguments.Length; index++)
            {
                if (IsApplyUpdateSwitch(arguments[index]))
                {
                    switchIndex = index;

                    break;
                }
            }

            if (switchIndex < 0 || arguments.Length < switchIndex + 3)
                return false;

            var targetPath = Decode(arguments[switchIndex + 1]);
            var forwardedArguments = arguments.Length > switchIndex + 3 ? Decode(arguments[switchIndex + 3]) : string.Empty;

            int previousProcessId;

            int.TryParse(arguments[switchIndex + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out previousProcessId);

            if (string.IsNullOrEmpty(targetPath))
                return false;

            WaitForProcessExit(previousProcessId);

            var currentPath = Helper.GetExecutablePath();

            if (string.IsNullOrEmpty(currentPath) || string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryReplaceExecutable(currentPath, targetPath))
            {
                Logger.Error("Update failed: could not replace the installed executable.");

                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    Arguments = forwardedArguments,
                    FileName = targetPath,
                    UseShellExecute = true
                };

                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Logger.Error(e);

                return false;
            }

            return true;
        }

        private static string Decode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
            catch (FormatException)
            {
                return string.Empty;
            }
        }

        private static string Encode(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static bool IsApplyUpdateSwitch(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return false;

            return string.Equals(argument.TrimStart('-', '/'), Constants.App.CommandLineArgument.ApplyUpdate, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryReplaceExecutable(string sourcePath, string targetPath)
        {
            for (var attempt = 0; attempt < MoveRetryCount; attempt++)
            {
                try
                {
                    File.Copy(sourcePath, targetPath, true);

                    return true;
                }
                catch (Exception e)
                {
                    if (attempt == MoveRetryCount - 1)
                    {
                        Logger.Error(e);

                        return false;
                    }

                    Thread.Sleep(MoveRetryDelayMilliseconds);
                }
            }

            return false;
        }

        private static void WaitForProcessExit(int processId)
        {
            if (processId <= 0)
                return;

            try
            {
                using (var previous = System.Diagnostics.Process.GetProcessById(processId))
                {
                    if (!previous.WaitForExit(ApplyUpdateTimeoutMilliseconds))
                        Logger.Warning("Update: the previous instance did not exit in time.");
                }
            }
            catch (ArgumentException)
            {
                // The previous instance already terminated.
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }
        }

        #endregion Methods (Apply)

        #region Methods (Download)

        private static void OnFileDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                    throw new Exception("File download failed.", e.Error);

                if (e.Cancelled)
                    return;

                var updateInfo = (Tuple<string, string, string, Version, string>)e.UserState;
                var temp = updateInfo.Item1;
                var path = updateInfo.Item2;
                var exe = updateInfo.Item3;
                var newestVersion = updateInfo.Item4;
                var arguments = updateInfo.Item5;

                if (!File.Exists(temp))
                    return;

                // The download must be a valid assembly carrying the expected version...
                if (!AssemblyName.GetAssemblyName(temp).Version.Equals(newestVersion))
                {
                    Logger.Warning("Update rejected: downloaded file has an unexpected version.");

                    Helper.DeleteFile(temp);

                    Reset();

                    return;
                }

                // ...and it must be signed by the pinned publisher certificate.
                if (!IsTrustedUpdate(temp))
                {
                    Logger.Error("Update rejected: the downloaded file is not signed by the expected publisher.");

                    Helper.DeleteFile(temp);

                    Reset();

                    return;
                }

                // Hand over to the staged copy. It waits for this process to exit,
                // replaces the installed executable and restarts it. No shell is
                // involved, so command-line arguments cannot be interpreted as commands.
                var currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

                Process = new ProcessStartInfo
                {
                    Arguments = string.Format
                    (
                        CultureInfo.InvariantCulture,
                        "/{0} \"{1}\" {2} \"{3}\"",
                        Constants.App.CommandLineArgument.ApplyUpdate,
                        Encode(path),
                        currentProcessId,
                        Encode(arguments)
                    ),
                    FileName = temp,
                    UseShellExecute = true
                };

                Logger.Information(string.Format(CultureInfo.InvariantCulture, "Update {0} staged for installation.", newestVersion));

                App.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        private static void OnVersionCheckCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                    throw new Exception("Version check failed.", e.Error);

                if (e.Cancelled)
                    return;

                var assemblyInfo = e.Result;
                var assemblyVersionMatch = Regex.Match(assemblyInfo, @"AssemblyVersion\(""(.*)""\)\]");

                if (!assemblyVersionMatch.Success)
                    return;

                var newestVersion = Version.Parse(assemblyVersionMatch.Groups[1].Value);

                if (App.Version >= newestVersion)
                {
                    Reset();
                    return;
                }

                var exe = Path.GetFileName(App.Path);
                var temp = Path.Combine(Path.GetTempPath(), exe);

                Helper.DeleteFile(temp);

                var updateInfo = Tuple.Create(temp, App.Path, exe, newestVersion, (string)e.UserState);

                _client.DownloadFileAsync(Constants.App.Repository.LatestExeUri, temp, updateInfo);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        private static void Reset()
        {
            try
            {
                if (_client != null)
                    _client.Dispose();
            }
            finally
            {
                _client = null;
            }

            try
            {
                if (Process != null)
                    Process = null;
            }
            catch (Exception e)
            {
                // Clearing a reference cannot realistically fail; log and carry on.
                Logger.Debug(e);
            }
        }

        #endregion Methods (Download)

        #region Methods (Verification)

        /// <summary>
        /// Determines whether the specified file is signed by the pinned publisher certificate.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>True when the signature matches a pinned thumbprint; otherwise, false.</returns>
        public static bool IsTrustedUpdate(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                using (var certificate = X509Certificate.CreateFromSignedFile(path))
                {
                    if (certificate == null)
                        return false;

                    var thumbprint = certificate.GetCertHashString();

                    return IsPinnedThumbprint(thumbprint);
                }
            }
            catch (Exception e)
            {
                Logger.Debug("Update signature check failed: " + e.GetMessage());

                return false;
            }
        }

        private static bool IsPinnedThumbprint(string thumbprint)
        {
            if (string.IsNullOrEmpty(thumbprint))
                return false;

            return string.Equals(thumbprint, Constants.App.Certificate.Release.Thumbprint, StringComparison.OrdinalIgnoreCase)
                || string.Equals(thumbprint, Constants.App.Certificate.Test.Thumbprint, StringComparison.OrdinalIgnoreCase);
        }

        #endregion Methods (Verification)

        #region Methods (Update)

        /// <summary>
        /// Check for new version and update if available
        /// </summary>
        public static void Update(params string[] args)
        {
            try
            {
                // Auto-update disabled by the user, or the check interval has not elapsed.
                if (!Settings.AutoUpdate || DateTimeOffset.Now.Subtract(_lastCheck).TotalHours < Constants.App.AutoUpdateInterval)
                    return;

                _lastCheck = DateTimeOffset.Now;

                Reset();

                _client = new WebClient();
                _client.DownloadFileCompleted += new AsyncCompletedEventHandler(OnFileDownloadCompleted);
                _client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(OnVersionCheckCompleted);

                ServicePointManager.DefaultConnectionLimit = 10;
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; // TLS 1.2 | TLS 1.3

                _client.DownloadStringAsync(Constants.App.Repository.AssemblyInfoUri, string.Join("\n", args ?? new string[0]));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        #endregion Methods (Update)
    }
}