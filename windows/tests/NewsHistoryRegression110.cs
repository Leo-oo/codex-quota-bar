using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
internal static class NewsHistoryRegression110 {
 static int count; static DateTimeOffset now=DateTimeOffset.Parse("2026-10-09T14:00:00+08:00");
 static void Check(bool value,string name){if(!value) throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
 static object Event(string id,string kind="reset_credit",string occurred=null,string confirmed="2026-10-08T11:44:55+08:00") {
  return new {id=id,type=kind,status="confirmed",occurredOn=occurred,confirmedAt=confirmed,confirmationBasis="source_post",presentation=new {kindExplicit=true,timeInferred=false}};
 }
 static ResetNewsSnapshot Parse(object[] events,string day="2026-10-09",string checkedAt="2026-10-09T14:00:00+08:00",DateTimeOffset? clock=null) {
  return ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today=day,checkedAt=checkedAt,events=events}),clock??now);
 }
 [STAThread] static int Main(string[] args) {
  // Only fresh caller-owned temp output and synthetic data. Never Program.Main/network/host probes.
  string dir=Path.Combine(Path.GetFullPath(args[0]),"history110-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
  var first=Parse(new[]{Event("credit")});
  Check(first.Items.Count==0 && first.LatestHistory!=null,"confirmation-only survives next day");
  Check(ResetNewsPresentation.EmptyHistory(first)=="上次发放重置卡：10月8日 11:44（确认时间）","exact source labelled confirmation");
  var direct=Parse(new[]{Event("direct","direct_reset")});Check(ResetNewsPresentation.EmptyHistory(direct).StartsWith("上次额度重置："),"direct reset label distinct");
  Check(Parse(new object[0]).LatestHistory==null,"no history omitted");
  Check(Parse(new[]{Event("none","reset_credit",null,null)}).LatestHistory==null,"missing reliable dates omitted");
  var day=Parse(new[]{Event("day","reset_credit","2026-10-07")});Check(ResetNewsParser.HistoryTime(day.LatestHistory)=="10月7日（发生日期）","occurrence preferred");
  var later=Event("later","reset_credit",null,"2026-10-08T12:01:00+08:00");Check(Parse(new[]{later,Event("earlier")}).LatestHistory.Id=="later","same-day exact ordering");
  Check(ResetNewsParser.HistoryTime(Parse(new[]{Event("date","reset_credit",null,"2026-10-08")}).LatestHistory)=="10月8日（确认日期）","confirmation date precision");
  Check(Parse(new[]{Event("zone","reset_credit",null,"2026-10-08T12:00:00")}).LatestHistory==null,"timezone-less rejected");
  Check(Parse(new[]{Event("future","reset_credit",null,"2026-10-10T12:00:00+08:00")}).LatestHistory==null,"future rejected");
  var utc=Event("utc","reset_credit",null,"2026-10-08T16:05:00Z");
  var boundary=Parse(new[]{utc},clock:DateTimeOffset.Parse("2026-10-08T16:10:00Z"));Check(boundary.Items.Count==1,"UTC timestamp belongs Beijing day");
  Check(ResetNewsParser.HistoryTime(boundary.LatestHistory)=="10月9日 00:05（确认时间）","Beijing formatting");
  var next=Parse(new[]{utc},"2026-10-10",clock:DateTimeOffset.Parse("2026-10-09T16:10:00Z"));Check(next.Items.Count==0 && next.LatestHistory.Id=="utc","Beijing day rollover keeps history");
  Check(Parse(new[]{Event("b"),Event("a")}).LatestHistory.Id=="a","stable identical-time tie");
  Check(Parse(new[]{Event("old","reset_credit","2026-10-07","2026-10-09T12:00:00+08:00"),Event("credit")}).LatestHistory.Id=="credit","later confirmation does not lift old occurrence day");
  Check(Parse(new[]{Event("known","direct_reset","2026-10-08","2026-10-08T13:00:00+08:00"),later}).LatestHistory.Id=="known","same-day occurrence tie uses available exact time");
  var state=new ResetNewsState(Path.Combine(dir,"state.json"));state.Apply(first,"a");state.Apply(Parse(new[]{later}),"b");Check(!state.HasUnread && state.Cache.Snapshot.Items.Count==0,"history-only changes no unread");
  var before=Parse(new[]{utc});var after=Parse(new[]{utc},checkedAt:"2026-10-09T15:00:00+08:00");Check(before.Items[0].Version==after.Items[0].Version,"check time leaves unread version unchanged");
  using(var owner=new Form())using(var card=new ResetNewsCard(delegate(string id,string version){throw new Exception("preview acknowledged");},delegate{})) {
   card.PreparePreview(owner,first,Appearance.Palette(false),1.25,now);
   Check(card.VisibleText.Contains("暂无新消息") && card.VisibleText.Contains("上次发放重置卡：10月8日 11:44（确认时间）"),"empty-state card history visible");
  }
  var migration=new ResetNewsState(Path.Combine(dir,"migration.json"));
  DateTimeOffset current=DateTimeOffset.Now;
  var fresh=Parse(new[]{Event("migration","reset_credit",null,current.ToString("o"))},ResetNewsParser.BeijingToday,clock:current);
  migration.Apply(fresh,"old-policy-etag");migration.MarkRead(fresh.Items[0].Id,fresh.Items[0].Version);
  migration.Cache.Snapshot.DisplayPolicy=3;migration.Success();
  var loaded=new ResetNewsState(Path.Combine(dir,"migration.json"));
  Check(loaded.Cache.ETag==null,"old policy forces refetch instead of stale304");
  Check(loaded.Cache.Snapshot.Items.Count==1 && !loaded.HasUnread,"old policy preserves today's read activities");
  Check(NewsHelpPopup.Explanation.Contains("北京时间") && NewsHelpPopup.Explanation.Contains("不是实际到账时间"),"time basis explained");
  Console.WriteLine(count+" Windows history checks passed");return 0;
 }
}
