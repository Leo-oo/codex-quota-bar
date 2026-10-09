import Foundation
@main struct CLIDiscoveryTests {
 static func main() {
  var checked=[String]();let fake:(String)->Bool={checked.append($0);return $0=="/fake/bin/codex"}
  precondition(QuotaClient.locate(environment:["PATH":"relative:/fake/bin"],home:"/synthetic",executable:fake)=="/fake/bin/codex")
  precondition(!checked.contains("relative/codex"))
  precondition(QuotaClient.locate(environment:["CODEX_USAGE_MINI_CODEX_PATH":"/override"],home:"/synthetic",executable:{$0=="/override"})=="/override")
  precondition(QuotaClient.locate(environment:[:],home:"/synthetic",executable:{$0=="/synthetic/Applications/Codex.app/Contents/Resources/codex"}) != nil)
  precondition(QuotaClient.locate(environment:[:],home:"/synthetic",executable:{_ in false})==nil)
  precondition(QuotaClient.locate(environment:[:],home:"/synthetic",executable:{$0=="/usr/local/bin/codex"}) != nil)
  print("6 CLI discovery checks passed; fake executable predicate, no real app/files inspected")
 }
}
