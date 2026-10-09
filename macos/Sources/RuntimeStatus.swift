import AppKit
extension AppDelegate {
 // State produced by this application only; no AX trees, account identifiers, or private host content.
 func writeRuntimeStatus(){
  guard !selfHost || diagnosticMode else{return}
  let record:[String:Any] = [
   "schema":1,"recordedAt":ISO8601DateFormatter().string(from:Date()),
   "pid":ProcessInfo.processInfo.processIdentifier,
   "executable":Bundle.main.executablePath ?? "unknown",
   "version":Bundle.main.object(forInfoDictionaryKey:"CFBundleShortVersionString") as? String ?? "unknown",
   "buildMarker":"startup-ui-diagnostic-20261005",
   "startupStage":startupStage,"mainHeartbeat":uiHeartbeat,
   "statusUnderNotch":StartupDiagnosticWindow.underNotch(self),
   "statusItemExists":status != nil,"statusItemVisible":status?.isVisible ?? false,
   "statusButtonExists":status?.button != nil,"statusImageValid":status?.button?.image?.isValid ?? false,
   "ownStatusFrame":status?.button?.window.map{NSStringFromRect($0.frame)} ?? "none",
   "hostScan":tracker.scan.record,
   "stage":tracker.status,"hostResolved":host != nil,
   "entryVisible":entry?.isVisible ?? false,"userHidden":hidden,
   "placementAccepted":host.flatMap{Placement.entry(host:$0,size:entry.frame.size,offset:preferences.offset)} != nil,
   "quotaErrorPresent":quotaError != nil,"quotaReads":quotaReads
  ]
  if let data=try? JSONSerialization.data(withJSONObject:record,options:[.prettyPrinted,.sortedKeys]) {try? data.write(to:runtime.appendingPathComponent("runtime-status.json"),options:.atomic)}
 }
}
