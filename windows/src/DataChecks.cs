using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static class DataChecks
{
    internal static void Run()
    {
        var json=new JavaScriptSerializer();
        var lines=new List<string>(); int failed=0;
        Action<string,bool> check=(name,ok)=>{lines.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++;};
        Func<string,QuotaSnapshot> parse=text=>QuotaParser.Parse(json.Deserialize<Dictionary<string,object>>(text));
        var weekly=parse("{\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":15,\"windowDurationMins\":10080,\"resetsAt\":1790082522},\"secondary\":null},\"other\":{\"primary\":{\"usedPercent\":1,\"windowDurationMins\":300}}}}");
        check("one real window does not invent a second",weekly.Windows.Count==1);
        check("weekly label follows duration not primary slot",weekly.Windows[0].Label=="周");
        check("remaining percentage",weekly.Windows[0].Remaining==85);
        check("reset timestamp interpreted as seconds",weekly.Windows[0].Reset.Value.ToUnixTimeSeconds()==1790082522);
        var two=parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":100,\"windowDurationMins\":300},\"secondary\":{\"usedPercent\":0,\"windowDurationMins\":10080}}}");
        check("legacy two-window response",two.Windows.Count==2);
        check("zero remaining stays zero",two.Windows[0].Remaining==0);
        check("unused stays 100",two.Windows[1].Remaining==100);
        check("missing percentage is unknown",parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300}}}").Windows.Count==0);
        check("text percentage is not silently coerced",parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":\"15\"}}}").Windows.Count==0);
        var clamp=parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":-3},\"secondary\":{\"usedPercent\":110}}}");
        check("out-of-range percentages bounded",clamp.Windows[0].Remaining==100 && clamp.Windows[1].Remaining==0);
        check("unknown duration uses neutral label",clamp.Windows[0].Label=="主");
        check("other bucket is not presented as general quota",parse("{\"rateLimitsByLimitId\":{\"other\":{\"primary\":{\"usedPercent\":2}}}}").Windows.Count==0);
        check("empty result remains empty",parse("{}").Windows.Count==0);
        check("invalid reset stays unknown",parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":2,\"resetsAt\":9999999999999}}}").Windows[0].Reset==null);
        check("elapsed reset marked as old",parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":2,\"resetsAt\":1}}}").ResetPassed);
        check("explicit light overrides system",Appearance.ParseTheme("appearanceTheme = \"light\"\n")==false);
        check("explicit dark overrides system",Appearance.ParseTheme("appearanceTheme = 'dark' # preference\n")==true);
        check("system preference delegates to Windows",Appearance.ParseTheme("appearanceTheme = \"system\"")==null);
        check("commented theme is ignored",Appearance.ParseTheme("# appearanceTheme = \"dark\"")==null);
        check("nested theme is not a root preference",Appearance.ParseTheme("[other]\nappearanceTheme = \"dark\"")==null);
        check("current desktop config location",Appearance.ParseTheme("[other]\nvalue=1\n[desktop]\nappearanceTheme = \"light\"")==false);
        check("desktop setting takes precedence over legacy root",Appearance.ParseTheme("appearanceTheme = \"dark\"\n[desktop]\nappearanceTheme = \"light\"")==false);
        check("quoted desktop header is supported",Appearance.ParseTheme("['desktop']\nappearanceTheme = 'dark'")==true);
        var now=new DateTimeOffset(2026,9,18,16,47,35,TimeSpan.FromHours(8));
        var example=new QuotaSnapshot { Fetched=now.AddSeconds(-35) };
        example.Windows.Add(new QuotaWindow { Minutes=300,Remaining=52,Reset=now.AddHours(3).AddMinutes(2) });
        example.Windows.Add(new QuotaWindow { Minutes=10080,Remaining=47,Reset=now.AddDays(4).AddHours(23) });
        string formatted=QuotaDetails.Format(example,now);
        check("requested tooltip format",formatted=="5 小时剩余：52%\n3小时2分后重置\n————————\n每周剩余：47%\n4天23小时后重置\n————————\n更新于"+example.Fetched.ToLocalTime().ToString("HH:mm")+"（35s前）");
        check("countdown updates without quota fetch",QuotaDetails.Format(example,now.AddSeconds(61)).Contains("3小时0分后重置") && QuotaDetails.Format(example,now.AddSeconds(61)).Contains("（96s前）"));
        check("sub-minute reset is explicit",QuotaDetails.Duration(TimeSpan.FromSeconds(30))=="不到1分钟后重置");
        check("elapsed reset does not claim fresh quota",QuotaDetails.Duration(TimeSpan.FromSeconds(-1))=="等待刷新");
        check("missing reset is explicit",QuotaDetails.Format(two,now).Contains("重置时间未提供"));
        check("weekly-only details do not invent five-hour quota",!QuotaDetails.Format(weekly,now).Contains("5 小时"));
        check("clock skew cannot produce negative update age",QuotaDetails.Format(example,now.AddMinutes(-1)).Contains("（0s前）"));
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tooltip-example.txt"),formatted);
        lines.Add("Failed="+failed);
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data-check.txt"),lines.ToArray());
        Environment.ExitCode=failed==0?0:1;
    }
    internal static void Probe()
    {
        var json=new JavaScriptSerializer();
        using(var client=new QuotaClient())
        {
            var first=client.Read();
            var second=first.Snapshot==null?first:client.Read();
            object report;
            if(second.Snapshot==null) { report=new{ok=false,error=second.Error}; Environment.ExitCode=1; }
            else report=new{ok=true,twoReads=first.Snapshot!=null,checkedAt=second.Snapshot.Fetched.ToString("o"),windows=second.Snapshot.Windows.Select(w=>new{label=w.Label,remaining=w.Remaining,minutes=w.Minutes,resetAt=w.Reset.HasValue?w.Reset.Value.ToString("o"):null}).ToArray()};
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"quota-check.json"),json.Serialize(report));
        }
    }
}
