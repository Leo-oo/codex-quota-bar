#if SELF_HOST_ONLY
import AppKit
extension AppDelegate {
 func recordFollow(_ label:String){
  guard followingTrace,entry != nil,let runtime else{return}
  let ids=NSWindow.windowNumbers(options:[])?.map(\.intValue) ?? []
  let j:[String:Any]=["marker":label,"t":ProcessInfo.processInfo.systemUptime,"mode":CFRunLoopCopyCurrentMode(CFRunLoopGetCurrent()).map{String(describing:$0)} ?? "none","active":NSApp.isActive,"heartbeat":uiHeartbeat,"ids":ids,"entry":entry.windowNumber,"host":host?.windowNumber ?? 0,"key":NSApp.keyWindow?.windowNumber ?? 0,"main":NSApp.mainWindow?.windowNumber ?? 0,"entryVisible":entry.isVisible,"occluded":!entry.occlusionState.contains(.visible),"blocked":entry.ignoresMouseEvents,"orderRevision":orderRevision,"stackBelow":host?.stack?.entryBelowHost ?? false,"sampledAt":host?.stack?.sampledAt ?? 0]
  let url=runtime.appendingPathComponent("follow-trace.jsonl")
  if !FileManager.default.fileExists(atPath:url.path){FileManager.default.createFile(atPath:url.path,contents:nil)}
  if let bytes=try? JSONSerialization.data(withJSONObject:j,options:.sortedKeys),let f=try? FileHandle(forWritingTo:url){f.seekToEndOfFile();f.write(bytes);f.write(Data([10]));try? f.close()}
 }
}
#endif
