import Foundation
@main struct HistoryTests {
 static func main() throws {
  let now=ISO8601DateFormatter().date(from:"2026-10-09T06:00:00Z")!;var count=0
  func check(_ yes:Bool,_ name:String){precondition(yes,name);count+=1;print("PASS "+name)}
  func event(_ id:String,_ type:String="reset_credit",_ date:String?=nil,_ confirmed:String?="2026-10-08T11:44:55+08:00")->[String:Any] {
   var e:[String:Any]=["id":id,"type":type,"status":"confirmed","confirmationBasis":"source_post","presentation":["kindExplicit":true,"timeInferred":false]]
   if let date{e["occurredOn"]=date};if let confirmed{e["confirmedAt"]=confirmed};return e
  }
  func parse(_ es:[[String:Any]],_ time:Date=now,_ checked:String="a") throws->NewsSnapshot {try NewsParser.parse(JSONSerialization.data(withJSONObject:["schemaVersion":1,"today":NewsParser.day(time),"checkedAt":checked,"events":es]),now:time)}
  let fallback=try parse([event("credit")]);check(fallback.items.isEmpty,"next-day empty activities");check(fallback.history?.id=="credit","confirmation-only retained")
  check(fallback.history.flatMap{NewsHistory.text($0,now:now)}=="上次发放重置卡：10月8日 11:44（确认时间）","confirmation labelled precisely")
  let reset=try parse([event("reset","direct_reset")]);check(reset.history?.historyText?.hasPrefix("上次额度重置：")==true,"direct reset distinct")
  check(try parse([]).history==nil,"no history omitted")
  check(try parse([event("none","reset_credit",nil,nil)]).history==nil,"no reliable time omitted")
  let known=try parse([event("day","reset_credit","2026-10-07")]);check(known.history?.historyText=="上次发放重置卡：10月7日（发生日期）","occurrence day preferred, no invented clock")
  let late=event("late","reset_credit",nil,"2026-10-08T12:01:00+08:00");check(try parse([late,event("early")]).history?.id=="late","same-day exact ordering")
  check(try parse([event("dateOnly","reset_credit",nil,"2026-10-08")]).history?.historyText=="上次发放重置卡：10月8日（确认日期）","confirmation date precision retained")
  check(try parse([event("nozone","reset_credit",nil,"2026-10-08T12:00:00")]).history==nil,"no-zone rejected")
  check(try parse([event("future","reset_credit",nil,"2026-10-10T12:00:00+08:00")]).history==nil,"future confirmation rejected")
  let boundary=ISO8601DateFormatter().date(from:"2026-10-08T16:10:00Z")!,utc=event("utc","reset_credit",nil,"2026-10-08T16:05:00Z")
  let at=try parse([utc],boundary);check(at.items.count==1,"UTC event uses Beijing current day");check(at.history?.historyText=="上次发放重置卡：10月9日 00:05（确认时间）","UTC timestamp formatted Beijing")
  let next=at.current(now:ISO8601DateFormatter().date(from:"2026-10-09T16:10:00Z")!);check(next.items.isEmpty && next.history?.id=="utc","Beijing next day preserves history")
  let tie1=event("a"),tie2=event("b");check(try parse([tie2,tie1]).history?.id=="a","stable tie id")
  check(try parse([event("oldOccurrence","reset_credit","2026-10-07","2026-10-09T12:00:00+08:00"),event("credit")]).history?.id=="credit","later confirmation cannot lift older occurrence day")
  check(try parse([event("knownLate","direct_reset","2026-10-08","2026-10-08T13:00:00+08:00"),late]).history?.id=="knownLate","same occurrence-day confirmation tie consistent")
  let changed=try parse([late],now,"new check");check(fallback.items.isEmpty && changed.items.isEmpty,"history-only update creates no unread activities")
  let a=try parse([utc],boundary,"first"),b=try parse([utc],boundary,"second");check(a.items.first?.version==b.items.first?.version,"check-time never changes unread version")
  let encoded=try JSONEncoder().encode(fallback);let round=try JSONDecoder().decode(NewsSnapshot.self,from:encoded);check(round.history?.confirmed==fallback.history?.confirmed,"confirmation survives cache coding")
  var legacy=try JSONSerialization.jsonObject(with:encoded) as! [String:Any];var h=legacy["history"] as! [String:Any];h.removeValue(forKey:"confirmed");legacy["history"]=h
  check((try JSONDecoder().decode(NewsSnapshot.self,from:JSONSerialization.data(withJSONObject:legacy))).history?.confirmed==nil,"legacy cache decodes without new optional field")
  print("\(count) history checks passed")
 }
}
