import AppKit
// A bounded ordering observation from the same window-list sample as the host.
// Window identifiers and ordering only; no pixels, titles, or UI contents.
struct HostStackObservation:Equatable {
 let hostNumber:Int,entryNumber:Int
 let entryBelowHost:Bool
 let sampledAt:TimeInterval
 static func read(_ ids:[Int],host:Int,entry:Int,now:TimeInterval)->HostStackObservation? {
  guard host != entry,let h=ids.firstIndex(of:host),let e=ids.firstIndex(of:entry) else{return nil}
  return HostStackObservation(hostNumber:host,entryNumber:entry,entryBelowHost:e>h,sampledAt:now)
 }
}
struct OverlayOrderState {
 private var host:Int?,entry:Int?,block:Int?
 private var lastOrder:TimeInterval = -.infinity
 mutating func needsOrder(host nextHost:Int,entry nextEntry:Int,block nextBlock:Int?,orderedIn:Bool,observation:HostStackObservation?,now:TimeInterval)->Bool {
  let stateChanged = !orderedIn || host != nextHost || entry != nextEntry || block != nextBlock
  // Only repair proven host/entry inversion, never generic occlusion by another app,
  // a native popup, or one of our own cards. Discard pre-repair in-flight samples.
  let inverted=nextBlock==nil && observation.map{
   $0.hostNumber==nextHost && $0.entryNumber==nextEntry && $0.entryBelowHost && $0.sampledAt>lastOrder
  } == true
  guard stateChanged || inverted else{return false}
  host=nextHost;entry=nextEntry;block=nextBlock;lastOrder=now
  return true
 }
}
struct HostGeometry:Equatable {
 let frame: NSRect
 let avatar: NSRect
 let railRight: CGFloat
 let nativeControls: [NSRect]
 let windowNumber: Int
 var occlusions:[NSRect] = []
 var structuralRail:NSRect? = nil
 // Explicit window-relative anchor; avatar is unavailable (.zero) in this mode.
 var windowAnchor:NSPoint? = nil
 var stack:HostStackObservation? = nil
 // Screenshot reference: 48px avatar mapped provisionally to 24pt (2x).
 // Avatar top 40pt + 50 image px / 2 - 8pt visible-line inset = panel bottom 57pt.
 // The 25pt visible gap reserves space; no fabricated native-control rectangle.
 static func windowRelative(frame:NSRect,number:Int,occlusions:[NSRect]=[],controls:[NSRect]?=nil)->HostGeometry {
  HostGeometry(frame:frame,avatar:.zero,railRight:frame.minX+52,nativeControls:controls ?? [],windowNumber:number,occlusions:occlusions,windowAnchor:NSPoint(x:frame.minX+26,y:frame.minY+57))
 }
 func calibrated(horizontal:CGFloat)->HostGeometry {
  guard let anchor=windowAnchor else{return self}
  return HostGeometry(frame:frame,avatar:.zero,railRight:railRight+horizontal,nativeControls:nativeControls,windowNumber:windowNumber,occlusions:occlusions,windowAnchor:NSPoint(x:anchor.x+horizontal,y:anchor.y),stack:stack)
 }
}
struct Placement {
 // Provisional native point values, awaiting measurement against the real host.
 // These are explicitly not a conversion of the Windows 125% pixel measurements.
 static let initialGap: CGFloat = 72
 static let minimumGap: CGFloat = 24
 static func entry(host: HostGeometry, size: NSSize, offset: CGFloat) -> NSRect? {
  let center=host.windowAnchor?.x ?? host.avatar.midX
  let bottom=host.windowAnchor.map{$0.y-offset} ?? (host.avatar.maxY+max(minimumGap,initialGap-offset))
  var r = NSRect(x:center-size.width/2,y:bottom,width:size.width,height:size.height)
  if host.windowAnchor != nil {
   guard host.frame.width>=size.width+4,host.frame.height>=size.height+4 else{return nil}
   r.origin.x=min(max(r.minX,host.frame.minX+2),host.frame.maxX-size.width-2)
   r.origin.y=min(max(r.minY,host.frame.minY+24),host.frame.maxY-size.height-2)
  }
  guard [r.minX,r.minY,r.width,r.height].allSatisfy({$0.isFinite}) else{return nil}
  for control in host.nativeControls.sorted(by:{$0.minY<$1.minY}) where r.insetBy(dx:-3,dy:-3).intersects(control) { r.origin.y = control.maxY+6 }
  guard host.frame.insetBy(dx:2,dy:2).contains(r),
   !host.nativeControls.contains(where:{r.insetBy(dx:-3,dy:-3).intersects($0)}),
   host.structuralRail.map({$0.contains(r)}) ?? true else { return nil }
  return r
 }
 static func popup(host: HostGeometry, size: NSSize, screen: NSRect) -> NSRect {
  let railRight:CGFloat
  if let anchor=host.windowAnchor {railRight=min(max(anchor.x,host.frame.minX+26),host.frame.maxX-26)+32} else{railRight=host.railRight}
  var x = railRight+4
  if x+size.width > screen.maxX { x = host.frame.minX-size.width-4 }
  return NSRect(x:min(max(x,screen.minX),max(screen.minX,screen.maxX-size.width)),y:min(max(host.frame.minY+8,screen.minY),max(screen.minY,screen.maxY-size.height)),width:size.width,height:size.height)
 }
 static func submenu(parent: NSRect, row: NSRect, size: NSSize, screen: NSRect) -> NSRect {
  var x = parent.maxX+4
  if x+size.width>screen.maxX { x=parent.minX-size.width-4 }
  return NSRect(x:max(screen.minX,x),y:max(screen.minY,min(row.midY-size.height/2,screen.maxY-size.height)),width:size.width,height:size.height)
 }
}
final class Preferences {
 let url: URL
 var offset: CGFloat = 0
 var horizontal:CGFloat = 0
 var theme="light"
 var legacyAnchor=false
 var read: [String:String] = [:]
 init(_ url: URL) {
  self.url=url
  if let d=try? Data(contentsOf:url),d.count<65536,let j=(try? JSONSerialization.jsonObject(with:d)) as? [String:Any] {
   theme=["light","dark"].contains(j["theme"] as? String ?? "") ? j["theme"] as! String:"light"
   offset=max(-258,min(32,j["offset"] as? Double ?? 0));horizontal=max(-6,min(600,j["horizontal"] as? Double ?? 0));read=j["read"] as? [String:String] ?? [:]
   legacyAnchor=j["legacyAnchor"] as? Bool ?? ((j["schemaVersion"] as? Int ?? 1)<3 && (offset != 0 || horizontal != 0))
  }
 }
 func calibrated(_ host:HostGeometry)->HostGeometry {
  var value=host.calibrated(horizontal:horizontal)
  if legacyAnchor,let anchor=value.windowAnchor {
   value.windowAnchor=NSPoint(x:anchor.x+6,y:anchor.y+79)
  }
  return value
 }
 @discardableResult func save(offset newOffset: CGFloat? = nil, read newRead:[String:String]? = nil,horizontal newHorizontal:CGFloat? = nil,theme newTheme:String? = nil,legacyAnchor newLegacy:Bool? = nil) -> Bool {
  guard (newHorizontal ?? self.horizontal).isFinite,(newOffset ?? self.offset).isFinite else{return false}
  let theme=newTheme ?? self.theme
  guard ["light","dark"].contains(theme) else{return false}
  let horizontal=max(-6,min(600,newHorizontal ?? self.horizontal))
  let offset=max(-258,min(32,newOffset ?? self.offset)); let read=newRead ?? self.read
  do { let d=try JSONSerialization.data(withJSONObject:["schemaVersion":3,"legacyAnchor":newLegacy ?? legacyAnchor,"theme":theme,"offset":offset,"horizontal":horizontal,"read":read],options:.sortedKeys)
   try FileManager.default.createDirectory(at:url.deletingLastPathComponent(),withIntermediateDirectories:true)
   try d.write(to:url,options:.atomic);self.legacyAnchor=newLegacy ?? legacyAnchor;self.theme=theme;self.offset=offset;self.horizontal=horizontal;self.read=read;return true
  } catch { return false }
 }
}

// Screenshot-calibrated ratios, independent of screenshot pixels per AppKit point.
// Only applied to a square AX button at the host's bottom-left corner, never image contents.
enum AvatarCalibration {
 static func circle(button r:NSRect,host f:NSRect)->NSRect? {
  guard r.width>=12,r.width<=80,r.height/r.width>=0.85,r.height/r.width<=1.15,f.contains(r) else{return nil}
  let left=(r.midX-f.minX)/r.width,bottom=(r.midY-f.minY)/r.width
  guard abs(left-bottom)<0.18 else{return nil}
  if (0.62...0.85).contains(left) && (0.62...0.85).contains(bottom) {return r.insetBy(dx:r.width/6,dy:r.height/6)}
  if (0.95...1.25).contains(left) && (0.95...1.25).contains(bottom) {return r}
  return nil
 }
 static func railRight(avatar:NSRect,host:NSRect)->CGFloat {host.minX+2*(avatar.midX-host.minX)}
}
