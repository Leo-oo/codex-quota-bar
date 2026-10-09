import Foundation
import AppKit
import ApplicationServices
@main struct Probe {
 static func main() {
  let runtime = URL(fileURLWithPath:CommandLine.arguments[1])
  let apps = NSWorkspace.shared.runningApplications.filter { $0.bundleIdentifier == "com.openai.codex" }
  var report: [String:Any] = ["architecture":"arm64", "hostApplications":apps.compactMap{$0.bundleURL?.path}, "accessibilityTrusted":AXIsProcessTrusted(), "screenCapturePreflight":CGPreflightScreenCaptureAccess(), "uiAutomation":"blocked: Computer Use denies com.openai.codex; no bypass attempted"]
  do {
   let s = try QuotaClient(runtime:runtime).read()
   report["quota"] = try JSONSerialization.jsonObject(with: JSONEncoder().encode(s))
   report["quotaStatus"] = "success"
  } catch { report["quotaStatus"] = "failed"; report["quotaError"] = error.localizedDescription }
  let data = try! JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys])
  print(String(data:data,encoding:.utf8)!)
 }
}
