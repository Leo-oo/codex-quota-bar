import AppKit
@main struct ResolverTests {
 static func main(){var count=0;func check(_ value:Bool,_ message:String){precondition(value,message);count+=1;print("PASS",message)}
  let frame=NSRect(x:100,y:200,width:900,height:650),rail=NSRect(x:100,y:200,width:76,height:650),anchor=NSRect(x:111,y:226,width:52,height:36)
  func node(_ id:Int,_ parent:Int?,_ r:NSRect?,action:Bool=false,account:Bool=false,nav:Bool=false,popup:Bool=false,update:Bool=false,role:String="AXGroup")->HostNode {HostNode(id:id,parent:parent,role:role,frame:r,actionable:action,account:account,navigation:nav,update:update,popup:popup)}
  let base=[node(0,nil,rail,nav:true),node(1,0,anchor,action:true,account:true,role:"AXPopUpButton")]
  func solve(_ nodes:[HostNode],_ f:NSRect?=nil,_ complete:Bool=true)->HostResolution {HostResolver.resolve(nodes:nodes,frame:f ?? frame,windowNumber:9,complete:complete)}
  let result=solve(base).geometry!
  check(result.avatar==anchor,"non-square actionable account uses actual bounds")
  check(result.railRight==176,"rail right uses container, not twice account center")
  check(solve(base,nil,false).geometry==nil,"partial reads never attach")
  check(solve([base[0],node(1,0,anchor,action:true)]).geometry==nil,"geometry alone cannot identify an account")
  check(solve([node(0,nil,frame,nav:true),base[1]]).geometry==nil,"wide document container is not a navigation rail")
  check(solve(base+[node(2,0,anchor.offsetBy(dx:0,dy:50),action:true,account:true)]).geometry==nil,"two semantic account triggers remain ambiguous")
  let nested=[base[0],node(1,0,anchor,action:true),node(2,1,anchor.insetBy(dx:10,dy:8),account:true,role:"AXImage")]
  check(solve(nested).geometry?.avatar==anchor,"semantic image resolves actionable ancestor")
  check(solve(nested+[node(3,1,anchor.insetBy(dx:8,dy:4),account:true)]).geometry != nil,"children deduplicate by trigger identity")
  check(solve(base+[node(9,nil,NSRect(x:400,y:300,width:30,height:30),action:true,account:true)]).geometry?.avatar==anchor,"account-labelled chat content outside rail cannot attach")
  let popup=NSRect(x:160,y:220,width:250,height:220)
  check(solve(base+[node(4,nil,popup,popup:true)]).geometry?.occlusions==[popup],"native popup bounds retained for priority")
  let update=NSRect(x:118,y:290,width:40,height:36)
  let withUpdate=solve(base+[node(4,0,update,action:true,update:true)]).geometry!
  let placed=Placement.entry(host:withUpdate,size:NSSize(width:48,height:33),offset:32)!
  check(!placed.intersects(update),"native update control has placement priority")
  let moved=base.map{n in node(n.id,n.parent,n.frame?.offsetBy(dx:500,dy:-100),action:n.actionable,account:n.account,nav:n.navigation)}
  check(solve(moved,frame.offsetBy(dx:500,dy:-100)).geometry?.avatar==anchor.offsetBy(dx:500,dy:-100),"window movement has no fixed screen coordinates")
  check(HostMeaning.flags(["Open account menu"]).account && HostMeaning.flags(["账户菜单"]).account,"English and Chinese explicit semantics")
  check(!HostMeaning.flags(["alice@example.org","LE","Account settings guide"]).account,"names initials and prose do not become semantics")
  check(solve([base[0],node(1,0,anchor.offsetBy(dx:0,dy:500),action:true,account:true)]).geometry==nil,"top content cannot become bottom account trigger")
  var structural=[node(0,nil,rail),base[1]]
  for i in 2...4 {structural.append(node(i,0,NSRect(x:110,y:CGFloat(400+i*45),width:30,height:30),action:true))}
  check(solve(structural).geometry != nil,"structured navigation can lack a container label")
  print("\(count) resolver checks passed; synthetic nodes only")
 }
}
