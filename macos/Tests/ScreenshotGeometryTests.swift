import AppKit
@main struct ScreenshotGeometryTests {
 static func main(){
  var passed=0
  func check(_ result:Bool,_ name:String){precondition(result,name);passed+=1;print("PASS",name)}
  // User-provided crop: these are screenshot pixels, never asserted to be host points.
  let circle=NSRect(x:38,y:544,width:48,height:48),tile=NSRect(x:26,y:532,width:72,height:72)
  check(circle.midX==tile.midX && circle.midY==tile.midY,"circle and account button share screenshot center")
  for scale:CGFloat in [1,2] {
   // Synthetic host only. Its size is invented for the test and not measured from the crop.
   let f=NSRect(x:200,y:150,width:640,height:540)
   let a=NSRect(x:f.minX+(circle.minX-9)/scale,y:f.minY+(620-circle.maxY)/scale,width:circle.width/scale,height:circle.height/scale)
   let h=HostGeometry(frame:f,avatar:a,railRight:f.minX+104/scale,nativeControls:[],windowNumber:0)
   let e=Placement.entry(host:h,size:NSSize(width:48,height:66),offset:0)!
   check(e.midX==a.midX,"scale hypothesis \(scale): center avatar without assuming screenshot is points")
   check(e.minY-a.maxY==Placement.initialGap,"scale hypothesis \(scale): gap based on avatar circle top")
   let moved=HostGeometry(frame:f.offsetBy(dx:65,dy:35),avatar:a.offsetBy(dx:65,dy:35),railRight:h.railRight+65,nativeControls:[],windowNumber:0)
   check(Placement.entry(host:moved,size:e.size,offset:0)==e.offsetBy(dx:65,dy:35),"scale hypothesis \(scale): translation invariant")
   let resized=HostGeometry(frame:NSRect(x:f.minX,y:f.minY,width:540,height:420),avatar:a,railRight:h.railRight,nativeControls:[],windowNumber:0)
   check(Placement.entry(host:resized,size:e.size,offset:0)==e,"scale hypothesis \(scale): bottom anchoring survives synthetic resize")
   let buttonTop=a.maxY+12/scale
   check(buttonTop-a.maxY==12/scale,"scale hypothesis \(scale): button-vs-circle top error remains explicit")
  }
  print("\(passed) static geometry checks; scale hypotheses, not measured Codex host integration")
 }
}
