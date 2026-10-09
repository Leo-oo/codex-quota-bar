// Standalone reduced experiment. Only two applications launched for this test.
// No AX, CG window enumeration, screenshots, or inspection of any other application.
// Compile into separate host/overlay bundles. Arguments: role output-directory.
import AppKit
let role=CommandLine.arguments[1],root=URL(fileURLWithPath:CommandLine.arguments[2])
let start=ProcessInfo.processInfo.systemUptime
func write(_ name:String,_ value:[String:Any]) {if let d=try? JSONSerialization.data(withJSONObject:value,options:.sortedKeys){try? d.write(to:root.appendingPathComponent(name),options:.atomic)}}
func read(_ name:String)->[String:Any]? {guard let d=try? Data(contentsOf:root.appendingPathComponent(name)) else{return nil};return (try? JSONSerialization.jsonObject(with:d)) as? [String:Any]}
func log(_ event:String,_ values:[String:Any]=[:]){var j=values;j["event"]=event;j["t"]=ProcessInfo.processInfo.systemUptime;j["pid"]=ProcessInfo.processInfo.processIdentifier;j["active"]=NSApp.isActive;guard let d=try? JSONSerialization.data(withJSONObject:j,options:.sortedKeys),let h=try? FileHandle(forWritingTo:root.appendingPathComponent(role+".jsonl")) else{return};h.seekToEndOfFile();h.write(d);h.write(Data([10]));try? h.close()}
final class HostView:NSView {override func mouseDown(with e:NSEvent){log("queued-click-delivered",["ownWindow":window?.windowNumber ?? 0])}}
final class Panel:NSPanel {override var canBecomeKey:Bool{false};override var canBecomeMain:Bool{false}}
final class D:NSObject,NSApplicationDelegate {
 var host:NSWindow?,popup:Panel?,entry:Panel?,timer:Timer?,observer:NSObjectProtocol?
 var mode="",phase="",sequence=0,lastCommand=0,hostID=0,blocked=false,hidden=false,orders=0,lastVisible:Bool?,began:Double?,lastPoll=0.0,clickSent=0
 func applicationDidFinishLaunching(_ note:Notification){
  FileManager.default.createFile(atPath:root.appendingPathComponent(role+".jsonl").path,contents:nil)
  if role=="host" {
   let w=NSWindow(contentRect:NSRect(x:360,y:250,width:640,height:480),styleMask:[.titled,.resizable],backing:.buffered,defer:false);w.title="SELF-OWNED TWO-PROCESS HOST";w.contentView=HostView();w.backgroundColor = .white;w.makeKeyAndOrderFront(nil);host=w
   let p=Panel(contentRect:NSRect(x:370,y:295,width:90,height:60),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);p.backgroundColor = .blue;p.hidesOnDeactivate=false;p.level = .normal;popup=p
   write("host-ready.json",["id":w.windowNumber,"pid":ProcessInfo.processInfo.processIdentifier])
  } else {
   let p=Panel(contentRect:NSRect(x:380,y:305,width:48,height:33),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);p.backgroundColor = .red;p.isOpaque=false;p.hidesOnDeactivate=false;p.isFloatingPanel=false;p.animationBehavior = .none;p.collectionBehavior=[.transient,.fullScreenAuxiliary];entry=p
   observer=NotificationCenter.default.addObserver(forName:NSWindow.didChangeOcclusionStateNotification,object:p,queue:.main){[weak self] _ in guard let self else{return};self.observe("notification");if self.mode=="normal-notified",p.isVisible,!p.occlusionState.contains(.visible),!self.blocked,!self.hidden {DispatchQueue.main.async{self.repair("notification")}}}
  }
  timer=Timer(timeInterval:0.01,repeats:true){[weak self] _ in self?.tick()};RunLoop.main.add(timer!,forMode:.common)
 }
 func command(_ action:String){sequence+=1;write("command.json",["n":sequence,"action":action,"mode":mode]);log("command",["action":action,"mode":mode])}
 func observe(_ reason:String){guard let p=entry else{return};let v=p.occlusionState.contains(.visible);if lastVisible != v {log("occlusion",["visible":v,"orderedIn":p.isVisible,"mode":mode,"phase":phase,"reason":reason]);lastVisible=v}}
 func repair(_ reason:String){guard let p=entry,hostID>0,!hidden,!blocked,mode=="normal-notified",!p.occlusionState.contains(.visible) else{return};p.order(.above,relativeTo:hostID);orders+=1;log("relative-order",["mode":mode,"phase":phase,"reason":reason,"orders":orders])}
 func begin(_ name:String){mode=name;phase="steady";clickSent=0;blocked=false;hidden=false;orders=0;lastVisible=nil;entry!.level=name=="upstream-floating" ? .floating:.normal;if name=="upstream-floating" {entry!.orderFrontRegardless()}else{entry!.order(.above,relativeTo:hostID)};orders+=1;command("activate");log("begin",["mode":name,"entryID":entry!.windowNumber,"knownOwnHostID":hostID])}
 func tick(){
  let now=ProcessInfo.processInfo.systemUptime
  if now-start>25 {NSApp.terminate(nil);return}
  if role=="host" {
   guard let cmd=read("command.json"),let n=cmd["n"] as? Int,n != lastCommand,let action=cmd["action"] as? String else{return};lastCommand=n
   switch action {
   case "activate": NSApp.activate(ignoringOtherApps:true);host!.makeKeyAndOrderFront(nil)
   case "click": for t in [NSEvent.EventType.leftMouseDown,.leftMouseUp] {if let e=NSEvent.mouseEvent(with:t,location:NSPoint(x:160,y:180),modifierFlags:[],timestamp:now,windowNumber:host!.windowNumber,context:nil,eventNumber:n,clickCount:1,pressure:1){NSApp.postEvent(e,atStart:false)}}
   case "raise":host!.makeKeyAndOrderFront(nil)
   case "popup":popup!.order(.above,relativeTo:host!.windowNumber)
   case "close-popup":popup!.orderOut(nil)
   case "finish":NSApp.terminate(nil)
   default:break
   }
   log("host-command",["action":action,"mode":cmd["mode"] ?? ""]);return
  }
  if began==nil {guard let ready=read("host-ready.json"),let id=ready["id"] as? Int else{return};hostID=id;began=now;begin("normal-notified")}
  let elapsed=now-began!,part=elapsed.truncatingRemainder(dividingBy:8)
  if elapsed>=16 {log("finished");command("finish");entry!.orderOut(nil);NSApp.terminate(nil);return}
  if elapsed>=8,mode != "upstream-floating" {begin("upstream-floating")}
  let next:String=part<1 ? "steady":part<2 ? "click":part<3 ? "raise":part<4 ? "popup":part<5 ? "close-popup":part<6 ? "manual-hide":part<7 ? "restore":"settle"
  if next != phase {phase=next;log("phase",["mode":mode,"phase":phase]);switch next {
   case "click","raise":command(next)
   case "popup":blocked=true;command(next);if mode=="normal-notified"{entry!.order(.below,relativeTo:hostID)}
   case "close-popup":command(next);blocked=false;entry!.order(.above,relativeTo:hostID)
   case "manual-hide":hidden=true;entry!.orderOut(nil);command("raise")
   case "restore":hidden=false;entry!.order(.above,relativeTo:hostID)
   default:break
  }}
  if phase=="click" {let count=Int((part-1)*4);if count>clickSent {clickSent=count;command("click")}}
  observe("10ms-sample");if now-lastPoll>=0.1 {lastPoll=now;repair("100ms-fallback")}
 }
}
let app=NSApplication.shared;app.setActivationPolicy(.regular);let d=D();app.delegate=d;app.run()
