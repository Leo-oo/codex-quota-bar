#if SELF_HOST_ONLY
import AppKit

extension SelfHostInputChecks {
 func candidate17Checks() async {
  guard let hostWindow=app.previewWindow else {check(false,"candidate17 own host exists");return}
  let savedFrame=hostWindow.frame,savedMinimum=hostWindow.minSize
  let savedSnapshot=app.snapshot,savedError=app.quotaError
  let savedOffset=app.preferences.offset,savedHorizontal=app.preferences.horizontal
  let savedLegacy=app.preferences.legacyAnchor,savedHidden=app.hidden
  defer {
   app.closePanels();app.hoverEntry=false;app.hoverTip=false
   app.snapshot=savedSnapshot;app.quotaError=savedError;app.hidden=savedHidden
   _=app.preferences.save(offset:savedOffset,horizontal:savedHorizontal,legacyAnchor:savedLegacy)
   hostWindow.minSize=savedMinimum;hostWindow.setFrame(savedFrame,display:true);app.update()
  }
  func noChildren(_ window:NSWindow?)->Bool {window?.childWindows?.isEmpty ?? true}
  func detached(_ window:NSWindow?)->Bool {guard let window else{return false};return window.parent==nil && !window.isVisible}
  func atHostAnchor(_ panel:NSWindow?)->Bool {
   guard let panel,let host=app.host,let card=panel.contentView as? CardSurface else{return false}
   let expected=Placement.popup(host:host,size:card.card.bounds.size,screen:app.screen).insetBy(dx:-8,dy:-8)
   return abs(panel.frame.minY-expected.minY)<0.5 && abs(panel.frame.minX-expected.minX)<0.5
  }
  app.closePanels();app.hoverEntry=false;app.hoverTip=false;app.hidden=false;app.quotaError=nil
  hostWindow.minSize = .zero
  hostWindow.setContentSize(NSSize(width:640,height:320))
  check(app.preferences.save(offset:-258,horizontal:0,legacyAnchor:false),"candidate17 edge calibration fixture saves")
  let week=QuotaWindow(remaining:23,minutes:10080,reset:Date().timeIntervalSince1970+3600,fallback:"周")
  let five=QuotaWindow(remaining:72,minutes:300,reset:Date().timeIntervalSince1970+1800,fallback:"5h")
  app.snapshot=QuotaSnapshot(windows:[week],fetched:Date());app.update();await pause()
  let singleOrigin=app.entry.frame.origin,unchangedHostFrame=app.host?.frame
  click(app.entryView,right:true);await pause()
  let menu=app.mainPanel
  check(menu != nil && menu?.parent === app.entry,"main card is an above-entry child")
  check(atHostAnchor(menu),"single-window menu starts at host bottom anchor")
  if let row=button("位置调整",in:menu?.contentView){click(row)};await pause()
  let position=app.subPanel
  check(position != nil && position?.parent === menu,"position submenu belongs to main card")
  app.snapshot=QuotaSnapshot(windows:[five,week],fetched:Date());app.update();await pause()
  check(app.host?.frame==unchangedHostFrame && app.entry.frame.origin != singleOrigin,"quota height changes clamped entry origin without changing host")
  check(app.mainPanel === menu && app.subPanel === position && atHostAnchor(menu),"attached cards retain identity and host bottom anchor after entry shift")
  if let menu,let position,app.menuButtons.count>2 {
   let row=app.menuButtons[2],rowFrame=menu.convertToScreen(row.convert(row.bounds,to:nil))
   let card=(position.contentView as! CardSurface).card
   let expected=Placement.submenu(parent:menu.frame.insetBy(dx:8,dy:8),row:rowFrame,size:card.bounds.size,screen:app.screen).insetBy(dx:-8,dy:-8)
   let detail:[String:Any] = ["actual":NSStringFromRect(position.frame),"expected":NSStringFromRect(expected),"parent":NSStringFromRect(menu.frame),"row":NSStringFromRect(rowFrame),"screen":NSStringFromRect(app.screen)]
   if let d=try? JSONSerialization.data(withJSONObject:detail,options:.prettyPrinted){try? d.write(to:app.runtime.appendingPathComponent("candidate17-submenu-anchor.json"))}
   check(abs(position.frame.midY-rowFrame.midY)<0.5 && app.screen.contains(position.frame),"child submenu center remains on parent row and within screen after entry shift")
  }else{check(false,"submenu geometry fixture exists")}
  if let row=button("主题色",in:menu?.contentView){click(row)};await pause()
  let theme=app.subPanel
  check(detached(position) && theme != nil && theme?.parent === menu,"switching submenu detaches old child and attaches new child")
  escape();await pause()
  check(detached(menu) && detached(theme) && noChildren(menu) && noChildren(app.entry),"Esc clears both parent links and entry child list")

  click(app.entryView,right:true);await pause()
  if let row=button("Tibo 重置消息",in:app.mainPanel?.contentView){click(row)};await pause()
  let news=app.mainPanel
  if let help=app.helpButton {click(help)};await pause()
  let explanation=app.helpPanel
  check(news != nil && news?.parent === app.entry && explanation != nil && explanation?.parent === news,"news and explanation use the two-level child chain")
  escape();await pause()
  check(detached(news) && detached(explanation) && noChildren(news) && noChildren(app.entry),"Esc detaches news and explanation without orphan children")

  click(app.entryView,right:true);await pause()
  if let row=button("位置调整",in:app.mainPanel?.contentView){click(row)};await pause()
  let outsideParent=app.mainPanel,outsideChild=app.subPanel
  if let view=hostWindow.contentView {click(view)};await pause()
  check(detached(outsideParent) && detached(outsideChild) && noChildren(outsideParent) && noChildren(app.entry),"outside input closes and detaches the entire card chain")
 }
}
#endif
