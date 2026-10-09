using System;
using System.Collections.Generic;
using System.Globalization;

internal static class QuotaDetails
{
    // Legacy plain-text output stays stable for the fallback and existing data checks.
    internal static string Duration(TimeSpan value)
    {
        if(value.TotalSeconds<=0) return "等待刷新";
        if(value.TotalMinutes<1) return "不到1分钟后重置";
        if(value.TotalDays>=1) return (int)value.TotalDays+"天"+value.Hours+"小时后重置";
        if(value.TotalHours>=1) return (int)value.TotalHours+"小时"+value.Minutes+"分后重置";
        return (int)value.TotalMinutes+"分后重置";
    }
    internal static string Name(QuotaWindow window)
    {
        if(window==null) return "额度";
        return window.Minutes==10080?"每周":
            window.Minutes>0 && window.Minutes%60==0 && window.Minutes<1440?(window.Minutes/60)+"小时":
            window.Minutes>0 && window.Minutes%1440==0?(window.Minutes/1440)+"天":
            window.Minutes>0?window.Minutes+"分钟":(String.IsNullOrEmpty(window.FallbackName)?"额度":window.FallbackName)+"窗口";
    }
    internal static bool TryFraction(QuotaWindow window,out double fraction)
    {
        fraction=0;
        if(window==null || Double.IsNaN(window.Remaining) || Double.IsInfinity(window.Remaining)) return false;
        fraction=Math.Max(0,Math.Min(1,window.Remaining/100.0));
        return true;
    }
    internal static string DisplayPercent(QuotaWindow window)
    {
        double fraction;
        return TryFraction(window,out fraction)?window.Percent:"—";
    }
    internal static string ResetText(QuotaWindow window,DateTimeOffset now)
    {
        if(window==null || !window.Reset.HasValue) return "重置时间未提供";
        return Duration(window.Reset.Value-now).Replace("分后重置","分钟后重置");
    }
    internal static string UpdatedText(QuotaSnapshot snapshot,DateTimeOffset now)
    {
        if(snapshot==null) return "";
        long age=(long)Math.Max(0,(now-snapshot.Fetched).TotalSeconds);
        return "更新于"+snapshot.Fetched.ToLocalTime().ToString("HH:mm",CultureInfo.InvariantCulture)+"（"+age+"s前）";
    }
    internal static string Format(QuotaSnapshot snapshot, DateTimeOffset now)
    {
        var blocks=new List<string>();
        foreach(var w in snapshot.Windows)
        {
            string name=w.Minutes==10080?"每周":
                w.Minutes>0 && w.Minutes%60==0 && w.Minutes<1440?(w.Minutes/60)+" 小时":
                w.Minutes>0 && w.Minutes%1440==0?(w.Minutes/1440)+" 天":
                w.Minutes>0?w.Minutes+" 分钟":w.FallbackName+"窗口";
            string reset="重置时间未提供";
            if(w.Reset.HasValue) reset=Duration(w.Reset.Value-now);
            blocks.Add(name+"剩余："+DisplayPercent(w)+"\n"+reset);
        }
        blocks.Add(UpdatedText(snapshot,now));
        return String.Join("\n————————\n",blocks.ToArray());
    }
}
