using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// Keep the privately launched Codex process tree within this app's lifetime.
internal sealed class ProcessJob : IDisposable
{
    IntPtr handle;
    [StructLayout(LayoutKind.Sequential)] struct Basic
    {
        public long PerProcessTime,PerJobTime;
        public uint Flags;
        public UIntPtr MinWorkingSet,MaxWorkingSet;
        public uint ActiveProcesses;
        public UIntPtr Affinity;
        public uint Priority,Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)] struct Io { public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] struct Extended
    {
        public Basic Basic;
        public Io Io;
        public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory;
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int kind,ref Extended info,uint length);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    internal ProcessJob(Process process)
    {
        handle=CreateJobObject(IntPtr.Zero,null);
        if(handle==IntPtr.Zero) throw new Win32Exception();
        var settings=new Extended(); settings.Basic.Flags=0x2000; // KILL_ON_JOB_CLOSE
        if(!SetInformationJobObject(handle,9,ref settings,(uint)Marshal.SizeOf(typeof(Extended))) || !AssignProcessToJobObject(handle,process.Handle))
        {
            int error=Marshal.GetLastWin32Error(); Dispose(); throw new Win32Exception(error);
        }
    }
    public void Dispose()
    {
        IntPtr owned=Interlocked.Exchange(ref handle,IntPtr.Zero);
        if(owned!=IntPtr.Zero) CloseHandle(owned);
    }
}
