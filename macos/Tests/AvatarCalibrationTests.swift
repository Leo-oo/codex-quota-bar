import AppKit
@main struct CalibrationTests {
 static func main(){var n=0;func check(_ b:Bool,_ s:String){precondition(b,s);n+=1;print("PASS",s)}
  for scale:CGFloat in [0.7,1,2] {
   let f=NSRect(x:120,y:150,width:1024*scale,height:600*scale)
   let tile=NSRect(x:f.minX+8*scale,y:f.minY+8*scale,width:36*scale,height:36*scale)
   let a=AvatarCalibration.circle(button:tile,host:f)!
   check(abs(a.width-24*scale)<0.001,"button border excluded at scale \(scale)")
   check(abs(AvatarCalibration.railRight(avatar:a,host:f)-(f.minX+52*scale))<0.001,"narrow rail, not task sidebar, at scale \(scale)")
   check(AvatarCalibration.circle(button:a,host:f)==a,"already-circle AX frame not shrunk twice")
   check(AvatarCalibration.circle(button:tile.offsetBy(dx:200*scale,dy:80*scale),host:f)==nil,"embedded screenshot avatar rejected")
   check(AvatarCalibration.circle(button:NSRect(x:f.minX+50*scale,y:f.minY+25*scale,width:180*scale,height:24*scale),host:f)==nil,"native menu settings row rejected")
  }
  print("\(n) ratio tests passed; no host APIs used")
 }
}
