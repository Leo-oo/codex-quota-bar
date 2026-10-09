using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

internal sealed class ResetNewsItem
{
    public string Id { get; set; }
    public string Version { get; set; }
    public ResetNewsView View { get; set; }
    public string Title { get; set; }
    public string Status { get; set; }
    public string Scope { get; set; }
    public string Body { get; set; }
    public string Published { get; set; }
    public string Schedule { get; set; }
    public string Through { get; set; }
    public string Url { get; set; }
    public string Type { get; set; }
    public bool? KindExplicit { get; set; }
    public string Products { get; set; }
    public string ConfirmedAt { get; set; }
    public string OccurredOn { get; set; }
    public string ConfirmationBasis { get; set; }
    public string ExpectedFrom { get; set; }
    public string EstimatedFrom { get; set; }
    public string EstimatedThrough { get; set; }
    public string EstimateBasis { get; set; }
    public bool TimeInferred { get; set; }
}
internal sealed class ResetNewsSnapshot
{
    public int DisplayPolicy { get; set; }
    public string Day { get; set; }
    public List<ResetNewsItem> Items { get; set; }
    public ResetNewsItem LatestHistory { get; set; }
    public string VerifiedAt { get; set; }
    public string Monitor { get; set; }
}
internal static class ResetNewsParser
{
    internal static string BeijingToday { get { return DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd"); } }
    static string DayOf(string value) { return value.Length>=10?value.Substring(0,10):""; }
    static string EventDay(Dictionary<string,object> item)
    {
        string day=Text(item,"occurredOn");
        if(day.Length==0) day=Text(item,"confirmedAt");
        if(day.Length==0) day=Text(Obj(Get(item,"estimate"))??Obj(Get(item,"schedule")),"from");
        if(day.Length==0) day=Text(item,"createdAt");
        DateTimeOffset exact;
        return SourceTimestamp(day,out exact)?exact.ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):DayOf(day);
    }
    internal static JavaScriptSerializer Json { get { return new JavaScriptSerializer { MaxJsonLength=2097152 }; } }
    internal static Dictionary<string,object> Obj(object v) { return v as Dictionary<string,object>; }
    internal static object Get(Dictionary<string,object> d,string key) { object v; return d!=null && d.TryGetValue(key,out v)?v:null; }
    internal static string Text(Dictionary<string,object> d,string key) { return Get(d,key) as string??""; }
    static object[] ArrayOf(object value) { return value as object[]??new object[0]; }
    internal static bool SafeLink(string link)
    {
        Uri uri;
        return Uri.TryCreate(link,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.IsDefaultPort && String.IsNullOrEmpty(uri.UserInfo) &&
            (uri.Host=="x.com" || uri.Host=="twitter.com" || uri.Host=="aihot.news");
    }
    internal static string LocalTime(string value)
    {
        if(String.IsNullOrEmpty(value)) return "未提供";
        DateTime day;
        if(DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day))
            return day.ToString("M月d日",CultureInfo.InvariantCulture);
        DateTimeOffset time;
        if(SourceTimestamp(value,out time)) return time.ToLocalTime().ToString("M月d日 HH:mm",CultureInfo.InvariantCulture);
        DateTime unspecified;
        if(DateTime.TryParseExact(value,new[]{"yyyy-MM-dd'T'HH:mm:ss.FFFFFFF","yyyy-MM-dd'T'HH:mm:ss","yyyy-MM-dd'T'HH:mm"},
            CultureInfo.InvariantCulture,DateTimeStyles.None,out unspecified)) return value+"（时区未提供）";
        return "时间未明确";
    }
    static bool SourceTimestamp(string value,out DateTimeOffset time)
    {
        // A date alone or a timestamp without a timezone is not an exact source time.
        return DateTimeOffset.TryParseExact(value,new[]{
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz","yyyy-MM-dd'T'HH:mm:sszzz","yyyy-MM-dd'T'HH:mmzzz",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'","yyyy-MM-dd'T'HH:mm:ss'Z'","yyyy-MM-dd'T'HH:mm'Z'"
        },CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out time);
    }
    static bool HistoryTimestamp(ResetNewsItem item,DateTimeOffset now,out DateTimeOffset time,out bool dateOnly)
    {
        time=default(DateTimeOffset); dateOnly=true;
        if(item==null || item.Status!="confirmed" || item.KindExplicit!=true ||
            (item.Type!="direct_reset" && item.Type!="reset_credit") ||
            (item.ConfirmationBasis!="source_post" && item.ConfirmationBasis!="receipt_review")) return false;
        if(item.TimeInferred) return false;
        DateTime day;
        if(DateTime.TryParseExact(item.OccurredOn,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)) {
            if(day<DateTime.MinValue.AddHours(8)) return false;
            time=new DateTimeOffset(DateTime.SpecifyKind(day,DateTimeKind.Unspecified),TimeSpan.FromHours(8));
            dateOnly=true; return time<=now;
        }
        // A confirmation is evidence of a public event, not its exact delivery time.
        if(SourceTimestamp(item.ConfirmedAt,out time)) { dateOnly=false; return time<=now; }
        if(DateTime.TryParseExact(item.ConfirmedAt,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day) && day>=DateTime.MinValue.AddHours(8)) {
            time=new DateTimeOffset(DateTime.SpecifyKind(day,DateTimeKind.Unspecified),TimeSpan.FromHours(8));
            dateOnly=true; return time<=now;
        }
        return false;
    }
    static bool HasOccurrence(ResetNewsItem item)
    {
        DateTime day; return DateTime.TryParseExact(item.OccurredOn,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day);
    }
    static DateTimeOffset HistoryOrderTime(ResetNewsItem item,DateTimeOffset time,DateTimeOffset now)
    {
        DateTimeOffset confirmation;
        if(HasOccurrence(item) && SourceTimestamp(item.ConfirmedAt,out confirmation) && confirmation<=now &&
            confirmation.ToOffset(TimeSpan.FromHours(8)).Date==time.ToOffset(TimeSpan.FromHours(8)).Date) return confirmation;
        return time;
    }
    internal static string HistoryLabel(ResetNewsItem item)
    {
        DateTimeOffset time; bool dateOnly;
        if(!HistoryTimestamp(item,DateTimeOffset.Now,out time,out dateOnly)) return "";
        return item.Type=="direct_reset"?"上次额度重置":"上次发放重置卡";
    }
    internal static string HistoryTime(ResetNewsItem item)
    {
        DateTimeOffset time; bool dateOnly;
        if(!HistoryTimestamp(item,DateTimeOffset.Now,out time,out dateOnly)) return "";
        string basis=HasOccurrence(item)?"发生日期":dateOnly?"确认日期":"确认时间";
        return time.ToOffset(TimeSpan.FromHours(8)).ToString(dateOnly?"M月d日":"M月d日 HH:mm",CultureInfo.InvariantCulture)+"（"+basis+"）";
    }

    internal static string CheckTime(string value,DateTimeOffset now)
    {
        DateTimeOffset time;
        if(!SourceTimestamp(value,out time)) return "";
        DateTimeOffset local=time.ToLocalTime();
        return "消息检查于 "+local.ToString(local.Date==now.ToLocalTime().Date?"HH:mm":"M月d日 HH:mm",CultureInfo.InvariantCulture);
    }
    internal static ResetNewsSnapshot Parse(string text)
    { return Parse(text,DateTimeOffset.Now); }
    internal static ResetNewsSnapshot Parse(string text,DateTimeOffset now)
    {
        var root=Obj(Json.DeserializeObject(text));
        if(Convert.ToString(Get(root,"schemaVersion"),CultureInfo.InvariantCulture)!="1" || !(Get(root,"events") is object[]))
            throw new InvalidDataException("消息接口格式已变化");
        string today=Text(root,"today"); if(today.Length==0) today=BeijingToday;
        var result=new ResetNewsSnapshot { DisplayPolicy=4,Day=today,Items=new List<ResetNewsItem>(), VerifiedAt=Text(root,"checkedAt"), Monitor=Text(Obj(Get(root,"monitor")),"status") };
        var history=new List<ResetNewsItem>();
        foreach(object value in ArrayOf(Get(root,"events")))
        {
            var item=Obj(value); string id=Text(item,"id");
            if(id.Length==0 || result.Items.Exists(x=>x.Id==id)) continue;
            var presentation=Obj(Get(item,"presentation")); var schedule=Obj(Get(item,"schedule")); var estimate=Obj(Get(item,"estimate"));
            string status=Text(presentation,"status"); if(status.Length==0) status=Text(item,"status");
            string audience=Text(presentation,"audienceZh");
            var posts=ArrayOf(Get(item,"posts")); var post=posts.Length>0?Obj(posts[0]):null;
            string url=Text(item,"url");
            string title=Text(item,"displayLabel"); if(title.Length==0) title="重置消息";
            // Presentation fields carry current meaning. Do not fall back to a potentially outdated broad scope.
            var parsed=new ResetNewsItem { Id=id,Title=title,Status=status,Scope=audience,
                Published=Text(post,"publishedAt"),Schedule=Text(schedule,"label"),Through=Text(schedule,"through"),Url=SafeLink(url)?url:"https://aihot.news/codex-reset",
                Type=Text(item,"type"),KindExplicit=Get(presentation,"kindExplicit") as bool?,Products=Text(presentation,"productsZh"),
                ConfirmedAt=Text(item,"confirmedAt"),OccurredOn=Text(item,"occurredOn"),ConfirmationBasis=Text(item,"confirmationBasis"),
                ExpectedFrom=Text(schedule,"from"),EstimatedFrom=Text(estimate,"from"),EstimatedThrough=Text(estimate,"through"),EstimateBasis=Text(estimate,"basis"),
                TimeInferred=Object.Equals(Get(presentation,"timeInferred"),true) };
            ResetNewsPresentation.Initialize(parsed,now);
            DateTimeOffset occurred; bool dateOnly;
            if(HistoryTimestamp(parsed,now,out occurred,out dateOnly)) history.Add(parsed);
            // Match the site's current-day calendar, keeping its outstanding announcement states.
            // Extract public history before this filter, without adding it to unread items.
            if(EventDay(item)!=today && status!="announced" && status!="in_progress" && status!="expired_unconfirmed" && status!="likely_completed") continue;
            result.Items.Add(parsed);
        }
        // A withdrawn event may disappear from the current events array. Keep its explicit correction readable.
        foreach(object value in ArrayOf(Get(root,"activities")))
        {
            var activity=Obj(value);
            if(Text(activity,"kind")!="event_update" || Text(activity,"action")!="withdraw" || !Object.Equals(Get(activity,"statusChanged"),true)) continue;
            if(DayOf(Text(activity,"publishedAt"))!=today) continue;
            foreach(object rawId in ArrayOf(Get(activity,"eventIds")))
            {
                string id=rawId as string; if(String.IsNullOrEmpty(id)) continue;
                history.RemoveAll(x=>x.Id==id);
                if(result.Items.Exists(x=>x.Id==id)) continue;
                string url=Text(activity,"url");
                var withdrawn=new ResetNewsItem { Id=id,Status="withdrawn",Title="重置消息已撤回",
                    Published=Text(activity,"publishedAt"),Scope="",Schedule="",Url="https://aihot.news/codex-reset" };
                ResetNewsPresentation.Initialize(withdrawn,now);
                result.Items.Add(withdrawn);
            }
        }
        DateTimeOffset latest=default(DateTimeOffset),latestConfirmation=default(DateTimeOffset);
        foreach(var item in history) {
            DateTimeOffset time; bool dateOnly;
            if(!HistoryTimestamp(item,now,out time,out dateOnly)) continue;
            DateTimeOffset order=HistoryOrderTime(item,time,now);
            DateTimeOffset day=new DateTimeOffset(time.ToOffset(TimeSpan.FromHours(8)).Date,TimeSpan.FromHours(8));
            if(result.LatestHistory==null || day>latest || (day==latest &&
                (order>latestConfirmation || (order==latestConfirmation && String.CompareOrdinal(item.Id,result.LatestHistory.Id)<0)))) {
                result.LatestHistory=item; latest=day; latestConfirmation=order;
            }
        }
        result.Items.Sort((a,b)=>String.CompareOrdinal(b.Published,a.Published));
        if(result.Items.Count>100) throw new InvalidDataException("消息数量异常");
        return result;
    }
    internal static string StatusLabel(string status)
    {
        switch(status) {
            case "confirmed": return "来源已确认";
            case "announced": return "已预告 · 尚未确认";
            case "in_progress": return "进行中 · 尚未确认";
            case "expired_unconfirmed": return "预计时间已过 · 尚未确认";
            case "likely_completed": return "按预计时间应已生效 · 尚未确认";
            case "withdrawn": return "已撤回";
            default: return "状态待核实";
        }
    }
    internal static string TypeLabel(ResetNewsItem item)
    {
        if(item.Status=="withdrawn") return "重置消息";
        if(item.Type!="direct_reset" && item.Type!="reset_credit") return "消息（类型未明确）";
        if(item.KindExplicit!=true) return "重置（形式未明确）";
        if(item.Type=="direct_reset") return "额度重置";
        if(item.Type=="reset_credit") return "发放重置卡";
        return "重置（类型未明确）";
    }
    internal static string StatusLabel(ResetNewsItem item,DateTimeOffset now)
    {
        if(item.Status=="confirmed") {
            if(item.ConfirmationBasis=="source_post") return "已落地（来源确认）";
            if(item.ConfirmationBasis=="receipt_review") return "已落地（来源核验）";
            return "尚未确认（确认依据未提供）";
        }
        if(item.Status=="withdrawn") return "已撤回（来源更正）";
        DateTimeOffset through;
        string expected=String.IsNullOrEmpty(item.EstimatedThrough)?item.Through:item.EstimatedThrough;
        if(item.Status=="expired_unconfirmed" || item.Status=="likely_completed" ||
            (DateTimeOffset.TryParse(expected,out through) && through<now)) return "尚未确认（预计时间已过）";
        if(item.Status=="announced") return "预告（尚未确认）";
        if(item.Status=="in_progress") return "预告（进行中，尚未确认）";
        return "尚未确认";
    }
    internal static string KeyTime(ResetNewsItem item)
    {
        // These are source timestamps, never an execution receipt or a personal arrival time.
        if(item.Status=="confirmed") {
            if(!String.IsNullOrEmpty(item.ConfirmedAt)) return "来源记录时间："+LocalTime(item.ConfirmedAt);
            if(!String.IsNullOrEmpty(item.OccurredOn)) {
                DateTime day;
                string occurrence=DateTime.TryParseExact(item.OccurredOn,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)?
                    day.ToString("M月d日",CultureInfo.InvariantCulture):"时间未明确";
                return "发生日期"+(item.TimeInferred?"（推算）":"")+"："+occurrence+"（北京时间）";
            }
        } else if(item.Status!="withdrawn") {
            if(!String.IsNullOrEmpty(item.EstimatedFrom)) {
                string from=LocalTime(item.EstimatedFrom),through=LocalTime(item.EstimatedThrough);
                return "预计窗口（AIHOT 推算）："+from+(through=="未提供"?"":"–"+through);
            }
            if(!String.IsNullOrEmpty(item.Schedule)) return "预告时间："+item.Schedule+(item.TimeInferred?"（时区／日期推算）":"");
            if(!String.IsNullOrEmpty(item.ExpectedFrom)) return "预告时间："+LocalTime(item.ExpectedFrom)+(item.TimeInferred?"（时区／日期推算）":"");
        }
        return "消息发布："+LocalTime(item.Published);
    }
}
internal sealed class ResetNewsCache
{
    public int Schema { get; set; }
    public int ReadPolicy { get; set; }
    public bool Initialized { get; set; }
    public string ETag { get; set; }
    public string LastSuccess { get; set; }
    public ResetNewsSnapshot Snapshot { get; set; }
    public Dictionary<string,string> Read { get; set; }
}
internal sealed class ResetNewsState
{
    readonly string path;
    internal ResetNewsCache Cache;
    internal string Error,StorageError;
    internal bool ContentChanged { get; private set; }
    internal ResetNewsState(string file)
    {
        path=file;
        Cache=new ResetNewsCache { Schema=1, ReadPolicy=1, Read=new Dictionary<string,string>(), Snapshot=new ResetNewsSnapshot { Items=new List<ResetNewsItem>() } };
        if(File.Exists(path))
            try {
                var loaded=ResetNewsParser.Json.Deserialize<ResetNewsCache>(File.ReadAllText(path));
                if(loaded==null || loaded.Schema!=1 || loaded.Read==null || loaded.Snapshot==null || loaded.Snapshot.Items==null ||
                    loaded.Snapshot.Items.Exists(x=>x==null || String.IsNullOrEmpty(x.Id) || String.IsNullOrEmpty(x.Version))) throw new InvalidDataException();
                Cache=loaded;
                // Policy 3 already held filtered activities and validated history.
                // Preserve those during an offline upgrade, but force a source refetch
                // because its historical candidates may have been incomplete.
                if(Cache.Snapshot.DisplayPolicy!=4) {
                    if(Cache.Snapshot.DisplayPolicy!=3) {
                        if((Cache.Snapshot.DisplayPolicy!=1 && Cache.Snapshot.DisplayPolicy!=2) || Cache.Snapshot.Day!=ResetNewsParser.BeijingToday) Cache.Snapshot.Items.Clear();
                        Cache.Snapshot.LatestHistory=null;
                    } else if(String.IsNullOrEmpty(ResetNewsParser.HistoryLabel(Cache.Snapshot.LatestHistory))) {
                        Cache.Snapshot.LatestHistory=null;
                    }
                    Cache.ETag=null;
                }
                MigrateReadVersions();
            } catch(Exception ex) {
                if(!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidOperationException)) throw;
                StorageError="本地消息记录不可用，将重新建立基线";
            }
    }
    void MigrateReadVersions()
    {
        bool changed=Cache.ReadPolicy!=1;
        DateTimeOffset clock;
        if(!DateTimeOffset.TryParse(Cache.LastSuccess,out clock)) clock=DateTimeOffset.Now;
        foreach(var item in Cache.Snapshot.Items) {
            if(ResetNewsPresentation.Valid(item.View) && item.Version==ResetNewsPresentation.Version(item.View)) continue;
            string previous=item.Version,read;
            bool wasRead=Cache.Read.TryGetValue(item.Id,out read) && read==previous;
            ResetNewsPresentation.Initialize(item,clock);
            if(wasRead) Cache.Read[item.Id]=item.Version;
            // A different or missing read version is evidence of unread content.
            // Orphan reads have no recoverable card projection and remain intact.
            changed=true;
        }
        Cache.ReadPolicy=1;
        if(changed) Save();
    }
    internal bool Unread(ResetNewsItem item) { string read; return !Cache.Read.TryGetValue(item.Id,out read) || read!=item.Version; }
    internal bool HasUnread { get { return Cache.Snapshot.Items.Exists(Unread); } }
    internal void Apply(ResetNewsSnapshot snapshot,string etag)
    {
        // Keep clock-only changes from turning an otherwise identical activity
        // into an alert. The card paints this same persisted projection; a fresh
        // source activity change creates a new projection at the current fetch.
        foreach(var item in snapshot.Items) {
            if(!ResetNewsPresentation.Valid(item.View)) ResetNewsPresentation.Initialize(item,DateTimeOffset.Now);
            var prior=Cache.Snapshot.Items.Find(x=>x.Id==item.Id);
            if(prior!=null && ResetNewsPresentation.Valid(prior.View)) {
                DateTimeOffset clock=ResetNewsPresentation.Clock(prior.View,DateTimeOffset.Now);
                var before=ResetNewsPresentation.Create(prior,clock);
                var after=ResetNewsPresentation.Create(item,clock);
                // Compare both source items in the same clock/timezone context;
                // keep the stored display even if the OS timezone has changed.
                if(ResetNewsPresentation.Version(before)==ResetNewsPresentation.Version(after)) { item.View=prior.View; item.Version=prior.Version; }
            }
        }
        ContentChanged=!ResetNewsPresentation.SameActivities(Cache.Snapshot.Items,snapshot.Items) ||
            ResetNewsPresentation.EmptyHistory(Cache.Snapshot)!=ResetNewsPresentation.EmptyHistory(snapshot);
        if(!Cache.Initialized)
            foreach(var item in snapshot.Items) Cache.Read[item.Id]=item.Version;
        else
        {
            // A regrouped event with identical semantic content should not become a new alert.
            foreach(var item in snapshot.Items)
                if(!Cache.Snapshot.Items.Exists(x=>x.Id==item.Id) && !Cache.Read.ContainsKey(item.Id))
                {
                    var prior=Cache.Snapshot.Items.Find(x=>x.Version==item.Version && !snapshot.Items.Exists(next=>next.Id==x.Id));
                    if(prior!=null && !Unread(prior)) Cache.Read[item.Id]=item.Version;
                }
        }
        Cache.Initialized=true; Cache.Snapshot=snapshot; Cache.ETag=etag; Success();
    }
    internal void Success() { Cache.LastSuccess=DateTimeOffset.Now.ToString("o"); Error=null; Save(); }
    internal void MarkRead(string id,string version) { Cache.Read[id]=version; Save(); }
    internal void MarkAllRead() { foreach(var item in Cache.Snapshot.Items) Cache.Read[item.Id]=item.Version; Save(); }
    void Save()
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp=path+".tmp"; File.WriteAllText(tmp,ResetNewsParser.Json.Serialize(Cache),Encoding.UTF8);
            if(File.Exists(path)) File.Replace(tmp,path,null); else File.Move(tmp,path);
            StorageError=null;
        } catch(IOException) { StorageError="已读状态暂时无法保存，重启后可能再次提示"; }
        catch(UnauthorizedAccessException) { StorageError="已读状态暂时无法保存，重启后可能再次提示"; }
    }
    internal string Summary
    {
        get {
            var parts=new List<string>();
            if(!String.IsNullOrEmpty(Error)) parts.Add(Error);
            else if(!Cache.Initialized) parts.Add("正在读取消息…");
            string checkedAt=ResetNewsParser.CheckTime(Cache.Snapshot.VerifiedAt,DateTimeOffset.Now);
            if(checkedAt.Length>0) parts.Add(checkedAt);
            if(Cache.Snapshot.Monitor!="healthy" && !String.IsNullOrEmpty(Cache.Snapshot.Monitor)) parts.Add("上游采集有延迟，消息可能不是最新");
            if(StorageError!=null) parts.Add(StorageError);
            return String.Join("\n",parts.ToArray());
        }
    }
}
internal sealed class ResetNewsResponse
{
    internal ResetNewsSnapshot Snapshot;
    internal bool Unchanged;
    internal string ETag,Error,Diagnostic;
    internal double RetrySeconds;
}
internal sealed class ResetNewsClient : IDisposable
{
    readonly object gate=new object();
    HttpWebRequest current;
    bool disposed;
    internal ResetNewsResponse Read(string etag)
    {
        HttpWebRequest request=(HttpWebRequest)WebRequest.Create("https://aihot.news/api/v1/codex-resets/recent");
        request.Method="GET"; request.UserAgent="CodexUsageMini/0.10.0 (personal quota widget)";
        request.Accept="application/json"; request.Timeout=15000; request.ReadWriteTimeout=15000;
        request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
        request.AllowAutoRedirect=false; request.UseDefaultCredentials=false;
        if(!String.IsNullOrEmpty(etag) && etag.IndexOfAny(new[]{'\r','\n'})<0) request.Headers[HttpRequestHeader.IfNoneMatch]=etag;
        lock(gate) { if(disposed) return new ResetNewsResponse { Error="消息服务已停止" }; current=request; }
        try {
            ServicePointManager.SecurityProtocol=SecurityProtocolType.SystemDefault;
            using(var response=(HttpWebResponse)request.GetResponse()) {
                if(response.StatusCode==HttpStatusCode.NotModified) return new ResetNewsResponse { Unchanged=true };
                if(response.StatusCode!=HttpStatusCode.OK) return new ResetNewsResponse { Error="消息暂时无法更新，稍后自动重试" };
                using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
                    var text=new StringBuilder(); var buffer=new char[4096]; int count;
                    while((count=reader.Read(buffer,0,buffer.Length))>0) { text.Append(buffer,0,count); if(text.Length>2097152) throw new InvalidDataException("消息响应过大"); }
                    return new ResetNewsResponse { Snapshot=ResetNewsParser.Parse(text.ToString()),ETag=response.Headers[HttpResponseHeader.ETag] };
                }
            }
        } catch(WebException ex) {
            using(var response=ex.Response as HttpWebResponse) {
                if(response!=null && response.StatusCode==HttpStatusCode.NotModified) return new ResetNewsResponse { Unchanged=true };
                double delay=0;
                if(response!=null) {
                    string retry=response.Headers["Retry-After"]; DateTimeOffset at;
                    if(!Double.TryParse(retry,NumberStyles.Integer,CultureInfo.InvariantCulture,out delay) && DateTimeOffset.TryParse(retry,out at)) delay=Math.Max(0,(at-DateTimeOffset.UtcNow).TotalSeconds);
                }
                return new ResetNewsResponse { Error="消息暂时无法更新，显示上次内容",RetrySeconds=delay,Diagnostic=ex.Status+": "+ex.GetBaseException().Message };
            }
        } catch(Exception ex) {
            if(!(ex is IOException || ex is ArgumentException || ex is InvalidOperationException)) throw;
            return new ResetNewsResponse { Error="消息格式暂时无法读取，显示上次内容" };
        } finally { lock(gate) { if(current==request) current=null; } }
    }
    public void Dispose() { lock(gate) { disposed=true; if(current!=null) current.Abort(); } }
}
