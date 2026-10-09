import AppKit
@main struct ExtendedTests {
 static func main() throws {
  var count=0
  func check(_ b:Bool,_ label:String){precondition(b,label);count+=1;print("PASS \(label)")}
  check((1...6).map{RetryPolicy.delay($0)} == [15,30,60,120,120,120],"bounded failure backoff")
  let identity=IdentityGate();check(!identity.changed("a") && !identity.changed("a") && identity.changed("b") && identity.changed(nil),"identity change and unverifiable account clear stale values")
  let raw:[String:Any] = ["rateLimits":["primary":["usedPercent":5,"resetsAt":Double.infinity]]]
  check(QuotaSnapshot.parse(raw).windows[0].reset==nil,"invalid reset cannot overflow countdown")
  let item=NewsItem(id:"a",version:"1",type:"reset_credit",status:"announced",scope:"范围",time:"预计日期：2026-10-05",url:"https://aihot.news/codex-reset",occurred:"",basis:"",explicit:true,inferred:false)
  let done=NewsItem(id:"d",version:"1",type:"direct_reset",status:"confirmed",scope:"范围",time:"2026-10-04",url:item.url,occurred:"2026-10-04",basis:"source_post",explicit:true,inferred:false)
  let snapshot=NewsSnapshot(items:[done,item],history:done,checked:"",day:"2026-10-04")
  let tomorrow=ISO8601DateFormatter().date(from:"2026-10-05T01:00:00Z")!
  let current=snapshot.current(now:tomorrow)
  check(current.items.map{$0.id} == ["a"] && current.history?.id=="d","cache day rollover retains announcement and genuine history")
  let blocks=NewsLayout.blocks([item],width:246,budget:500)
  check(NewsLayout.visibleVersions(blocks).count==1,"fully displayed version eligible for read")
  let long=NewsItem(id:"l",version:"1",type:item.type,status:item.status,scope:String(repeating:"很长的范围",count:200),time:item.time,url:item.url,occurred:"",basis:"",explicit:true,inferred:false)
  let longBlocks=NewsLayout.blocks([long],width:246,budget:500)
  check(longBlocks.count==1 && longBlocks[0].height<220 && NewsLayout.visibleVersions(longBlocks).isEmpty,"compact truncated message retains unread")
  let limited=NewsLayout.blocks([item,item,item],width:246,budget:blocks[0].height+1)
  check(limited.count==1 && NewsLayout.visibleVersions(limited).count==1,"offscreen versions not marked read")
  check(done.state=="已发生","explicit confirmed known event uses compact status")
  let unknown=NewsItem(id:"u",version:"1",type:"other",status:"confirmed",scope:"",time:"来源记录时间：10月7日 11:35",url:item.url,occurred:"",basis:"receipt_review",explicit:false,inferred:false)
  check(unknown.title=="消息（类型未明确）" && unknown.state=="已落地（来源核验）","unknown type and review basis remain honest")
  check(unknown.scopeValue=="未明确" && unknown.timeField.label=="来源记录时间" && unknown.timeField.value=="10月7日 11:35","field labels separate source timestamp and missing scope")
  let compact=NewsLayout.blocks([done],width:211.2,budget:340)[0]
  let ambiguous=NewsLayout.blocks([unknown],width:211.2,budget:340)[0]
  check(compact.titleWidth>ambiguous.titleWidth && abs(compact.titleWidth+compact.stateWidth+6.4-211.2)<0.01,"dynamic status width returns space to title")
  check(ambiguous.titleHeight>=NewsLayout.height(unknown.title,ambiguous.titleWidth,12) && ambiguous.stateHeight>=NewsLayout.height(unknown.state,ambiguous.stateWidth-14,8),"text measured at actual drawing widths")
  check(ambiguous.scopeY>=ambiguous.scopeLabelY+14 && ambiguous.timeLabelY>=ambiguous.scopeY+ambiguous.scopeHeight+9 && ambiguous.height>=ambiguous.timeY+ambiguous.timeHeight+12,"all field rows and footer spacing fit")
  let root=URL(fileURLWithPath:CommandLine.arguments[1]);setenv("CODEX_USAGE_MINI_CODEX_PATH",root.appendingPathComponent("Tests/FakeServer.py").path,1)
  let client=QuotaClient(runtime:root.appendingPathComponent("evidence/fake-rpc-runtime"));var changes=0;client.onIdentityChanged={changes+=1}
  setenv("QUOTA_TEST_SCENARIO","ok",1);let first=try client.read();check(first.windows.map{$0.label+$0.percent} == ["周23%"],"full initialized account and rate-limit protocol")
  setenv("QUOTA_TEST_SCENARIO","switch",1);_ = try client.read();check(changes==1,"protocol account switch invokes invalidation")
  for scenario in ["auth","logout","empty"] {setenv("QUOTA_TEST_SCENARIO",scenario,1);do{_ = try client.read();check(false,"expected failure")}catch let e as QuotaError{check(e.discardSnapshot,"\(scenario) invalidates stale quota")}}
  setenv("QUOTA_TEST_SCENARIO","timeout",1);let start=Date();do{_ = try client.read();check(false,"expected timeout")}catch let e as QuotaError{check(e.message.contains("超时") && Date().timeIntervalSince(start)>=14.8 && Date().timeIntervalSince(start)<18,"real RPC timeout at15seconds")}
  setenv("QUOTA_TEST_SCENARIO","ok",1);check(try client.read().windows.count==1,"recover after timeout")
  client.cancel();do{_ = try client.read();check(false,"cancelled client")}catch{check(true,"cancel prevents restart")}
  print("\(count) extended checks passed; test fixtures, no Codex window access")
 }
}
