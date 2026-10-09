import AppKit
final class ApplicationHome:NSWindowController {
 let statusText=NSTextField(wrappingLabelWithString:"")
 init(app:AppDelegate){
  let w=NSWindow(contentRect:NSRect(x:360,y:360,width:530,height:280),styleMask:[.titled,.closable,.miniaturizable],backing:.buffered,defer:false)
  w.title="Codex额度条 · 诊断信息";w.isReleasedWhenClosed=false;super.init(window:w)
  let view=NSView(frame:NSRect(x:0,y:0,width:530,height:280));w.contentView=view
  let title=NSTextField(labelWithString:"Codex额度条");title.font=Visual.font(24,.medium);title.frame=NSRect(x:24,y:225,width:480,height:34);view.addSubview(title)
  let credit=NSTextField(labelWithString:"里奥Leo @staley_leo · Mac 版");credit.font=Visual.font(12);credit.textColor = .secondaryLabelColor;credit.frame=NSRect(x:24,y:200,width:480,height:20);view.addSubview(credit)
  statusText.font=Visual.font(13);statusText.frame=NSRect(x:24,y:82,width:480,height:105);view.addSubview(statusText)
  let note=NSTextField(wrappingLabelWithString:"关闭此窗口后继续运行；需要时从菜单栏的“诊断信息…”打开。\n首次使用如提示辅助功能权限，请仅为本应用手动授权。");note.font=Visual.font(11);note.textColor = .secondaryLabelColor;note.frame=NSRect(x:24,y:35,width:480,height:40);view.addSubview(note)
 }
 required init?(coder:NSCoder){fatalError()}
 func refresh(_ app:AppDelegate){
  let quota=app.snapshot?.windows.map{$0.label+$0.percent}.joined(separator:"  ") ?? app.quotaError ?? "正在连接本机 Codex 额度服务"
  let host=app.selfHost ? "自有宿主验证（非真实 Codex）":app.tracker.status
  statusText.stringValue="额度：\(quota)\n入口：\(host)\n\(app.updated)\n此窗口仅供诊断；入口实际行为以使用情况为准。"
 }
}
extension AppDelegate {
 func installApplicationMenu(){
  let bar=NSMenu(),root=NSMenuItem(),menu=NSMenu()
  let show=menu.addItem(withTitle:"诊断信息…",action:#selector(showApplicationHome),keyEquivalent:"0");show.target=self
  let toggle=menu.addItem(withTitle:hidden ? "显示额度条":"隐藏额度条",action:#selector(toggleHidden),keyEquivalent:"");toggle.target=self
  let settings=NSMenuItem(title:"设置",action:nil,keyEquivalent:"");settings.submenu=themeMenu();menu.addItem(settings)
  menu.addItem(.separator());let quit=menu.addItem(withTitle:"退出 Codex额度条",action:#selector(quit),keyEquivalent:"q");quit.target=self
  root.submenu=menu;bar.addItem(root);NSApp.mainMenu=bar
 }
 @objc func showApplicationHome(){if applicationHome==nil{applicationHome=ApplicationHome(app:self)};applicationHome?.refresh(self);applicationHome?.showWindow(nil);NSApp.activate(ignoringOtherApps:true)}
 func applicationShouldHandleReopen(_ sender:NSApplication,hasVisibleWindows flag:Bool)->Bool {scheduleHostUpdate();return false}
}
