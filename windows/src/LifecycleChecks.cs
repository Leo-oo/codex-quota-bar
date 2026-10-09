using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Runs the actual UI timer against the menu shared with NotifyIcon.
// Callback messages target only the fixture's icon; no desktop input is injected.
internal static class LifecycleChecks
{
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
    internal static void ShowTrayMenu(NotifyIcon tray)
    {
        var field=typeof(NotifyIcon).GetField("window",BindingFlags.Instance|BindingFlags.NonPublic);
        var window=field==null?null:field.GetValue(tray) as NativeWindow;
        if(window==null || window.Handle==IntPtr.Zero) throw new InvalidOperationException("Fixture tray window unavailable");
        var callback=typeof(NotifyIcon).GetField("WM_TRAYMOUSEMESSAGE",BindingFlags.Static|BindingFlags.NonPublic);
        if(callback==null) throw new InvalidOperationException("Fixture tray callback unavailable");
        int message=(int)callback.GetRawConstantValue();
        SendMessage(window.Handle,message,IntPtr.Zero,new IntPtr(0x204));
        SendMessage(window.Handle,message,IntPtr.Zero,new IntPtr(0x205));
    }
    static T Field<T>(UsageBar bar,string name) { return (T)typeof(UsageBar).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bar); }
    static ToolStripItem Find(ToolStripItemCollection items,string text)
    {
        foreach(ToolStripItem item in items) {
            if(item.Text==text) return item;
            var group=item as ToolStripDropDownItem;
            if(group!=null) { var found=Find(group.DropDownItems,text); if(found!=null) return found; }
        }
        return null;
    }
    internal static void Run(FixtureWindow host,UsageBar bar)
    {
        var log=new List<string>(); int step=0,failed=0,attempts=0;
        var menu=Field<ContextMenuStrip>(bar,"menu");
        var tray=Field<NotifyIcon>(bar,"tray");
        var timer=new System.Windows.Forms.Timer { Interval=450 };
        Action<string,bool> check=(name,ok)=> { log.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; };
        Action show=()=>ShowTrayMenu(tray);
        timer.Tick+=delegate {
            if(step==0 && !bar.Visible && ++attempts<20) return;
            switch(step++) {
                case 0:
                    check("fixture is visible",bar.Visible);
                    tray.Visible=true;
                    check("real NotifyIcon and bar share one menu",tray.Visible && tray.ContextMenuStrip==menu);
                    Find(menu.Items,"暂时隐藏").PerformClick();
                    check("pause hides overlay",!bar.Visible);
                    show(); check("tray menu opens while paused",menu.Visible); break;
                case 1:
                    check("tray menu survives paused timer ticks",menu.Visible);
                    menu.Close(); Find(menu.Items,"恢复显示").PerformClick();
                    check("restore command resumes overlay",bar.Visible);
                    host.WindowState=FormWindowState.Minimized; bar.UpdateTracking();
                    check("minimized host hides overlay",!bar.Visible);
                    show(); check("tray menu opens while minimized",menu.Visible); break;
                case 2:
                    check("tray menu survives minimized host timer ticks",menu.Visible);
                    menu.Close(); host.WindowState=FormWindowState.Normal; break;
                case 3:
                    bar.UpdateTracking(); check("overlay returns after host restore",bar.Visible);
                    bar.Shutdown();
                    check("navigation worker stops before host disposal",bar.NavigationWorkerStopped);
                    check("shutdown releases tray and mouse observer",!tray.Visible && !Field<MenuDismissal>(bar,"menuDismissal").Active);
                    bool lateOpenIgnored=true;
                    try { typeof(UsageBar).GetMethod("OpenNews",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(bar,null); }
                    catch(TargetInvocationException error) { lateOpenIgnored=false; log.Add("LateOpenError="+error.InnerException.GetType().Name); }
                    check("late queued news open is ignored after shutdown",lateOpenIgnored && Field<ResetNewsCard>(bar,"newsCard").IsDisposed);
                    log.Add("Failed="+failed);
                    File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"lifecycle-check.txt"),log.ToArray());
                    Environment.ExitCode=failed==0?0:1;
                    timer.Stop(); timer.Dispose(); tray.Visible=false; host.Close(); break;
            }
        };
        host.Shown+=delegate { Native.ShowWindow(host.Handle,5); host.Activate(); timer.Start(); };
    }
}
