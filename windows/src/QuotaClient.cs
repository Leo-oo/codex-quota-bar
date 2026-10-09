using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

internal sealed class QuotaWindow
{
    internal double Remaining;
    internal int Minutes;
    internal string FallbackName;
    internal DateTimeOffset? Reset;
    internal string Label
    {
        get
        {
            if(Minutes==10080) return "周";
            if(Minutes>0 && Minutes%1440==0) return (Minutes/1440)+"天";
            if(Minutes>0 && Minutes%60==0) return (Minutes/60)+"h";
            return Minutes>0 ? Minutes+"m" : FallbackName;
        }
    }
    internal string Percent { get { return Remaining.ToString("0.#",CultureInfo.InvariantCulture)+"%"; } }
}

internal sealed class QuotaSnapshot
{
    internal readonly List<QuotaWindow> Windows=new List<QuotaWindow>();
    internal DateTimeOffset Fetched=DateTimeOffset.Now;
    internal string AccountKey;
    internal bool ResetPassed
    {
        get { foreach(var w in Windows) if(w.Reset.HasValue && w.Reset.Value<=DateTimeOffset.Now) return true; return false; }
    }
}

internal static class QuotaParser
{
    internal static Dictionary<string,object> Object(object value) { return value as Dictionary<string,object>; }
    internal static object Get(Dictionary<string,object> d,string key) { object v; return d!=null && d.TryGetValue(key,out v)?v:null; }
    internal static bool Number(object value,out double number)
    {
        number=0;
        if(!(value is int || value is long || value is double || value is decimal || value is float)) return false;
        number=Convert.ToDouble(value,CultureInfo.InvariantCulture);
        return !Double.IsNaN(number) && !Double.IsInfinity(number);
    }
    internal static QuotaSnapshot Parse(Dictionary<string,object> result)
    {
        var all=Object(Get(result,"rateLimitsByLimitId"));
        var bucket=Object(Get(all,"codex"));
        if(bucket==null)
        {
            var legacy=Object(Get(result,"rateLimits"));
            var id=Get(legacy,"limitId") as string;
            if(legacy!=null && (id==null || id=="codex")) bucket=legacy;
        }
        QuotaSnapshot snapshot=new QuotaSnapshot();
        foreach(string key in new[]{"primary","secondary"})
        {
            var window=Object(Get(bucket,key)); double used,minutes,seconds;
            if(!Number(Get(window,"usedPercent"),out used)) continue;
            var w=new QuotaWindow { Remaining=Math.Max(0,Math.Min(100,100-used)), FallbackName=key=="primary"?"主":"次" };
            if(Number(Get(window,"windowDurationMins"),out minutes) && minutes>0 && minutes<=Int32.MaxValue) w.Minutes=(int)minutes;
            if(Number(Get(window,"resetsAt"),out seconds) && seconds>=0 && seconds<=253402300799)
                w.Reset=DateTimeOffset.FromUnixTimeSeconds((long)seconds);
            snapshot.Windows.Add(w);
        }
        snapshot.Windows.Sort((a,b)=>
        {
            int byDuration=(a.Minutes==0?Int32.MaxValue:a.Minutes).CompareTo(b.Minutes==0?Int32.MaxValue:b.Minutes);
            return byDuration!=0?byDuration:String.CompareOrdinal(a.FallbackName,b.FallbackName);
        });
        return snapshot;
    }
    internal static QuotaSnapshot Demo()
    {
        var s=new QuotaSnapshot();
        s.Windows.Add(new QuotaWindow { Minutes=300,Remaining=52,Reset=new DateTimeOffset(DateTime.Today.AddHours(17).AddMinutes(2)) });
        s.Windows.Add(new QuotaWindow { Minutes=10080,Remaining=47,Reset=new DateTimeOffset(DateTime.Today.AddDays(4).AddHours(21).AddMinutes(5)) }); return s;
    }
}

internal sealed class QuotaResult
{
    internal QuotaSnapshot Snapshot;
    internal string Error;
    internal string VerifiedAccount;
}

// Only account/read and account/rateLimits/read are issued after protocol initialization.
// Credentials stay owned by Codex. Runtime DB and logs are isolated beside this executable.
internal sealed class QuotaClient : IDisposable
{
    readonly JavaScriptSerializer json=new JavaScriptSerializer { MaxJsonLength=2097152 };
    Process process;
    ProcessJob job;
    long nextId;
    volatile bool disposed;
    readonly object gate=new object();
    readonly object processLifetime=new object();
    internal static string RuntimeDirectory { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime"); } }
    internal QuotaResult Read()
    {
        lock(gate)
        {
            var outcome=new QuotaResult();
            try
            {
                if(disposed) throw new InvalidOperationException("额度服务已停止");
                EnsureStarted();
                var accountResult=Request("account/read",new{refreshToken=false});
                var account=QuotaParser.Object(QuotaParser.Get(accountResult,"account"));
                if(account==null) throw new InvalidOperationException("请先在 Codex 登录账户");
                // In-memory identity only; never output or persist email or account identifiers.
                string identity=(QuotaParser.Get(account,"id") as string) ?? (QuotaParser.Get(account,"email") as string);
                if(!String.IsNullOrEmpty(identity)) outcome.VerifiedAccount=identity;
                var data=Request("account/rateLimits/read",null);
                var snapshot=QuotaParser.Parse(data);
                if(snapshot.Windows.Count==0) throw new InvalidOperationException("服务未提供通用额度窗口");
                snapshot.AccountKey=identity;
                outcome.Snapshot=snapshot;
            }
            catch(Exception ex)
            {
                if(ex is InvalidOperationException || ex is TimeoutException || ex is FileNotFoundException)
                    outcome.Error=ex.Message;
                else outcome.Error="额度连接中断，稍后自动重试";
                StopProcess();
            }
            return outcome;
        }
    }
    void EnsureStarted()
    {
        lock(processLifetime)
        {
        if(disposed) throw new InvalidOperationException("额度服务已停止");
        if(process!=null && !process.HasExited) return;
        StopProcess();
        Directory.CreateDirectory(RuntimeDirectory);
        string root=Environment.GetEnvironmentVariable("CODEX_HOME");
        if(String.IsNullOrWhiteSpace(root)) root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".codex");
        string runtime=RuntimeDirectory.Replace('\\','/');
        var start=new ProcessStartInfo(LocateCodex())
        {
            Arguments="-c "+Quote("sqlite_home="+json.Serialize(runtime))+" -c "+Quote("log_dir="+json.Serialize(runtime+"/logs"))+" app-server",
            UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,
            WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
        };
        start.EnvironmentVariables["CODEX_HOME"]=root;
        process=new Process { StartInfo=start };
        process.ErrorDataReceived+=delegate { }; // Drain, but never log raw server diagnostics.
        if(!process.Start()) throw new InvalidOperationException("无法启动 Codex 额度服务");
        job=new ProcessJob(process);
        process.BeginErrorReadLine();
        process.StandardInput.AutoFlush=true;
        }
        Request("initialize",new{clientInfo=new{name="codex_usage_mini",version="0.10.0"},capabilities=new{}});
        Process initialized;
        lock(processLifetime) initialized=process;
        if(disposed || initialized==null) throw new InvalidOperationException("额度服务已停止");
        initialized.StandardInput.WriteLine(json.Serialize(new{method="initialized",@params=new{}}));
    }
    Dictionary<string,object> Request(string method,object parameters)
    {
        Process current;
        lock(processLifetime) current=process;
        if(disposed || current==null) throw new InvalidOperationException("额度服务已停止");
        long id=++nextId;
        var request=new Dictionary<string,object>{{"id",id},{"method",method}};
        if(parameters!=null) request["params"]=parameters;
        current.StandardInput.WriteLine(json.Serialize(request));
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<15000)
        {
            if(disposed) throw new InvalidOperationException("额度服务已停止");
            Task<string> pending=current.StandardOutput.ReadLineAsync();
            if(!pending.Wait((int)Math.Max(1,15000-watch.ElapsedMilliseconds))) throw new TimeoutException("额度读取超时，稍后自动重试");
            string line=pending.Result;
            if(line==null) throw new InvalidOperationException("Codex 额度服务提前退出");
            Dictionary<string,object> message;
            try { message=json.Deserialize<Dictionary<string,object>>(line); } catch(ArgumentException) { continue; }
            double responseId;
            if(!QuotaParser.Number(QuotaParser.Get(message,"id"),out responseId) || responseId!=id) continue;
            var error=QuotaParser.Object(QuotaParser.Get(message,"error"));
            if(error!=null)
            {
                string description=Convert.ToString(QuotaParser.Get(error,"message"),CultureInfo.InvariantCulture).ToLowerInvariant();
                if(description.Contains("auth") || description.Contains("login") || description.Contains("401"))
                    throw new InvalidOperationException("登录状态不可用，请在 Codex 检查登录");
                throw new InvalidOperationException("服务暂时无法返回额度");
            }
            return QuotaParser.Object(QuotaParser.Get(message,"result")) ?? new Dictionary<string,object>();
        }
        throw new TimeoutException("额度读取超时，稍后自动重试");
    }
    internal static string LocateCodex()
    {
        string manual=Environment.GetEnvironmentVariable("CODEX_USAGE_MINI_CODEX_PATH");
        if(!String.IsNullOrEmpty(manual) && File.Exists(manual) && manual.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) return manual;
        foreach(string entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(';'))
        {
            string path=Path.Combine(entry.Trim('"'),"codex.exe"); if(File.Exists(path)) return path;
        }
        string bin=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"OpenAI","Codex","bin");
        if(Directory.Exists(bin))
        {
            var folders=new List<DirectoryInfo>(new DirectoryInfo(bin).GetDirectories());
            folders.Sort((a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            foreach(var folder in folders) { string path=Path.Combine(folder.FullName,"codex.exe"); if(File.Exists(path)) return path; }
        }
        throw new FileNotFoundException("未找到 Codex，请先安装并登录客户端");
    }
    internal static string Quote(string value)
    {
        var b=new StringBuilder("\""); int slashes=0;
        foreach(char c in value)
        {
            if(c=='\\') { slashes++; continue; }
            if(c=='"') { b.Append('\\',slashes*2+1); b.Append('"'); }
            else { b.Append('\\',slashes); b.Append(c); }
            slashes=0;
        }
        b.Append('\\',slashes*2); b.Append('"'); return b.ToString();
    }
    void StopProcess()
    {
        Process p; ProcessJob ownedJob;
        lock(processLifetime)
        {
            p=process; process=null;
            ownedJob=job; job=null;
        }
        // Detach ownership once before touching streams: a failed read and UI
        // shutdown may arrive together, but must never close the same handle twice.
        if(ownedJob!=null) ownedJob.Dispose();
        if(p==null) return;
        try { p.StandardInput.Close(); if(!p.WaitForExit(2000)) { p.Kill(); p.WaitForExit(2000); } }
        catch(InvalidOperationException) { }
        catch(System.ComponentModel.Win32Exception) { }
        catch(IOException) { }
        finally { p.Dispose(); }
    }
    public void Dispose() { disposed=true; StopProcess(); }
}
