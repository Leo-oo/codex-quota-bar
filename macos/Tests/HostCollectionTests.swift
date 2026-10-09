import AppKit
@main struct CollectionTests {
 static func main(){var count=0
 func check(_ v:Bool,_ m:String){precondition(v,m);count+=1;print("PASS",m)}
 let host=NSRect(x:0,y:0,width:1000,height:800),rail=NSRect(x:0,y:0,width:64,height:800)
 struct Raw {let id:Int;let parent:Int?;let role:String;let frame:NSRect;var subrole="";var label=""}
 func collect(_ input:[Raw])->([HostNode],Bool){
  var policy=HostCollectionPolicy(),walk=HostTraversal(root:input[0]),nodes:[HostNode]=[]
  while let (r,depth)=walk.next(elapsed:0) {
   let read=policy.readsSemantics(id:r.id,parent:r.parent,role:r.role,bounds:r.frame,host:host)
   let meaning=HostMeaning.flags(read ? [r.label]:[])
   let action=HostCollectionPolicy.actionRoles.contains(r.role)
   policy.recordAction(id:r.id,actionable:action)
   nodes.append(HostNode(id:r.id,parent:r.parent,role:r.role,frame:r.frame,actionable:action,account:meaning.account,navigation:meaning.navigation,update:meaning.update,popup:false,subrole:r.subrole))
   walk.append(input.filter{$0.parent==r.id},at:depth)
  }
  return(nodes,walk.complete)
 }
 func solve(_ raw:[Raw])->HostResolution{let (n,done)=collect(raw);return HostResolver.resolve(nodes:n,frame:host,windowNumber:11,complete:done)}
 let root=Raw(id:0,parent:nil,role:"AXWindow",frame:host)
 let nav=Raw(id:2,parent:1,role:"AXGroup",frame:rail,label:"navigation")
 let button=Raw(id:3,parent:2,role:"AXButton",frame:NSRect(x:16,y:18,width:32,height:32),label:"account")
 for wrapper in [Raw(id:1,parent:0,role:"AXGroup",frame:host,subrole:"AXLandmarkMain"),Raw(id:1,parent:0,role:"AXGroup",frame:NSRect(x:750,y:0,width:200,height:800))] {
  check(solve([root,wrapper,nav,button]).geometry?.avatar==button.frame,"metadata traversal preserves valid descendant under \(wrapper.subrole.isEmpty ? "outlying wrapper":"main landmark")")
 }
 let wrapper=Raw(id:1,parent:0,role:"AXGroup",frame:host)
 let plain=Raw(id:3,parent:2,role:"AXButton",frame:button.frame)
 let label=Raw(id:4,parent:3,role:"AXStaticText",frame:button.frame,label:"account")
 check(solve([root,wrapper,nav,plain,label]).geometry?.avatar==button.frame,"static text semantic label resolves actionable ancestor")
 let prose=Raw(id:4,parent:2,role:"AXStaticText",frame:button.frame,label:"account")
 check(!collect([root,wrapper,nav,prose]).0.contains{$0.account},"unattached static text does not expose semantic attributes")
 let rightButton=Raw(id:3,parent:0,role:"AXButton",frame:NSRect(x:800,y:30,width:32,height:32),label:"account")
 check(!collect([root,rightButton]).0.contains{$0.account},"outlying control semantic attributes not requested")
 check(solve([root,rightButton]).geometry==nil,"right content never becomes sidebar account")
 let unrelated=Raw(id:4,parent:3,role:"AXStaticText",frame:button.frame,label:"Account settings guide")
 check(solve([root,wrapper,nav,plain,unrelated]).geometry==nil,"prose label never broadens exact semantics")
 let noHelp=[60,120,180,750].enumerated().map{Raw(id:$0.offset+2,parent:1,role:"AXButton",frame:NSRect(x:16,y:800-$0.element-32,width:32,height:32))}
 let unlabelledRail=Raw(id:1,parent:0,role:"AXGroup",frame:rail)
 let fixture=[root,unlabelledRail]+noHelp
 check(solve(fixture).geometry?.avatar==noHelp.last!.frame,"exact public no-help geometry resolves after collection")
 for height:CGFloat in [33,66] {
  let placed=Placement.entry(host:solve(fixture).geometry!,size:NSSize(width:48,height:height),offset:0)
  check(placed.map{rect in rail.contains(rect) && noHelp.allSatisfy{!$0.frame.intersects(rect)}} == true,"our product preserves safe entry size \(height)")
 }
 let obstacle=Raw(id:9,parent:1,role:"AXButton",frame:NSRect(x:4,y:90,width:20,height:20))
 check(solve(fixture+[obstacle]).geometry==nil,"exact public off-center obstruction hides no-help fallback")
 check(solve(Array(fixture.dropLast())).geometry==nil,"missing fourth control still rejected")
 let near=Raw(id:9,parent:1,role:"AXButton",frame:NSRect(x:16,y:52,width:32,height:32))
 check(solve(fixture+[near]).geometry==nil,"account gap below four still rejected")
 let crowded=Raw(id:9,parent:1,role:"AXButton",frame:NSRect(x:4,y:123,width:20,height:20))
 check(solve(fixture+[crowded]).geometry==nil,"expanded obstacle margin protects near slot")
 let away=Raw(id:9,parent:1,role:"AXButton",frame:NSRect(x:4,y:200,width:20,height:20))
 check(solve(fixture+[away]).geometry != nil,"off-center control outside safety slot is allowed")
 let large=[root]+(1...805).map{Raw(id:$0,parent:0,role:"AXGroup",frame:host)}
 check(!collect(large).1 && solve(large).geometry==nil,"removing spatial cuts retains hard traversal budget")
 let labelledAccount=Raw(id:10,parent:5,role:"AXStaticText",frame:noHelp.last!.frame,label:"account")
 let labelledFixture=[root,Raw(id:1,parent:0,role:"AXGroup",frame:rail,label:"navigation")]+noHelp+[labelledAccount]
 check(solve(labelledFixture).geometry != nil,"semantic no-help account permits clear slot")
 check(solve(labelledFixture+[obstacle]).geometry==nil,"semantic label cannot bypass off-center no-help blocker")
 let ordinary=Raw(id:11,parent:1,role:"AXButton",frame:NSRect(x:16,y:140,width:32,height:32))
 let semantic=solve(labelledFixture+[ordinary]).geometry!
 check(semantic.nativeControls.contains(ordinary.frame),"semantic route protects ordinary native controls")
 // second top account fails bottom criterion; use a distinct bottom action for ambiguity.
 let secondButton=Raw(id:13,parent:1,role:"AXButton",frame:NSRect(x:16,y:220,width:32,height:32),label:"account")
 check(solve(labelledFixture+[secondButton]).geometry==nil,"multiple valid semantic bottom accounts reject")
 let deep=[root]+(1...70).map{Raw(id:$0,parent:$0-1,role:"AXGroup",frame:host)}
 check(!collect(deep).1 && solve(deep).geometry==nil,"depth-truncated metadata never attaches")
 let inner=Raw(id:14,parent:5,role:"AXButton",frame:noHelp.last!.frame)
 let innerLabel=Raw(id:10,parent:14,role:"AXStaticText",frame:noHelp.last!.frame,label:"account")
 let nestedFixture=labelledFixture.filter{$0.id != 10}+[inner,innerLabel]
 check(solve(nestedFixture).geometry != nil,"nested equivalent account wrappers resolve")
 check(solve(nestedFixture+[obstacle]).geometry==nil,"duplicate account rectangles cannot bypass obstruction")
 print("\(count) collection/no-help checks passed; synthetic metadata only")
 }
}
