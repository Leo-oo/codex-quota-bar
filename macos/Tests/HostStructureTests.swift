import AppKit
@main struct StructureTests {
 static func main(){var count=0
  func check(_ v:Bool,_ m:String){precondition(v,m);count+=1;print("PASS",m)}
  let f=NSRect(x:100,y:200,width:1000,height:700)
  func n(_ id:Int,_ parent:Int?,_ role:String,_ r:NSRect?,_ sub:String="")->HostNode{HostNode(id:id,parent:parent,role:role,frame:r,actionable:["AXButton","AXPopUpButton","AXLink"].contains(role),account:false,navigation:false,update:false,popup:role=="AXMenu",subrole:sub)}
  let rail=NSRect(x:100,y:200,width:64,height:700)
  let base=[n(0,nil,"AXGroup",rail),n(1,0,"AXButton",NSRect(x:116,y:210,width:32,height:32)),n(2,0,"AXButton",NSRect(x:116,y:270,width:32,height:32)),n(3,0,"AXLink",NSRect(x:116,y:600,width:32,height:32)),n(4,0,"AXButton",NSRect(x:116,y:660,width:32,height:32))]
  func solve(_ nodes:[HostNode],complete:Bool=true)->HostResolution{HostResolver.resolve(nodes:nodes,frame:f,windowNumber:17,complete:complete)}
  check(solve(base).geometry?.avatar==base[1].frame,"unlabelled complete rail contract resolves")
  check(solve(base,complete:false).geometry==nil,"truncated rail rejected")
  check(solve(Array(base.dropLast())).geometry==nil,"three controls rejected")
  for width:CGFloat in [47,89] {check(solve([n(0,nil,"AXGroup",NSRect(x:100,y:200,width:width,height:700))]+base.dropFirst()).geometry==nil,"rail width boundary rejected \(width)")}
  check(solve([n(0,nil,"AXGroup",rail.offsetBy(dx:9,dy:0))]+base.dropFirst()).geometry==nil,"detached sidebar rejected")
  check(solve([n(0,nil,"AXGroup",NSRect(x:100,y:200,width:64,height:500))]+base.dropFirst()).geometry==nil,"short rail rejected")
  check(solve(base+[n(8,0,"AXButton",nil)]).geometry==nil,"unknown control bounds cannot prove safe slot")
  check(solve(base+[n(8,0,"AXButton",base[1].frame)]).geometry==nil,"unrelated duplicate hit rectangles rejected")
  check(solve(base+[n(8,1,"AXButton",base[1].frame)]).geometry != nil,"nested equivalent trigger deduplicated")
  check(solve(base+[base[1]]).geometry==nil,"duplicate identities rejected without crash")
  let host=solve(base).geometry!
  check(host.nativeControls.count==3,"all other native controls protected without update labels")
  let p=Placement.entry(host:host,size:NSSize(width:48,height:66),offset:0)!
  check(host.nativeControls.allSatisfy{!$0.intersects(p)} && rail.contains(p),"dual-window entry fits safe rail slot")
  check(Placement.entry(host:host,size:NSSize(width:90,height:66),offset:0)==nil,"oversized entry cannot spill outside rail")
  let packed=base+[n(8,0,"AXButton",NSRect(x:105,y:320,width:54,height:550))]
  check(solve(packed).geometry.map{Placement.entry(host:$0,size:NSSize(width:48,height:66),offset:0)==nil} == true,"no free slot hides entry")
  let popup=NSRect(x:100,y:250,width:200,height:300)
  check(solve(base+[n(9,nil,"AXMenu",popup)]).geometry?.occlusions.contains(popup)==true,"popup retained for host priority and recovery")
  let side=NSRect(x:100,y:200,width:280,height:700)
  let legacy=[n(0,nil,"AXGroup",side,"AXLandmarkComplementary"),n(1,0,"AXPopUpButton",NSRect(x:108,y:210,width:190,height:40)),n(2,1,"AXImage",NSRect(x:112,y:214,width:32,height:32)),n(3,0,"AXButton",NSRect(x:336,y:214,width:32,height:32)),n(4,nil,"AXGroup",NSRect(x:380,y:200,width:720,height:700),"AXLandmarkMain")]
  check(solve(legacy).geometry?.avatar==legacy[2].frame,"legacy footer uses avatar image bounds, never wide popup center")
  check(solve(legacy.filter{$0.id != 2}).geometry==nil,"legacy needs avatar geometry")
  check(solve(legacy.filter{$0.id != 4}).geometry==nil,"legacy needs main landmark separation")
  check(solve([n(0,nil,"AXGroup",side)]+legacy.dropFirst()).geometry==nil,"ordinary wide group cannot become legacy sidebar")
  check(solve(legacy+[n(6,0,"AXPopUpButton",NSRect(x:109,y:208,width:188,height:40)),n(7,6,"AXImage",NSRect(x:113,y:212,width:32,height:32))]).geometry==nil,"ambiguous legacy footer rejected")
  check(solve(legacy+[n(8,1,"AXImage",NSRect(x:140,y:214,width:32,height:32))]).geometry==nil,"multiple avatar images rejected")
  let hidden=[n(0,nil,"AXGroup",NSRect(x:100,y:200,width:1000,height:700),"AXLandmarkMain")]
  check(solve(hidden).geometry==nil,"full main content without sidebar stays hidden")
  check(solve(base+[n(10,999,"AXButton",NSRect(x:116,y:500,width:32,height:32))]).geometry==nil,"missing ancestor rejects tree")
  check(solve(base+[n(10,11,"AXGroup",nil),n(11,10,"AXGroup",nil)]).geometry==nil,"cyclic parent links reject tree")
  let tooLow=[base[0],n(1,0,"AXButton",NSRect(x:116,y:240,width:32,height:32))]+Array(base.dropFirst(2))
  check(solve(tooLow).geometry==nil,"account bottom inset beyond 28 rejects")
  let farHelp=[base[0],base[1],n(2,0,"AXButton",NSRect(x:116,y:320,width:32,height:32))]+Array(base.dropFirst(3))
  check(solve(farHelp).geometry==nil,"no-help fallback still rejects occupied account-adjacent slot")
  let translated=base.map{n($0.id,$0.parent,$0.role,$0.frame?.offsetBy(dx:-900,dy:400))}
  check(HostResolver.resolve(nodes:translated,frame:f.offsetBy(dx:-900,dy:400),windowNumber:19,complete:true).geometry?.avatar==base[1].frame?.offsetBy(dx:-900,dy:400),"negative-screen coordinates translate contract")
  check(Placement.entry(host:host,size:NSSize(width:48,height:33),offset:32).map{entry in rail.contains(entry) && host.nativeControls.allSatisfy{!$0.intersects(entry)}} == true,"single-window moved entry protects all controls")
  print("\(count) structural checks passed; synthetic contracts, no real Codex inspection")
 }
}
