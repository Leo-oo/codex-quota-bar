// Compile as main.swift together with the unmodified production FloatingAttachment.swift.
// Only self-created host/overlay/other processes communicate through this directory.
import AppKit
let role=CommandLine.arguments[1],root=URL(fileURLWithPath:CommandLine.arguments[2])
let started=ProcessInfo.processInfo.systemUptime
func save(_ name:String,_ value:[String:Any]){if let d=try? JSONSerialization.data(withJSONObject:value,options:.sortedKeys){try? d.write(to:root.appendingPathComponent(name),options:.atomic)}}
func read(_ name:String)->[String:Any]?{guard let d=try? Data(contentsOf:root.appendingPathComponent(name)) else{return nil};return (try? JSONSerialization.jsonObject(with:d)) as? [String:Any]}
func log(_ event:String,_ values:[String:Any]=[:]){var v=values;v["event"]=event;v["t"]=ProcessInfo.processInfo.systemUptime;v["role"]=role;guard let d=try? JSONSerialization.data(withJSONObject:v,options:.sortedKeys),let f=try? FileHandle(forWritingTo:root.appendingPathComponent(role+".jsonl")) else{return};f.seekToEndOfFile();f.write(d);f.write(Data([10]));try? f.close()}
final class Entry:NSPanel {override var canBecomeKey:Bool{false};override var canBecomeMain:Bool{false};var shows=0;override func orderFrontRegardless(){shows+=1;super.orderFrontRegardless()}}
final class D:NSObject,NSApplicationDelegate {
 var host:NSWindow!,popup:Entry!,entry:Entry!,input:NSTextField!,timer:Timer?
 var attach=FloatingAttachment(),begin:Double?,phase="",seq=0,last=0,clicks=0,hidden=false,lastText=""
 func command(_ s:String){seq+=1;save("command.json",["seq":seq,"action":s]);log("command",["action":s])}
 func applicationDidFinishLaunching(_ n:Notification){
  FileManager.default.createFile(atPath:root.appendingPathComponent(role+".jsonl").path,contents:nil)
  if role=="host" || role=="other" {
   host=NSWindow(contentRect:NSRect(x:role=="host" ? 360:1050,y:250,width:640,height:480),styleMask:[.titled,.miniaturizable,.closable,.resizable],backing:.buffered,defer:false);host.isReleasedWhenClosed=false;host.title="SELF OWNED candidate17 "+role;host.backgroundColor = .white
   input=NSTextField(frame:NSRect(x:120,y:100,width:220,height:28));host.contentView!.addSubview(input)
   popup=Entry(contentRect:NSRect(x:380,y:305,width:48,height:33),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);popup.backgroundColor = .blue;popup.isOpaque=true;popup.hasShadow=false;popup.hidesOnDeactivate=false
   if role=="host" {host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)}
  } else {
   entry=Entry(contentRect:NSRect(x:380,y:305,width:48,height:33),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);entry.backgroundColor = .red;entry.isOpaque=true;entry.hasShadow=false;entry.hidesOnDeactivate=false;entry.isFloatingPanel=false;entry.animationBehavior = .none
  }
  timer=Timer(timeInterval:0.01,repeats:true){[weak self] _ in self?.tick()};RunLoop.main.add(timer!,forMode:.common)
 }
 func tick(){let now=ProcessInfo.processInfo.systemUptime
  if now-started>25 {NSApp.terminate(nil);return}
  if role != "overlay" {
   if role=="host" {
    save("host-state.json",["id":host.windowNumber,"active":NSApp.isActive,"visible":host.isVisible,"mini":host.isMiniaturized,"frame":NSStringFromRect(host.frame)])
    if input.stringValue != lastText {lastText=input.stringValue;log("typed",["text":lastText,"hostActive":NSApp.isActive,"hostKey":host.isKeyWindow])}
   }
   guard let c=read("command.json"),let n=c["seq"] as? Int,n != last,let action=c["action"] as? String else{return};last=n
   if action=="finish" {NSApp.terminate(nil);return}
   if role=="other" {if action=="switch-other" {host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true);log("activated-other")};return}
   switch action {
   case "click":
    for t in [NSEvent.EventType.leftMouseDown,.leftMouseUp]{if let e=NSEvent.mouseEvent(with:t,location:NSPoint(x:150,y:114),modifierFlags:[],timestamp:now,windowNumber:host.windowNumber,context:nil,eventNumber:n,clickCount:1,pressure:1){NSApp.postEvent(e,atStart:false)}}
    if let e=NSEvent.keyEvent(with:.keyDown,location:.zero,modifierFlags:[],timestamp:now,windowNumber:host.windowNumber,context:nil,characters:"x",charactersIgnoringModifiers:"x",isARepeat:false,keyCode:7){NSApp.postEvent(e,atStart:false)}
   case "raise":host.makeKeyAndOrderFront(nil)
   case "move-resize":host.setFrame(host.frame.offsetBy(dx:24,dy:16),display:true);host.setContentSize(NSSize(width:680,height:510))
   case "popup","higher-popup":popup.level=action=="popup" ? .normal:.modalPanel;popup.setFrame(NSRect(x:host.frame.minX+20,y:host.frame.minY+55,width:48,height:33),display:true);popup.orderFrontRegardless()
   case "close-popup":popup.orderOut(nil)
   case "minimize":host.miniaturize(nil)
   case "restore":host.deminiaturize(nil);host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)
   case "activate-host":host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)
   case "close-host":host.close()
   case "reopen-host":host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)
   default:break
   }
   log("host-command",["action":action]);return
  }
  guard let state=read("host-state.json"),let id=state["id"] as? Int,let frameText=state["frame"] as? String else{return}
  if begin==nil {begin=now}
  let t=now-begin!
  if t>=19 {command("finish");attach.apply(to:entry,host:nil,hidden:true);NSApp.terminate(nil);return}
  let phases=["steady","click","settle","raise","move-resize","popup","close-popup","higher-popup","close-popup","manual-hide","manual-show","minimize","restore","switch-other","activate-host","close-host","reopen-host","settle","settle"]
  let index=min(18,Int(t)),next="\(index)-\(phases[index])"
  if phase != next {phase=next;let action=phases[index];if action=="manual-hide"{hidden=true;command("raise")}else if action=="manual-show"{hidden=false}else if !["steady","settle"].contains(action){command(action)}}
  if index==1 {let count=Int((t-1)*4);if count>clicks{clicks=count;command("click")}}
  let available=(state["active"] as? Bool)==true && (state["visible"] as? Bool)==true && (state["mini"] as? Bool)==false
  let hostFrame=NSRectFromString(frameText),target=NSRect(x:hostFrame.minX+20,y:hostFrame.minY+55,width:48,height:33)
  if entry.frame != target {entry.setFrame(target,display:true)}
  attach.apply(to:entry,host:available ? id:nil,hidden:hidden)
  log("sample",["phase":phase,"visible":entry.isVisible,"occluded":!entry.occlusionState.contains(.visible),"available":available,"hidden":hidden,"shows":entry.shows,"level":entry.level.rawValue,"key":entry.isKeyWindow,"overlayActive":NSApp.isActive,"canKey":entry.canBecomeKey,"frame":NSStringFromRect(entry.frame)])
 }
}
let app=NSApplication.shared;app.setActivationPolicy(.regular);let d=D();app.delegate=d;app.run()
