import AppKit

// Pure policies shared by the production adapter and offline tests.
struct HostProcessIdentity:Equatable {
 let pid:Int32
 let launchedAt:Date?
}
struct HostWindowFrame {let id:Int;let frame:NSRect}
enum HostWindowPolicy {
 // Onscreen metadata includes fully transparent helper windows. Their bounds are
 // not a visible popup. Any positive alpha remains conservative: native panels win.
 static func isVisibleOccluder(frame:NSRect,alpha:Double)->Bool {
  alpha.isFinite && alpha>0 && frame.width>0 && frame.height>0 && [frame.minX,frame.minY,frame.width,frame.height].allSatisfy{$0.isFinite}
 }
 static func display(in screens:[NSRect],anchor:NSPoint?,host:NSRect?)->NSRect? {
  if let anchor,let screen=screens.first(where:{$0.contains(anchor)}) {return screen}
  if let host,let best=screens.max(by:{a,b in
   let x=a.intersection(host),y=b.intersection(host)
   return (x.isNull ? 0:x.width*x.height)<(y.isNull ? 0:y.width*y.height)
  }),best.intersects(host){return best}
  return screens.first
 }

 static func uniqueWindowID(in windows:[HostWindowFrame],matching frame:NSRect)->Int? {
  let matches=windows.filter {candidate in
   let r=candidate.frame
   let origin=abs(r.minX-frame.minX)+abs(r.minY-frame.minY)
   let size=abs(r.width-frame.width)+abs(r.height-frame.height)
   return candidate.id>0 && origin+size<8
  }
  guard matches.count==1 else{return nil}
  return matches[0].id
 }

 // CGWindowList is front-to-back. Windows behind the host must not block it.
 static func occluders(orderedWindowIDs:[Int],hostID:Int)->Set<Int> {
  guard let hostIndex=orderedWindowIDs.firstIndex(of:hostID) else{return []}
  return Set(orderedWindowIDs.prefix(hostIndex))
 }
}
enum CardHitRegion {
 static func contains(_ point:NSPoint,in rect:NSRect,radius:CGFloat)->Bool {
  guard rect.contains(point) else{return false}
  let r=max(0,min(radius,min(rect.width,rect.height)/2))
  let center=NSPoint(x:min(max(point.x,rect.minX+r),rect.maxX-r),y:min(max(point.y,rect.minY+r),rect.maxY-r))
  let dx=point.x-center.x,dy=point.y-center.y
  return dx*dx+dy*dy<=r*r
 }
}
