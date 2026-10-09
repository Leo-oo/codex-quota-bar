// Standalone self-owned window experiment; compile alone, pass an existing output directory.
// Reduced strategies, not a complete upstream application; never loaded by production.
import AppKit
import CoreGraphics
let root=URL(fileURLWithPath:CommandLine.arguments[1]);let start=ProcessInfo.processInfo.systemUptime
func log(_ event:String,_ extra:[String:Any]=[:]){var j=extra;j["event"]=event;j["t"]=ProcessInfo.processInfo.systemUptime-start;if let data=try? JSONSerialization.data(withJSONObject:j,options:.sortedKeys),let h=try? FileHandle(forWritingTo:root.appendingPathComponent("events.jsonl")){h.seekToEndOfFile();h.write(data);h.write(Data([10]));try? h.close()}}
func later(_ t:Double,_ f:@escaping()->Void){DispatchQueue.main.asyncAfter(deadline:.now()+t,execute:f)}
final class P:NSPanel {override var canBecomeKey:Bool{false};override var canBecomeMain:Bool{false}}
final class D:NSObject,NSApplicationDelegate{
 var host:NSWindow!;var entry:P!;var helper:P!;var field:NSTextField!;var timer:Timer?;var mode="";var phase="";var lastBlocked:Bool?;var lastVisible:Bool?;var orders=0;var zeroListed=false;var zeroOnscreen=false;var transitions=0;var keyChanged=0;var monitor:Any?;var observed:Any?;var phaseSamples=Set<String>();var occlusionObserver:NSObjectProtocol?
 func applicationDidFinishLaunching(_ n:Notification){
  host=NSWindow(contentRect:NSRect(x:360,y:230,width:640,height:480),styleMask:[.titled,.resizable],backing:.buffered,defer:false);host.title="OWN policy comparison";host.contentView!.wantsLayer=true;host.contentView!.layer!.backgroundColor=NSColor.white.cgColor;field=NSTextField(frame:NSRect(x:150,y:80,width:220,height:30));host.contentView!.addSubview(field);host.makeKeyAndOrderFront(nil);NSApp.activate(ignoringOtherApps:true)
  entry=P(contentRect:NSRect(x:380,y:275,width:48,height:34),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);entry.isOpaque=false;entry.backgroundColor = .systemRed;entry.hidesOnDeactivate=false;entry.isFloatingPanel=false;entry.animationBehavior = .none
  helper=P(contentRect:entry.frame.insetBy(dx:-10,dy:-10),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false);helper.isOpaque=false;helper.backgroundColor = .clear;helper.hidesOnDeactivate=false;helper.hasShadow=false;helper.level = .normal
  monitor=NSEvent.addLocalMonitorForEvents(matching:[.leftMouseDown,.leftMouseUp]){e in log("delivered-input",["mode":self.mode,"phase":self.phase,"type":e.type.rawValue,"ownHost":e.window===self.host]);return e}
  timer=Timer(timeInterval:0.016,repeats:true){_ in self.sample()};RunLoop.main.add(timer!,forMode:.common)
  occlusionObserver=NotificationCenter.default.addObserver(forName:NSWindow.didChangeOcclusionStateNotification,object:entry,queue:.main){[weak self] _ in
   guard let self,self.mode=="normal-notified",self.entry.isVisible,!self.entry.occlusionState.contains(.visible) else{return}
   log("own-occlusion-notification",["mode":self.mode,"phase":self.phase])
   DispatchQueue.main.async {guard self.mode=="normal-notified",self.entry.isVisible else{return};self.sample(force:true)}
  }
  for (index,name) in ["alpha-filtered-normal","normal-notified"].enumerated(){let base=Double(index)*9+1
   later(base){self.begin(name)}
   later(base+0.7){self.phase="input";self.click()};later(base+1.1){self.click()};later(base+1.5){self.click()}
   later(base+2){self.phase="alpha-zero-helper";self.helper.backgroundColor = .clear;self.helper.alphaValue=0;self.helper.order(.above,relativeTo:self.host.windowNumber);log("phase",["mode":self.mode,"phase":self.phase])}
   later(base+3){self.phase="clear-alpha-one-helper";self.helper.alphaValue=1;self.helper.backgroundColor = .clear;self.helper.order(.above,relativeTo:self.host.windowNumber);log("phase",["mode":self.mode,"phase":self.phase])}
   later(base+4){self.phase="opaque-popup";self.helper.backgroundColor = .systemBlue;self.helper.order(.above,relativeTo:self.host.windowNumber);log("phase",["mode":self.mode,"phase":self.phase])}
   later(base+5){self.phase="popup-close";self.helper.orderOut(nil)}
   later(base+6){self.phase="host-raise";self.host.makeKeyAndOrderFront(nil)}
   later(base+7){self.phase="move";self.host.setFrame(self.host.frame.offsetBy(dx:8,dy:0),display:true);self.entry.setFrame(self.entry.frame.offsetBy(dx:8,dy:0),display:true);self.helper.setFrame(self.helper.frame.offsetBy(dx:8,dy:0),display:false)}
   later(base+8){log("mode-summary",["mode":self.mode,"orders":self.orders,"occlusionTransitions":self.transitions,"alphaZeroInOwnWindowNumbers":self.zeroListed,"alphaZeroCGOnscreen":self.zeroOnscreen]);self.entry.orderOut(nil);self.helper.orderOut(nil);self.mode=""}
  }
  later(20){self.timer?.invalidate();self.host.orderOut(nil);NSApp.terminate(nil)}
 }
 func begin(_ name:String){mode=name;phase="steady";lastBlocked=nil;lastVisible=nil;orders=0;transitions=0;zeroListed=false;zeroOnscreen=false;phaseSamples=[];helper.orderOut(nil);entry.level=name=="upstream-floating" ? .floating:.normal;entry.collectionBehavior=name=="upstream-floating" ? [.canJoinAllSpaces,.fullScreenAuxiliary,.ignoresCycle]:[.transient,.fullScreenAuxiliary];if name=="upstream-floating" {entry.orderFrontRegardless()}else{entry.order(.above,relativeTo:host.windowNumber)};orders+=1;log("begin",["mode":name])}
 func click(){let pos=field.convert(NSPoint(x:40,y:15),to:nil);for t in [NSEvent.EventType.leftMouseDown,.leftMouseUp]{if let e=NSEvent.mouseEvent(with:t,location:pos,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:host.windowNumber,context:nil,eventNumber:0,clickCount:1,pressure:1){NSApp.postEvent(e,atStart:false)}}}
 var lastPoll=0.0
 func sample(force:Bool=false){guard !mode.isEmpty else{return};let now=ProcessInfo.processInfo.systemUptime-start
  let visible=entry.occlusionState.contains(.visible)
  if lastVisible != visible{transitions+=1;log("entry-occlusion",["mode":mode,"phase":phase,"visible":visible,"orderedIn":entry.isVisible]);lastVisible=visible}
  guard force || now-lastPoll>=0.1 else{return};if !force {lastPoll=now}
  if force {log("notification-fresh-sample",["mode":mode,"phase":phase])}
  // Only this application's owned window numbers. Never allApplications.
  let ids=(NSWindow.windowNumbers(options:[]) ?? []).map(\.intValue)
  let own=[host!,entry!,helper!];let ownIDs=Set(own.map(\.windowNumber));let scoped=ids.filter{ownIDs.contains($0)}
  var rawIDs=scoped.map{UnsafeRawPointer(bitPattern:$0)}
  let idArray=CFArrayCreate(nil,&rawIDs,rawIDs.count,nil)!
  let descriptions=CGWindowListCreateDescriptionFromArray(idArray) as? [[String:Any]] ?? []
  let meta=descriptions.first{($0[kCGWindowNumber as String] as? Int)==helper.windowNumber}
  let alpha=meta?[kCGWindowAlpha as String] as? Double ?? -1;let onscreen=meta?[kCGWindowIsOnscreen as String] as? Bool ?? false
  let helperAbove=(ids.firstIndex(of:helper.windowNumber) ?? Int.max)<(ids.firstIndex(of:host.windowNumber) ?? -1)
  let included=ids.contains(helper.windowNumber)
  if phase=="alpha-zero-helper" {zeroListed = zeroListed || included;zeroOnscreen = zeroOnscreen || onscreen}
  if !phaseSamples.contains(phase){phaseSamples.insert(phase);log("actual-owned-metadata",["mode":mode,"phase":phase,"helperInOwnWindowNumbers":included,"helperCGOnscreen":onscreen,"helperCGAlpha":alpha,"helperAppKitAlpha":helper.alphaValue,"helperAboveHost":helperAbove])}
  if mode=="upstream-floating" {return}
  let blocked=helperAbove && onscreen && (alpha>0)
  let inverted=(ids.firstIndex(of:entry.windowNumber) ?? -1)>(ids.firstIndex(of:host.windowNumber) ?? Int.max)
  if lastBlocked != blocked || (!blocked && inverted){entry.ignoresMouseEvents=blocked;entry.order(blocked ? .below:.above,relativeTo:host.windowNumber);orders+=1;log("order",["mode":mode,"phase":phase,"blocked":blocked,"trigger":force ? "notification":"poll","reason":lastBlocked != blocked ? "block-change":"inversion"]);lastBlocked=blocked}
 }
}
FileManager.default.createFile(atPath:root.appendingPathComponent("events.jsonl").path,contents:nil)
let app=NSApplication.shared;app.setActivationPolicy(.regular);let d=D();app.delegate=d;app.run()
