using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Keeps the tool's real message loop alive after its test host is destroyed.
internal static class OrphanTrayChecks
{
    static T Field<T>(UsageBar bar,string name) { return (T)typeof(UsageBar).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(bar); }
    internal static void Run()
    {
        using(var host=new FixtureWindow())
        using(var bar=new UsageBar(host.Handle,true))
        using(var timer=new System.Windows.Forms.Timer { Interval=500 })
        {
            var log=new List<string>(); int step=0,failed=0,attempts=0; bool completed=false;
            Action<string,bool> check=(name,ok)=> { log.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; };
            var menu=Field<ContextMenuStrip>(bar,"menu"); var tray=Field<NotifyIcon>(bar,"tray");
            Action showTrayMenu=()=>LifecycleChecks.ShowTrayMenu(tray);
            tray.Visible=true;
            bar.FormClosed+=delegate {
                completed=true;
                check("exit command releases NotifyIcon",!tray.Visible);
                check("exit stops navigation reader",bar.NavigationWorkerStopped);
                log.Add("Failed="+failed);
                File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orphan-tray-check.txt"),log.ToArray());
                Environment.ExitCode=failed==0?0:1;
            };
            timer.Tick+=delegate {
                if(step==0 && !bar.Visible && ++attempts<20) return;
                switch(step++) {
                    case 0:
                        check("test host and overlay are visible",bar.Visible);
                        host.Hide(); bar.UpdateTracking();
                        showTrayMenu(); break;
                    case 1:
                        check("tray menu remains open while host hidden",menu.Visible);
                        menu.Close(); host.Close(); bar.UpdateTracking();
                        check("destroyed host conceals overlay",!bar.Visible);
                        break;
                    case 2:
                        showTrayMenu();
                        check("tray menu opens after host destruction",menu.Visible); break;
                    case 3:
                        check("tray menu remains open after host destruction",menu.Visible);
                        timer.Stop();
                        foreach(ToolStripItem item in menu.Items) if(item.Text=="退出额度条") { item.PerformClick(); break; }
                        break;
                }
            };
            host.Show(); Native.ShowWindow(host.Handle,5); host.Activate(); timer.Start();
            Application.Run(bar);
            if(!completed) {
                check("message loop survives host destruction until tray exit",false);
                log.Add("Failed="+failed);
                File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orphan-tray-check.txt"),log.ToArray());
                Environment.ExitCode=1;
            }
        }
    }
}
