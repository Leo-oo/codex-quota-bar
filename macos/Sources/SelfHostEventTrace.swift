#if SELF_HOST_ONLY
import AppKit
// Debugging is strictly scoped to events already delivered to this test application.
final class SelfHostEventTrace {
 weak var app:AppDelegate?
 var monitor:Any?
 private var written=0
 init(_ app:AppDelegate){
  self.app=app
  monitor=NSEvent.addLocalMonitorForEvents(matching:[.leftMouseDown,.leftMouseUp,.rightMouseDown,.rightMouseUp,.keyDown]){[weak self] event in
   self?.record(event);return event
  }
 }
 deinit {if let monitor{NSEvent.removeMonitor(monitor)}}
 func record(_ event:NSEvent){
  guard let app,let window=event.window,NSApp.windows.contains(where:{$0===window}) else{return}
  let role=window===app.entry ? "entry":window===app.mainPanel ? "main":window===app.subPanel ? "sub":window===app.previewWindow ? "host":window===app.harness?.window ? "controls":"own-other"
  let location=window.convertPoint(toScreen:event.locationInWindow)
  let obj:[String:Any] = ["time":ProcessInfo.processInfo.systemUptime,"type":event.type.rawValue,"windowRole":role,"eventLocation":NSStringFromPoint(event.locationInWindow),"screenLocation":NSStringFromPoint(location),"pointer":NSStringFromPoint(NSEvent.mouseLocation),"windowFrame":NSStringFromRect(window.frame),"entryFrame":NSStringFromRect(app.entry.frame),"entryVisible":app.entry.isVisible,"entryOccluded":!app.entry.occlusionState.contains(.visible),"entryIgnores":app.entry.ignoresMouseEvents,"surface":app.surface,"keyWindowRole":NSApp.keyWindow===app.entry ? "entry":NSApp.keyWindow===app.mainPanel ? "main":NSApp.keyWindow===app.subPanel ? "sub":"other","windowOrder":NSApp.orderedWindows.map{$0===app.entry ? "entry":$0===app.mainPanel ? "main":$0===app.subPanel ? "sub":$0===app.previewWindow ? "host":"other"}]
  write(obj)
 }
 func marker(_ name:String){
  guard let app else{return}
  write(["time":ProcessInfo.processInfo.systemUptime,"marker":name,"surface":app.surface,"entryVisible":app.entry.isVisible,"entryIgnores":app.entry.ignoresMouseEvents,"keyWindowRole":NSApp.keyWindow===app.mainPanel ? "main":NSApp.keyWindow===app.subPanel ? "sub":NSApp.keyWindow===app.previewWindow ? "host":"other"])
 }
 private func write(_ object:[String:Any]){
  guard let app,written<2000 else{return};written+=1
  guard var data=try? JSONSerialization.data(withJSONObject:object,options:.sortedKeys) else{return};data.append(10)
  let url=app.runtime.appendingPathComponent("owned-input-trace.jsonl")
  if !FileManager.default.fileExists(atPath:url.path){FileManager.default.createFile(atPath:url.path,contents:nil)}
  if let file=try? FileHandle(forWritingTo:url){defer{try? file.close()};_ = try? file.seekToEnd();try? file.write(contentsOf:data)}
 }
}
#endif
