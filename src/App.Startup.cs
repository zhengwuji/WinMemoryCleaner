using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Windows Memory Cleaner - Windows integration applied at startup (scheduled task and process priority).
    /// </summary>
    public partial class App
    {
        #region Methods

        /// <summary>
        /// Runs the app on startup
        /// </summary>
        /// <param name="enable">if set to <c>true</c> [enable].</param>
        public static void RunOnStartup(bool enable)
        {
            try
            {
                if (enable)
                {
                    var isTaskCreated = false;

                    try
                    {
                        var taskXml = string.Format
                            (
                                CultureInfo.InvariantCulture,
                                @"<?xml version=""1.0"" encoding=""UTF-16""?>
                                <Task version=""1.2""
	                                xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
	                                <RegistrationInfo>
		                                <Author>{3}</Author>
		                                <Description>Runs {0} at logon.</Description>
		                                <Date>{4}</Date>
	                                </RegistrationInfo>
	                                <Triggers>
		                                <LogonTrigger>
			                                <Enabled>true</Enabled>
		                                </LogonTrigger>
	                                </Triggers>
	                                <Principals>
		                                <Principal id=""Author"">
			                                <UserId>{2}</UserId>
			                                <LogonType>InteractiveToken</LogonType>
			                                <RunLevel>HighestAvailable</RunLevel>
		                                </Principal>
	                                </Principals>
	                                <Settings>
		                                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
		                                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
		                                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
		                                <AllowHardTerminate>true</AllowHardTerminate>
		                                <StartWhenAvailable>true</StartWhenAvailable>
		                                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
		                                <IdleSettings>
			                                <WaitTimeout>PT10M</WaitTimeout>
			                                <StopOnIdleEnd>false</StopOnIdleEnd>
			                                <RestartOnIdle>false</RestartOnIdle>
		                                </IdleSettings>
		                                <AllowStartOnDemand>true</AllowStartOnDemand>
		                                <Enabled>true</Enabled>
		                                <Hidden>false</Hidden>
		                                <RunOnlyIfIdle>false</RunOnlyIfIdle>
		                                <WakeToRun>false</WakeToRun>
		                                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
		                                <Priority>7</Priority>
	                                </Settings>
	                                <Actions Context=""Author"">
		                                <Exec>
			                                <Command>""{1}""</Command>
		                                </Exec>
	                                </Actions>
                                </Task>",
                                Constants.App.Title,
                                Path,
                                WindowsIdentity.GetCurrent().User.Value,
                                string.Format(CultureInfo.InvariantCulture, "WMC {0} ({1})", string.Format(Localizer.Culture, Constants.App.VersionFormat, Version.Major, Version.Minor, Version.Build), Environment.UserName),
                                DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)
                            );

                                var tempXmlFile = System.IO.Path.GetTempFileName();

                        File.WriteAllText(tempXmlFile, taskXml);

                        var createStartInfo = new ProcessStartInfo("schtasks")
                        {
                            Arguments = string.Format(CultureInfo.InvariantCulture, @"/CREATE /F /TN ""{0}"" /XML ""{1}""", Constants.App.Title, tempXmlFile),
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardError = true
                        };

                        using (var createProcess = Process.Start(createStartInfo))
                        {
                            var errorMessage = createProcess.StandardError.ReadToEnd();
                            createProcess.WaitForExit();

                            if (createProcess.ExitCode == Constants.Windows.SystemErrorCode.ErrorSuccess)
                                isTaskCreated = true;
                            else
                                Logger.Error(string.Format(Localizer.Culture, "XML task creation failed (will attempt fallback). Error: {0}", errorMessage));
                        }

                        Helper.DeleteFile(tempXmlFile);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format(Localizer.Culture, "An exception occurred during XML task creation (will attempt fallback): {0}", ex.GetMessage()));
                    }

                    if (!isTaskCreated)
                    {
                        Logger.Information("Attempting basic fallback method to create startup task.");

                        var createStartInfo = new ProcessStartInfo("schtasks")
                        {
                            Arguments = string.Format(CultureInfo.InvariantCulture, @"/CREATE /F /SC ONLOGON /TN ""{0}"" /TR ""{1}"" /RU ""{2}""", Constants.App.Title, Path, Environment.UserName),
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardError = true
                        };

                        using (var createProcess = Process.Start(createStartInfo))
                        {
                            var errorMessage = createProcess.StandardError.ReadToEnd();
                            createProcess.WaitForExit();

                            if (createProcess.ExitCode != Constants.Windows.SystemErrorCode.ErrorSuccess)
                                Logger.Error(string.Format(Localizer.Culture, "Fallback task creation also failed for '{0}'. Error: {1}", Constants.App.Title, errorMessage));
                        }
                    }
                }
                else
                {
                    var deleteStartInfo = new ProcessStartInfo("schtasks")
                    {
                        Arguments = string.Format(CultureInfo.InvariantCulture, @"/DELETE /F /TN ""{0}""", Constants.App.Title),
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using (var deleteProcess = Process.Start(deleteStartInfo))
                    {
                        deleteProcess.WaitForExit();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(string.Format(Localizer.Culture, "An error occurred while managing the scheduled task for app startup. Error: {0}", e.GetMessage()));
            }
        }

        /// <summary>
        /// Sets the app priority for the Windows
        /// </summary>
        public static void SetPriority(Enums.Priority priority)
        {
            bool priorityBoostEnabled;
            ProcessPriorityClass processPriorityClass;
            ThreadPriority threadPriority;
            ThreadPriorityLevel threadPriorityLevel;

            switch (priority)
            {
                case Enums.Priority.Low:
                    priorityBoostEnabled = false;
                    processPriorityClass = ProcessPriorityClass.Idle;
                    threadPriority = ThreadPriority.Lowest;
                    threadPriorityLevel = ThreadPriorityLevel.Idle;
                    break;

                case Enums.Priority.Normal:
                    priorityBoostEnabled = true;
                    processPriorityClass = ProcessPriorityClass.Normal;
                    threadPriority = ThreadPriority.Normal;
                    threadPriorityLevel = ThreadPriorityLevel.Normal;
                    break;

                case Enums.Priority.High:
                    priorityBoostEnabled = true;
                    processPriorityClass = ProcessPriorityClass.High;
                    threadPriority = ThreadPriority.Highest;
                    threadPriorityLevel = ThreadPriorityLevel.Highest;
                    break;

                default:
                    throw new NotImplementedException();
            }

            try
            {
                Thread.CurrentThread.Priority = threadPriority;
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to set the thread priority.");
            }

            try
            {
                var process = Process.GetCurrentProcess();

                try
                {
                    process.PriorityBoostEnabled = priorityBoostEnabled;
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Failed to set PriorityBoostEnabled on the process.");
                }

                try
                {
                    process.PriorityClass = processPriorityClass;
                }
                catch (Exception e)
                {
                    Logger.Debug(e, "Failed to set the process priority class.");
                }

                foreach (ProcessThread thread in process.Threads)
                {
                    try
                    {
                        thread.PriorityBoostEnabled = priorityBoostEnabled;
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e, "Failed to set PriorityBoostEnabled on a thread.");
                    }

                    try
                    {
                        thread.PriorityLevel = threadPriorityLevel;
                    }
                    catch (Exception e)
                    {
                        Logger.Debug(e, "Failed to set the priority level on a thread.");
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e, "Failed to apply the process and thread priorities.");
            }
        }


        #endregion
    }
}
