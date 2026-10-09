#if SELF_HOST_ONLY
import AppKit
extension SelfHostInputChecks {
 func clickContinuityChecks() async {
  guard let window=app.previewWindow,let raw=app.geometry() else{check(false,"own geometry for focus tests");return}
  app.closePanels();app.hoverEntry=false;app.hoverTip=false
  var session=HostSession();session.selectProcess(ProcessInfo.processInfo.processIdentifier)
  func confirmed(_ value:inout HostSession){value.accept(HostResolution(geometry:raw,reason:"own-host read"),token:value.token!,now:ProcessInfo.processInfo.systemUptime)}
  confirmed(&session)
  app.harness?.lifecycleSession=session
  // Reproduce the prior callback rule using the same UI update path.
  session.invalidate("old focus callback");app.harness?.lifecycleSession=session;app.update()
  check(!app.entry.isVisible,"old focus invalidation reproduces entry hiding on own host")
  confirmed(&session);app.harness?.lifecycleSession=session;app.update()
  let hides=app.entry.hideRequests
  let input=NSTextField(frame:NSRect(x:100,y:95,width:220,height:30));window.contentView!.addSubview(input)
  for cycle in 0..<8 {
   confirmed(&session);app.harness?.lifecycleSession=session
   window.makeKeyAndOrderFront(nil);click(input);await pause(0.06)
   session.lifecycle(.refresh);app.harness?.lifecycleSession=session;app.scheduleHostUpdate()
   await pause(0.08)
   check(app.entry.isVisible && app.entry.hideRequests==hides,"focus refresh never orders entry out \(cycle)")
   check(ownAbove(app.entry,window),"focus click retains recovery against host raising \(cycle)")
  }
  input.removeFromSuperview()
  confirmed(&session);app.harness?.lifecycleSession=session
  hover(app.entryView,true);await pause(0.18)
  let tip=app.mainPanel
  session.lifecycle(.refresh);app.harness?.lifecycleSession=session;app.update()
  check(app.mainPanel === tip && app.surface=="tooltip" && app.entry.hideRequests==hides,"focus refresh keeps tooltip and entry without hide-show")
  hover(app.entryView,false);await pause(0.2);app.closePanels()
  session.lifecycle(.invalidate);app.harness?.lifecycleSession=session;app.update()
  check(!app.entry.isVisible,"confirmed hard lifecycle invalidation still hides immediately")
  confirmed(&session);app.harness?.lifecycleSession=session;app.update()
  check(app.entry.isVisible,"fresh hard-invalidation recovery still works")
  app.harness?.lifecycleSession=nil;app.update()
  app.installApplicationMenu()
  check(app.applicationHome==nil && app.startupDiagnostic==nil,"installing product menu does not create diagnostic window")
  let reopen=app.applicationShouldHandleReopen(NSApp,hasVisibleWindows:false);await pause()
  check(!reopen && app.applicationHome==nil,"application-icon reopen never creates diagnostic window")
  let menu=app.makeStatusMenu()
  check(menu.items.contains{$0.title=="主题色"} && menu.items.contains{$0.title=="隐藏额度条"},"theme and visibility controls remain discoverable")
  if let index=menu.items.firstIndex(where:{$0.title=="诊断信息…"}) {menu.performActionForItem(at:index)}else{check(false,"explicit diagnostic item exists")}
  await pause()
  check(app.applicationHome?.window?.isVisible==true,"explicit diagnostic menu action opens retained diagnostics")
  app.applicationHome?.close()
  _=app.applicationShouldHandleReopen(NSApp,hasVisibleWindows:false);await pause()
  check(app.applicationHome?.window?.isVisible==false,"application-icon reopen leaves closed diagnostics closed")
  app.hoverEntry=false;app.closePanels();app.previewWindow?.makeKeyAndOrderFront(nil);await pause()
 }
}
#endif
