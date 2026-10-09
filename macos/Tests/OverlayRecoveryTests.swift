import AppKit
@main struct RecoveryTests {
 static func main(){
 var count=0
 func check(_ x:Bool,_ name:String){precondition(x,name);count+=1;print("PASS",name)}
 func obs(_ ids:[Int],_ t:Double)->HostStackObservation?{HostStackObservation.read(ids,host:1,entry:2,now:t)}
 var state=OverlayOrderState()
 check(state.needsOrder(host:1,entry:2,block:nil,orderedIn:false,observation:nil,now:0),"initial attachment")
 for i in 1...60 {check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([2,1],Double(i)),now:Double(i)),"stable stack has no repeated order")}
 check(state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([1,2],61),now:61.1),"same geometry host raised repairs despite ordered-in true")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([1,2],61),now:61.2),"cached observation cannot retrigger")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([1,2],61.05),now:61.3),"in-flight pre-repair observation rejected")
 check(state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([1,2],62),now:62.1),"lost notification recovered by next poll")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([9,2,1],63),now:63.1),"covering own card does not trigger repair")
 check(state.needsOrder(host:1,entry:2,block:9,orderedIn:true,observation:obs([9,1,2],64),now:64.1),"native popup receives ordering priority")
 check(!state.needsOrder(host:1,entry:2,block:9,orderedIn:true,observation:obs([9,1,2],65),now:65.1),"blocked inversion never fights popup")
 check(state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:obs([1,2],66),now:66.1),"popup close restores entrance")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:HostStackObservation(hostNumber:7,entryNumber:2,entryBelowHost:true,sampledAt:67),now:67.1),"old host observation rejected")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:HostStackObservation(hostNumber:1,entryNumber:7,entryBelowHost:true,sampledAt:68),now:68.1),"old entry observation rejected")
 check(obs([1],69)==nil && obs([2],69)==nil,"missing stack member stays unknown")
 check(!state.needsOrder(host:1,entry:2,block:nil,orderedIn:true,observation:nil,now:70),"unknown order not guessed")
 check(state.needsOrder(host:1,entry:2,block:nil,orderedIn:false,observation:nil,now:71),"valid result after hidden state reattaches")
 var session=HostSession();session.selectProcess(8);let token=session.token!
 let host=HostGeometry.windowRelative(frame:NSRect(x:0,y:0,width:800,height:600),number:1)
 for i in 0..<8 {
  let t=Double(i)*2
  session.accept(HostResolution(geometry:nil,reason:"transient focus/read",kind:.hidden),token:session.token!,now:t)
  check(session.current(now:t)==nil,"transient unavailable hides")
  session.accept(HostResolution(geometry:host,reason:"fresh poll"),token:session.token!,now:t+0.1)
  check(session.current(now:t+0.2) != nil,"good polling result recovers without app activation")
 }
 session.invalidate("foreground transition",forgetProcess:true);session.selectProcess(8)
 check(!session.accept(HostResolution(geometry:host,reason:"old"),token:token,now:20),"late pre-activation result rejected")
 session.accept(HostResolution(geometry:host,reason:"new"),token:session.token!,now:20.1)
 check(session.current(now:20.2) != nil,"new foreground result recovers")
 print("\(count) attachment recovery checks passed; synthetic windows/order/state")
 }
}
