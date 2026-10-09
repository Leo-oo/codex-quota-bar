import Foundation
// Numeric diagnostics only: no labels, identifiers, coordinates or AX element contents.
final class HostScanMetrics {
 var active=false
 var deepest=0
 var visited=0,pending=0,buttons=0,buttonRects=0,squareButtons=0,containedSquares=0,candidates=0,depthLimited=0,attributeFailures=0,lastAXError=0,elapsedMS=0
 var stop="not-started"
 var failureSummary:String {
  if stop=="time-limit" || stop=="node-limit" {return "头像扫描提前结束：\(stop)，已查\(visited)，待查\(pending)，候选\(candidates)"}
  if candidates>1 {return "头像候选不唯一：\(candidates)个，已查\(visited)"}
  if depthLimited>0 {return "头像候选为0；有\(depthLimited)个节点达到深度限制"}
  if buttons==0 {return "未找到按钮节点：已查\(visited)，属性失败\(attributeFailures)"}
  if buttonRects==0 {return "按钮存在但无可用边界：\(buttons)个按钮"}
  return "头像几何筛选为0：按钮\(buttons)，方形\(squareButtons)，窗内\(containedSquares)"
 }
 var record:[String:Any] {["depthLimit":HostTraversal<Int>.depthLimit,"deepest":deepest,"stop":stop,"visited":visited,"pending":pending,"buttons":buttons,"buttonRects":buttonRects,"squareButtons":squareButtons,"containedSquares":containedSquares,"candidates":candidates,"depthLimited":depthLimited,"attributeFailures":attributeFailures,"lastAXError":lastAXError,"elapsedMS":elapsedMS]}
}
