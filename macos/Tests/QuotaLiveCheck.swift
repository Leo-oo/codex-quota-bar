import Foundation
@main struct LiveCheck {
 static func main(){
  var report:[String:Any]=["checkedAt":ISO8601DateFormatter().string(from:Date()),"methods":["account/read","account/rateLimits/read"],"identityPersisted":false]
  do {let snapshot=try QuotaClient(runtime:URL(fileURLWithPath:CommandLine.arguments[1])).read();report["success"]=true;report["windows"]=snapshot.windows.map{["label":$0.label,"remainingPercent":$0.remaining] as [String:Any]}}
  catch {report["success"]=false;report["error"]=error.localizedDescription}
  let data=try! JSONSerialization.data(withJSONObject:report,options:[.prettyPrinted,.sortedKeys]);print(String(decoding:data,as:UTF8.self))
 }
}
