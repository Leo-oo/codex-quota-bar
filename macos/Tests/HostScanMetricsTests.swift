import Foundation
@main struct ScanTests {
 static func main() {
  let m=HostScanMetrics()
  m.stop="time-limit";m.visited=23;m.pending=80
  precondition(m.failureSummary.contains("提前结束"))
  m.stop="exhausted";m.pending=0;m.candidates=2
  precondition(m.failureSummary.contains("不唯一"))
  m.candidates=0;m.depthLimited=1
  precondition(m.failureSummary.contains("深度限制"))
  m.depthLimited=0
  precondition(m.failureSummary.contains("未找到按钮"))
  m.buttons=4
  precondition(m.failureSummary.contains("无可用边界"))
  m.buttonRects=4;m.squareButtons=2;m.containedSquares=2
  precondition(m.failureSummary.contains("几何筛选为0"))
  precondition(JSONSerialization.isValidJSONObject(m.record))
  print("7 scan diagnostics checks passed; no host access")
 }
}
