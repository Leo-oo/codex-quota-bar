using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

internal static class UiStateChecks
{
    internal static void Run(FixtureWindow host,UsageBar bar)
    {
        var timer=new System.Windows.Forms.Timer { Interval=200 };
        var log=new List<string>(); int attempts=0,failed=0;
        timer.Tick+=delegate {
            bar.UpdateTracking(); if(!bar.Visible && ++attempts<30) return;
            timer.Stop();
            bar.CheckQuotaStates((name,ok)=> { log.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; });
            log.Add("Failed="+failed);
            File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quota-state-check.txt"),log.ToArray());
            Environment.ExitCode=failed==0?0:1; timer.Dispose(); host.Close();
        };
        host.Shown+=delegate { Native.ShowWindow(host.Handle,5); host.Activate(); timer.Start(); };
    }
}
