import AppKit

// In-memory semantic model. Never serialized: no UI text, names or identifiers retained.
struct HostNode {
 let id:Int
 let parent:Int?
 let role:String
 let frame:NSRect?
 let actionable:Bool
 let account:Bool
 let navigation:Bool
 let update:Bool
 let popup:Bool
 var subrole:String = ""
}
enum HostMeaning {
 static func flags(_ strings:[String])->(account:Bool,navigation:Bool,update:Bool) {
  let values=strings.map{$0.lowercased().replacingOccurrences(of:"-",with:" ").replacingOccurrences(of:"_",with:" ").trimmingCharacters(in:.whitespacesAndNewlines)}
  let accounts=["account","account menu","open account menu","user menu","open user menu","profile","profile menu","user profile","账户","账号","账户菜单","账号菜单","个人资料","个人菜单"]
  let nav=["navigation","primary navigation","main navigation","app navigation","导航","主导航","应用导航"]
  let updates=["update","check for updates","update available","更新","检查更新","有可用更新"]
  return (values.contains{accounts.contains($0)},values.contains{nav.contains($0)},values.contains{updates.contains($0)})
 }
}
enum HostResolutionKind {case resolved,unmatched,temporarilyUnavailable,hidden}
struct HostResolution {
 let geometry:HostGeometry?
 let reason:String
 let kind:HostResolutionKind
 let observedWindowNumber:Int?
 init(geometry:HostGeometry?,reason:String,kind:HostResolutionKind?=nil,observedWindowNumber:Int?=nil){
  self.geometry=geometry;self.reason=reason;self.kind=kind ?? (geometry == nil ? .unmatched:.resolved);self.observedWindowNumber=observedWindowNumber ?? geometry?.windowNumber
 }
}
enum HostResolver {
 static func resolve(nodes:[HostNode],frame:NSRect,windowNumber:Int,complete:Bool,extraOcclusions:[NSRect]=[])->HostResolution {
  guard complete else{return HostResolution(geometry:nil,reason:"宿主结构尚未完整读取，暂不附着")}
  guard Set(nodes.map(\.id)).count==nodes.count else{return HostResolution(geometry:nil,reason:"宿主节点身份重复，暂不附着")}
  let index=Dictionary(uniqueKeysWithValues:nodes.map{($0.id,$0)})
  for node in nodes {
   var next=node.parent,seen:Set<Int>=[node.id]
   while let id=next {
    guard let parent=index[id],seen.insert(id).inserted else{return HostResolution(geometry:nil,reason:"宿主树关系不完整，暂不附着")}
    next=parent.parent
   }
  }
  func ancestors(_ node:HostNode)->[HostNode] {
   var result=[node],next=node.parent,seen:Set<Int>=[node.id]
   while let id=next,let p=index[id],seen.insert(id).inserted {result.append(p);next=p.parent}
   return result
  }
  func railShape(_ r:NSRect)->Bool {
   r.width>0 && r.width<=min(240,frame.width*0.28) && r.height>=frame.height*0.45 &&
   abs(r.minX-frame.minX)<=max(24,frame.width*0.02) && frame.insetBy(dx:-1,dy:-1).contains(r)
  }
  var pairs:[Int:(HostNode,NSRect)]=[:]
  for label in nodes where label.account {
   let chain=ancestors(label)
   // The semantic label may belong to an image/text child of the actual trigger.
   guard let trigger=chain.first(where:{$0.actionable && $0.frame != nil}),let r=trigger.frame,r.width>0,r.height>0,frame.contains(r) else{continue}
   let rails=ancestors(trigger).dropFirst().filter {n in
    guard let bounds=n.frame,railShape(bounds) else{return false}
    if n.navigation {return true}
    // Structural fallback still requires a semantically identified account trigger.
    let actions=nodes.filter{$0.actionable && $0.id != trigger.id && ancestors($0).contains(where:{$0.id==n.id})}
    return actions.count>=3
   }
   guard let rail=rails.min(by:{$0.frame!.width<$1.frame!.width}),let railFrame=rail.frame,
    r.midY<railFrame.minY+railFrame.height*0.4,railFrame.contains(r) else{continue}
   pairs[trigger.id]=(trigger,railFrame)
  }
  if pairs.isEmpty {return HostStructure.resolve(nodes:nodes,frame:frame,windowNumber:windowNumber,extraOcclusions:extraOcclusions)}
  guard pairs.count==1,let (trigger,rail)=pairs.values.first,let anchor=trigger.frame else {
   return HostResolution(geometry:nil,reason:pairs.isEmpty ? "当前版本尚未匹配此 Codex 的账户入口与导航结构":"发现多个账户入口，暂不附着")
  }
  let railActions=nodes.filter{$0.actionable && $0.id != trigger.id && !ancestors($0).contains(where:{$0.id==trigger.id}) && ancestors($0).contains(where:{$0.frame==rail})}
  guard railActions.allSatisfy({node in guard let r=node.frame else{return false};return r.minX.isFinite && r.minY.isFinite && r.width.isFinite && r.height.isFinite && r.width>0 && r.height>0 && frame.contains(r)}) else{return HostResolution(geometry:nil,reason:"原生控件边界尚未确认，暂不附着")}
  let controls=railActions.compactMap(\.frame)
  guard HostStructure.noHelpSlotIsClear(rail:rail,account:anchor,controls:controls+[anchor]) else{return HostResolution(geometry:nil,reason:"账户上方原生控件占用，暂不附着")}
  let updates=nodes.filter{$0.update && $0.frame.map{rail.intersects($0)} == true}.compactMap(\.frame)+controls
  let popups=nodes.filter(\.popup).compactMap(\.frame)+extraOcclusions
  return HostResolution(geometry:HostGeometry(frame:frame,avatar:anchor,railRight:rail.maxX,nativeControls:updates,windowNumber:windowNumber,occlusions:popups),reason:"已定位账户控件与导航栏")
 }
}
