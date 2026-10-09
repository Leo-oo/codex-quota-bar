#if !DISTRIBUTION
import AppKit
// Self-owned test controls only. This class never enumerates or controls another application.
final class HarnessController:NSObject {
 unowned let app:AppDelegate
 var window:NSWindow!
 var occludingWindow:NSWindow?
 var popupHits=0
 var hostUnavailable=false
 var lifecycleSession:HostSession?
 var updateVisible=true,updateExpanded=false
 var anchorShift:CGFloat=0
 var injectedOcclusions:[NSRect]=[]
 var statusLabel:NSTextField!
 var timer:Timer?
 init(app:AppDelegate){self.app=app;super.init()
  window=NSWindow(contentRect:NSRect(x:900,y:180,width:340,height:550),styleMask:[.titled,.closable],backing:.buffered,defer:false)
  window.title="额度条 · 自有测试控制";window.isReleasedWhenClosed=false
  let v=FlippedView(frame:NSRect(x:0,y:0,width:340,height:550));window.contentView=v
  let title=NSTextField(wrappingLabelWithString:"只控制此应用自己的模拟宿主。\n真实Codex窗口代码未参与验证。");title.frame=NSRect(x:16,y:12,width:310,height:40);title.font = .systemFont(ofSize:12);v.addSubview(title)
  let commands:[(String,Selector)] = [("移动宿主",#selector(moveHost)),("缩小／恢复尺寸",#selector(resizeHost)),("最小化宿主",#selector(minimizeHost)),("恢复宿主",#selector(restoreHost)),("更新入口切换",#selector(toggleUpdate)),("更新入口扩大／还原",#selector(expandUpdate)),("显示原生遮挡层",#selector(showOcclusion)),("关闭原生遮挡层",#selector(closeOcclusion)),("聚焦额度入口",#selector(focusEntry)),("聚焦宿主／外点",#selector(focusHost)),("刷新真实额度",#selector(refreshQuota)),("示例消息新版本",#selector(reviseNews)),("示例长消息",#selector(longNews)),("关闭宿主",#selector(closeHost)),("改变内部锚点",#selector(shiftAnchor)),("同框替换宿主",#selector(replaceHost))]
  for (i,command) in commands.enumerated(){let b=NSButton(title:command.0,target:self,action:command.1);b.bezelStyle = .rounded;b.frame=NSRect(x:14+CGFloat(i%2)*160,y:62+CGFloat(i/2)*40,width:152,height:30);if command.0.hasPrefix("示例") {b.isEnabled=app.preview};v.addSubview(b)}
  statusLabel=NSTextField(wrappingLabelWithString:"");statusLabel.frame=NSRect(x:16,y:425,width:306,height:112);statusLabel.font = .monospacedSystemFont(ofSize:11,weight:.regular);v.addSubview(statusLabel)
  timer=Timer.scheduledTimer(withTimeInterval:0.25,repeats:true){[weak self] _ in self?.refreshStatus()}
 }
 func show(){window.makeKeyAndOrderFront(nil)}
 func event(_ action:String){app.recordHarness(action);refreshStatus()}
 func refreshStatus(){statusLabel.stringValue="数据：\(app.liveSelfHost ? "真实额度":"示例额度")\n\(app.snapshot?.windows.map{$0.label+$0.percent}.joined(separator:"，") ?? app.quotaError ?? "读取中")\n读取成功：\(app.quotaReads)\n宿主最小化：\(app.previewWindow?.isMiniaturized ?? false)\n入口可见：\(app.entry?.isVisible ?? false) · 命中穿透：\(app.entry?.ignoresMouseEvents ?? false)\n原生弹层点击：\(popupHits)"}
 @objc func moveHost(){guard let w=app.previewWindow else{return};w.setFrameOrigin(NSPoint(x:w.frame.minX+65,y:w.frame.minY+35));app.update();event("move-host")}
 @objc func resizeHost(){guard let w=app.previewWindow else{return};w.setContentSize(w.frame.width>580 ? NSSize(width:540,height:420):NSSize(width:640,height:540));app.update();event("resize-host")}
 @objc func minimizeHost(){app.previewWindow?.miniaturize(nil);app.update();window.makeKeyAndOrderFront(nil);event("minimize-host")}
 @objc func restoreHost(){app.previewWindow?.deminiaturize(nil);app.previewWindow?.makeKeyAndOrderFront(nil);app.update();window.makeKeyAndOrderFront(nil);event("restore-host")}
 func refreshHostView(){if let v=app.previewWindow?.contentView as? PreviewHost {v.updateVisible=updateVisible;v.updateExpanded=updateExpanded;v.needsDisplay=true};app.update()}
 @objc func toggleUpdate(){updateVisible.toggle();refreshHostView();event("toggle-update")}
 @objc func expandUpdate(){updateVisible=true;updateExpanded.toggle();refreshHostView();event("expand-update")}
 @objc func showOcclusion(){closeOcclusion();app.closePanels(restoreEntryFocus:false)
  let f=app.entry.frame;let p=NSPanel(contentRect:NSRect(x:f.minX-5,y:f.minY-10,width:190,height:max(110,f.height+20)),styleMask:[.titled,.closable],backing:.buffered,defer:false)
  p.title="自有原生弹层";p.isReleasedWhenClosed=false
  let v=FlippedView(frame:NSRect(origin:.zero,size:p.contentLayoutRect.size));p.contentView=v
  let b=NSButton(title:"原生弹层命中",target:self,action:#selector(hitPopup));b.bezelStyle = .rounded;b.frame=NSRect(x:8,y:15,width:174,height:45);v.addSubview(b)
  let label=NSTextField(labelWithString:"应遮住入口并优先接收点击");label.font = .systemFont(ofSize:10);label.frame=NSRect(x:8,y:72,width:180,height:20);v.addSubview(label)
  app.previewWindow?.addChildWindow(p,ordered:.above);occludingWindow=p;p.makeKeyAndOrderFront(nil);app.update();event("show-native-popup")
 }
 @objc func hitPopup(){popupHits+=1;event("native-popup-hit")}
 @objc func closeOcclusion(){if let p=occludingWindow{app.previewWindow?.removeChildWindow(p);p.orderOut(nil)};occludingWindow=nil;app.update();event("close-native-popup")}
 @objc func focusEntry(){app.entry.makeKeyAndOrderFront(nil);event("focus-entry")}
 @objc func focusHost(){app.closePanels(restoreEntryFocus:false);app.previewWindow?.makeKeyAndOrderFront(nil);event("focus-host")}
 @objc func refreshQuota(){if app.liveData{app.nextQuota = .distantPast;app.update()};event("request-quota")}
 @objc func reviseNews(){guard app.preview else{return};let version=UUID().uuidString;app.news=NewsSnapshot(items:[NewsItem(id:"test-version",version:version,type:"reset_credit",status:"announced",scope:"测试范围",time:"预告时间：2026-10-05",url:"https://aihot.news/codex-reset",occurred:"",basis:"",explicit:true,inferred:false)],history:nil,checked:"示例数据",day:NewsParser.day(Date()));app.closePanels(restoreEntryFocus:false);app.update();event("new-news-version")}
 @objc func longNews(){guard app.preview else{return};app.news=NewsSnapshot(items:(0..<8).map{NewsItem(id:"long-\($0)",version:"v3",type:"reset_credit",status:"announced",scope:String(repeating:"长适用范围仅用于测试。",count:25),time:"预告时间：2026-10-05",url:"https://aihot.news/codex-reset",occurred:"",basis:"",explicit:true,inferred:false)},history:nil,checked:"示例数据",day:NewsParser.day(Date()));app.closePanels(restoreEntryFocus:false);app.update();event("long-news")}
 @objc func shiftAnchor(){anchorShift=anchorShift==0 ? 16:0;if let view=app.previewWindow?.contentView as? PreviewHost {view.anchorShift=anchorShift;view.needsDisplay=true};app.update();event("internal-anchor-change")}
 @objc func replaceHost(){guard let old=app.previewWindow else{return};let frame=old.frame;closeOcclusion();old.close();app.setupPreview();app.previewWindow?.setFrame(frame,display:true);anchorShift=0;app.update();event("same-frame-window-replacement")}
 @objc func closeHost(){app.previewWindow?.close();app.update();window.makeKeyAndOrderFront(nil);event("close-host")}
}
extension AppDelegate {
 func installHarnessMenu(){let bar=NSMenu();let root=NSMenuItem(title:"验证",action:nil,keyEquivalent:"");let menu=NSMenu();let control=menu.addItem(withTitle:"自有测试控制面板",action:#selector(showHarness),keyEquivalent:"1");control.target=self;let quitItem=menu.addItem(withTitle:"退出额度条",action:#selector(quit),keyEquivalent:"q");quitItem.target=self;root.submenu=menu;bar.addItem(root);NSApp.mainMenu=bar}
 @objc func showHarness(){harness?.show()}
 func recordHarness(_ action:String){guard selfHost else{return};update();let obj:[String:Any] = ["time":ISO8601DateFormatter().string(from:Date()),"action":action,"entry":NSStringFromRect(entry.frame),"entryVisible":entry.isVisible,"host":NSStringFromRect(host?.frame ?? .zero),"offset":preferences.offset,"passthrough":entry.ignoresMouseEvents,"popupHits":harness?.popupHits ?? 0,"hostWindowID":host?.windowNumber ?? 0,"entryCanBecomeKey":entry.canBecomeKey]
  guard var data=try? JSONSerialization.data(withJSONObject:obj,options:.sortedKeys) else{return};data.append(10)
  let url=runtime.appendingPathComponent("harness-events.jsonl");if !FileManager.default.fileExists(atPath:url.path){FileManager.default.createFile(atPath:url.path,contents:nil)}
  if let file=try? FileHandle(forWritingTo:url){defer{try? file.close()};_ = try? file.seekToEnd();try? file.write(contentsOf:data)}
 }
}

#endif
