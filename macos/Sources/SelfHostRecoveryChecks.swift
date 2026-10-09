#if SELF_HOST_ONLY
import AppKit
extension SelfHostInputChecks {
 func ownAbove(_ first:NSWindow,_ second:NSWindow)->Bool {
  // Empty options enumerate only this application, including its NSPanel instances.
  let ids=NSWindow.windowNumbers(options:[])?.map(\.intValue) ?? []
  guard let a=ids.firstIndex(of:first.windowNumber),let b=ids.firstIndex(of:second.windowNumber) else{return false}
  return a<b
 }
 func recoveryChecks() async {
  guard let host=app.previewWindow else{check(false,"recovery host exists");return}
  app.closePanels();app.hoverEntry=false;app.hoverTip=false
  await pause(0.4)
  check(ownAbove(app.entry,host),"entry truly ordered above own host before recovery tests")
  hover(app.entryView,true);await pause(0.22)
  let tip=app.mainPanel
  host.order(.above,relativeTo:app.entry.windowNumber);await pause(0.35)
  check(app.surface=="tooltip" && app.mainPanel === tip && ownAbove(app.entry,host),"stack repair preserves live tooltip identity")
  hover(app.entryView,false);await pause(0.2);app.closePanels()
  let px=app.preferences.horizontal,py=app.preferences.offset,theme=app.preferences.theme
  for cycle in 0..<4 {
   host.setFrameOrigin(NSPoint(x:host.frame.minX+4,y:host.frame.minY+3))
   host.order(.above,relativeTo:app.entry.windowNumber)
   await pause(0.35)
   check(ownAbove(app.entry,host),"move-end restores actual relative order \(cycle)")
   host.setContentSize(NSSize(width:cycle%2==0 ? 610:620,height:cycle%2==0 ? 510:520))
   host.order(.above,relativeTo:app.entry.windowNumber)
   await pause(0.35)
   check(ownAbove(app.entry,host),"resize-end restores actual relative order \(cycle)")
  }
  let before=app.orderRevision
  host.order(.above,relativeTo:app.entry.windowNumber)
  await pause(0.35)
  check(ownAbove(app.entry,host) && app.orderRevision==before,"host raising needs no reactive entry reordering at floating level")
  app.followingTrace=true;app.recordFollow("typing-start")
  let input=NSTextField(frame:NSRect(x:110,y:100,width:210,height:28));host.contentView!.addSubview(input)
  host.makeKeyAndOrderFront(nil);click(input);await pause()
  if let e=NSEvent.keyEvent(with:.keyDown,location:.zero,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:host.windowNumber,context:nil,characters:"x",charactersIgnoringModifiers:"x",isARepeat:false,keyCode:7){NSApp.postEvent(e,atStart:false)}
  app.recordFollow("key-enqueued");await pause(0.35);app.recordFollow("typing-check")
  check(input.stringValue.contains("x"),"own input field receives dispatched typing")
  check(ownAbove(app.entry,host),"input focus and typing do not strand entry behind host")
  await pause(0.3);app.recordFollow("typing-after-check");app.followingTrace=false
  input.removeFromSuperview()
  for cycle in 0..<3 {
   control("显示原生遮挡层");await pause()
   check(!app.entry.ignoresMouseEvents && app.harness?.occludingWindow.map{ownAbove(app.entry,$0)}==true,"accepted floating entry stays above ordinary native popup \(cycle)")
   control("关闭原生遮挡层");await pause()
   check(!app.entry.ignoresMouseEvents && ownAbove(app.entry,host),"native popup dismissal restores entry automatically \(cycle)")
  }
  app.harness?.hostUnavailable=true;await pause(0.3)
  check(!app.entry.isVisible,"temporary host unavailability hides entry")
  app.harness?.hostUnavailable=false;await pause(0.35)
  check(app.entry.isVisible && ownAbove(app.entry,host),"fresh host poll restores without opening application home")
  app.hidden=true;app.update();host.orderFront(nil);await pause(0.3)
  check(!app.entry.isVisible,"manual hidden state overrides ordering repair")
  app.hidden=false;await pause(0.35)
  check(ownAbove(app.entry,host),"explicit show resumes ordering recovery")
  click(app.entryView,right:true);await pause()
  if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
  let parent=app.mainPanel,sub=app.subPanel,revision=app.panelRevision
  host.order(.above,relativeTo:app.entry.windowNumber);await pause(0.35)
  check(ownAbove(app.entry,host),"repair also works with menu open")
  check(app.mainPanel===parent && app.subPanel===sub && app.panelRevision==revision,"repair preserves menu and submenu identity")
  check(parent.map{ownAbove($0,app.entry)}==true && sub.flatMap{s in parent.map{ownAbove(s,$0)}}==true,"repair restores host entry parent submenu order")
  let stable=app.orderRevision
  for _ in 0..<60 {app.update()}
  check(app.orderRevision==stable,"stable repaired stack avoids repeated ordering")
  escape();await pause()
  check(app.preferences.horizontal==px && app.preferences.offset==py && app.preferences.theme==theme,"recovery preserves saved calibration and theme")
  check(app.entry.level == .floating && !app.entry.canBecomeKey,"entry uses only floating level and cannot steal keyboard focus")
 }
}
#endif
