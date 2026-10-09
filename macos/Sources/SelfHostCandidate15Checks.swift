#if SELF_HOST_ONLY
import AppKit
extension SelfHostInputChecks {
 func candidate15Checks() async {
  app.closePanels();app.hoverEntry=false;app.hoverTip=false
  let priorOffset=app.preferences.offset,priorX=app.preferences.horizontal
  for value in ["dark","light"] {
   click(app.entryView,right:true);await pause()
   check(app.menuButtons.map{$0.title} == ["Tibo 重置消息","暂时隐藏","位置调整","主题色","退出额度条"],"theme row immediately follows position")
   if let row=button("主题色",in:app.mainPanel?.contentView) as? RowButton {
    click(row);await pause()
    check(app.submenuKind=="theme" && app.subPanel?.isVisible==true,"dispatched click opens theme submenu")
    let menu=app.subPanel,card=(menu?.contentView as? CardSurface)?.card
    check(card?.subviews.compactMap{$0 as? RowButton}.map{$0.title} == ["浅色","深色"],"custom submenu has exactly two choices")
    let rowScreen=row.window!.convertToScreen(row.convert(row.bounds,to:nil))
    check(abs((menu?.frame.midY ?? 0)-rowScreen.midY)<1,"theme submenu centered on parent row")
    for _ in 0..<5 {hover(row,true);app.update()}
    check(app.subPanel===menu,"repeated theme hover preserves panel")
    if let choice=button(value=="dark" ? "深色":"浅色",in:menu?.contentView){click(choice)};await pause()
    check(app.preferences.theme==value && Preferences(app.preferences.url).theme==value,"dispatched theme choice persists \(value)")
    check(app.mainPanel==nil && app.subPanel==nil,"theme selection closes both menus")
    check(app.entryView.effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == (value=="dark" ? .darkAqua:.aqua),"theme choice updates appearance \(value)")
   }else{check(false,"theme row exists")}
  }
  click(app.entryView,right:true);await pause()
  if let row=button("主题色",in:app.mainPanel?.contentView){click(row)};await pause();escape();await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"Esc closes theme parent and child")
  click(app.entryView,right:true);await pause()
  if let row=button("主题色",in:app.mainPanel?.contentView) as? RowButton {hover(row,true)};await pause()
  if let row=button("位置调整",in:app.mainPanel?.contentView) as? RowButton {hover(row,true)};await pause()
  check(app.submenuKind=="position" && button("上微调",in:app.subPanel?.contentView) != nil && button("浅色",in:app.subPanel?.contentView)==nil,"moving between submenu parents replaces child")
  if let row=button("主题色",in:app.mainPanel?.contentView) as? RowButton {hover(row,true)};await pause()
  if let view=app.previewWindow?.contentView{click(view)};await pause()
  check(app.mainPanel==nil && app.subPanel==nil,"outside click closes theme without orphan")
  check(app.preferences.offset==priorOffset && app.preferences.horizontal==priorX,"theme preserves position calibration")
  // Observe real dispatched own-host clicks at short intervals, not merely final visibility.
  app.closePanels();await pause(0.3)
  var trace:[[String:Any]]=[]
  let counts=[app.entry.hideRequests,app.entry.orderRequests,app.entry.frameRequests,app.entry.contentAssignments]
  for index in 0..<6 {
   if let host=app.previewWindow?.contentView {click(host,at:NSPoint(x:300,y:240))}
   for sample in 0..<16 {
    await pause(0.01)
    trace.append(["click":index,"sample":sample,"uptime":ProcessInfo.processInfo.systemUptime,"visible":app.entry.isVisible,"occluded":!app.entry.occlusionState.contains(.visible),"orderOut":app.entry.hideRequests,"order":app.entry.orderRequests,"setFrame":app.entry.frameRequests,"contentAssignments":app.entry.contentAssignments,"panelRevision":app.panelRevision])
   }
  }
  let after=[app.entry.hideRequests,app.entry.orderRequests,app.entry.frameRequests,app.entry.contentAssignments]
  check(after[0]==counts[0] && after[2]==counts[2] && after[3]==counts[3],"ordinary own-host clicks do not hide, reframe or replace entry content")
  try! JSONSerialization.data(withJSONObject:["before":counts,"after":after,"samples":trace,"sampling":"10ms target; own windows only; not physical/atomic frame evidence"],options:[.prettyPrinted,.sortedKeys]).write(to:app.runtime.appendingPathComponent("candidate15-click-trace.json"))
  let file=app.runtime.appendingPathComponent("theme-migration-fixture.json")
  try! Data("{\"theme\":\"system\"}".utf8).write(to:file)
  check(Preferences(file).theme=="light","removed system preference migrates to default light")
 }
}
#endif
