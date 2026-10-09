using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// The card and unread version share exactly these activity strings. Source check
// time, quota values and undisplayed source metadata never enter this projection.
internal sealed class ResetNewsView
{
    public string Title { get; set; }
    public string Status { get; set; }
    public string Scope { get; set; }
    public string TimeLabel { get; set; }
    public string TimeValue { get; set; }
    public string AsOf { get; set; }
}
internal static class ResetNewsPresentation
{
    internal const string VersionPrefix="view1:";
    internal static string Compact(string text)
    { return String.IsNullOrWhiteSpace(text)?"":String.Join(" ",text.Split((char[])null,StringSplitOptions.RemoveEmptyEntries)); }
    internal static ResetNewsView Create(ResetNewsItem item,DateTimeOffset now)
    {
        string title=item.KindExplicit==true && item.Type=="direct_reset" && item.Status!="withdrawn"?"直接额度重置":ResetNewsParser.TypeLabel(item);
        string status;
        if(item.Status=="confirmed" && item.KindExplicit==true && (item.Type=="direct_reset" || item.Type=="reset_credit") &&
            (item.ConfirmationBasis=="source_post" || item.ConfirmationBasis=="receipt_review")) status="已发生";
        else {
            status=ResetNewsParser.StatusLabel(item,now);
            DateTimeOffset from;
            string expected=String.IsNullOrEmpty(item.EstimatedFrom)?item.ExpectedFrom:item.EstimatedFrom;
            if(item.Status=="announced" && status=="预告（尚未确认）" && DateTimeOffset.TryParse(expected,out from) && from>now) status="预告";
        }
        string scope=Compact(item.Scope),products=Compact(item.Products);
        scope=(scope.Length==0?"未明确":scope)+(products.Length==0?"":" · "+products);
        string key=Compact(ResetNewsParser.KeyTime(item));
        int colon=key.IndexOf('：');
        string label=colon<0?"消息时间":key.Substring(0,colon),value=colon<0?key:key.Substring(colon+1);
        if(item.Status=="announced" && (label.StartsWith("预计",StringComparison.Ordinal) || label.StartsWith("预告",StringComparison.Ordinal))) {
            if(label.IndexOf("AIHOT",StringComparison.Ordinal)>=0) value+="（AIHOT 推算）";
            label="预计生效时间";
        } else if(label.StartsWith("预计",StringComparison.Ordinal)) {
            if(label.IndexOf("AIHOT",StringComparison.Ordinal)>=0) value+="（AIHOT 推算）";
            label="预告窗口 · 尚未确认";
        } else if(label=="来源确认" || label=="来源核验") label="来源记录时间";
        else if(label=="消息发布") label="消息发布时间";
        return new ResetNewsView { Title=title,Status=status,Scope=scope,TimeLabel=label,TimeValue=value,AsOf=now.ToString("o") };
    }
    internal static string Version(ResetNewsView view)
    {
        string material=ResetNewsParser.Json.Serialize(new string[] { view.Title,view.Status,view.Scope,view.TimeLabel,view.TimeValue });
        using(var sha=SHA256.Create()) return VersionPrefix+Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(material)));
    }
    internal static bool Valid(ResetNewsView view)
    { return view!=null && view.Title!=null && view.Status!=null && view.Scope!=null && view.TimeLabel!=null && view.TimeValue!=null; }
    internal static DateTimeOffset Clock(ResetNewsView view,DateTimeOffset fallback)
    { DateTimeOffset time; return view!=null && DateTimeOffset.TryParse(view.AsOf,out time)?time:fallback; }
    internal static void Initialize(ResetNewsItem item,DateTimeOffset now)
    { item.View=Create(item,now); item.Version=Version(item.View); }
    internal static bool SameActivities(IList<ResetNewsItem> left,IList<ResetNewsItem> right)
    {
        if(left.Count!=right.Count) return false;
        foreach(var item in left) {
            bool found=false;
            foreach(var other in right) if(item.Id==other.Id && item.Version==other.Version) { found=true; break; }
            if(!found) return false;
        }
        return true;
    }
    internal static string EmptyHistory(ResetNewsSnapshot snapshot)
    {
        if(snapshot.Items.Count>0) return "";
        string label=ResetNewsParser.HistoryLabel(snapshot.LatestHistory),time=ResetNewsParser.HistoryTime(snapshot.LatestHistory);
        return String.IsNullOrEmpty(label) || String.IsNullOrEmpty(time)?"":label+"："+time;
    }
}
