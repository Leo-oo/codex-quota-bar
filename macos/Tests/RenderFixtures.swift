import AppKit
@main struct RenderFixtures {
 static func main() throws {
  _=NSApplication.shared
  let out=URL(fileURLWithPath:CommandLine.arguments[1]);try FileManager.default.createDirectory(at:out,withIntermediateDirectories:true)
  let week=QuotaWindow(remaining:23,minutes:10080,reset:Date().timeIntervalSince1970+180000,fallback:"次")
  let five=QuotaWindow(remaining:72,minutes:300,reset:Date().timeIntervalSince1970+8290,fallback:"主")
  let hundred=QuotaWindow(remaining:100,minutes:10080,reset:nil,fallback:"次")
  var count=0
  for dark in [false,true] {for (name,windows) in [("week",[week]),("5h",[five]),("dual",[five,week]),("100",[hundred]),("5h100",[QuotaWindow(remaining:100,minutes:300,reset:nil,fallback:"主")])] {for hover in [false,true] {
   let v=EntryView(frame:NSRect(x:0,y:0,width:48,height:CGFloat(windows.count)*33));v.appearance=NSAppearance(named:dark ? .darkAqua:.aqua);v.snapshot=QuotaSnapshot(windows:windows,fetched:Date());v.hovered=hover
   let bitmap=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:Int(v.bounds.width)*2,pixelsHigh:Int(v.bounds.height)*2,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!
   bitmap.size=v.bounds.size;v.cacheDisplay(in:v.bounds,to:bitmap)
   let filename="\(name)-\(dark ? "dark":"light")-\(hover ? "hover":"normal").png"
   try bitmap.representation(using:.png,properties:[:])!.write(to:out.appendingPathComponent(filename))
   if hover {let c=bitmap.colorAt(x:54,y:4)!.usingColorSpace(.deviceRGB)!;let expected=dark ? NSColor.hex(0x31303e):NSColor.hex(0xd5d5df);precondition(abs(c.redComponent-expected.redComponent)<0.02 && abs(c.greenComponent-expected.greenComponent)<0.02 && abs(c.blueComponent-expected.blueComponent)<0.02,"exact hover fill")}
   if !hover {precondition((bitmap.colorAt(x:0,y:0)?.alphaComponent ?? 1)==0,"normal corner must be transparent")}
   let texts=windows.map{$0.label+$0.percent}.joined(separator:" / ");print("PASS \(filename) \(texts)");count+=1
  }}}
  for dark in [false,true] {
   let v=QuotaCard(frame:NSRect(x:0,y:0,width:248,height:190));v.appearance=NSAppearance(named:dark ? .darkAqua:.aqua);v.snapshot=QuotaSnapshot(windows:[five,week],fetched:Date());v.updateText="更新于22:40（2s前）"
   let bitmap=v.bitmapImageRepForCachingDisplay(in:v.bounds)!;v.cacheDisplay(in:v.bounds,to:bitmap);try bitmap.representation(using:.png,properties:[:])!.write(to:out.appendingPathComponent("tooltip-\(dark ? "dark":"light").png"))
  }
  print("\(count) entry renders; normal transparency checked. Rendering fixtures, not desktop screenshots.")
 }
}
