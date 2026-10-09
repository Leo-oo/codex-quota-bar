using System;
using System.Drawing;
using System.Runtime.InteropServices;

// Alpha-zero pixels pass through a layered HWND. Route only a right-click that
// starts inside the currently eligible entry; never intercept left clicks.
internal sealed class EntryInput : IDisposable
{
    delegate IntPtr MouseCallback(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int kind,MouseCallback callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    readonly Func<Point,bool> eligible;
    readonly Action<Point> open;
    readonly MouseCallback callback;
    IntPtr hook;
    bool disposed,captured;
    Point pressPoint;
    internal bool Active { get { return hook!=IntPtr.Zero; } }
    internal int LastError { get; private set; }
    internal EntryInput(Func<Point,bool> canRoute,Action<Point> showMenu)
    {
        if(canRoute==null || showMenu==null) throw new ArgumentNullException("entry input callback");
        eligible=canRoute; open=showMenu; callback=OnMouse;
    }
    internal void SetEnabled(bool enabled)
    {
        if(disposed) return;
        if(!enabled) { Release(); return; }
        if(hook!=IntPtr.Zero) return;
        hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
        LastError=hook==IntPtr.Zero?Marshal.GetLastWin32Error():0;
    }
    internal bool ProcessRightMessage(long kind,Point point)
    {
        if(disposed) return false;
        if(kind==0x204) {
            captured=eligible(point);
            if(captured) pressPoint=point;
            return captured;
        }
        if(kind==0x205 && captured) {
            captured=false;
            if(eligible(pressPoint)) open(pressPoint);
            return true;
        }
        return false;
    }
    IntPtr OnMouse(int code,IntPtr message,IntPtr data)
    {
        long kind=message.ToInt64();
        if(code>=0 && (kind==0x204 || kind==0x205)) {
            Point point=(Point)Marshal.PtrToStructure(data,typeof(Point));
            if(ProcessRightMessage(kind,point)) return new IntPtr(1);
        }
        return CallNextHookEx(IntPtr.Zero,code,message,data);
    }
    void Release()
    {
        captured=false;
        IntPtr installed=hook; hook=IntPtr.Zero;
        if(installed!=IntPtr.Zero) UnhookWindowsHookEx(installed);
    }
    public void Dispose()
    {
        if(disposed) return;
        disposed=true; Release();
    }
}
