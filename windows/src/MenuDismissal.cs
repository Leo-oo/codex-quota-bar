using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Non-activating owner windows do not reliably receive outside-click dismissal.
// Observe outside presses only while open; never consume input or store it.
internal sealed class MenuDismissal : IDisposable
{
    delegate IntPtr MouseCallback(int code,IntPtr message,IntPtr data);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int kind,MouseCallback callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
    [DllImport("kernel32.dll",CharSet=CharSet.Auto)] static extern IntPtr GetModuleHandle(string name);
    readonly ToolStripDropDown menu;
    readonly MouseCallback callback;
    IntPtr hook;
    int generation;
    bool disposed;
    internal bool Active { get { return hook!=IntPtr.Zero; } }
    internal Action<long,string,string,bool,bool> ObserveOwnPress;
    internal MenuDismissal(ToolStripDropDown target) : this(target,true) { }
    internal MenuDismissal(ToolStripDropDown target,bool autoClose)
    {
        menu=target; callback=OnMouse;
        menu.Opened+=Opened; menu.Closing+=Closing; menu.Closed+=Closed; menu.Disposed+=MenuDisposed;
        menu.AutoClose=autoClose;
    }
    void Closing(object sender,ToolStripDropDownClosingEventArgs e)
    {
        // WinForms excludes AutoClose=false children from its modal menu stack.
        // A press in such a child is therefore mistaken for an outside press
        // on the parent. Let our existing recursive outside observer decide
        // AppClicked while that child is open; keep native keyboard/focus close.
        if(!disposed && e.CloseReason==ToolStripDropDownCloseReason.AppClicked && HasPersistentVisibleChild(menu))
            e.Cancel=true;
    }
    static bool HasPersistentVisibleChild(ToolStripDropDown dropdown)
    {
        foreach(ToolStripItem item in dropdown.Items) {
            var child=item as ToolStripDropDownItem;
            if(child!=null && child.HasDropDownItems && child.DropDown.Visible &&
                (!child.DropDown.AutoClose || HasPersistentVisibleChild(child.DropDown))) return true;
        }
        return false;
    }
    void Opened(object sender,EventArgs e)
    {
        if(disposed) return;
        Release();
        hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);
        if(hook==IntPtr.Zero)
        {
            int error=Marshal.GetLastWin32Error();
            menu.Close();
            MessageBox.Show("无法启用菜单外点击关闭，请重新启动额度条。错误："+error,"额度条",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    void Closed(object sender,ToolStripDropDownClosedEventArgs e) { Release(); }
    void MenuDisposed(object sender,EventArgs e) { Dispose(); }
    void Release()
    {
        generation++;
        IntPtr installed=hook; hook=IntPtr.Zero;
        if(installed!=IntPtr.Zero) UnhookWindowsHookEx(installed);
    }
    IntPtr OnMouse(int code,IntPtr message,IntPtr data)
    {
        long kind=message.ToInt64();
        // Optional diagnostic observer sees only presses/releases whose real
        // hit HWND is this visible tool menu or one of its visible children.
        // It stores no pointer coordinates and never affects input handling.
        if(!disposed && !menu.IsDisposed && code>=0 && menu.Visible &&
            ObserveOwnPress!=null && (kind==0x201 || kind==0x202))
        {
            Point point=(Point)Marshal.PtrToStructure(data,typeof(Point));
            var hit=FindOwnMenu(menu,point);
            if(hit!=null) {
                var item=hit.GetItemAt(hit.PointToClient(point));
                string name=item==null?"other":item.Name;
                if(name!="up" && name!="down" && name!="reset" && name!="move" && name!="entry") name="other";
                string role=hit==menu?"main":hit.OwnerItem!=null && hit.OwnerItem.Name=="move"?"position":"other";
                try {ObserveOwnPress(kind,role,name,item!=null && item.Enabled,item!=null && item.Available);}
                catch(Exception) { }
            }
        }
        // Ordinary menus retain native non-left-button behavior. A persistent
        // child or card delegates all outside buttons to this same observer.
        bool press=kind==0x201 || ((!menu.AutoClose || HasPersistentVisibleChild(menu)) &&
            (kind==0x204 || kind==0x207 || kind==0x20B));
        if(!disposed && !menu.IsDisposed && code>=0 && press && menu.Visible)
        {
            Point point=(Point)Marshal.PtrToStructure(data,typeof(Point));
            int observed=generation;
            if(!ContainsPoint(menu,point))
                try { menu.BeginInvoke((MethodInvoker)delegate { if(!disposed && observed==generation) ProcessLeftPress(point); }); }
                catch(InvalidOperationException) { }
        }
        return CallNextHookEx(IntPtr.Zero,code,message,data);
    }
    static ToolStripDropDown FindOwnMenu(ToolStripDropDown dropdown,Point point)
    {
        if(!dropdown.Visible || dropdown.IsDisposed) return null;
        foreach(ToolStripItem item in dropdown.Items) {
            var child=item as ToolStripDropDownItem;
            if(child!=null && child.HasDropDownItems && child.DropDown.Visible) {
                var found=FindOwnMenu(child.DropDown,point);if(found!=null) return found;
            }
        }
        if(!dropdown.Bounds.Contains(point) || !dropdown.IsHandleCreated) return null;
        return Native.Root(Native.WindowFromPoint(point))==dropdown.Handle?dropdown:null;
    }
    static bool ContainsPoint(ToolStripDropDown dropdown,Point point)
    {
        if(dropdown.Visible && dropdown.Bounds.Contains(point)) return true;
        foreach(ToolStripItem item in dropdown.Items)
        {
            var child=item as ToolStripDropDownItem;
            if(child!=null && child.HasDropDownItems && child.DropDown.Visible && ContainsPoint(child.DropDown,point)) return true;
        }
        return false;
    }
    internal void ProcessLeftPress(Point point)
    {
        if(!disposed && !menu.IsDisposed && menu.Visible && !ContainsPoint(menu,point))
            menu.Close(menu.AutoClose && !HasPersistentVisibleChild(menu)?
                ToolStripDropDownCloseReason.AppClicked:ToolStripDropDownCloseReason.CloseCalled);
    }
    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        Release(); menu.Opened-=Opened; menu.Closing-=Closing; menu.Closed-=Closed; menu.Disposed-=MenuDisposed;
    }
}
