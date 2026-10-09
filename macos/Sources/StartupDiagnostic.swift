import AppKit
final class StartupDiagnosticWindow:NSWindowController {
 static func underNotch(_ app:AppDelegate)->Bool {guard let frame=app.status?.button?.window?.frame,let s=app.status?.button?.window?.screen ?? NSScreen.main,let left=s.auxiliaryTopLeftArea,let right=s.auxiliaryTopRightArea else{return false};return frame.maxX>left.maxX && frame.minX<right.minX && frame.maxY>left.minY}
 let statusText=NSTextField(wrappingLabelWithString:"")
 let icon=NSImageView(frame:NSRect(x:20,y:245,width:40,height:40))
 init(){let w=NSWindow(contentRect:NSRect(x:320,y:300,width:580,height:340),styleMask:[.titled,.closable],backing:.buffered,defer:false);w.title="Codex额度条 · 启动诊断（本程序）";w.isReleasedWhenClosed=false;super.init(window:w)
  let v=NSView(frame:NSRect(x:0,y:0,width:580,height:340));w.contentView=v
  let heading=NSTextField(labelWithString:"左侧是额度条菜单栏图标的实际图像");heading.frame=NSRect(x:76,y:257,width:480,height:24);heading.font=Visual.font(14);v.addSubview(heading);v.addSubview(icon)
  statusText.frame=NSRect(x:20,y:35,width:540,height:200);statusText.font = .monospacedSystemFont(ofSize:11,weight:.regular);v.addSubview(statusText)
  let note=NSTextField(labelWithString:"不申请权限、不重启其他程序；请保留此窗口供诊断。");note.frame=NSRect(x:20,y:10,width:540,height:20);note.font=Visual.font(11);v.addSubview(note)
 }
 required init?(coder:NSCoder){fatalError()}
 func refresh(_ app:AppDelegate){icon.image=app.status?.button?.image
  let item=app.status
  let ownFrame=item?.button?.window.map{NSStringFromRect($0.frame)} ?? "无"
  statusText.stringValue="版本：\(Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") ?? "未知")\nPID：\(ProcessInfo.processInfo.processIdentifier) · 主线程心跳：\(app.uiHeartbeat)\nUI阶段：\(app.startupStage)\n菜单栏对象：\(item != nil) · isVisible：\(item?.isVisible ?? false)\n按钮：\(item?.button != nil) · 图像有效：\(item?.button?.image?.isValid ?? false)\n自身状态栏窗口：\(ownFrame)\n与刘海区重叠：\(Self.underNotch(app))\n定位：\(app.selfHost ? "自有测试，未访问Codex":app.tracker.status)\n入口可见：\(app.entry?.isVisible ?? false)"
 }
}
