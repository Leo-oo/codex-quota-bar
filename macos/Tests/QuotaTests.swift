import Foundation
@main struct Tests {
 static func main() {
  var passed = 0
  func check(_ condition: Bool, _ name: String) { if !condition { fatalError(name) }; passed += 1; print("PASS \(name)") }
  func parse(_ bucket: [String:Any]) -> [QuotaWindow] { QuotaSnapshot.parse(["rateLimitsByLimitId":["codex":bucket]]).windows }
  let week: [String:Any] = ["usedPercent":77,"windowDurationMins":10080,"resetsAt":1791200000]
  let five: [String:Any] = ["usedPercent":0,"windowDurationMins":300]
  let one = parse(["secondary":week]); check(one.count == 1 && one[0].label+one[0].percent == "周23%", "single real window; remaining calculation; compact text")
  let two = parse(["primary":week,"secondary":five]); check(two.count == 2 && two[0].label == "5h" && two[0].remaining == 100,"sort actual durations; 100%")
  check(parse(["primary":["usedPercent":true]]).isEmpty,"reject boolean quota")
  check(parse(["primary":["windowDurationMins":300]]).isEmpty,"missing value is unavailable")
  check(parse(["primary":["usedPercent":150]])[0].remaining == 0,"clamp exhausted")
  check(parse(["primary":["usedPercent":-2]])[0].remaining == 100,"clamp over 100")
  check(QuotaSnapshot.parse(["rateLimits":["limitId":"other","primary":week]]).windows.isEmpty,"exclude other bucket")
  check(QuotaSnapshot.parse(["rateLimits":["primary":week]]).windows.count == 1,"legacy codex fallback")
  check(QuotaSnapshot.parse(["rateLimitsByLimitId":["codex":["primary":five]],"rateLimits":["primary":week]]).windows[0].label == "5h","prefer codex bucket")
  check(parse(["primary":["usedPercent":50,"resetsAt":1]])[0].remaining == 50,"expired reset never fabricates replenishment")
  print("\(passed) parser checks passed; fixtures only, not GUI or account acceptance")
 }
}
