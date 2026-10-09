#if SELF_HOST_ONLY
import AppKit
extension SelfHostInputChecks {
 func hover(_ view:NSView,_ inside:Bool){
  guard let w=view.window,let e=NSEvent.enterExitEvent(with:inside ? .mouseEntered:.mouseExited,location:.zero,modifierFlags:[],timestamp:ProcessInfo.processInfo.systemUptime,windowNumber:w.windowNumber,context:nil,eventNumber:0,trackingNumber:0,userData:nil) else{return}
  // Controlled tracking callbacks, not physical mouse movement.
  if inside {view.mouseEntered(with:e)}else{view.mouseExited(with:e)}
 }
 func candidate10Checks() async {
  app.closePanels();app.hoverEntry=false;app.hoverTip=false
  let order=app.orderRevision
  for _ in 0..<60 {app.update()}
  check(app.orderRevision==order,"60 unchanged ticks perform zero extra entry orders")
  for cycle in 0..<4 {
   hover(app.entryView,true);await pause(0.22)
   let tip=app.mainPanel
   check(app.surface=="tooltip" && tip != nil,"controlled hover opens tooltip \(cycle)")
   for _ in 0..<30 {app.update()}
   check(app.mainPanel === tip,"stationary hover keeps tooltip identity \(cycle)")
   app.snapshot=app.snapshot.map{QuotaSnapshot(windows:$0.windows,fetched:Date())};app.updateTooltipContents()
   check(app.mainPanel === tip,"quota refresh updates tooltip in place \(cycle)")
   let priorSnapshot=app.snapshot
   app.quotaError="示例暂不可用";app.updateTooltipContents()
   check(app.mainPanel === tip && app.tooltipView?.error != nil,"error-height change preserves tooltip identity \(cycle)")
   if let w=app.snapshot?.windows.first {app.snapshot=QuotaSnapshot(windows:[w,w],fetched:Date());app.updateTooltipContents()}
   check(app.mainPanel === tip && app.tooltipView?.snapshot?.windows.count==2,"window-count change preserves tooltip identity \(cycle)")
   app.snapshot=priorSnapshot;app.quotaError=nil;app.updateTooltipContents()
   if let view=app.tooltipView {hover(view,true)}
   hover(app.entryView,false);await pause(0.16)
   check(app.mainPanel === tip,"entry to tooltip transfer retains card \(cycle)")
   if let view=app.tooltipView {hover(view,false)};await pause(0.2)
   check(app.mainPanel==nil,"leaving both hover regions closes card \(cycle)")
  }
  click(app.entryView,right:true);await pause()
  if let row=button("位置调整",in:app.mainPanel?.contentView) as? RowButton {
   hover(row,true);await pause()
   let sub=app.subPanel,main=app.mainPanel
   for _ in 0..<10 {hover(row,true);app.update()}
   check(sub != nil && app.subPanel === sub && app.mainPanel === main,"repeated position hover does not recreate menu windows")
   app.harness?.injectedOcclusions=[NSRect(x:app.host!.frame.maxX-20,y:app.host!.frame.maxY-20,width:5,height:5)]
   app.update();check(app.subPanel === sub,"unrelated occlusion change does not close submenu")
   app.harness?.injectedOcclusions=[]
   app.snapshot=app.snapshot.map{QuotaSnapshot(windows:$0.windows,fetched:Date())};app.update()
   check(app.mainPanel === main && app.subPanel === sub,"quota refresh keeps open menu and submenu")
   check((button("左微调",in:sub?.contentView) as? RowButton)?.icon == .left && (button("右微调",in:sub?.contentView) as? RowButton)?.icon == .right,"left and right use matching directional chevrons")
  } else {check(false,"position row available")}
  escape();await pause()
  click(app.entryView,right:true);await pause()
  if let b=button("Tibo 重置消息",in:app.mainPanel?.contentView){click(b)};await pause()
  if let help=app.helpButton {
   hover(help,true);await pause()
   let parent=app.mainPanel,p=app.helpPanel
   check(p != nil && p?.canBecomeKey==false,"news question mark hover opens nonactivating explanation")
   if let card=(p?.contentView as? CardSurface)?.card {hover(card,true)}
   hover(help,false);await pause(0.16)
   check(app.helpPanel === p && app.mainPanel === parent,"help hover transfer preserves news parent")
   if let card=(p?.contentView as? CardSurface)?.card {hover(card,false)};await pause(0.16)
   check(app.helpPanel==nil && app.mainPanel === parent,"help leave closes only explanation")
   click(help);await pause();check(app.helpPanel != nil && app.mainPanel === parent,"question mark click reaches help action")
   if let b=button("详情",in:app.mainPanel?.contentView) as? PlainButton {
    hover(b,true);check(b.hovered,"details link provides hover state");hover(b,false);check(!b.hovered,"details hover clears on exit")
   } else {check(false,"details link available")}
   escape();await pause();check(app.helpPanel==nil && app.mainPanel==nil,"Esc removes news and help without orphan")
  } else {check(false,"news help button available")}
  let x=app.preferences.horizontal,y=app.preferences.offset
  for _ in 0..<3 {
   let hide=app.makeStatusMenu();let i=hide.items.firstIndex{$0.title=="隐藏额度条"}!
   hide.performActionForItem(at:i);await pause()
   check(app.hidden && !app.entry.isVisible && app.status.button != nil,"status menu hide leaves menu bar available")
   let show=app.makeStatusMenu();let j=show.items.firstIndex{$0.title=="显示额度条"}!
   show.performActionForItem(at:j);await pause()
   check(!app.hidden && app.entry.isVisible && app.preferences.horizontal==x && app.preferences.offset==y,"status menu restores saved calibration")
  }
  for value in ["dark","light"] {
   let m=app.themeMenu();let i=m.items.firstIndex{($0.representedObject as? String)==value}!
   m.performActionForItem(at:i);await pause()
   check(app.entryView.effectiveAppearance.bestMatch(from:[.aqua,.darkAqua])==(value=="dark" ? .darkAqua:.aqua),"theme selection updates entry \(value)")
   check(Preferences(app.preferences.url).theme==value,"theme survives independent reload \(value)")
  }
  check(app.themeMenu().items.map{$0.title} == ["浅色","深色"],"theme choices are light and dark only")
  check(app.preferences.horizontal==x && app.preferences.offset==y,"theme changes preserve calibration")
  click(app.entryView,right:true);await pause()
  if let b=button("位置调整",in:app.mainPanel?.contentView){click(b)};await pause()
  if let b=button("恢复初始",in:app.subPanel?.contentView){click(b)};await pause()
  check(app.preferences.horizontal==0 && app.preferences.offset==0 && !app.preferences.legacyAnchor,"reset clears XY and adopts new reference anchor")
  app.hoverEntry=false;app.hoverTip=false;app.closePanels()
 }
}
#endif
