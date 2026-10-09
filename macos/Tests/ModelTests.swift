import AppKit
@main struct Tests {
 static func main() throws {
  var count=0
  func check(_ b:Bool,_ message:String){precondition(b,message);count+=1;print("PASS \(message)")}
  let frame=NSRect(x:100,y:100,width:800,height:600),avatar=NSRect(x:118,y:118,width:30,height:30)
  let host=HostGeometry(frame:frame,avatar:avatar,railRight:166,nativeControls:[],windowNumber:1)
  let a=Placement.entry(host:host,size:NSSize(width:54,height:78),offset:0)!
  check(a.minY-avatar.maxY==72,"entry bottom uses avatar top")
  check(Placement.entry(host:host,size:a.size,offset:100)!.minY-avatar.maxY==24,"minimum gap")
  let occupied=HostGeometry(frame:frame,avatar:avatar,railRight:166,nativeControls:[NSRect(x:118,y:190,width:30,height:30)],windowNumber:1)
  check(!Placement.entry(host:occupied,size:a.size,offset:0)!.intersects(occupied.nativeControls[0]),"update control avoidance")
  let screen=NSRect(x:0,y:0,width:1440,height:900)
  let popups=[NSSize(width:248,height:190),NSSize(width:232,height:190),NSSize(width:278,height:244)].map{Placement.popup(host:host,size:$0,screen:screen)}
  check(Set(popups.map{$0.minY}).count==1 && popups[0].minY==108,"shared bottom anchor")
  let row=NSRect(x:170,y:300,width:220,height:32)
  let sub=Placement.submenu(parent:popups[1],row:row,size:NSSize(width:164,height:116),screen:screen)
  check(sub.midY==row.midY,"submenu centered on parent row")
  let path=URL(fileURLWithPath:CommandLine.arguments[1]).appendingPathComponent("model-test-state.json")
  let p=Preferences(path);check(p.save(offset:-24),"save offset")
  check(Preferences(path).offset == -24,"offset survives restart")
  check(p.save(offset:900) && Preferences(path).offset==32,"bounded offset")
  let now=ISO8601DateFormatter().date(from:"2026-10-04T15:00:00Z")!
  func parse(_ events:[[String:Any]],_ activities:[[String:Any]]=[] ) throws -> NewsSnapshot {try NewsParser.parse(JSONSerialization.data(withJSONObject:["schemaVersion":1,"today":"2026-10-04","events":events,"activities":activities]),now:now)}
  let historical:[String:Any] = ["id":"old","type":"direct_reset","status":"confirmed","occurredOn":"2026-10-02","confirmationBasis":"source_post","presentation":["kindExplicit":true]]
  let old=try parse([historical]);check(old.items.isEmpty && old.history?.occurred=="2026-10-02","only verified past event in empty state")
  let announcement:[String:Any] = ["id":"a","type":"reset_credit","status":"announced","createdAt":"2026-10-01","schedule":["from":"2026-10-05"],"presentation":["kindExplicit":true,"audienceZh":"测试"]]
  let next=try parse([announcement]);check(next.items.count==1 && next.history==nil,"future announcement retained and excluded from history")
  check(next.items[0].time.contains("2026-10-05") && !next.items[0].time.contains("00:00"),"date-only precision preserved")
  var changed=announcement;changed["presentation"]=["kindExplicit":true,"audienceZh":"另一范围"]
  check(try parse([changed]).items[0].version != next.items[0].version,"semantic update changes read version")
  let correction=try parse([historical],[["id":"withdraw1","kind":"event_update","action":"withdraw","statusChanged":true,"publishedAt":"2026-10-04T13:00:00Z","eventIds":["old"]]])
  check(correction.items.first?.status=="withdrawn" && correction.history==nil,"withdrawal correction removes historical event")
  check(NewsParser.safeURL("https://evil.example/a")=="https://aihot.news/codex-reset","details URL allowlist")
  check(NewsParser.safeURL("https://user@aihot.news/a")=="https://aihot.news/codex-reset","details URL excludes userinfo")
  print("\(count) model checks passed; fixtures only")
 }
}
