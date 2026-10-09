import AppKit
@main struct FocusTests {
 static func main(){
  var n=0
  func check(_ x:Bool,_ label:String){precondition(x,label);n+=1;print("PASS",label)}
  let host=HostGeometry.windowRelative(frame:NSRect(x:0,y:0,width:800,height:600),number:1)
  var s=HostSession();s.selectProcess(20);let token=s.token!
  s.accept(HostResolution(geometry:host,reason:"confirmed"),token:token,now:0)
  for i in 1...5 {s.lifecycle(.refresh);check(s.current(now:Double(i)/10) != nil,"focus refresh preserves bounded confirmed window")}
  check(s.current(now:0.61)==nil,"repeated refresh does not extend stale deadline")
  check(s.token==token,"refresh does not invalidate in-flight same-process read")
  s.accept(HostResolution(geometry:host,reason:"fresh"),token:token,now:1)
  s.lifecycle(.invalidate);check(s.current(now:1.01)==nil,"hard minimize/space/destroy invalidation immediate")
  check(!s.accept(HostResolution(geometry:host,reason:"late"),token:token,now:1.02),"old hard-invalidated result rejected")
  s.accept(HostResolution(geometry:host,reason:"new"),token:s.token!,now:1.1);check(s.current(now:1.11) != nil,"new result restores after hard transition")
  s.lifecycle(.refresh);s.accept(HostResolution(geometry:nil,reason:"confirmed hidden",kind:.hidden),token:s.token!,now:1.2)
  check(s.current(now:1.21)==nil,"real hidden result is not softened")
  let newHost=HostGeometry.windowRelative(frame:NSRect(x:300,y:100,width:800,height:600),number:2)
  s.accept(HostResolution(geometry:newHost,reason:"focus switched host"),token:s.token!,now:2)
  check(s.current(now:2.1)?.windowNumber==2,"focus refresh atomically adopts newly confirmed host")
  s.lifecycle(.restart);check(s.current(now:2.2)==nil && s.token==nil,"restart clears process and geometry")
  print("\(n) focus continuity assertions passed; synthetic state only")
 }
}
