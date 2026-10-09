import AppKit
@main struct IconAssets {
 static func main() throws {
  let root=URL(fileURLWithPath:CommandLine.arguments[1]);var count=0
  for name in ["quota-tray-light.ico","quota-tray-dark.ico","AppIcon.icns"] {
   let image=NSImage(contentsOf:root.appendingPathComponent(name))!;precondition(image.isValid && !image.representations.isEmpty);count+=1
   print(name,"decodes",image.representations.map{"\($0.pixelsWide)x\($0.pixelsHigh)"}.joined(separator:","))
  }
  for (name,dark) in [("quota-tray-light.ico",false),("quota-tray-dark.ico",true)] {
   let image=NSImage(contentsOf:root.appendingPathComponent(name))!
   for rep in image.representations {
    guard let b=rep as? NSBitmapImageRep else{continue};var clear=0,ink=0
    for y in 0..<b.pixelsHigh {for x in 0..<b.pixelsWide {let c=b.colorAt(x:x,y:y)!.usingColorSpace(.deviceRGB)!;if c.alphaComponent<0.01 {clear+=1}else{ink+=1;precondition(dark ? c.redComponent>0.98:c.redComponent<0.02)}}}
    precondition(clear>ink && ink>0);count+=1
   }
  }
  print("\(count) icon decoding/color/transparency checks passed")
 }
}
