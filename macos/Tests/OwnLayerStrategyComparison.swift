// Standalone reduced experiment. Only two applications launched for this test.
// No AX, CG window enumeration, screenshots, or inspection of any other application.
// Compile into separate host/overlay bundles. Arguments: role output-directory.
// Four reduced level/attachment strategies; not the full production host reader.
// Opaque equal-sized probes measure whole-window occlusion, not UI pixel visibility.
import AppKit
let role=CommandLine.arguments[1],root=URL(fileURLWithPath:CommandLine.arguments[2])
let start=ProcessInfo.processInfo.systemUptime
func write(_ name:String,_ value:[String:Any]) {if let d=try? JSONSerialization.data(withJSONObject:value,options:.sortedKeys){try? d.write(to:root.appendingPathComponent(name),options:.atomic)}}
func read(_ name:String)->[String:Any]? {guard let d=try? Data(contentsOf:root.appendingPathComponent(name)) else{return nil};return (try? JSONSerialization.jsonObject(with:d)) as? [String:Any]}
func log(_ event:String,_ values:[String:Any]=[:]){var j=values;j["event"]=event;j["t"]=ProcessInfo.processInfo.systemUptime;j["pid"]=ProcessInfo.processInfo.processIdentifier;j["active"]=NSApp.isActive;guard let d=try? JSONSerialization.data(withJSONObject:j,options:.sortedKeys),let h=try? FileHandle(forWritingTo:root.appendingPathComponent(role+".jsonl")) else{return};h.seekToEndOfFile();h.write(d);h.write(Data([10]));try? h.close()}
final class HostView:NSView {override func mouseDown(with e:NSEvent){log("queued-click-delivered",["ownWindow":window?.windowNumber ?? 0])}}
final class Panel:NSPanel {override var canBecomeKey:Bool{false};override var canBecomeMain:Bool{false}}
final class D:NSObject,NSApplicationDelegate {
 var host:NSWindow?,popup:Panel?,entry:Panel?,child:Panel?,timer:Timer?,observer:NSObjectProtocol?
 var popupObserver:NSObjectProtocol?
 var popupLast:Bool?,childLast:Bool?,hostMode="",hostPhase=""
 var mode="",phase="",sequence=0,lastCommand=0,hostID=0,blocked=false,hidden=false,orders=0,lastVisible:Bool?,began:Double?,lastPoll=0.0,clickSent=0
 func applicationDidFinishLaunching(_ note:Notification){
  FileManager.default.createFile(atPath:root.appendingPathComponent(role+".jsonl").path,contents:nil)
  if role=="host" {
   let w=NSWindow(contentRect:NSRect(x:360,y:250,width:640,height:480),styleMask:[.titled,.resizable],backing:.buffered,defer:false);w.title="SELF-OWNED TWO-PROCESS HOST";w.contentView=HostView();w.backgroundColor = .white;w.makeKeyAndOrderFront(nil);host=w
   let p=Panel(contentRect:NSRect(x:380,y:305,width:48,height:33),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);p.backgroundColor = .blue;p.isOpaque=true;p.hasShadow=false;p.hidesOnDeactivate=false;p.level = .normal;popup=p
   let c=Panel(contentRect:p.frame,styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);c.backgroundColor = .red;c.isOpaque=true;c.hasShadow=false;c.hidesOnDeactivate=false;c.level = .normal;child=c
   observer=NotificationCenter.default.addObserver(forName:NSWindow.didChangeOcclusionStateNotification,object:c,queue:.main){[weak self] _ in self?.hostObservations()}
   popupObserver=NotificationCenter.default.addObserver(forName:NSWindow.didChangeOcclusionStateNotification,object:p,queue:.main){[weak self] _ in self?.hostObservations()}
   write("host-ready.json",["id":w.windowNumber,"pid":ProcessInfo.processInfo.processIdentifier])
  } else {
   let p=Panel(contentRect:NSRect(x:380,y:305,width:48,height:33),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);p.backgroundColor = .red;p.isOpaque=true;p.hasShadow=false;p.hidesOnDeactivate=false;p.isFloatingPanel=false;p.animationBehavior = .none;p.collectionBehavior=[.transient,.fullScreenAuxiliary];entry=p
   observer=NotificationCenter.default.addObserver(forName:NSWindow.didChangeOcclusionStateNotification,object:p,queue:.main){[weak self] _ in guard let self else{return};self.observe("notification");if self.mode=="normal-notified",p.isVisible,!p.occlusionState.contains(.visible),!self.blocked,!self.hidden {DispatchQueue.main.async{self.repair("notification")}}}
  }
  timer=Timer(timeInterval:0.01,repeats:true){[weak self] _ in self?.tick()};RunLoop.main.add(timer!,forMode:.common)
 }
 func command(_ action:String){sequence+=1;write("command.json",["n":sequence,"action":action,"mode":mode]);log("command",["action":action,"mode":mode])}
 func observe(_ reason:String){guard let p=entry else{return};let v=p.occlusionState.contains(.visible);if lastVisible != v {log("occlusion",["visible":v,"orderedIn":p.isVisible,"mode":mode,"phase":phase,"reason":reason]);lastVisible=v}}
 func repair(_ reason:String){guard let p=entry,hostID>0,!hidden,!blocked,mode=="normal-notified",!p.occlusionState.contains(.visible) else{return};p.order(.above,relativeTo:hostID);orders+=1;log("relative-order",["mode":mode,"phase":phase,"reason":reason,"orders":orders])}
 func begin(_ name:String){mode=name;phase="steady";clickSent=0;blocked=false;hidden=false;orders=0;lastVisible=nil
  entry!.orderOut(nil);entry!.level=(name=="upstream-floating" || name=="floating-yield") ? .floating:.normal
  if name=="host-child" {log("cross-process-window-object",["lookupReturnedObject":NSApp.window(withWindowNumber:hostID) != nil,"knownOwnHostID":hostID])}
  else if name=="hidden-baseline" {}
  else if name=="normal-notified" {entry!.order(.above,relativeTo:hostID)}else{entry!.orderFrontRegardless()}
  orders+=1;command("activate");log("begin",["mode":name,"entryID":entry!.windowNumber,"knownOwnHostID":hostID])
 }
 func hostObservations(){
  if let p=popup,p.isVisible {let v=p.occlusionState.contains(.visible);if popupLast != v {popupLast=v;log("popup-occlusion",["mode":hostMode,"phase":hostPhase,"visible":v])}}else{popupLast=nil}
  if let p=child,p.isVisible {let v=p.occlusionState.contains(.visible);if childLast != v {childLast=v;log("occlusion",["mode":hostMode,"phase":hostPhase,"visible":v,"orderedIn":true])}}else{childLast=nil}
 }

 func tick(){
  let now=ProcessInfo.processInfo.systemUptime
  if now-start>50 {NSApp.terminate(nil);return}
  if role=="host" {
   hostObservations()
   guard let cmd=read("command.json"),let n=cmd["n"] as? Int,n != lastCommand,let action=cmd["action"] as? String else{return};lastCommand=n;hostMode=cmd["mode"] as? String ?? "";hostPhase=action
   switch action {
   case "activate":
    if let parent=child!.parent {parent.removeChildWindow(child!)};child!.orderOut(nil)
    if let parent=popup!.parent {parent.removeChildWindow(popup!)};popup!.orderOut(nil)
    if hostMode=="host-child" {host!.addChildWindow(child!,ordered:.above);child!.orderFront(nil)}
    NSApp.activate(ignoringOtherApps:true);host!.makeKeyAndOrderFront(nil)
   case "click": for t in [NSEvent.EventType.leftMouseDown,.leftMouseUp] {if let e=NSEvent.mouseEvent(with:t,location:NSPoint(x:160,y:180),modifierFlags:[],timestamp:now,windowNumber:host!.windowNumber,context:nil,eventNumber:n,clickCount:1,pressure:1){NSApp.postEvent(e,atStart:false)}}
   case "raise":host!.makeKeyAndOrderFront(nil)
   case "popup":
    if hostMode=="host-child" {host!.addChildWindow(popup!,ordered:.above);popup!.order(.above,relativeTo:child!.windowNumber)}else{popup!.order(.above,relativeTo:host!.windowNumber)}
    write("popup-state.json",["open":true,"mode":hostMode]);log("popup-ordered",["mode":hostMode])
   case "close-popup":if let parent=popup!.parent {parent.removeChildWindow(popup!)};popup!.orderOut(nil);write("popup-state.json",["open":false,"mode":hostMode])
   case "manual-hide":child!.orderOut(nil);host!.makeKeyAndOrderFront(nil)
   case "restore":if hostMode=="host-child" {child!.order(.above,relativeTo:host!.windowNumber)}
   case "finish":NSApp.terminate(nil)
   default:break
   }
   log("host-command",["action":action,"mode":cmd["mode"] ?? ""]);return
  }
  if began==nil {guard let ready=read("host-ready.json"),let id=ready["id"] as? Int else{return};hostID=id;began=now;begin("normal-notified")}
  let elapsed=now-began!,part=elapsed.truncatingRemainder(dividingBy:8)
  if elapsed>=40 {log("finished");command("finish");entry!.orderOut(nil);NSApp.terminate(nil);return}
  let desired=["normal-notified","upstream-floating","floating-yield","host-child","hidden-baseline"][min(4,Int(elapsed/8))]
  if mode != desired {begin(desired)}
  let next:String=part<1 ? "steady":part<2 ? "click":part<3 ? "raise":part<4 ? "popup":part<5 ? "close-popup":part<6 ? "manual-hide":part<7 ? "restore":"settle"
  if next != phase {phase=next;log("phase",["mode":mode,"phase":phase]);switch next {
   case "click","raise":command(next)
   case "popup":command(next)
   case "close-popup":command(next)
   case "manual-hide":hidden=true;entry!.orderOut(nil);command(next)
   case "restore":hidden=false;if mode != "host-child" && mode != "hidden-baseline" {entry!.order(.above,relativeTo:hostID)};command(next)
   default:break
  }}
  if phase=="click" {let count=Int((part-1)*4);if count>clickSent {clickSent=count;command("click")}}
  if let state=read("popup-state.json"),state["mode"] as? String==mode,let open=state["open"] as? Bool,blocked != open {
   blocked=open
   if !hidden && (mode=="normal-notified" || mode=="floating-yield") {
    if mode=="floating-yield" {entry!.level=open ? .normal:.floating}
    entry!.order(open ? .below:.above,relativeTo:hostID);log("popup-state-order",["mode":mode,"open":open])
   }
  }
  if mode != "host-child" && mode != "hidden-baseline" {observe("10ms-sample")};if now-lastPoll>=0.1 {lastPoll=now;repair("100ms-fallback")}
 }
}
let app=NSApplication.shared;app.setActivationPolicy(.regular);let d=D();app.delegate=d;app.run()
