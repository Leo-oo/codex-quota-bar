// Inspect only exported own-view PNGs. Run with current and candidate15 export directories.
import AppKit
let root=URL(fileURLWithPath:CommandLine.arguments[1]),old=URL(fileURLWithPath:CommandLine.arguments[2])
func bitmap(_ name:String)->NSBitmapImageRep {NSBitmapImageRep(data:try! Data(contentsOf:root.appendingPathComponent(name+"@2.0x.png")))!}
func rgb(_ b:NSBitmapImageRep,_ x:Int,_ y:Int)->[Int] {var p=[Int](repeating:0,count:4);b.getPixel(&p,atX:x,y:y);return Array(p.prefix(3))}
let b=bitmap("light-fixture-short")
func bounds(_ box:[Int],_ threshold:Int)->[Double] {var pts=[(Int,Int)]();for y in box[1]..<box[3] {for x in box[0]..<box[2] {if rgb(b,x,y).max()!<threshold {pts.append((x,y))}}};return [Double(pts.map{$0.0}.min()!),Double(pts.map{$0.1}.min()!),Double(pts.map{$0.0}.max()!),Double(pts.map{$0.1}.max()!)]}
let icon=bounds([45,52,72,85],180),title=bounds([80,50,238,90],150),badge=bounds([397,121,459,149],180)
func midY(_ b:[Double])->Double {(b[1]+b[3])/2}
var checks=0
func check(_ ok:Bool,_ s:String){precondition(ok,s);checks+=1;print("PASS "+s)}
print("icon",icon,"title",title,"badge",badge)
check(abs(midY(icon)-midY(title))<=1,"heading glyph/icon visible center within one export pixel")
check(abs(midY(badge)-134.5)<=1,"badge vertical center within one export pixel")
check(abs((badge[0]+badge[2])/2-427)<=1,"badge horizontal center within one export pixel")
let hover=rgb(bitmap("light-entry-hover"),86,47),normal=rgb(bitmap("light-entry-normal"),86,47)
print("hoverRGB",hover,"normalRGB",normal)
check(hover==[213,213,223],"light hover stronger neutral color")
check(normal==[232,232,232],"normal entry transparent on own gray fixture")
for theme in ["light","dark"] {let name=theme+"-tray@2.0x.png";check(try! Data(contentsOf:root.appendingPathComponent(name))==Data(contentsOf:old.appendingPathComponent(name)),theme+" tray pixels unchanged")}
let choices=bitmap("light-theme-submenu")
let light=rgb(choices,59,55),dark=rgb(choices,59,113)
check(light.min()!>230 && dark.max()!<150,"light icon has empty center, dark icon filled center")
print("\(checks) candidate16 pixel checks passed; own exports, not user screenshots or real DPI")
