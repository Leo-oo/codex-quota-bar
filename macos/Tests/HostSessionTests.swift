import AppKit
@main struct SessionTests {
 static func main(){
  var count=0
  func check(_ value:Bool,_ reason:String){precondition(value,reason);count+=1;print("PASS",reason)}
  let frame=NSRect(x:100,y:100,width:800,height:600)
  func host(_ id:Int,_ dx:CGFloat=0)->HostGeometry {HostGeometry(frame:frame,avatar:NSRect(x:110+dx,y:120,width:40,height:40),railRight:170+dx,nativeControls:[],windowNumber:id)}
  func good(_ id:Int)->HostResolution{HostResolution(geometry:host(id),reason:"confirmed")}
  func temporary(_ id:Int?)->HostResolution{HostResolution(geometry:nil,reason:"temporary",kind:.temporarilyUnavailable,observedWindowNumber:id)}
  var session=HostSession();session.selectProcess(10);let first=session.token!
  check(session.current(now:0)==nil,"no anchor before first successful match")
  check(session.accept(good(7),token:first,now:1),"matching process/generation accepts result")
  check(session.current(now:1.1)?.windowNumber==7,"confirmed geometry available")
  session.accept(temporary(7),token:first,now:1.3)
  check(session.current(now:1.5)?.windowNumber==7,"same-window temporary read uses bounded continuity")
  session.accept(temporary(7),token:first,now:1.55)
  check(session.current(now:1.61)==nil,"repeated failures do not renew deadline")
  session.accept(good(7),token:first,now:2)
  session.accept(temporary(8),token:first,now:2.1)
  check(session.current(now:2.1)==nil,"temporary failure for another window cannot reuse old geometry")
  session.accept(good(7),token:first,now:3)
  session.accept(temporary(nil),token:first,now:3.1)
  check(session.current(now:3.1)==nil,"unknown window identity cannot retain previous anchor")
  session.accept(good(7),token:first,now:4)
  session.accept(HostResolution(geometry:nil,reason:"unmatched"),token:first,now:4.1)
  check(session.current(now:4.1)==nil,"unmatched structure hides without grace")
  session.accept(good(7),token:first,now:5)
  session.accept(HostResolution(geometry:nil,reason:"minimized",kind:.hidden),token:first,now:5.1)
  check(session.current(now:5.1)==nil,"explicit hidden result bypasses continuity")
  session.accept(good(7),token:first,now:6);session.invalidate("window switched")
  check(!session.accept(good(7),token:first,now:6.1),"late worker result after lifecycle invalidation rejected")
  check(session.current(now:6.1)==nil,"invalidated anchor cannot reappear from stale result")
  let second=session.token!;session.accept(good(8),token:second,now:7)
  check(session.windowNumber==8,"new confirmed window identity replaces old identity")
  session.selectProcess(11)
  check(session.current(now:7.1)==nil && !session.accept(good(8),token:second,now:7.2),"process restart clears cache and rejects old process result")
  let third=session.token!;session.accept(good(9),token:third,now:8)
  session.invalidate("background",forgetProcess:true)
  check(session.token==nil && session.current(now:8.1)==nil,"background or termination drops active session")
  check(!session.accept(good(9),token:third,now:8.2),"late terminated-process result rejected")
  session.selectProcess(22,launchedAt:Date(timeIntervalSince1970:10))
  let reusedPID=session.token!;session.accept(good(7),token:reusedPID,now:9)
  session.selectProcess(22,launchedAt:Date(timeIntervalSince1970:20))
  check(session.current(now:9.1)==nil,"same PID with different launch identity clears window cache")
  check(!session.accept(good(7),token:reusedPID,now:9.2),"same PID restart rejects old launch result")
  session.selectProcess(22,launchedAt:Date(timeIntervalSince1970:20));let restartToken=session.token!
  session.invalidate("terminated",forgetProcess:true);session.selectProcess(22,launchedAt:Date(timeIntervalSince1970:20))
  check(!session.accept(good(7),token:restartToken,now:10),"termination boundary rejects same PID and recycled window number")
  check(host(1) != host(2),"same-frame window switch changes placement identity")
  check(host(1) != host(1,12),"internal account/rail change detected without host resize")
  var covered=host(1);covered.occlusions=[NSRect(x:400,y:100,width:100,height:100)]
  check(covered != host(1),"native occlusion participates in changed host state")
  print("\(count) lifecycle checks passed; pure state tests, no target access")
 }
}
