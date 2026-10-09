import AppKit
@main struct WindowTests {
 static func main(){var count=0
 func check(_ v:Bool,_ name:String){precondition(v,name);count+=1;print("PASS",name)}
 let f=NSRect(x:200,y:100,width:900,height:700)
 func host(_ frame:NSRect,_ id:Int=3)->HostGeometry{HostGeometry.windowRelative(frame:frame,number:id)}
 let h=host(f),s=NSSize(width:48,height:33),d=NSSize(width:48,height:66)
 check(h.windowAnchor != nil && h.avatar == .zero,"explicit window anchor has no detected avatar")
 check(Placement.entry(host:h,size:s,offset:0) != nil,"entry does not require account nodes")
 for delta in [NSPoint(x:320,y:90),NSPoint(x:-1500,y:-600)] {
  check(Placement.entry(host:host(f.offsetBy(dx:delta.x,dy:delta.y)),size:s,offset:0)==Placement.entry(host:h,size:s,offset:0)!.offsetBy(dx:delta.x,dy:delta.y),"positive/negative screen translation")
 }
 check(Placement.entry(host:host(NSRect(x:200,y:100,width:600,height:400)),size:s,offset:0)==Placement.entry(host:h,size:s,offset:0),"resize preserves local position")
 let moved=h.calibrated(horizontal:80)
 check(Placement.entry(host:moved,size:s,offset:0)!.minX==Placement.entry(host:h,size:s,offset:0)!.minX+80,"horizontal calibration")
 check(moved.railRight==h.railRight+80,"menu anchor follows horizontal calibration")
 check(Placement.entry(host:h,size:s,offset:-8)!.minY==Placement.entry(host:h,size:s,offset:0)!.minY+8,"vertical calibration")
 check(Placement.entry(host:h,size:d,offset:0)!.minY==Placement.entry(host:h,size:s,offset:0)!.minY,"single/dual bottom anchor shared")
 check(Placement.entry(host:h,size:NSSize(width:48,height:47),offset:0) != nil,"quota failure height does not disable follow")
 let small=host(NSRect(x:0,y:0,width:520,height:320)).calibrated(horizontal:600)
 check(Placement.entry(host:small,size:d,offset:-258).map{small.frame.contains($0)} == true,"resize clamps rendered placement without changing saved offset")
 let screen=NSRect(x:-1600,y:-300,width:1600,height:900)
 let popup=Placement.popup(host:host(NSRect(x:-1500,y:-200,width:900,height:700)),size:NSSize(width:228,height:166),screen:screen)
 check(screen.contains(popup),"popup respects negative-origin display")
 // Point-space geometry is independent of 1x/2x backing pixels.
 for scale:CGFloat in [1,2] {let rect=Placement.entry(host:h,size:s,offset:0)!;let pixels=NSRect(x:rect.minX*scale,y:rect.minY*scale,width:rect.width*scale,height:rect.height*scale);check(pixels.width/scale==48,"backing scale preserves logical entry width")}
 let path=URL(fileURLWithPath:CommandLine.arguments[1]).appendingPathComponent("calibration-test.json")
 let prefs=Preferences(path);check(prefs.save(offset:-16,horizontal:80),"XY saved atomically")
 let restored=Preferences(path);check(restored.offset == -16 && restored.horizontal==80,"XY restart restore")
 check(restored.save(read:["a":"1"]) && Preferences(path).horizontal==80,"news persistence does not erase calibration")
 check(!restored.save(horizontal:.nan),"nonfinite calibration rejected")
 var session=HostSession();session.selectProcess(20,launchedAt:Date(timeIntervalSince1970:1));let token=session.token!
 session.accept(HostResolution(geometry:h,reason:"window"),token:token,now:0)
 check(session.current(now:0.1)?.windowAnchor != nil,"window-only state accepted")
 session.invalidate("Space transition");check(session.current(now:0.2)==nil,"Space transition invalidates stale window")
 check(!session.accept(HostResolution(geometry:h,reason:"late"),token:token,now:0.3),"late old-Space result rejected")
 let t=session.token!;session.accept(HostResolution(geometry:host(f,4),reason:"new fullscreen window"),token:t,now:1)
 check(session.current(now:1.1)?.windowNumber==4,"new fullscreen/window identity replaces old")
 check(session.current(now:1.7)==nil,"stale window state expires")
 session.invalidate("host terminated",forgetProcess:true);check(session.current(now:2)==nil,"termination hides")
 session.selectProcess(20,launchedAt:Date(timeIntervalSince1970:2));check(session.token != t,"same PID restart gets new generation")
 let left=NSRect(x:-1000,y:0,width:1000,height:800),right=NSRect(x:0,y:0,width:1000,height:800)
 let crossing=NSRect(x:-20,y:20,width:900,height:700)
 check(HostWindowPolicy.display(in:[left,right],anchor:NSPoint(x:12,y:156),host:crossing)==right,"entry screen wins across display boundary")
 check(HostWindowPolicy.display(in:[left,right],anchor:nil,host:crossing)==right,"largest intersection used without entry")
 check(HostWindowPolicy.display(in:[left,right],anchor:NSPoint(x:-10,y:156),host:crossing)==left,"anchor display wins even if host mostly elsewhere")
 let boundedPopup=Placement.popup(host:small,size:NSSize(width:228,height:166),screen:NSRect(x:0,y:0,width:800,height:600))
 check(NSRect(x:0,y:0,width:800,height:600).contains(boundedPopup),"clamped extreme calibration menu remains on usable screen")

 let fresh=HostGeometry.windowRelative(frame:f,number:3,controls:[])
 let initial=Placement.entry(host:fresh,size:s,offset:0)!
 check(initial.midX==f.minX+26 && initial.minY+8-(f.minY+40)==25,"reference screenshot 2x assumption yields 50 image-pixel visible gap")
 let legacyURL=path.deletingLastPathComponent().appendingPathComponent("legacy-anchor-test.json")
 try! Data(#"{"schemaVersion":2,"offset":-16,"horizontal":8}"#.utf8).write(to:legacyURL)
 let legacy=Preferences(legacyURL)
 check(legacy.legacyAnchor && legacy.offset == -16 && legacy.horizontal==8,"old manual calibration retained")
 check(legacy.calibrated(fresh).windowAnchor==NSPoint(x:f.minX+40,y:f.minY+136),"old calibrated window anchor preserved on migration")
 check(legacy.save(theme:"dark") && Preferences(legacyURL).legacyAnchor,"theme save retains old calibrated base")
 check(legacy.save(offset:0,horizontal:0,legacyAnchor:false) && !Preferences(legacyURL).legacyAnchor,"reset adopts screenshot reference base")
 check(!legacy.save(theme:"invalid") && legacy.theme=="dark","unknown theme rejected")
 print("\(count) window-follow checks passed; synthetic state/coordinates only")
 }
}
