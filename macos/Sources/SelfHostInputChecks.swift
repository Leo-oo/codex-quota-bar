#if SELF_HOST_ONLY
import AppKit
// Application-local event queue tests. This file is absent from production builds.
// No accessibility, screenshots or event injection directed at other applications.
@MainActor final class SelfHostInputChecks {
 unowned let app:AppDelegate
 var results:[[String:Any]]=[]
 init(_ app:AppDelegate){self.app=app}
 func check(_ value:Bool,_ name:String){
  var record:[String:Any]=["name":name,"passed":value]
  if !value {record["ownWindowOrder"]=NSWindow.windowNumbers(options:[])?.map(\.intValue) ?? [];record["entryID"]=app.entry.windowNumber;record["hostID"]=app.previewWindow?.windowNumber ?? 0;record["entryVisible"]=app.entry.isVisible;record["entryBlocked"]=app.entry.ignoresMouseEvents;record["orderRevision"]=app.orderRevision;record["heartbeat"]=app.uiHeartbeat}
  results.append(record);save()
 }
 func save(){let data=try? JSONSerialization.data(withJSONObject:["mode":"application-local NSEvent queue; not physical/CUA input", "externalHostAccess":"excluded at compile time","checks":results],options:[.prettyPrinted,.sortedKeys]);try? data?.write(to:app.runtime.appendingPathComponent("input-checks.json"),options:.atomic)}
 func pause(_ seconds:Double=0.2) async {try? await Task.sleep(nanoseconds:UInt64(seconds*1_000_000_000))}
 func click(_ view:NSView,right:Bool=false,at location:NSPoint?=nil){
  guard let window=view.window else{check(false,"event target has window");return}
  let point=view.convert(location ?? NSPoint(x:view.bounds.midX,y:view.bounds.midY),to:nil)
  for type:NSEvent.EventType in right ? [.rightMouseDown,.rightMouseUp]:[.leftMouseDown,.leftMouseUp] {
   if let event=NSEvent.mouseEvent(with:type,location:point,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:window.windowNumber,context:nil,eventNumber:0,clickCount:1,pressure:1){NSApp.postEvent(event,atStart:false)}
  }
 }
 func escape(){guard let w=app.subPanel ?? app.mainPanel else{return};if let e=NSEvent.keyEvent(with:.keyDown,location:.zero,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:w.windowNumber,context:nil,characters:"\u{1b}",charactersIgnoringModifiers:"\u{1b}",isARepeat:false,keyCode:53){NSApp.postEvent(e,atStart:false)}}
 func button(_ title:String,in root:NSView?)->NSButton? {guard let root else{return nil};if let b=root as? NSButton,b.title==title{return b};for child in root.subviews {if let b=button(title,in:child){return b}};return nil}
 func control(_ title:String){guard let b=button(title,in:app.harness?.window.contentView) else{check(false,"missing control "+title);return};app.harness?.show();click(b)}
 func run() async {
  await pause(0.8)
  check(app.entry.isVisible,"entry appears on own host")
  if CommandLine.arguments.contains("--click-checks"){check(app.applicationHome==nil && app.startupDiagnostic==nil,"ordinary startup has no diagnostic panel")}
  check(!app.entry.canBecomeKey && !app.entry.isKeyWindow,"entry cannot take keyboard focus")
  click(app.entryView,right:true);await pause()
  check(app.surface=="menu" && app.mainPanel?.isVisible==true,"right mouse events open menu through dispatch")
  if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
  check(app.subPanel?.isVisible==true,"position row events open submenu")
  let before=app.preferences.offset
  if let b=button("上微调",in:app.subPanel?.contentView){click(b)};await pause()
  check(app.preferences.offset==before-8,"submenu input reaches action without premature outside dismissal")
  check(app.subPanel==nil && app.mainPanel==nil,"adjustment leaves no orphan panel")
  let persisted=Preferences(app.preferences.url)
  check(persisted.offset==app.preferences.offset,"position survives independent preferences reload")
  click(app.entryView,right:true);await pause()
  if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause();escape();await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"Esc event closes parent and submenu")
  click(app.entryView,right:true);await pause()
  if let surface=app.mainPanel?.contentView {click(surface,at:NSPoint(x:2,y:surface.bounds.midY))};await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"transparent parent shadow margin acts as outside click")
  click(app.entryView,right:true);await pause()
  if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
  if let surface=app.subPanel?.contentView{click(surface,at:NSPoint(x:2,y:surface.bounds.midY))};await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"transparent submenu shadow margin closes both panels")
  click(app.entryView,right:true);await pause()
  if let view=app.previewWindow?.contentView{click(view)};await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"outside event closes menu")
  for cycle in 0..<5 {
   click(app.entryView,right:true);await pause()
   check(app.surface=="menu" && app.mainPanel?.isVisible==true,"repeat menu open \(cycle)")
   if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
   escape();await pause()
   check(app.mainPanel==nil && app.subPanel==nil,"repeat submenu Esc no orphan \(cycle)")
  }
  let fixtureSnapshot=app.snapshot
  app.snapshot=fixtureSnapshot.map{QuotaSnapshot(windows:$0.windows,fetched:Date())}
  app.update();await pause()
  check(app.entry.isVisible && app.snapshot?.windows.map{$0.label+$0.percent}==fixtureSnapshot?.windows.map{$0.label+$0.percent},"synthetic refresh retains quota and entry")
  let original=app.entry.frame;control("移动宿主");await pause()
  check(app.entry.isVisible && app.entry.frame.origin != original.origin,"own host move updates entry")
  control("缩小／恢复尺寸");await pause()
  check(app.entry.isVisible,"own host resize retains entry")
  let id=app.host?.windowNumber;control("同框替换宿主");await pause()
  check(app.entry.isVisible && app.host?.windowNumber != id,"same-frame replacement reattaches own host")
  // Internal geometry reflow while menu remains open; no button action is called directly.
  click(app.entryView,right:true);await pause();let oldX=app.mainPanel?.frame.minX
  app.harness?.anchorShift=16
  if let v=app.previewWindow?.contentView as? PreviewHost{v.anchorShift=16;v.needsDisplay=true}
  await pause()
  check(oldX != nil && app.mainPanel?.frame.minX == oldX!+16,"unchanged outer frame still reflows open menu with rail")
  // Place an occluder only at the future right edge, outside the previous panel frame.
  if let panel=app.mainPanel {
   app.harness?.injectedOcclusions=[NSRect(x:panel.frame.maxX+4,y:panel.frame.midY,width:8,height:8)]
   app.harness?.anchorShift=32
   app.update()
   check(app.mainPanel === panel && app.entry.level == .floating,"accepted floating policy preserves card over ordinary host occlusion")
   app.harness?.injectedOcclusions=[]
  }
  escape();await pause()
  control("最小化宿主");await pause(1)
  check(!app.entry.isVisible && app.mainPanel==nil && app.subPanel==nil,"completed minimization hides entry and panels")
  control("恢复宿主");await pause(1)
  check(app.entry.isVisible,"restore recovers own entry")
  control("显示原生遮挡层");await pause()
  check(!app.entry.ignoresMouseEvents && app.entry.level == .floating,"ordinary native overlay does not demote or disable floating entry")
  if let b=button("原生弹层命中",in:app.harness?.occludingWindow?.contentView){click(b)};await pause()
  check(app.harness?.popupHits==1,"directly queued native popup event reaches its target; not a hit-priority claim")
  control("关闭原生遮挡层");await pause()
  check(!app.entry.ignoresMouseEvents && app.entry.isVisible,"popup close restores entry")
  check(app.inputMouseEvents>=15,"events passed through application input monitors")
  if CommandLine.arguments.contains("--window-relative") {
   let prior=app.preferences.horizontal
   click(app.entryView,right:true);await pause()
   if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
   if let b=button("右微调",in:app.subPanel?.contentView){click(b)};await pause()
   check(app.preferences.horizontal==prior+8,"horizontal calibration through dispatched menu input")
   check(Preferences(app.preferences.url).horizontal==app.preferences.horizontal,"horizontal calibration survives reload")
   let snapshot=app.snapshot;app.snapshot=nil;app.quotaError="示例读取失败";app.update();await pause()
   check(app.entry.isVisible && app.entryView.stale,"quota failure retains visible honest error entry")
   app.snapshot=snapshot;app.quotaError=nil;app.update()
   app.hidden=true;app.update();check(!app.entry.isVisible,"disabled overlay hides despite valid window")
   app.hidden=false;app.update();check(app.entry.isVisible,"reenabling restores calibrated entry")
  }
  if CommandLine.arguments.contains("--candidate10-checks") {await candidate10Checks()}
  if CommandLine.arguments.contains("--candidate15-checks") {await candidate15Checks()}
  if CommandLine.arguments.contains("--recovery-checks") {await recoveryChecks()}
  if CommandLine.arguments.contains("--click-checks") {await clickContinuityChecks()}
  if CommandLine.arguments.contains("--candidate17-checks") {await candidate17Checks()}
  if CommandLine.arguments.contains("--history-checks") {await historyChecks()}
  control("关闭宿主");await pause()
  check(!app.entry.isVisible && app.mainPanel==nil && app.subPanel==nil,"host close removes all attached windows")
  save();NSApp.terminate(nil)
 }
}
#endif
