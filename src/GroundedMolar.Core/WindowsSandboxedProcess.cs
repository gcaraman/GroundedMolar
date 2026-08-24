using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace GroundedMolar.Core;

internal sealed record SandboxedProcessResult(int ExitCode, string StandardOutput, string StandardError, byte[] BinaryStandardOutput);

internal static class WindowsSandboxedProcess
{
    private const uint ExtendedStartupInfoPresent = 0x00080000, CreateNoWindow = 0x08000000, CreateSuspended = 4, CreateUnicodeEnvironment = 0x400;
    private const uint StartfUseStdHandles = 0x100, HandleFlagInherit = 1, ProcThreadAttributeHandleList = 0x00020002, ProcThreadAttributeSecurityCapabilities = 0x00020009;
    private const uint JobObjectExtendedLimitInformation = 9, JobObjectCpuRateControlInformation = 15;
    private const uint JobObjectLimitProcessTime = 2, JobObjectLimitJobTime = 4, JobObjectLimitActiveProcess = 8, JobObjectLimitProcessMemory = 0x100, JobObjectLimitKillOnJobClose = 0x2000;
    private const uint JobObjectCpuRateControlEnable = 1, JobObjectCpuRateControlHardCap = 4;
    private const uint GenericRead = 0x80000000, FileShareRead = 1, FileShareWrite = 2, OpenExisting = 3;
    private const uint WaitObject0 = 0, WaitTimeout = 258, Infinite = uint.MaxValue;
    private const int ErrorFileNotFound = 2;

    internal static SandboxedProcessResult Run(string executablePath, IReadOnlyList<string> arguments, string workingDirectory,
        TimeSpan wallClockLimit, TimeSpan cpuTimeLimit, long processMemoryLimitBytes, int diagnosticLimit = 16 * 1024,
        long maximumDirectoryBytes = 128L * 1024 * 1024, int? binaryStandardOutputLimit = null,
        string? writableOutputPath = null, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The Kraken sandbox requires Windows.");
        if (wallClockLimit <= TimeSpan.Zero || wallClockLimit.TotalMilliseconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(wallClockLimit));
        if (cpuTimeLimit <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(cpuTimeLimit));
        if (processMemoryLimitBytes <= 0) throw new ArgumentOutOfRangeException(nameof(processMemoryLimitBytes));
        if (diagnosticLimit < 0) throw new ArgumentOutOfRangeException(nameof(diagnosticLimit));
        if (binaryStandardOutputLimit is < 0) throw new ArgumentOutOfRangeException(nameof(binaryStandardOutputLimit));
        if (maximumDirectoryBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDirectoryBytes));
        if (writableOutputPath is not null)
        {
            writableOutputPath = Path.GetFullPath(writableOutputPath);
            var workingRoot = Path.GetFullPath(workingDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!writableOutputPath.StartsWith(workingRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(writableOutputPath))
                throw new ArgumentException("The writable sandbox output must be a pre-created file inside the working directory.", nameof(writableOutputPath));
        }

        var profileName = $"GroundedMolar.Decoder.{Guid.NewGuid():N}";
        var profileCreated = false;
        IntPtr sid = IntPtr.Zero, attributes = IntPtr.Zero, capabilitiesBuffer = IntPtr.Zero, handleBuffer = IntPtr.Zero, environment = IntPtr.Zero, job = IntPtr.Zero;
        IntPtr stdoutRead = IntPtr.Zero, stdoutWrite = IntPtr.Zero, stderrRead = IntPtr.Zero, stderrWrite = IntPtr.Zero, stdin = IntPtr.Zero;
        ProcessInformation process = default;
        try
        {
            var profileResult = CreateAppContainerProfile(profileName, "GroundedMolar decoder", "Ephemeral decoder sandbox", IntPtr.Zero, 0, out sid);
            if (profileResult == 0) profileCreated = true;
            else
            {
                if (DeriveAppContainerSidFromAppContainerName(profileName, out sid) != 0)
                    throw NativeFailure("Create/derive AppContainer SID", unchecked((int)profileResult));
            }
            GrantDirectoryAccess(workingDirectory, sid, writableOutputPath);
            CreateInheritedPipe(out stdoutRead, out stdoutWrite);
            CreateInheritedPipe(out stderrRead, out stderrWrite);
            var inheritable = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
            stdin = CreateFileW("NUL", GenericRead, FileShareRead | FileShareWrite, ref inheritable, OpenExisting, 0, IntPtr.Zero);
            if (stdin == new IntPtr(-1)) throw NativeFailure("CreateFile(NUL)");

            job = CreateJobObjectW(IntPtr.Zero, null);
            if (job == IntPtr.Zero) throw NativeFailure("CreateJobObject");
            ConfigureJob(job, cpuTimeLimit, processMemoryLimitBytes);

            nuint attributeBytes = 0;
            _ = InitializeProcThreadAttributeList(IntPtr.Zero, 2, 0, ref attributeBytes);
            attributes = Marshal.AllocHGlobal(checked((int)attributeBytes));
            if (!InitializeProcThreadAttributeList(attributes, 2, 0, ref attributeBytes)) throw NativeFailure("InitializeProcThreadAttributeList");
            capabilitiesBuffer = Marshal.AllocHGlobal(Marshal.SizeOf<SecurityCapabilities>());
            Marshal.StructureToPtr(new SecurityCapabilities { AppContainerSid = sid }, capabilitiesBuffer, false);
            if (!UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributeSecurityCapabilities, capabilitiesBuffer,
                    (nuint)Marshal.SizeOf<SecurityCapabilities>(), IntPtr.Zero, IntPtr.Zero)) throw NativeFailure("UpdateProcThreadAttribute(SecurityCapabilities)");
            var inheritedHandles = new[] { stdin, stdoutWrite, stderrWrite };
            handleBuffer = Marshal.AllocHGlobal(IntPtr.Size * inheritedHandles.Length);
            Marshal.Copy(inheritedHandles, 0, handleBuffer, inheritedHandles.Length);
            if (!UpdateProcThreadAttribute(attributes, 0, ProcThreadAttributeHandleList, handleBuffer,
                    (nuint)(IntPtr.Size * inheritedHandles.Length), IntPtr.Zero, IntPtr.Zero)) throw NativeFailure("UpdateProcThreadAttribute(HandleList)");

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Cb = Marshal.SizeOf<StartupInfoEx>(), Flags = StartfUseStdHandles, StdInput = stdin, StdOutput = stdoutWrite, StdError = stderrWrite },
                AttributeList = attributes
            };
            var commandLine = new StringBuilder(Quote(executablePath));
            foreach (var argument in arguments) commandLine.Append(' ').Append(Quote(argument));
            environment = BuildEnvironment(workingDirectory);
            for (var attempt = 0; ; attempt++)
            {
                if (CreateProcessW(executablePath, commandLine, IntPtr.Zero, IntPtr.Zero, true,
                        ExtendedStartupInfoPresent | CreateNoWindow | CreateSuspended | CreateUnicodeEnvironment,
                        environment, workingDirectory, ref startup, out process)) break;
                var error = Marshal.GetLastWin32Error();
                if (error != ErrorFileNotFound || attempt >= 49) throw NativeFailure("CreateProcess(AppContainer)", error);
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }
            if (!AssignProcessToJobObject(job, process.Process)) throw NativeFailure("AssignProcessToJobObject");
            Close(ref stdoutWrite); Close(ref stderrWrite); Close(ref stdin);
            using var stdoutStream = new FileStream(new SafeFileHandle(stdoutRead, true), FileAccess.Read, 4096, false); stdoutRead = IntPtr.Zero;
            using var stderrStream = new FileStream(new SafeFileHandle(stderrRead, true), FileAccess.Read, 4096, false); stderrRead = IntPtr.Zero;
            var stdoutTextTask = binaryStandardOutputLimit is null ? ReadBounded(stdoutStream, diagnosticLimit) : null;
            var stdoutBinaryTask = binaryStandardOutputLimit is { } binaryLimit ? ReadBoundedBytes(stdoutStream, binaryLimit) : null;
            var stderrTask = ReadBounded(stderrStream, diagnosticLimit);
            if (ResumeThread(process.Thread) == uint.MaxValue) throw NativeFailure("ResumeThread");
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            uint wait;
            while ((wait = WaitForSingleObject(process.Process, 25)) == WaitTimeout)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    TerminateAndDrain(job, process.Process, stdoutTextTask, stdoutBinaryTask, stderrTask, 0xDEAD0003);
                    cancellationToken.ThrowIfCancellationRequested();
                }
                if (DirectoryBytes(workingDirectory) > maximumDirectoryBytes)
                {
                    TerminateAndDrain(job, process.Process, stdoutTextTask, stdoutBinaryTask, stderrTask, 0xDEAD0002);
                    throw new InvalidDataException($"Sandboxed decoder exceeded its {maximumDirectoryBytes:N0}-byte private-directory quota.");
                }
                if (elapsed.Elapsed >= wallClockLimit)
                {
                    TerminateAndDrain(job, process.Process, stdoutTextTask, stdoutBinaryTask, stderrTask, 0xDEAD0001);
                    throw new TimeoutException($"Sandboxed Kraken decompression exceeded {wallClockLimit.TotalSeconds:N0} seconds.");
                }
            }
            if (wait != WaitObject0) throw NativeFailure("WaitForSingleObject");
            Task.WaitAll(new Task[] { stdoutTextTask is not null ? stdoutTextTask : stdoutBinaryTask!, stderrTask });
            if (DirectoryBytes(workingDirectory) > maximumDirectoryBytes)
                throw new InvalidDataException($"Sandboxed decoder exceeded its {maximumDirectoryBytes:N0}-byte private-directory quota.");
            if (!GetExitCodeProcess(process.Process, out var exitCode)) throw NativeFailure("GetExitCodeProcess");
            return new SandboxedProcessResult(unchecked((int)exitCode), stdoutTextTask?.Result ?? "", stderrTask.Result, stdoutBinaryTask?.Result ?? []);
        }
        finally
        {
            if (process.Process != IntPtr.Zero) CloseHandle(process.Process);
            if (process.Thread != IntPtr.Zero) CloseHandle(process.Thread);
            Close(ref stdoutRead); Close(ref stdoutWrite); Close(ref stderrRead); Close(ref stderrWrite); Close(ref stdin);
            if (job != IntPtr.Zero) CloseHandle(job);
            if (attributes != IntPtr.Zero) { DeleteProcThreadAttributeList(attributes); Marshal.FreeHGlobal(attributes); }
            if (capabilitiesBuffer != IntPtr.Zero) Marshal.FreeHGlobal(capabilitiesBuffer);
            if (handleBuffer != IntPtr.Zero) Marshal.FreeHGlobal(handleBuffer);
            if (environment != IntPtr.Zero) Marshal.FreeHGlobal(environment);
            if (sid != IntPtr.Zero) FreeSid(sid);
            if (profileCreated) _ = DeleteAppContainerProfile(profileName);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void GrantDirectoryAccess(string directory, IntPtr sidPointer, string? writableOutputPath)
    {
        var info = new DirectoryInfo(directory);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sidPointer), FileSystemRights.ReadAndExecute,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        info.SetAccessControl(security);
        foreach (var file in info.EnumerateFiles())
        {
            var fileSecurity = file.GetAccessControl();
            var rights = writableOutputPath is not null && file.FullName.Equals(writableOutputPath, StringComparison.OrdinalIgnoreCase)
                ? FileSystemRights.Modify
                : FileSystemRights.ReadAndExecute | FileSystemRights.Read;
            fileSecurity.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sidPointer), rights,
                AccessControlType.Allow));
            file.SetAccessControl(fileSecurity);
        }
    }

    private static void ConfigureJob(IntPtr job, TimeSpan cpuTime, long memory)
    {
        SetJob(job, JobObjectExtendedLimitInformation, new JobObjectExtendedLimitInformationValue
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                PerProcessUserTimeLimit = cpuTime.Ticks,
                PerJobUserTimeLimit = cpuTime.Ticks,
                LimitFlags = JobObjectLimitKillOnJobClose | JobObjectLimitActiveProcess | JobObjectLimitProcessMemory | JobObjectLimitProcessTime | JobObjectLimitJobTime,
                ActiveProcessLimit = 1
            },
            ProcessMemoryLimit = checked((nuint)memory)
        });
        SetJob(job, JobObjectCpuRateControlInformation, new JobObjectCpuRateControlInformationValue
            { ControlFlags = JobObjectCpuRateControlEnable | JobObjectCpuRateControlHardCap, CpuRate = 2500 });
    }

    private static void SetJob<T>(IntPtr job, uint informationClass, T value) where T : struct
    {
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        try { Marshal.StructureToPtr(value, pointer, false); if (!SetInformationJobObject(job, informationClass, pointer, (uint)Marshal.SizeOf<T>())) throw NativeFailure("SetInformationJobObject"); }
        finally { Marshal.FreeHGlobal(pointer); }
    }

    private static void CreateInheritedPipe(out IntPtr read, out IntPtr write)
    {
        var security = new SecurityAttributes { Length = Marshal.SizeOf<SecurityAttributes>(), InheritHandle = true };
        if (!CreatePipe(out read, out write, ref security, 0)) throw NativeFailure("CreatePipe");
        if (!SetHandleInformation(read, HandleFlagInherit, 0)) throw NativeFailure("SetHandleInformation");
    }

    private static async Task<string> ReadBounded(Stream stream, int limit)
    {
        using var retained = new MemoryStream(Math.Min(limit, 4096));
        var buffer = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0) break;
            var keep = Math.Min(count, limit - checked((int)retained.Length));
            if (keep > 0) retained.Write(buffer, 0, keep);
        }
        return Encoding.UTF8.GetString(retained.ToArray());
    }

    private static async Task<byte[]> ReadBoundedBytes(Stream stream, int limit)
    {
        using var retained = new MemoryStream(Math.Min(limit, 64 * 1024));
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var count = await stream.ReadAsync(buffer);
            if (count == 0) break;
            var keep = Math.Min(count, limit - checked((int)retained.Length));
            if (keep > 0) retained.Write(buffer, 0, keep);
        }
        return retained.ToArray();
    }

    private static long DirectoryBytes(string directory)
    {
        long total = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            total = checked(total + new FileInfo(path).Length);
        return total;
    }

    private static void TerminateAndDrain(IntPtr job, IntPtr process, Task<string>? stdoutText, Task<byte[]>? stdoutBinary, Task<string> stderr, uint exitCode)
    {
        _ = TerminateJobObject(job, exitCode);
        _ = WaitForSingleObject(process, Infinite);
        Task.WaitAll(new Task[] { stdoutText is not null ? stdoutText : stdoutBinary!, stderr });
    }

    private static IntPtr BuildEnvironment(string temporaryDirectory)
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var system = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ALLUSERSPROFILE"] = programData,
            ["APPDATA"] = roamingAppData,
            ["LOCALAPPDATA"] = localAppData,
            ["OS"] = "Windows_NT",
            ["Path"] = system,
            ["ProgramData"] = programData,
            ["ProgramFiles"] = programFiles,
            ["SystemDrive"] = Path.GetPathRoot(windows) ?? "C:\\",
            ["SystemRoot"] = windows,
            ["TEMP"] = temporaryDirectory,
            ["TMP"] = temporaryDirectory,
            ["USERPROFILE"] = userProfile,
            ["WINDIR"] = windows
        };
        var block = string.Join('\0', values.Select(pair => $"{pair.Key}={pair.Value}")) + "\0\0";
        var pointer = Marshal.AllocHGlobal(block.Length * sizeof(char));
        Marshal.Copy(block.ToCharArray(), 0, pointer, block.Length);
        return pointer;
    }

    private static string Quote(string value)
    {
        if (value.Length > 0 && !value.Any(c => char.IsWhiteSpace(c) || c == '"')) return value;
        var result = new StringBuilder("\""); var slashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { slashes++; continue; }
            if (c == '"') result.Append('\\', slashes * 2 + 1).Append(c); else result.Append('\\', slashes).Append(c);
            slashes = 0;
        }
        return result.Append('\\', slashes * 2).Append('"').ToString();
    }

    private static Win32Exception NativeFailure(string operation, int? error = null)
    {
        var nativeError = error ?? Marshal.GetLastWin32Error();
        return new Win32Exception(nativeError, $"{operation} failed with Windows error {nativeError}");
    }
    private static void Close(ref IntPtr value) { if (value != IntPtr.Zero && value != new IntPtr(-1)) CloseHandle(value); value = IntPtr.Zero; }

    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr SecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] public bool InheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int Cb; public string? Reserved; public string? Desktop; public string? Title; public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags; public ushort ShowWindow, Reserved2; public IntPtr Reserved2Pointer, StdInput, StdOutput, StdError; }
    [StructLayout(LayoutKind.Sequential)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr AttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityCapabilities { public IntPtr AppContainerSid, Capabilities; public uint CapabilityCount, Reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct JobObjectBasicLimitInformation { public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags; public nuint MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters { public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount; }
    [StructLayout(LayoutKind.Sequential)] private struct JobObjectExtendedLimitInformationValue { public JobObjectBasicLimitInformation BasicLimitInformation; public IoCounters IoInfo; public nuint ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed; }
    [StructLayout(LayoutKind.Sequential)] private struct JobObjectCpuRateControlInformationValue { public uint ControlFlags, CpuRate; }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern uint CreateAppContainerProfile(string name, string displayName, string description, IntPtr capabilities, uint count, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern int DeriveAppContainerSidFromAppContainerName(string name, out IntPtr sid);
    [DllImport("userenv.dll", CharSet = CharSet.Unicode)] private static extern uint DeleteAppContainerProfile(string name);
    [DllImport("advapi32.dll")] private static extern IntPtr FreeSid(IntPtr sid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateJobObjectW(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(IntPtr job, uint informationClass, IntPtr information, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool TerminateJobObject(IntPtr job, uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreatePipe(out IntPtr read, out IntPtr write, ref SecurityAttributes attributes, uint size);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr CreateFileW(string name, uint access, uint share, ref SecurityAttributes attributes, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, uint flags, ref nuint size);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, nuint attribute, IntPtr value, nuint size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateProcessW(string applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string currentDirectory, ref StartupInfoEx startup, out ProcessInformation process);
}
