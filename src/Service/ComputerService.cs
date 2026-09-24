using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Computer Service
    /// </summary>
    public class ComputerService : IComputerService
    {
        #region Fields

        private Memory _memory = new Memory(new Structs.Windows.MemoryStatusEx());
        private OperatingSystem _operatingSystem;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the memory info (RAM)
        /// </summary>
        public Memory Memory
        {
            get
            {
                try
                {
                    var memoryStatusEx = new Structs.Windows.MemoryStatusEx();

                    if (!NativeMethods.GlobalMemoryStatusEx(memoryStatusEx))
                        Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));

                    _memory = new Memory(memoryStatusEx);
                }
                catch (Exception e)
                {
                    Logger.Error(e);
                }

                return _memory;
            }
        }

        /// <summary>
        /// Gets the operating system info
        /// </summary>
        public OperatingSystem OperatingSystem
        {
            get
            {
                if (_operatingSystem == null)
                {
                    var operatingSystem = Environment.OSVersion;

                    _operatingSystem = new OperatingSystem
                    {
                        Is64Bit = Environment.Is64BitOperatingSystem,
                        IsWindows7OrGreater = (operatingSystem.Version.Major > 6) || (operatingSystem.Version.Major == 6 && operatingSystem.Version.Minor >= 1),
                        IsWindows8OrGreater = operatingSystem.Version.Major >= 6.2,
                        IsWindows81OrGreater = operatingSystem.Version.Major >= 6.3,
                        IsWindowsVistaOrGreater = operatingSystem.Version.Major >= 6,
                        IsWindowsXpOrGreater = operatingSystem.Version.Major >= 5.1
                    };
                }

                return _operatingSystem;
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when [optimize progress is update].
        /// </summary>
        public event Action<byte, string> OnOptimizeProgressUpdate;

        #endregion

        #region Methods (Computer)

        private static SafeFileHandle OpenVolumeHandle(string driveLetter)
        {
            if (string.IsNullOrWhiteSpace(driveLetter))
                return null;

            return NativeMethods.CreateFile
            (
                @"\\.\" + driveLetter.TrimEnd(':', '\\') + ":",
                FileAccess.ReadWrite,
                FileShare.Read | FileShare.Write,
                IntPtr.Zero,
                FileMode.Open,
                (int)FileAttributes.Normal | Constants.Windows.File.FlagsNoBuffering,
                IntPtr.Zero
            );
        }

        /// <summary>
        /// Increase the Privilege using a privilege name
        /// </summary>
        /// <param name="privilegeName">The name of the privilege that needs to be increased</param>
        /// <returns></returns>
        private bool SetIncreasePrivilege(string privilegeName)
        {
            var result = false;

            using (var current = WindowsIdentity.GetCurrent(TokenAccessLevels.Query | TokenAccessLevels.AdjustPrivileges))
            {
                Structs.Windows.TokenPrivileges newState;
                newState.Count = 1;
                newState.Luid = 0L;
                newState.Attr = Constants.Windows.PrivilegeAttribute.Enabled;

                // Retrieves the uid used on a specified system to locally represent the specified privilege name
                if (NativeMethods.LookupPrivilegeValue(null, privilegeName, ref newState.Luid))
                {
                    // Enables or disables privileges in a specified access token
                    result = NativeMethods.AdjustTokenPrivileges(current.Token, false, ref newState, 0, IntPtr.Zero, IntPtr.Zero);
                }

                if (!result)
                    Logger.Error(new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return result;
        }

        #endregion

        #region Methods (Memory)

        /// <summary>
        /// Optimize the computer
        /// </summary>
        /// <param name="reason">Optimization reason</param>
        /// <param name="areas">Memory areas</param>
        public void Optimize(Enums.Memory.Optimization.Reason reason, Enums.Memory.Areas areas)
        {
            if (areas == Enums.Memory.Areas.None)
                return;

            var errorRuntime = new TimeSpan();
            var infoRuntime = new TimeSpan();
            var optimizationReason = reason.GetString();
            var stopwatch = new Stopwatch();
            var value = (byte)0;

            var error = new LogOptimizationData { Reason = optimizationReason };
            var info = new LogOptimizationData { Reason = optimizationReason };

            foreach (var step in GetOptimizationSteps(areas))
            {
                if (OnOptimizeProgressUpdate != null)
                {
                    value++;
                    OnOptimizeProgressUpdate(value, step.Name);
                }

                stopwatch.Restart();

                try
                {
                    step.Optimize();

                    info.MemoryAreas.Add(CreateOptimizationResult(step.Name, stopwatch.Elapsed, null));
                    infoRuntime = infoRuntime.Add(stopwatch.Elapsed);
                }
                catch (Exception e)
                {
                    Logger.Debug(e);

                    error.MemoryAreas.Add(CreateOptimizationResult(step.Name, stopwatch.Elapsed, e.GetMessage()));
                    errorRuntime = errorRuntime.Add(stopwatch.Elapsed);
                }
            }

            // Garbage Collector: always runs last and never fails the optimization.
            if (OnOptimizeProgressUpdate != null)
            {
                value++;
                OnOptimizeProgressUpdate(value, Localizer.String.GarbageCollector);
            }

            try
            {
                App.ReleaseMemory();
            }
            catch (Exception e)
            {
                Logger.Debug(e);
            }

            // Log
            try
            {
                // Info
                if (info.MemoryAreas.Any())
                {
                    info.Duration = string.Format(Localizer.Culture, "{0:0.0} {1}", infoRuntime.TotalSeconds, Localizer.String.Seconds.ToLower(Localizer.Culture));

                    Logger.Log(new Log(Enums.Log.Levels.Information, Localizer.String.MemoryOptimized, info));
                }

                // Error
                if (error.MemoryAreas.Any())
                {
                    error.Duration = string.Format(Localizer.Culture, "{0:0.0} {1}", errorRuntime.TotalSeconds, Localizer.String.Seconds.ToLower(Localizer.Culture));

                    Logger.Log(new Log(Enums.Log.Levels.Error, Localizer.String.Invalid, error));
                }
            }
            catch (Exception e)
            {
                Logger.Debug(e);
            }
        }

        /// <summary>
        /// Builds the ordered list of optimization steps selected by the specified areas.
        /// </summary>
        /// <param name="areas">The memory areas to optimize.</param>
        /// <returns>The steps to run, in execution order.</returns>
        private List<OptimizationStep> GetOptimizationSteps(Enums.Memory.Areas areas)
        {
            var steps = new List<OptimizationStep>();

            if ((areas & Enums.Memory.Areas.WorkingSet) != 0)
                steps.Add(new OptimizationStep(Localizer.String.WorkingSet, OptimizeWorkingSet));

            if ((areas & Enums.Memory.Areas.SystemFileCache) != 0)
                steps.Add(new OptimizationStep(Localizer.String.SystemFileCache, OptimizeSystemFileCache));

            if ((areas & Enums.Memory.Areas.ModifiedPageList) != 0)
                steps.Add(new OptimizationStep(Localizer.String.ModifiedPageList, OptimizeModifiedPageList));

            // The low priority standby list is a distinct operation with its own label.
            if ((areas & (Enums.Memory.Areas.StandbyList | Enums.Memory.Areas.StandbyListLowPriority)) != 0)
            {
                var lowPriority = (areas & Enums.Memory.Areas.StandbyListLowPriority) != 0;
                var name = lowPriority ? Localizer.String.StandbyListLowPriority : Localizer.String.StandbyList;

                steps.Add(new OptimizationStep(name, () => OptimizeStandbyList(lowPriority)));
            }

            if ((areas & Enums.Memory.Areas.CombinedPageList) != 0)
                steps.Add(new OptimizationStep(Localizer.String.CombinedPageList, OptimizeCombinedPageList));

            if ((areas & Enums.Memory.Areas.RegistryCache) != 0)
                steps.Add(new OptimizationStep(Localizer.String.RegistryCache, OptimizeRegistryCache));

            if ((areas & Enums.Memory.Areas.ModifiedFileCache) != 0)
                steps.Add(new OptimizationStep(Localizer.String.ModifiedFileCache, OptimizeModifiedFileCache));

            return steps;
        }

        /// <summary>
        /// Creates a per-area optimization result entry.
        /// </summary>
        /// <param name="name">The localized area name.</param>
        /// <param name="elapsed">The elapsed time.</param>
        /// <param name="error">The error message, if any.</param>
        /// <returns>The result entry.</returns>
        private static LogOptimizationDataMemoryArea CreateOptimizationResult(string name, TimeSpan elapsed, string error)
        {
            return new LogOptimizationDataMemoryArea
            {
                Name = name,
                Duration = string.Format(Localizer.Culture, "{0:0.0} {1}", elapsed.TotalSeconds, Localizer.String.Seconds.ToLower(Localizer.Culture)),
                Error = error
            };
        }

        /// <summary>
        /// Describes one optimization step: its label and the operation to run.
        /// </summary>
        private sealed class OptimizationStep
        {
            private readonly Action _optimize;

            public OptimizationStep(string name, Action optimize)
            {
                Name = name;
                _optimize = optimize;
            }

            /// <summary>
            /// Gets the localized step name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Runs the step.
            /// </summary>
            public void Optimize()
            {
                _optimize();
            }
        }

        #endregion


        /// <summary>
        /// Optimize the combined page list.
        /// </summary>
        private void OptimizeCombinedPageList()
        {
            // Windows minimum version
            if (!OperatingSystem.HasCombinedPageList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.CombinedPageList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            var handle = default(GCHandle);

            try
            {
                var memoryCombineInformationEx = new Structs.Windows.MemoryCombineInformationEx();

                handle = GCHandle.Alloc(memoryCombineInformationEx, GCHandleType.Pinned);

                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemCombinePhysicalMemoryInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryCombineInformationEx)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the modified file cache.
        /// </summary>
        private void OptimizeModifiedFileCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasModifiedFileCache)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.ModifiedFileCache));

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive == null || drive.DriveType != DriveType.Fixed || string.IsNullOrWhiteSpace(drive.Name))
                    continue;

                using (var handle = OpenVolumeHandle(drive.Name))
                {
                    if (handle == null || handle.IsInvalid)
                        continue;

                    int bytesReturned;

                    if (OperatingSystem.IsWindows7OrGreater)
                    {
                        try
                        {
                            var buffer = Marshal.AllocHGlobal(1);

                            try
                            {
                                if (!NativeMethods.DeviceIoControl(
                                    handle,
                                    Constants.Windows.Drive.IoControlResetWriteOrder,
                                    buffer,
                                    1,
                                    IntPtr.Zero,
                                    0,
                                    out bytesReturned,
                                    IntPtr.Zero))
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(buffer);
                            }
                        }
                        catch (Exception e)
                        {
                            // Resetting the write order is best-effort: some volumes do
                            // not support it, and the flush below still runs.
                            Logger.Debug(e, "Failed to reset the write order for the volume.");
                        }

                        if (OperatingSystem.IsWindows8OrGreater)
                        {
                            try
                            {
                                if (!NativeMethods.DeviceIoControl(
                                    handle,
                                    Constants.Windows.Drive.FsctlDiscardVolumeCache,
                                    IntPtr.Zero,
                                    0,
                                    IntPtr.Zero,
                                    0,
                                    out bytesReturned,
                                    IntPtr.Zero))
                                {
                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                }
                            }
                            catch (Exception e)
                            {
                                // Discarding the volume cache is best-effort.
                                Logger.Debug(e, "Failed to discard the volume cache.");
                            }
                        }
                    }

                    if (!NativeMethods.FlushFileBuffers(handle))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        /// <summary>
        /// Optimize the modified page list.
        /// </summary>
        private void OptimizeModifiedPageList()
        {
            // Windows minimum version
            if (!OperatingSystem.HasModifiedPageList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.ModifiedPageList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            var handle = GCHandle.Alloc(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList, GCHandleType.Pinned);

            try
            {
                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(Constants.Windows.SystemMemoryListCommand.MemoryFlushModifiedList)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the registry cache.
        /// </summary>
        private void OptimizeRegistryCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasRegistryHive)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.RegistryCache));

            if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemRegistryReconciliationInformation, IntPtr.Zero, 0) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Optimize the standby list.
        /// </summary>
        /// <param name="lowPriority">if set to <c>true</c> [low priority].</param>
        private void OptimizeStandbyList(bool lowPriority = false)
        {
            // Windows minimum version
            if (!OperatingSystem.HasStandbyList)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.StandbyList));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeProfSingleProcessName));

            object memoryPurgeStandbyList = lowPriority ? Constants.Windows.SystemMemoryListCommand.MemoryPurgeLowPriorityStandbyList : Constants.Windows.SystemMemoryListCommand.MemoryPurgeStandbyList;
            var handle = GCHandle.Alloc(memoryPurgeStandbyList, GCHandleType.Pinned);

            try
            {
                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(memoryPurgeStandbyList)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }
        }

        /// <summary>
        /// Optimize the system file cache.
        /// </summary>
        private void OptimizeSystemFileCache()
        {
            // Windows minimum version
            if (!OperatingSystem.HasSystemFileCache)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.SystemFileCache));

            // Check privilege
            if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeIncreaseQuotaName))
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeIncreaseQuotaName));

            var handle = default(GCHandle);

            try
            {
                object systemFileCacheInformation;

                if (OperatingSystem.Is64Bit)
                    systemFileCacheInformation = new Structs.Windows.SystemFileCacheInformation64 { MinimumWorkingSet = -1L, MaximumWorkingSet = -1L };
                else
                    systemFileCacheInformation = new Structs.Windows.SystemFileCacheInformation32 { MinimumWorkingSet = int.MaxValue, MaximumWorkingSet = int.MaxValue };

                handle = GCHandle.Alloc(systemFileCacheInformation, GCHandleType.Pinned);

                if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemFileCacheInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(systemFileCacheInformation)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                try
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
                catch (InvalidOperationException)
                {
                    // ignored
                }
            }

            var fileCacheSize = IntPtr.Subtract(IntPtr.Zero, 1); // Flush

            if (!NativeMethods.SetSystemFileCacheSize(fileCacheSize, fileCacheSize, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Optimize the working set.
        /// </summary>
        private void OptimizeWorkingSet()
        {
            // Windows minimum version
            if (!OperatingSystem.HasWorkingSet)
                throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorMemoryAreaOptimizationNotSupported, Localizer.String.WorkingSet));

            var errors = new StringBuilder();

            if (Settings.ProcessExclusionList.Any())
            {
                // Check privilege
                if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeDebugName))
                    throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeDebugName));

                var processes = Process.GetProcesses().Where(process => process != null && !Settings.ProcessExclusionList.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase));

                foreach (var process in processes)
                {
                    using (process)
                    {
                        try
                        {
                            if (!NativeMethods.EmptyWorkingSet(process.Handle))
                                throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                        catch (InvalidOperationException)
                        {
                            // ignored
                        }
                        catch (Win32Exception e)
                        {
                            if (e.NativeErrorCode != Constants.Windows.SystemErrorCode.ErrorAccessDenied)
                                errors.Append(string.Format(Localizer.Culture, "{0}: {1} | ", process.ProcessName, e.GetMessage()));
                        }
                    }
                }

                if (errors.Length > 3)
                    errors.Remove(errors.Length - 3, 3);
            }
            else
            {
                // Check privilege
                if (!SetIncreasePrivilege(Constants.Windows.Privilege.SeProfSingleProcessName))
                    throw new Exception(string.Format(Localizer.Culture, Localizer.String.ErrorAdminPrivilegeRequired, Constants.Windows.Privilege.SeDebugName));

                var handle = GCHandle.Alloc(Constants.Windows.SystemMemoryListCommand.MemoryEmptyWorkingSets, GCHandleType.Pinned);

                try
                {
                    if (NativeMethods.NtSetSystemInformation(Constants.Windows.SystemInformationClass.SystemMemoryListInformation, handle.AddrOfPinnedObject(), (uint)Marshal.SizeOf(Constants.Windows.SystemMemoryListCommand.MemoryEmptyWorkingSets)) != Constants.Windows.SystemErrorCode.ErrorSuccess)
                    {
                        var e = new Win32Exception(Marshal.GetLastWin32Error());

                        if (e != null)
                        {
                            if (errors.Length > 0)
                                errors.Append(" | ");

                            errors.Append(e.GetMessage());
                        }
                    }
                }
                finally
                {
                    try
                    {
                        if (handle.IsAllocated)
                            handle.Free();
                    }
                    catch (InvalidOperationException)
                    {
                        // ignored
                    }
                }
            }

            if (errors.Length > 0)
                throw new Exception(errors.ToString());
        }
    }
}
