import AppKit

// Metadata traversal is bounded by HostTraversal, never pruned on a parent's
// frame/subrole: neither guarantees that all descendants are inside that frame.
// Only these semantic attributes are used by the adapter: identifier/title/description.
// AXValue, text ranges and conversation text are never requested.
struct HostCollectionPolicy {
 static let actionRoles:Set<String>=["AXButton","AXPopUpButton","AXMenuButton","AXLink"]
 private static let semanticRoles:Set<String>=["AXButton","AXPopUpButton","AXMenuButton","AXLink","AXGroup","AXImage","AXToolbar","AXTabGroup"]
 private var actionLineage:Set<Int>=[]
 mutating func readsSemantics(id:Int,parent:Int?,role:String,bounds:NSRect?,host:NSRect)->Bool {
  let inAction=parent.map{actionLineage.contains($0)} ?? false
  if inAction || Self.actionRoles.contains(role) {actionLineage.insert(id)}
  guard let r=bounds,r.width>0,r.height>0,host.intersects(r),r.minX<host.minX+min(700,host.width) else{return false}
  // Text children can label an actionable ancestor; arbitrary document labels cannot.
  return Self.semanticRoles.contains(role) || (role=="AXStaticText" && inAction)
 }
 mutating func recordAction(id:Int,actionable:Bool){if actionable{actionLineage.insert(id)}}
}
