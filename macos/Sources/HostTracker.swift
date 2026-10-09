import AppKit
#if SELF_HOST_ONLY
// Validation builds contain no external application access implementation.
final class HostTracker {
 var onChange:(()->Void)?
 let scan=HostScanMetrics()
 let status="自有宿主验证；真实应用适配器未编入"
 func requestRefresh(){onChange?()}
 func current(entryWindowNumber:Int?=nil)->HostGeometry? {fatalError("SELF_HOST_ONLY cannot inspect an external host")}
}
#else
import ApplicationServices

// Production adapter: compiled, never executed by agent-side acceptance tests.
// UI strings are reduced to semantic booleans in memory, never exported or logged.
final class HostTracker {
 var onChange:(()->Void)?
 var scan=HostScanMetrics()
 var status:String {session.status}
 private var session=HostSession()
 private var requestedAt:TimeInterval = -.infinity
 private var pending=false
 private var needsRefresh=true
 private let worker=DispatchQueue(label:"leo.staley.host-reader",qos:.userInitiated)
 private lazy var lifecycle=HostLifecycle { [weak self] change in
  guard let self else{return}
  self.session.lifecycle(change)
  self.needsRefresh=true;self.onChange?()
 }
 // Own entry occlusion is a prompt to re-read, never proof to raise or hide.
 func requestRefresh(){needsRefresh=true;onChange?()}
 func current(entryWindowNumber:Int?=nil)->HostGeometry? {
  guard let app=NSWorkspace.shared.runningApplications.first(where:{$0.bundleIdentifier=="com.openai.codex" && !$0.isHidden && !$0.isTerminated}) else {suspend("等待 Codex 窗口");return nil}
  let front=NSWorkspace.shared.frontmostApplication?.processIdentifier
  guard let front else{return session.processIdentity==HostProcessIdentity(pid:app.processIdentifier,launchedAt:app.launchDate) ? session.current(now:ProcessInfo.processInfo.systemUptime):nil}
  guard front==app.processIdentifier || front==ProcessInfo.processInfo.processIdentifier else {suspend("Codex 不在前台");return nil}
  session.selectProcess(app.processIdentifier,launchedAt:app.launchDate);lifecycle.selectProcess(HostProcessIdentity(pid:app.processIdentifier,launchedAt:app.launchDate))
  let now=ProcessInfo.processInfo.systemUptime
  // Events request an early scan, but never enqueue parallel or unbounded full-tree reads.
  let interval:TimeInterval=needsRefresh ? 0:0.1
  if !pending,now-requestedAt>=interval,let token=session.token {
   pending=true;requestedAt=now;needsRefresh=false
   let top=NSScreen.screens.first?.frame.maxY ?? 0
   worker.async {let result=HostReader.read(pid:token.processIdentifier,desktopTop:top,entryWindowNumber:entryWindowNumber)
    DispatchQueue.main.async {
     self.pending=false
     guard self.session.accept(result.0,token:token,now:ProcessInfo.processInfo.systemUptime) else{self.onChange?();return}
     self.scan=result.1
     if let window=result.2 {self.lifecycle.selectWindow(window)}
     self.onChange?()
    }
   }
  }
  return session.current(now:now)
 }
 private func suspend(_ message:String){
  if session.processIdentifier != nil || session.status != message {session.invalidate(message,forgetProcess:true)}
  lifecycle.stopTarget();needsRefresh=true
 }
}

// Production-only observer. SELF_HOST_ONLY builds exclude every external AX call.
// Events invalidate cached identities; reading stays on the bounded worker above.
private final class HostLifecycle {
 private let changed:(HostLifecycleChange)->Void
 private var workspaceObservers:[NSObjectProtocol]=[]
 private var screenObserver:NSObjectProtocol?
 private var identity:HostProcessIdentity?
 private var lastObserverAttempt:TimeInterval = -.infinity
 private var observer:AXObserver?
 private var application:AXUIElement?
 private var window:AXUIElement?
 private static let appEvents=[kAXFocusedWindowChangedNotification,kAXMainWindowChangedNotification,kAXWindowCreatedNotification]
 private static let windowEvents=[kAXUIElementDestroyedNotification,kAXWindowMiniaturizedNotification,kAXWindowDeminiaturizedNotification,kAXMovedNotification,kAXResizedNotification]
 init(changed:@escaping(HostLifecycleChange)->Void){
  self.changed=changed
  let center=NSWorkspace.shared.notificationCenter
  for name in [NSWorkspace.didActivateApplicationNotification,NSWorkspace.didLaunchApplicationNotification,NSWorkspace.didTerminateApplicationNotification,NSWorkspace.didHideApplicationNotification,NSWorkspace.didUnhideApplicationNotification,NSWorkspace.activeSpaceDidChangeNotification,NSWorkspace.didWakeNotification] {
   workspaceObservers.append(center.addObserver(forName:name,object:nil,queue:.main){[weak self] notification in
    guard let self else{return}
    if let app=notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication {
     if name != NSWorkspace.didActivateApplicationNotification && app.bundleIdentifier != "com.openai.codex" {return}
     // Moving focus into our menu must not destroy its host anchor.
     if name == NSWorkspace.didActivateApplicationNotification && (app.processIdentifier==ProcessInfo.processInfo.processIdentifier || app.bundleIdentifier=="com.openai.codex") {self.changed(.refresh);return}
    }
    let restarted=name==NSWorkspace.didLaunchApplicationNotification || name==NSWorkspace.didTerminateApplicationNotification
    if restarted {self.stopTarget()}
    self.changed(restarted ? .restart:.invalidate)
   })
  }
  screenObserver=NotificationCenter.default.addObserver(forName:NSApplication.didChangeScreenParametersNotification,object:nil,queue:.main){[weak self] _ in self?.changed(.invalidate)}
 }
 deinit {stopTarget();for item in workspaceObservers{NSWorkspace.shared.notificationCenter.removeObserver(item)};if let item=screenObserver{NotificationCenter.default.removeObserver(item)}}
 func selectProcess(_ next:HostProcessIdentity){
  if identity != next {stopTarget();identity=next;lastObserverAttempt = -.infinity}
  guard observer==nil else{return}
  let now=ProcessInfo.processInfo.systemUptime
  guard now-lastObserverAttempt>=1 else{return};lastObserverAttempt=now
  let root=AXUIElementCreateApplication(next.pid)
  var created:AXObserver?
  let callback:AXObserverCallback={_,_,notification,context in
   guard let context else{return}
   let owner=Unmanaged<HostLifecycle>.fromOpaque(context).takeUnretainedValue()
   let name=notification as String
   owner.changed(name==kAXUIElementDestroyedNotification || name==kAXWindowMiniaturizedNotification ? .invalidate:.refresh)
  }
  guard AXObserverCreate(next.pid,callback,&created) == .success,let created else{return}
  observer=created;application=root
  for event in Self.appEvents {AXObserverAddNotification(created,root,event as CFString,Unmanaged.passUnretained(self).toOpaque())}
  CFRunLoopAddSource(CFRunLoopGetMain(),AXObserverGetRunLoopSource(created),.commonModes)
 }
 func selectWindow(_ next:AXUIElement){
  if let window,CFEqual(window,next){return}
  guard let observer else{return}
  if let window {for event in Self.windowEvents{AXObserverRemoveNotification(observer,window,event as CFString)}}
  window=next
  for event in Self.windowEvents{AXObserverAddNotification(observer,next,event as CFString,Unmanaged.passUnretained(self).toOpaque())}
 }
 func stopTarget(){
  if let observer {
   CFRunLoopRemoveSource(CFRunLoopGetMain(),AXObserverGetRunLoopSource(observer),.commonModes)
   if let window {for event in Self.windowEvents{AXObserverRemoveNotification(observer,window,event as CFString)}}
   if let application {for event in Self.appEvents{AXObserverRemoveNotification(observer,application,event as CFString)}}
  }
  observer=nil;application=nil;window=nil;identity=nil
 }
}
private enum HostReader {
 static func attribute(_ e:AXUIElement,_ name:String)->CFTypeRef? {var value:CFTypeRef?;return AXUIElementCopyAttributeValue(e,name as CFString,&value) == .success ? value:nil}
 static func rect(_ e:AXUIElement,top:CGFloat)->NSRect? {
  guard let p=attribute(e,kAXPositionAttribute),CFGetTypeID(p)==AXValueGetTypeID(),let s=attribute(e,kAXSizeAttribute),CFGetTypeID(s)==AXValueGetTypeID() else{return nil}
  var point=CGPoint.zero,size=CGSize.zero
  guard AXValueGetValue(p as! AXValue,.cgPoint,&point),AXValueGetValue(s as! AXValue,.cgSize,&size),size.width>0,size.height>0 else{return nil}
  return NSRect(x:point.x,y:top-point.y-size.height,width:size.width,height:size.height)
 }
 static func read(pid:pid_t,desktopTop:CGFloat,entryWindowNumber:Int?)->(HostResolution,HostScanMetrics,AXUIElement?) {
  let metrics=HostScanMetrics()
  func fail(_ reason:String,_ kind:HostResolutionKind = .hidden,_ observed:Int?=nil)->(HostResolution,HostScanMetrics,AXUIElement?){(HostResolution(geometry:nil,reason:reason,kind:kind,observedWindowNumber:observed),metrics,nil)}
  // Window-only attachment inspired by caisimai/codex-usage-overlay (MIT),
  // b2d52e63048a6ee912c15a256e29e695dfffb289. Never traverse account/UI subtrees.
  let sampledAt=ProcessInfo.processInfo.systemUptime
  let list=CGWindowListCopyWindowInfo([.optionOnScreenOnly,.excludeDesktopElements],kCGNullWindowID) as? [[String:Any]] ?? []
  func bounds(_ item:[String:Any])->NSRect? {
   guard let b=item[kCGWindowBounds as String] as? [String:Any],let x=b["X"] as? Double,let y=b["Y"] as? Double,let w=b["Width"] as? Double,let h=b["Height"] as? Double else{return nil}
   return NSRect(x:x,y:desktopTop-y-h,width:w,height:h)
  }
  let candidates=list.filter{($0[kCGWindowOwnerPID as String] as? Int)==Int(pid) && ($0[kCGWindowLayer as String] as? Int)==0 && ($0[kCGWindowAlpha as String] as? Double ?? 1)>0}.compactMap {item->HostWindowFrame? in
   guard let id=item[kCGWindowNumber as String] as? Int,let f=bounds(item),f.width>500,f.height>300 else{return nil}
   return HostWindowFrame(id:id,frame:f)
  }
  guard let front=candidates.first else{return fail("等待可见的 Codex 窗口")}
  var selected:AXUIElement?,frame=front.frame
  if AXIsProcessTrusted() {
   let root=AXUIElementCreateApplication(pid);AXUIElementSetMessagingTimeout(root,0.02)
   if let windows=attribute(root,kAXWindowsAttribute) as? [AXUIElement],let w=windows.first(where:{attribute($0,kAXFocusedAttribute) as? Bool == true}) ?? windows.first(where:{attribute($0,kAXMainAttribute) as? Bool == true}) {
    guard attribute(w,kAXMinimizedAttribute) as? Bool != true else{return fail("Codex 窗口已最小化")}
    if let f=rect(w,top:desktopTop),f.width>500,f.height>300 {selected=w;frame=f}
   }
  }
  guard let number=HostWindowPolicy.uniqueWindowID(in:candidates,matching:frame) else{return fail("窗口身份尚未唯一确认，暂不附着")}
  let frontIDs=HostWindowPolicy.occluders(orderedWindowIDs:list.compactMap{$0[kCGWindowNumber as String] as? Int},hostID:number)
  let visibleBounds=list.filter{item in
   guard (item[kCGWindowOwnerPID as String] as? Int)==Int(pid),let id=item[kCGWindowNumber as String] as? Int else{return false}
   return frontIDs.contains(id)
  }.compactMap {item->NSRect? in
   guard let b=item[kCGWindowBounds as String] as? [String:Any],let x=b["X"] as? Double,let y=b["Y"] as? Double,let w=b["Width"] as? Double,let h=b["Height"] as? Double else{return nil}
   let frame=NSRect(x:x,y:desktopTop-y-h,width:w,height:h)
   guard HostWindowPolicy.isVisibleOccluder(frame:frame,alpha:item[kCGWindowAlpha as String] as? Double ?? 1) else{return nil}
   return frame
  }
  metrics.candidates=1
  var geometry=HostGeometry.windowRelative(frame:frame,number:number,occlusions:visibleBounds)
  if let entry=entryWindowNumber {
   geometry.stack=HostStackObservation.read(list.compactMap{$0[kCGWindowNumber as String] as? Int},host:number,entry:entry,now:sampledAt)
  }
  return (HostResolution(geometry:geometry,reason:"窗口跟随；可在位置调整中校准"),metrics,selected)
 }
}
#endif
