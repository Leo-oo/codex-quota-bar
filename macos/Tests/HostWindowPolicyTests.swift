import AppKit
@main struct WindowPolicyTests {
 static func main(){var checks=0;func check(_ value:Bool,_ name:String){precondition(value,name);checks+=1;print("PASS",name)}
  check(HostWindowPolicy.occluders(orderedWindowIDs:[1,2,3],hostID:2)==[1],"only windows above host can occlude")
  check(HostWindowPolicy.occluders(orderedWindowIDs:[2,1,3],hostID:2).isEmpty,"back windows never occlude front host")
  check(HostWindowPolicy.occluders(orderedWindowIDs:[1,3],hostID:2).isEmpty,"unknown host identity has no speculative occluders")
  let frame=NSRect(x:100,y:100,width:600,height:400)
  check(!HostWindowPolicy.isVisibleOccluder(frame:frame,alpha:0),"onscreen fully transparent helper cannot suppress entry")
  check(HostWindowPolicy.isVisibleOccluder(frame:frame,alpha:1),"opaque native panel still takes priority")
  check(HostWindowPolicy.isVisibleOccluder(frame:frame,alpha:0.01),"fading native panel retains priority without arbitrary threshold")
  check(!HostWindowPolicy.isVisibleOccluder(frame:.zero,alpha:1),"empty helper geometry ignored")
  check(!HostWindowPolicy.isVisibleOccluder(frame:frame,alpha:.nan),"invalid alpha cannot create a blocker")
  let one=HostWindowFrame(id:8,frame:frame),two=HostWindowFrame(id:9,frame:frame)
  check(HostWindowPolicy.uniqueWindowID(in:[one],matching:frame)==8,"single same-frame identity accepted")
  check(HostWindowPolicy.uniqueWindowID(in:[one,two],matching:frame)==nil,"same-frame window IDs remain ambiguous")
  check(HostWindowPolicy.uniqueWindowID(in:[one,HostWindowFrame(id:9,frame:frame.offsetBy(dx:2,dy:0))],matching:frame)==nil,"near-identical matching windows not guessed")
  check(HostWindowPolicy.uniqueWindowID(in:[one,HostWindowFrame(id:9,frame:frame.offsetBy(dx:30,dy:0))],matching:frame)==8,"distant window does not create ambiguity")
  check(HostWindowPolicy.uniqueWindowID(in:[HostWindowFrame(id:0,frame:frame)],matching:frame)==nil,"invalid window ID rejected")
  let card=NSRect(x:8,y:8,width:228,height:166.4)
  check(CardHitRegion.contains(NSPoint(x:100,y:50),in:card,radius:14.4),"card interior interactive")
  check(!CardHitRegion.contains(NSPoint(x:3,y:50),in:card,radius:14.4),"transparent shadow margin is outside")
  check(!CardHitRegion.contains(NSPoint(x:9,y:9),in:card,radius:14.4),"rounded card corner is outside")
  check(CardHitRegion.contains(NSPoint(x:22.4,y:8),in:card,radius:14.4),"rounded top tangent stays inside")
  check(!CardHitRegion.contains(NSPoint(x:240,y:70),in:card,radius:14.4),"right shadow margin is outside")
  check(CardHitRegion.contains(NSPoint(x:0,y:0),in:NSRect(x:0,y:0,width:20,height:20),radius:0),"zero radius rectangular region")
  print("\(checks) window/shape policy checks passed; no target access")
 }
}
