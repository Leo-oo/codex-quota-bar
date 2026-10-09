import AppKit

// Structural thresholds adapted from MMMuFF/CodexQuota v0.8.7, commit
// 9acb12fd8d94f1345fa8b4802f0b74406e759afe (MIT; THIRD-PARTY-NOTICES.txt).
// No-help fallback: upstream PR12 commit 8b64e966bf1f60829bea33b21013df4124e1eb14 (MIT).
// Pure AppKit-coordinate contract. It identifies a layout slot, not an account identity.
// No target UI data is collected by tests. Incomplete input never reaches this resolver.
enum HostStructure {
 // Shared by semantic and structural recognition: labels cannot bypass native controls.
 static func noHelpSlotIsClear(rail:NSRect,account:NSRect,controls:[NSRect])->Bool {
  var distinct:[NSRect]=[]
  for r in controls where !distinct.contains(r) {distinct.append(r)}
  let aligned=distinct.filter{rail.contains($0) && (20...56).contains($0.width) && (20...56).contains($0.height) && abs($0.midX-rail.midX)<=8}.sorted{$0.minY<$1.minY}
  guard (48...88).contains(rail.width),aligned.count>=4,aligned.first==account,aligned[1].minY-account.maxY>72 else{return true}
  let slot=NSRect(x:rail.minX+4,y:account.maxY+8,width:rail.width-8,height:64)
  return rail.contains(slot) && !distinct.contains{$0.insetBy(dx:-4,dy:-8).intersects(slot)}
 }

 static func resolve(nodes:[HostNode],frame:NSRect,windowNumber:Int,extraOcclusions:[NSRect])->HostResolution {
  let index=Dictionary(uniqueKeysWithValues:nodes.map{($0.id,$0)})
  func below(_ node:HostNode,_ root:Int)->Bool {
   var next=node.parent,seen:Set<Int>=[node.id]
   while let id=next,seen.insert(id).inserted {if id==root{return true};next=index[id]?.parent}
   return false
  }
  func valid(_ r:NSRect)->Bool {r.origin.x.isFinite && r.origin.y.isFinite && r.width.isFinite && r.height.isFinite && r.width>0 && r.height>0 && frame.contains(r)}
  let roles:Set<String>=["AXButton","AXPopUpButton","AXLink","AXMenuButton"]
  func controls(_ root:HostNode)->[HostNode]? {
   let children=nodes.filter{below($0,root.id) && (roles.contains($0.role) || $0.actionable)}
   guard children.allSatisfy({$0.frame.map(valid) == true}) else{return nil}
   // Nested interactive duplicates must have identical bounds; conflicting hit areas are ambiguous.
   var result:[HostNode]=[]
   for n in children {
    if let prior=result.first(where:{$0.frame==n.frame}) {
     guard below(n,prior.id) || below(prior,n.id) else{return nil}
    } else {result.append(n)}
   }
   return result
  }
  struct Match {let anchor:NSRect;let rail:NSRect;let controls:[NSRect];let branch:String}
  var matches:[Match]=[]
  for container in nodes where ["AXGroup","AXToolbar","AXTabGroup","AXSplitGroup"].contains(container.role) {
   guard let rail=container.frame,valid(rail),abs(rail.minX-frame.minX)<=8,rail.height>=frame.height*0.75,
    let actions=controls(container) else{continue}
   if (48...88).contains(rail.width),abs(rail.minY-frame.minY)<=8 {
    let aligned=actions.filter {n in guard let r=n.frame else{return false};return rail.contains(r) && (20...56).contains(r.width) && (20...56).contains(r.height) && abs(r.midX-rail.midX)<=8}.sorted{$0.frame!.minY<$1.frame!.minY}
    guard aligned.count>=4,let account=aligned.first?.frame else{continue}
    let preceding=aligned[1].frame!,gap=preceding.minY-account.maxY
    guard account.minY-rail.minY<=28,gap>=4 else{continue}
    guard noHelpSlotIsClear(rail:rail,account:account,controls:actions.compactMap(\.frame)) else{continue}
    matches.append(Match(anchor:account,rail:rail,controls:actions.compactMap(\.frame).filter{$0 != account},branch:gap>72 ? "rail-pr12-no-help":"rail-v0.8.7"))
   } else if container.subrole=="AXLandmarkComplementary",(180...700).contains(rail.width) {
    // Require a separate main landmark, unique bottom popup, and the full trailing-button row.
    guard nodes.contains(where:{$0.subrole=="AXLandmarkMain" && $0.frame.map{r in valid(r) && r.height>=frame.height*0.75 && r.minX>=rail.maxX-8} == true}) else{continue}
    for account in actions where account.role=="AXPopUpButton" {
     let a=account.frame!,bottom=a.minY-rail.minY
     guard rail.contains(a),(24...56).contains(a.height),(0...24).contains(bottom) else{continue}
     let trailing=actions.filter {n in
      guard n.id != account.id,["AXButton","AXPopUpButton"].contains(n.role),let t=n.frame else{return false}
      return (20...min(72,max(48,rail.width*0.2))).contains(t.width) && (20...min(72,max(50,a.height*1.5))).contains(t.height) && t.minX>=a.maxX+4 && abs(t.midY-a.midY)<=6 && (0...20).contains(rail.maxX-t.maxX)
     }
     guard trailing.count==1,let end=trailing[0].frame else{continue}
     let row=actions.compactMap(\.frame).filter{$0.minX>=a.maxX+4 && $0.maxX<=end.minX-4 && abs($0.midY-a.midY)<=6 && $0.width>=20 && $0.height>=20}
     guard a.width+end.minX-(row.map(\.minX).min() ?? end.minX)>=rail.width*0.55 else{continue}
     // Our product sits above the avatar: a wide account button is NOT an avatar.
     let images=nodes.filter{$0.role=="AXImage" && below($0,account.id)}.compactMap(\.frame).filter{r in valid(r) && a.contains(r) && (16...48).contains(r.width) && (0.85...1.15).contains(r.height/r.width) && r.midX<=a.minX+56}
     guard Set(images.map{NSStringFromRect($0)}).count==1,let avatar=images.first else{continue}
     matches.append(Match(anchor:avatar,rail:rail,controls:actions.filter{!below($0,account.id) && $0.id != account.id}.compactMap(\.frame),branch:"legacy-footer-v0.8.7"))
    }
   }
  }
  // Equivalent wrappers are harmless; conflicting container geometry or anchors are not.
  var unique:[Match]=[]
  for m in matches {if !unique.contains(where:{$0.anchor==m.anchor && $0.rail==m.rail && Set($0.controls.map{NSStringFromRect($0)})==Set(m.controls.map{NSStringFromRect($0)})}){unique.append(m)}}
  guard unique.count==1,let match=unique.first else{return HostResolution(geometry:nil,reason:unique.isEmpty ? "当前版本尚未匹配此 Codex 的账户入口与导航结构":"侧栏结构存在多个候选，暂不附着")}
  return HostResolution(geometry:HostGeometry(frame:frame,avatar:match.anchor,railRight:match.rail.maxX,nativeControls:match.controls,windowNumber:windowNumber,occlusions:nodes.filter(\.popup).compactMap(\.frame)+extraOcclusions,structuralRail:match.rail),reason:"已匹配结构契约 "+match.branch+"（实际兼容待验证）")
 }
}
