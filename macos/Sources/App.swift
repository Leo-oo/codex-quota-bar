import AppKit

@main final class AppDelegate:NSObject,NSApplicationDelegate {
 static func main(){let app=NSApplication.shared;let delegate=AppDelegate();app.delegate=delegate;withExtendedLifetime(delegate){app.run()}}
 #if DISTRIBUTION
 let preview=false
 let liveSelfHost=false
 #else
 let preview=CommandLine.arguments.contains("--preview")
 let liveSelfHost=CommandLine.arguments.contains("--self-host-live")
 #endif
 var selfHost:Bool {preview || liveSelfHost}
 var referenceLayout:Bool {selfHost && CommandLine.arguments.contains("--reference-layout")}
 var liveData:Bool {!preview}
 let tracker=HostTracker()
 #if !DISTRIBUTION
 var harness:HarnessController?
 #endif
 var lastDiagnostic=Date.distantPast
 var lastRuntimeStatus=Date.distantPast
 var startupStage="launching",uiHeartbeat=0
 var startupDiagnostic:StartupDiagnosticWindow?
 var applicationHome:ApplicationHome?
 #if DISTRIBUTION
 let diagnosticMode=false
 #else
 var diagnosticMode:Bool {CommandLine.arguments.contains("--startup-panel")}
 #endif
 var quotaReads=0
 var newsDay=NewsParser.day(Date())
 var previewWindow:NSWindow?
 var host:HostGeometry?
 var runtime:URL!
 var quotaClient:QuotaClient!
 var preferences:Preferences!
 var snapshot:QuotaSnapshot?
 var quotaError:String?
 var news:NewsSnapshot?
 var newsError:String?
 var status:NSStatusItem!
 var statusAppearance:NSKeyValueObservation?
 var entry:OverlayPanel!
 let entryView=EntryView()
 var mainPanel:OverlayPanel?
 var subPanel:OverlayPanel?
 var submenuKind=""
 var surface=""
 var tooltipView:QuotaCard?
 var helpPanel:OverlayPanel?
 var helpButton:PlainButton?
 var helpHovered=false,helpCardHovered=false
 var helpTimer:Timer?
 var attachment=FloatingAttachment()
 var orderRevision=0,panelRevision=0
 var menuUpdateLabel:NSTextField?
 var hidden=false
 var placementNote:String?
 var localMonitor:Any?,globalMonitor:Any?
 var focusObservers:[NSObjectProtocol]=[]
 #if SELF_HOST_ONLY
 var selfHostTrace:SelfHostEventTrace?
 var followingTrace=false
 #endif
 var inputMouseEvents=0
 var hostUpdateQueued=false
 var tick:Timer?
 var requestRunning=false,failures=0
 var nextQuota=Date.distantPast,nextNews=Date.distantPast
 var newsRunning=false
 var hoverEntry=false,hoverTip=false
 var hoverDue:Date?,leaveDue:Date?
 var hoverTimer:Timer?
 var menuButtons:[RowButton]=[]
 var screen:NSRect{HostWindowPolicy.display(in:NSScreen.screens.map(\.visibleFrame),anchor:entry?.isVisible == true ? NSPoint(x:entry.frame.midX,y:entry.frame.midY):host?.windowAnchor,host:host?.frame) ?? NSRect(x:0,y:0,width:1440,height:900)}
 var updated:String{guard let s=snapshot else{return quotaError ?? "正在读取额度"};let f=DateFormatter();f.dateFormat="HH:mm";return "更新于"+f.string(from:s.fetched)+"（\(max(0,Int(Date().timeIntervalSince(s.fetched))))s前）"}
 var unread:Bool{news?.items.contains{preferences.read[$0.id] != $0.version} ?? false}
 func applicationDidFinishLaunching(_ notification:Notification){
  NSApp.setActivationPolicy(.regular)
  let env=ProcessInfo.processInfo.environment
  let base=env["CODEX_USAGE_RUNTIME"].map{URL(fileURLWithPath:$0)} ?? FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("CodexUsageMac")
  runtime=selfHost ? base.appendingPathComponent(liveSelfHost ? "live-self-host":"preview"):base
  try? FileManager.default.createDirectory(at:runtime,withIntermediateDirectories:true)
  quotaClient=QuotaClient(runtime:runtime.appendingPathComponent("app-server"))
  quotaClient.onIdentityChanged={[weak self] in DispatchQueue.main.async {self?.snapshot=nil}}
  preferences=Preferences(runtime.appendingPathComponent("ui-state.json"));applyTheme()
  if liveData,let data=try? Data(contentsOf:runtime.appendingPathComponent("public-news.json")),data.count<2_097_152,let cached=try? JSONDecoder().decode(NewsSnapshot.self,from:data) {news=cached.current();newsError="正在更新上次公开消息"}
  entry=OverlayPanel(size:NSSize(width:48,height:33),entry:true);entry.contentView=entryView;entry.title="Codex额度入口";entryView.setAccessibilityElement(true);entryView.setAccessibilityRole(.button)
  entryView.onRight={[weak self] in
   #if SELF_HOST_ONLY
   self?.selfHostTrace?.marker("entry-right-handler")
   #endif
   self?.showMenu()
  };entryView.onHover={[weak self] inside in self?.hoverEntry=inside;self?.refreshHover()}
  status=NSStatusBar.system.statusItem(withLength:NSStatusItem.squareLength)
  status.autosaveName="CodexUsageMac.Status"
  if let button=status.button {button.image=trayImage(dark:button.effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua);statusAppearance=button.observe(\.effectiveAppearance,options:[.new]){[weak self] button,_ in button.image=self?.trayImage(dark:button.effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua)};button.toolTip="Codex额度条 · 里奥Leo @staley_leo\n右键打开菜单";button.target=self;button.action=#selector(statusClicked);button.sendAction(on:[.leftMouseUp,.rightMouseUp])}
  startupStage="status-item-created"
  if diagnosticMode {startupDiagnostic=StartupDiagnosticWindow();startupDiagnostic?.refresh(self);startupDiagnostic?.showWindow(nil);NSApp.activate(ignoringOtherApps:true)}
  localMonitor=NSEvent.addLocalMonitorForEvents(matching:[.leftMouseDown,.rightMouseDown,.keyDown]){[weak self] event in
   guard let self else{return event}
   if event.type == .keyDown,event.keyCode==53 {self.closePanels();return nil}
   if event.type != .keyDown {self.inputMouseEvents+=1;self.dismissOutside(event)};return event
  }
  focusObservers.append(NotificationCenter.default.addObserver(forName:NSWindow.didBecomeKeyNotification,object:nil,queue:.main){[weak self] _ in
   DispatchQueue.main.async {guard let self,self.mainPanel != nil else{return};let key=NSApp.keyWindow
    #if SELF_HOST_ONLY
    self.selfHostTrace?.marker("key-window-check")
    #endif
    if self.surface != "tooltip" && key !== self.mainPanel && key !== self.subPanel {self.closePanels(restoreEntryFocus:false)}
   }
  })
  focusObservers.append(NotificationCenter.default.addObserver(forName:NSApplication.didResignActiveNotification,object:NSApp,queue:.main){[weak self] _ in self?.closePanels(restoreEntryFocus:false)})
  globalMonitor=NSEvent.addGlobalMonitorForEvents(matching:[.leftMouseDown,.rightMouseDown]){[weak self] _ in self?.dismissOutside()}
  #if SELF_HOST_ONLY
  guard selfHost else {fatalError("This build only supports --preview or --self-host-live")}
  #endif
  if !selfHost && !diagnosticMode {installApplicationMenu()}
  #if !DISTRIBUTION
  if selfHost && CommandLine.arguments.contains("--home-test") {installApplicationMenu();showApplicationHome()}
  if selfHost && !diagnosticMode && !CommandLine.arguments.contains("--home-test") {setupPreview();harness=HarnessController(app:self);installHarnessMenu();DispatchQueue.main.asyncAfter(deadline:.now()+0.5){if !self.diagnosticMode{self.entry.makeKeyAndOrderFront(nil)}}}
  #endif
  tracker.onChange={[weak self] in self?.scheduleHostUpdate()}
  tick=Timer(timeInterval:0.1,repeats:true){[weak self] _ in self?.update()};RunLoop.main.add(tick!,forMode:.common)
  #if SELF_HOST_ONLY
  if CommandLine.arguments.contains("--trace-input") {selfHostTrace=SelfHostEventTrace(self)}
  if CommandLine.arguments.contains("--input-checks") {Task { @MainActor in await SelfHostInputChecks(self).run() }}
  #endif
  startupStage="event-loop-ready"
  if diagnosticMode {DispatchQueue.main.async {self.update();self.startupDiagnostic?.showWindow(nil);self.startupDiagnostic?.window?.makeKeyAndOrderFront(nil)}} else {update()}
  #if !DISTRIBUTION
  if preview && CommandLine.arguments.contains("--export-visuals") {DispatchQueue.main.asyncAfter(deadline:.now()+1){self.exportVisuals()}}
  #endif
 }
 #if !DISTRIBUTION
 func setupPreview(){
  let fixture=CommandLine.arguments.first(where:{$0.hasPrefix("--fixture=")})?.split(separator:"=").last.map(String.init) ?? "dual"
  let week=QuotaWindow(remaining:fixture=="100" ? 100:23,minutes:10080,reset:Date().timeIntervalSince1970+5*86400+3600,fallback:"次")
  let five=QuotaWindow(remaining:fixture=="100" ? 100:72,minutes:300,reset:Date().timeIntervalSince1970+8290,fallback:"主")
  if preview {snapshot=QuotaSnapshot(windows:fixture=="week" ? [week]:fixture=="5h" ? [five]:[five,week],fetched:Date())
  news=NewsSnapshot(items:[NewsItem(id:"preview-announcement",version:"preview-v16",type:"reset_credit",status:"announced",scope:"示例套餐用户",time:"预告时间：2026-10-05（示例）",url:"https://aihot.news/codex-reset",occurred:"",basis:"",explicit:true,inferred:false)],history:nil,checked:"示例数据",day:NewsParser.day(Date()))
  if CommandLine.arguments.contains("--empty-news") {news=NewsSnapshot(items:[],history:nil,checked:"示例数据",day:NewsParser.day(Date()))}
  }
  let w=NSWindow(contentRect:NSRect(x:220,y:180,width:640,height:540),styleMask:[.titled,.closable,.miniaturizable,.resizable],backing:.buffered,defer:false)
  w.title=liveSelfHost ? "Codex额度条 · 真实额度／自有宿主":"Codex额度条 · 自有窗口预览（非真实账户）";w.minSize=NSSize(width:520,height:390);w.isReleasedWhenClosed=false;let hostView=PreviewHost();hostView.live=liveSelfHost;hostView.referenceLayout=referenceLayout;w.contentView=hostView;w.makeKeyAndOrderFront(nil);previewWindow=w
  if CommandLine.arguments.contains("--dark") {NSApp.appearance=NSAppearance(named:.darkAqua)}
  if CommandLine.arguments.contains("--light") {NSApp.appearance=NSAppearance(named:.aqua)}
  NSApp.activate(ignoringOtherApps:true)
 }
 #endif
 func geometry()->HostGeometry? {
  #if !DISTRIBUTION
  if selfHost {
   if let session=harness?.lifecycleSession {return session.current(now:ProcessInfo.processInfo.systemUptime)}
   guard harness?.hostUnavailable != true else{return nil}
   guard let w=previewWindow,w.isVisible,!w.isMiniaturized,let content=w.contentView else{return nil}
   let frame=w.convertToScreen(content.convert(content.bounds,to:nil))
   if CommandLine.arguments.contains("--window-relative") {
    var result=HostGeometry.windowRelative(frame:frame,number:w.windowNumber,occlusions:harness?.injectedOcclusions ?? [],controls:(harness?.updateVisible ?? true) ? [NSRect(x:frame.minX+18,y:frame.minY+105,width:30,height:(harness?.updateExpanded ?? false) ? 94:30)]:[])
    let shift=harness?.anchorShift ?? 0
    result=HostGeometry(frame:result.frame,avatar:.zero,railRight:result.railRight+shift,nativeControls:result.nativeControls,windowNumber:result.windowNumber,occlusions:result.occlusions,windowAnchor:result.windowAnchor.map{NSPoint(x:$0.x+shift,y:$0.y)})
    return result
   }
   if referenceLayout {
    let tile=NSRect(x:frame.minX+8,y:frame.minY+8,width:36,height:36)
    guard let avatar=AvatarCalibration.circle(button:tile,host:frame) else{return nil}
    return HostGeometry(frame:frame,avatar:avatar,railRight:AvatarCalibration.railRight(avatar:avatar,host:frame),nativeControls:(harness?.updateVisible ?? true) ? [NSRect(x:frame.minX+11,y:frame.minY+81,width:30,height:(harness?.updateExpanded ?? false) ? 94:30)]:[],windowNumber:w.windowNumber)
   }
   let shift=harness?.anchorShift ?? 0
   let rail=NSRect(x:frame.minX,y:frame.minY,width:66+shift,height:frame.height)
   let anchor=NSRect(x:frame.minX+12+shift,y:frame.minY+15,width:42,height:34)
   let nodes=[HostNode(id:0,parent:nil,role:"AXToolbar",frame:rail,actionable:false,account:false,navigation:true,update:false,popup:false),HostNode(id:1,parent:0,role:"AXPopUpButton",frame:anchor,actionable:true,account:true,navigation:false,update:false,popup:false)]
   let resolution=HostResolver.resolve(nodes:nodes,frame:frame,windowNumber:w.windowNumber,complete:true)
   guard var geometry=resolution.geometry else{return nil}
   geometry = HostGeometry(frame:geometry.frame,avatar:geometry.avatar,railRight:geometry.railRight,nativeControls:(harness?.updateVisible ?? true) ? [NSRect(x:frame.minX+18,y:frame.minY+81,width:30,height:(harness?.updateExpanded ?? false) ? 94:30)]:[],windowNumber:w.windowNumber,occlusions:harness?.injectedOcclusions ?? [])
   return geometry
  }
  #endif
  return tracker.current(entryWindowNumber:entry.windowNumber)
 }
 func scheduleHostUpdate(){
  guard !hostUpdateQueued else{return};hostUpdateQueued=true
  DispatchQueue.main.async {[weak self] in guard let self else{return};self.hostUpdateQueued=false;self.update()}
 }
 func update(){
  uiHeartbeat+=1
  #if SELF_HOST_ONLY
  recordFollow("tick-begin")
  #endif
  let recordThisTick=diagnosticMode && Date().timeIntervalSince(lastRuntimeStatus)>1
  if recordThisTick {startupStage="before-host-resolution";writeRuntimeStatus()}
  let priorEntryOrigin=entry.frame.origin
  var newHost=geometry().map{preferences.calibrated($0)}
  if selfHost,let resolved=newHost {
   newHost?.stack=HostStackObservation.read((NSWindow.windowNumbers(options:[])?.map(\.intValue) ?? []),host:resolved.windowNumber,entry:entry.windowNumber,now:ProcessInfo.processInfo.systemUptime)
  }
  var moved=newHost?.frame != host?.frame || newHost?.windowAnchor != host?.windowAnchor || newHost?.railRight != host?.railRight;host=newHost
  startupStage="host-resolution-returned"
  if diagnosticMode {startupDiagnostic?.refresh(self)}
  if let host,!hidden {
   let height=CGFloat(max(1,snapshot?.windows.count ?? 0))*33+(quotaError==nil ? 0:14)
   if let frame=Placement.entry(host:host,size:NSSize(width:48,height:height),offset:preferences.offset){
    if entry.frame != frame {entry.setFrame(frame,display:true)}
    entry.ignoresMouseEvents=false
    if attachment.apply(to:entry,host:host.windowNumber,hidden:hidden) {orderRevision+=1;orderCards()}
   }else{attachment.apply(to:entry,host:nil,hidden:hidden);closePanels()}
  } else {attachment.apply(to:entry,host:nil,hidden:hidden);if surface != "status-menu"{closePanels()}}
  moved = moved || entry.frame.origin != priorEntryOrigin
  #if !DISTRIBUTION
  if selfHost && Date().timeIntervalSince(lastDiagnostic)>0.25 {
   lastDiagnostic=Date()
   let report:[String:Any] = ["entryVisible":entry.isVisible,"entryFrame":NSStringFromRect(entry.frame),"hostFrame":NSStringFromRect(host?.frame ?? .zero),"avatar":NSStringFromRect(host?.avatar ?? .zero),"surface":surface,"mainPanel":NSStringFromRect(mainPanel?.frame ?? .zero),"submenu":NSStringFromRect(subPanel?.frame ?? .zero),"offset":preferences.offset,"hoverEntry":hoverEntry,"entryPassThrough":entry.ignoresMouseEvents,"nativePopupHits":harness?.popupHits ?? 0,"quotaReads":quotaReads,"dataMode":liveSelfHost ? "live":"fixtures","quotaText":snapshot?.windows.map{$0.label+$0.percent}.joined(separator:"，") ?? "unavailable","quotaError":quotaError ?? "","codexHostAccess":"excluded in SELF_HOST_ONLY build","newsReadVersions":preferences.read,"hostMinimized":previewWindow?.isMiniaturized ?? false,"nativeMouseDownEvents":inputMouseEvents,"hostWindowID":host?.windowNumber ?? 0,"entryCanBecomeKey":entry.canBecomeKey,"entryIsKey":entry.isKeyWindow]
   try? FileManager.default.createDirectory(at:runtime,withIntermediateDirectories:true)
   if let data=try? JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]) {try? data.write(to:runtime.appendingPathComponent("preview-diagnostic.json"),options:.atomic)}
  }
  #endif
  entryView.snapshot=snapshot;entryView.stale=quotaError != nil;entryView.unread=unread;entryView.needsDisplay=true
  entryView.setAccessibilityLabel((snapshot?.windows.map{$0.label+$0.percent}.joined(separator:"，") ?? "额度不可用")+(unread ? "，有未读消息":""))
  updateTooltipContents();menuUpdateLabel?.stringValue=placementNote ?? updated
  if moved,let panel=mainPanel,let host,surface != "status-menu" {let size=(panel.contentView as? CardSurface)?.card.bounds.size ?? panel.frame.size;let frame=Placement.popup(host:host,size:size,screen:screen).insetBy(dx:-8,dy:-8);if panel.frame != frame {panel.setFrame(frame,display:true)};layoutSubmenu();layoutHelp()}
  if liveData {
   if Date()>=nextQuota && !requestRunning {fetchQuota()}
   if Date()>=nextNews && !newsRunning {fetchNews()}
   status.button?.toolTip="Codex额度条 · 里奥Leo @staley_leo\n"+tracker.status+"\n"+updated
  }
  if newsDay != NewsParser.day(Date()) {news=news?.current();newsDay=NewsParser.day(Date());nextNews=Date.distantPast;if surface=="news"{closePanels()}}
  if recordThisTick || (!selfHost && Date().timeIntervalSince(lastRuntimeStatus)>1) {lastRuntimeStatus=Date();writeRuntimeStatus()}
  applicationHome?.refresh(self)
  refreshHover()
 }
 func fetchQuota(){requestRunning=true
  DispatchQueue.global(qos:.utility).async{[weak self] in guard let self else{return};let result=Result{try self.quotaClient.read()}
   DispatchQueue.main.async{self.requestRunning=false;switch result{case .success(let value):self.quotaReads+=1;self.snapshot=value;self.quotaError=nil;self.failures=0;self.nextQuota=Date().addingTimeInterval(60)
   case .failure(let error):if (error as? QuotaError)?.discardSnapshot == true {self.snapshot=nil};self.quotaError=error.localizedDescription;self.failures+=1;self.nextQuota=Date().addingTimeInterval(RetryPolicy.delay(self.failures))}
    if self.surface=="tooltip"{self.updateTooltipContents()}
   }
  }
 }
 func fetchNews(){newsRunning=true;nextNews=Date().addingTimeInterval(600)
  NewsClient.read{[weak self] result in DispatchQueue.main.async{guard let self else{return};self.newsRunning=false;switch result {case .success(let value):self.news=value;self.newsError=nil;if let data=try? JSONEncoder().encode(value){try? data.write(to:self.runtime.appendingPathComponent("public-news.json"),options:.atomic)};case .failure(let error):self.newsError=error.localizedDescription}
  }}
 }
 func scheduleHover(_ seconds:Double){hoverTimer?.invalidate();hoverTimer=Timer(timeInterval:seconds,repeats:false){[weak self] _ in self?.refreshHover()};RunLoop.main.add(hoverTimer!,forMode:.common)}
 func refreshHover(){
  guard entry.isVisible,!entry.ignoresMouseEvents,surface.isEmpty || surface=="tooltip" else {hoverDue=nil;leaveDue=nil;return}
  if hoverEntry || hoverTip {leaveDue=nil;if surface.isEmpty {if hoverDue==nil{hoverDue=Date().addingTimeInterval(0.15);scheduleHover(0.15)};if Date()>hoverDue!{showTooltip();hoverDue=nil}}}
  else {hoverDue=nil;if surface=="tooltip"{if leaveDue==nil{leaveDue=Date().addingTimeInterval(0.12);scheduleHover(0.12)};if Date()>leaveDue!{closePanels()}}}
 }
 func closePanels(restoreEntryFocus:Bool=true){
  #if SELF_HOST_ONLY
  if mainPanel != nil || subPanel != nil {selfHostTrace?.marker("close-panels")}
  #endif
  hideHelp();helpButton=nil;menuUpdateLabel=nil;hoverTimer?.invalidate();let hadPanel=mainPanel != nil;mainPanel?.dismiss();subPanel?.dismiss();mainPanel=nil;subPanel=nil;submenuKind="";tooltipView=nil;surface="";hoverDue=nil;leaveDue=nil;hoverTip=false;menuButtons=[];if restoreEntryFocus && selfHost && hadPanel && entry.isVisible && !entry.ignoresMouseEvents {previewWindow?.makeKey()}}
 func dismissOutside(_ event:NSEvent?=nil){guard mainPanel != nil else{return}
  if let event,let source=event.window {
   if source === mainPanel || source === subPanel || source === helpPanel,
    let card=source.contentView as? CardSurface,
    card.containsInteractivePoint(card.convert(event.locationInWindow,from:nil)) {return}
   closePanels(restoreEntryFocus:false);return
  }
  let point=NSEvent.mouseLocation
  if [mainPanel,subPanel,helpPanel].compactMap({$0}).contains(where:{panel in
   guard let card=panel.contentView as? CardSurface else{return false}
   return card.containsInteractivePoint(card.convert(panel.convertPoint(fromScreen:point),from:nil))
  }) {return}
  closePanels(restoreEntryFocus:false)
 }
 func panel(_ size:NSSize,_ view:NSView,_ kind:String)->OverlayPanel? {
  guard let host else{return nil};closePanels();surface=kind
  panelRevision+=1
  let surfaceView=CardSurface(view as! CardView)
  let p=OverlayPanel(size:surfaceView.frame.size);p.contentView=surfaceView;p.escaped={[weak self] in self?.closePanels()}
  p.setFrame(Placement.popup(host:host,size:size,screen:screen).insetBy(dx:-8,dy:-8),display:true);entry.addChildWindow(p,ordered:.above);p.order(.above,relativeTo:entry.windowNumber);mainPanel=p
  return p
 }
 func showTooltip(){
  if surface=="tooltip",tooltipView != nil {updateTooltipContents();return}
  let height=CGFloat(max(1,snapshot?.windows.count ?? 0))*68.8+36+(quotaError==nil ? 0:22)
  let view=QuotaCard(frame:NSRect(x:0,y:0,width:243.2,height:height));view.snapshot=snapshot;view.error=quotaError;view.updateText=updated
  view.onHover={[weak self] inside in self?.hoverTip=inside;self?.refreshHover()}
  if panel(NSSize(width:243.2,height:height),view,"tooltip") != nil {tooltipView=view}
 }
 @objc func statusClicked(){
  let menu=makeStatusMenu();status.menu=menu;status.button?.performClick(nil);status.menu=nil
 }
 func makeStatusMenu()->NSMenu {
  let menu=NSMenu();menu.addItem(withTitle:selfHost ? "自有宿主验证":tracker.status,action:nil,keyEquivalent:"")
  let restore=menu.addItem(withTitle:hidden ? "显示额度条":"隐藏额度条",action:#selector(toggleHidden),keyEquivalent:"");restore.target=self
  menu.addItem(.separator());let quit=menu.addItem(withTitle:"退出额度条",action:#selector(quit),keyEquivalent:"");quit.target=self
  let appearance=NSMenuItem(title:"主题色",action:nil,keyEquivalent:"");appearance.submenu=themeMenu();menu.insertItem(appearance,at:2)
  let diagnostic=menu.insertItem(withTitle:"诊断信息…",action:#selector(showApplicationHome),keyEquivalent:"",at:3);diagnostic.target=self
  return menu
 }
 func row(_ title:String,_ y:CGFloat,_ view:NSView,_ selector:Selector,_ icon:Glyph = .news)->RowButton {
  let b=RowButton(frame:NSRect(x:5.6,y:y,width:view.bounds.width-11.2,height:28.8));b.title=title;b.icon=icon;b.target=self;b.action=selector;b.isBordered=false;b.setAccessibilityLabel(title);view.addSubview(b);return b
 }
 func showMenu(){
  let view=CardView(frame:NSRect(x:0,y:0,width:228,height:195.2))
  let label=NSTextField(labelWithString:placementNote ?? updated);label.font = Visual.font(8.8);label.textColor = Visual.muted;label.frame=NSRect(x:38.4,y:11,width:176,height:16);view.addSubview(label)
  let clock=GlyphView(frame:NSRect(x:14.4,y:12,width:13.6,height:13.6));clock.kind = .clock;clock.color=Visual.muted;view.addSubview(clock)
  let newsButton=row("Tibo 重置消息",28.8,view,#selector(openNews),.news)
  if unread {view.addSubview(DotView(frame:NSRect(x:207.6,y:40.4,width:5.6,height:5.6)))}
  let hide=row(hidden ? "恢复显示":"暂时隐藏",62.4,view,#selector(toggleHidden),.hide)
  let position=row("位置调整",91.2,view,#selector(openPosition),.position);position.chevron=true
  let theme=row("主题色",120,view,#selector(openTheme),.theme);theme.chevron=true
  let quitButton=row("退出额度条",159.2,view,#selector(quit),.exit)
  for y in [CGFloat(60),CGFloat(154.4)] {view.addSubview(RuleView(frame:NSRect(x:12.8,y:y,width:202.4,height:0.8)))}
  guard let p=panel(view.bounds.size,view,"menu") else{return};menuButtons=[newsButton,hide,position,theme,quitButton];menuUpdateLabel=label
  theme.onEnter={[weak self] in self?.showTheme()}
  position.onEnter={[weak self] in self?.showPosition()};newsButton.onEnter={[weak self] in self?.closeSubmenu()};hide.onEnter={[weak self] in self?.closeSubmenu()};quitButton.onEnter={[weak self] in self?.closeSubmenu()}
  p.makeKey()
  #if SELF_HOST_ONLY
  selfHostTrace?.marker("menu-opened")
  #endif
 }
 func closeSubmenu(){let wasKey=subPanel?.isKeyWindow == true;subPanel?.dismiss();subPanel=nil;submenuKind="";if wasKey {mainPanel?.makeKey()}}
 @objc func openPosition(){showPosition()}
 func showPosition(){guard surface=="menu",let parent=mainPanel,menuButtons.count>2 else{return}
  if subPanel != nil && submenuKind=="position" {layoutSubmenu();return}
  closeSubmenu();submenuKind="position"
  let view=CardView(frame:NSRect(x:0,y:0,width:147.2,height:153.6))
  _=row("上微调",4.8,view,#selector(moveUp),.up);_=row("下微调",33.6,view,#selector(moveDown),.down);_=row("左微调",62.4,view,#selector(moveLeft),.left);_=row("右微调",91.2,view,#selector(moveRight),.right);_=row("恢复初始",122.4,view,#selector(resetPosition),.reset)
  view.corner=12;view.addSubview(RuleView(frame:NSRect(x:12.8,y:120,width:121.6,height:0.8)))
  let b=menuButtons[2];let screenRow=parent.convertToScreen(b.convert(b.bounds,to:nil))
  let surfaceView=CardSurface(view);let p=OverlayPanel(size:surfaceView.frame.size);p.contentView=surfaceView;p.escaped={[weak self] in self?.closePanels()}
  p.setFrame(Placement.submenu(parent:parent.frame.insetBy(dx:8,dy:8),row:screenRow,size:view.bounds.size,screen:screen).insetBy(dx:-8,dy:-8),display:true);parent.addChildWindow(p,ordered:.above);p.order(.above,relativeTo:parent.windowNumber);subPanel=p;p.makeKey()
  #if SELF_HOST_ONLY
  selfHostTrace?.marker("submenu-opened")
  #endif
 }
 @objc func openTheme(){showTheme()}
 func showTheme(){
  guard surface=="menu",let parent=mainPanel,menuButtons.count>3 else{return}
  if subPanel != nil && submenuKind=="theme" {layoutSubmenu();return}
  closeSubmenu();submenuKind="theme"
  let view=CardView(frame:NSRect(x:0,y:0,width:147.2,height:67.2));view.corner=12
  let light=row("浅色",4.8,view,#selector(chooseLight),.lightTheme),dark=row("深色",33.6,view,#selector(chooseDark),.darkTheme)
  light.state=preferences.theme=="light" ? .on:.off;dark.state=preferences.theme=="dark" ? .on:.off
  let card=CardSurface(view),p=OverlayPanel(size:card.frame.size);p.contentView=card;p.escaped={[weak self] in self?.closePanels()}
  subPanel=p;layoutSubmenu();parent.addChildWindow(p,ordered:.above);p.order(.above,relativeTo:parent.windowNumber);p.makeKey()
 }
 @objc func chooseLight(){setTheme("light")}
 @objc func chooseDark(){setTheme("dark")}
 func setTheme(_ value:String){if preferences.save(theme:value){applyTheme();entryView.needsDisplay=true;closePanels();if !selfHost{installApplicationMenu()}}}
 func move(_ delta:CGFloat,reset:Bool=false){
  let requested=reset ? 0:preferences.offset+delta
  placementNote=nil
  if !preferences.save(offset:requested,horizontal:reset ? 0:nil,legacyAnchor:reset ? false:nil){placementNote="位置保存失败，请检查目录权限"}
  closePanels();update();if placementNote != nil {showMenu()}
 }
 func moveHorizontal(_ delta:CGFloat){
  placementNote=nil
  let value=max(-6,min(600,preferences.horizontal+delta))
  if let raw=geometry(),Placement.entry(host:preferences.calibrated(raw).calibrated(horizontal:value-preferences.horizontal),size:entry.frame.size,offset:preferences.offset) != nil {if !preferences.save(horizontal:value){placementNote="位置保存失败"}}
  closePanels();update()
 }
 @objc func moveLeft(){moveHorizontal(-8)}
 @objc func moveRight(){moveHorizontal(8)}
 @objc func moveUp(){move(-8)}
 @objc func moveDown(){move(8)}
 @objc func resetPosition(){move(0,reset:true)}
 @objc func toggleHidden(){hidden.toggle();closePanels();update();if !selfHost{installApplicationMenu()}}
 @objc func quit(){NSApp.terminate(nil)}
 @objc func openNews(){showNews()}
 func label(_ text:String,_ y:CGFloat,_ h:CGFloat,_ view:NSView,_ size:CGFloat=11,_ color:NSColor = Visual.secondary){let l=NSTextField(wrappingLabelWithString:text);l.font = Visual.font(size);l.textColor=color;l.frame=NSRect(x:16,y:y,width:view.bounds.width-32,height:h);view.addSubview(l)}
 func showNews(){
  let blocks=NewsLayout.blocks(news?.items ?? [],width:211.2,budget:min(340,max(0,screen.height-120)))
  let height:CGFloat=blocks.isEmpty ? (news?.history==nil ? 109.6:132):blocks.reduce(97.2){$0+$1.height}
  let view=CardView(frame:NSRect(x:0,y:0,width:blocks.isEmpty ? 256:240,height:height))
  view.corner=16
  let empty=blocks.isEmpty
  let doc=GlyphView(frame:NSRect(x:empty ? 16:14.4,y:26.4-(empty ? 15.2:13.6)/2,width:empty ? 15.2:13.6,height:empty ? 15.2:13.6));view.addSubview(doc)
  view.addSubview(NewsHeading(frame:NSRect(x:empty ? 39.2:35.2,y:16.4,width:175,height:20),size:empty ? 12.8:11.2))
  let help=PlainButton(frame:NSRect(x:view.bounds.width-(empty ? 27.6:26.8),y:empty ? 18:18.8,width:12,height:12));help.title="";help.icon = .help;help.isBordered=false;help.setAccessibilityLabel("公开消息说明");help.target=self;help.action=#selector(toggleHelp);help.onHover={[weak self] inside in self?.helpHovered=inside;self?.refreshHelp()};view.addSubview(help)
  if empty {
   let text=news==nil ? (newsError ?? "正在检查公开消息"):(news?.items.isEmpty == false ? "消息内容较长，请查看详情":"暂无新消息")
   view.addSubview(DesignText(text,x:36,baseline:53.6,width:204,size:10.4,color:Visual.color(0x646464,0xaaa8b5)))
   let calendar=GlyphView(frame:NSRect(x:16,y:43.2,width:13.6,height:13.6));calendar.kind = .calendar;calendar.color=Visual.color(0x949494,0x93909f);view.addSubview(calendar)
   if let text=news?.history?.historyText {label(text,61,22,view,9.6)}
  } else {var y:CGFloat=50
   for block in blocks {let n=block.item
    func text(_ value:String,_ top:CGFloat,_ h:CGFloat,_ size:CGFloat,_ color:NSColor=Visual.text){view.addSubview(NewsText(value,frame:NSRect(x:14.4,y:y+top,width:211.2,height:h),size:size,color:color))}
    view.addSubview(NewsText(n.title,frame:NSRect(x:14.4,y:y,width:block.titleWidth,height:block.titleHeight),size:12))
    let badge=BadgeView(frame:NSRect(x:225.6-block.stateWidth,y:y,width:block.stateWidth,height:block.stateHeight+6));badge.text=n.state;view.addSubview(badge)
    text("适用范围",block.scopeLabelY,14.4,8,Visual.muted)
    text(n.scopeValue,block.scopeY,block.scopeHeight,9.6,Visual.color(0x474747,0xc2c0ca))
    text(n.timeField.label,block.timeLabelY,block.labelHeight,8,Visual.muted)
    text(n.timeField.value,block.timeY,block.timeHeight,9.6,Visual.color(0x474747,0xc2c0ca))
    if !block.complete {text("更多内容见详情 · 保留未读",block.moreY,16,9,Visual.secondary)}
    y+=block.height
   }
  }
  let separatorY=height-47.2
  view.addSubview(RuleView(frame:NSRect(x:16,y:separatorY,width:view.bounds.width-32,height:0.8)))
  view.addSubview(DesignText(newsError ?? "消息检查于 "+NewsParser.date(news?.checked ?? ""),x:16,baseline:height-30.4,width:view.bounds.width-32,size:8,color:Visual.color(0x8a8a8a,0x93909f)))
  view.addSubview(DesignText("来源：AIHOT",x:16,baseline:height-13.6,width:170,size:8,color:Visual.color(0x8a8a8a,0x93909f)))
  let details=PlainButton(frame:NSRect(x:view.bounds.width-56,y:height-25.6,width:40,height:20));details.title="详情";details.icon = .external;details.isBordered=false;details.target=self;details.action=#selector(openDetails);view.addSubview(details)
  guard let p=panel(view.bounds.size,view,"news") else{return};helpButton=help;p.makeKey()
  let shown=NewsLayout.visibleVersions(blocks)
  DispatchQueue.main.asyncAfter(deadline:.now()+0.15){[weak self,weak p] in guard let self,let p,p.isVisible,p.occlusionState.contains(.visible),self.mainPanel===p,self.surface=="news" else{return}
   var read=self.preferences.read;for(id,v) in shown{read[id]=v};_ = self.preferences.save(read:read);self.entryView.unread=self.unread;self.entryView.needsDisplay=true
  }
 }
 func orderCards(){
  if let mainPanel {mainPanel.order(.above,relativeTo:entry.windowNumber)}
  if let subPanel,let mainPanel {subPanel.order(.above,relativeTo:mainPanel.windowNumber)}
  if let helpPanel,let mainPanel {helpPanel.order(.above,relativeTo:mainPanel.windowNumber)}
 }
 func updateTooltipContents(){
  guard surface=="tooltip",let view=tooltipView,let panel=mainPanel,let host else{return}
  view.snapshot=snapshot;view.error=quotaError;view.updateText=updated
  let height=CGFloat(max(1,snapshot?.windows.count ?? 0))*68.8+36+(quotaError==nil ? 0:22)
  if view.frame.height != height {
   view.setFrameSize(NSSize(width:243.2,height:height));panel.contentView?.setFrameSize(NSSize(width:259.2,height:height+16))
   panel.setFrame(Placement.popup(host:host,size:view.bounds.size,screen:screen).insetBy(dx:-8,dy:-8),display:true)
  }
  view.needsDisplay=true
 }
 func layoutSubmenu(){
  guard let p=subPanel,let parent=mainPanel,menuButtons.count>2,let card=p.contentView as? CardSurface else{return}
  let b=menuButtons[submenuKind=="theme" ? 3:2],row=parent.convertToScreen(b.convert(b.bounds,to:nil))
  let frame=Placement.submenu(parent:parent.frame.insetBy(dx:8,dy:8),row:row,size:card.card.bounds.size,screen:screen).insetBy(dx:-8,dy:-8)
  if p.frame != frame {p.setFrame(frame,display:true)}
 }
 func applyTheme(){NSApp.appearance=NSAppearance(named:preferences.theme=="dark" ? .darkAqua:.aqua)}
 func themeMenu()->NSMenu {
  let menu=NSMenu()
  for (title,value) in [("浅色","light"),("深色","dark")] {
   let item=menu.addItem(withTitle:title,action:#selector(selectTheme(_:)),keyEquivalent:"");item.target=self;item.representedObject=value;item.state=preferences.theme==value ? .on:.off
  };return menu
 }
 @objc func selectTheme(_ sender:NSMenuItem){guard let value=sender.representedObject as? String else{return};setTheme(value)}
 static let newsExplanation="AIHOT公开活动不代表本人额度已重置或收到卡。历史按北京时间显示；确认时间不是实际到账时间。"
 func hideHelp(){helpTimer?.invalidate();helpPanel?.dismiss();helpPanel=nil;helpHovered=false;helpCardHovered=false}
 func refreshHelp(){
  helpTimer?.invalidate()
  guard surface=="news" else{hideHelp();return}
  if helpHovered || helpCardHovered {showHelp()}
  else {helpTimer=Timer(timeInterval:0.12,repeats:false){[weak self] _ in guard let self,!self.helpHovered,!self.helpCardHovered else{return};self.hideHelp()};RunLoop.main.add(helpTimer!,forMode:.common)}
 }
 @objc func toggleHelp(){if helpPanel != nil {hideHelp()}else{showHelp()}}
 func showHelp(){
  guard surface=="news",let parent=mainPanel,helpButton != nil else{return}
  if helpPanel != nil {layoutHelp();return}
  let view=HoverCard(frame:NSRect(x:0,y:0,width:224,height:76));view.corner=9.6
  label(Self.newsExplanation,11,56,view,9.6)
  view.onHover={[weak self] inside in self?.helpCardHovered=inside;self?.refreshHelp()}
  let surface=CardSurface(view),p=OverlayPanel(size:NSSize(width:240,height:92),entry:true)
  p.contentView=surface;helpPanel=p;layoutHelp();parent.addChildWindow(p,ordered:.above);p.order(.above,relativeTo:parent.windowNumber)
 }
 func layoutHelp(){
  guard let p=helpPanel,let b=helpButton,let parent=mainPanel else{return}
  let target=parent.convertToScreen(b.convert(b.bounds,to:nil))
  let width:CGFloat=224,height:CGFloat=76
  var y=target.minY-height-6.4
  if y<screen.minY {y=target.maxY+6.4}
  let frame=NSRect(x:min(max(target.maxX-width,screen.minX),screen.maxX-width),y:min(max(y,screen.minY),screen.maxY-height),width:width,height:height).insetBy(dx:-8,dy:-8)
  if p.frame != frame {p.setFrame(frame,display:true)}
 }
 @objc func openDetails(){NSWorkspace.shared.open(URL(string:"https://aihot.news/codex-reset")!)}
 func trayImage(dark:Bool=false)->NSImage {TrayArtwork.image(dark:dark)}

 func applicationWillTerminate(_ notification:Notification){tick?.invalidate();hoverTimer?.invalidate();quotaClient?.cancel();focusObservers.forEach{NotificationCenter.default.removeObserver($0)};if let localMonitor{NSEvent.removeMonitor(localMonitor)};if let globalMonitor{NSEvent.removeMonitor(globalMonitor)};entry?.orderOut(nil);closePanels()}
}
