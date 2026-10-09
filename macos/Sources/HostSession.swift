import Foundation

// Pure lifecycle state: no AX calls. Uptime deadlines cannot be prolonged by failed reads.
struct HostReadToken:Equatable {let generation:Int;let processIdentifier:Int32}
enum HostLifecycleChange {case refresh, invalidate, restart}
struct HostSession {
 static let continuityLimit:TimeInterval=0.6
 private(set) var generation=0
 private(set) var processIdentifier:Int32?
 private(set) var processIdentity:HostProcessIdentity?
 private(set) var windowNumber:Int?
 private(set) var geometry:HostGeometry?
 private(set) var confirmedAt:TimeInterval = -.infinity
 private(set) var status="等待 Codex 窗口"
 var token:HostReadToken? {processIdentifier.map{HostReadToken(generation:generation,processIdentifier:$0)}}
 mutating func selectProcess(_ pid:Int32,launchedAt:Date?=nil){
  let next=HostProcessIdentity(pid:pid,launchedAt:launchedAt)
  guard processIdentity != next else{return}
  invalidate("正在确认 Codex 窗口");processIdentifier=pid;processIdentity=next
 }
 mutating func invalidate(_ message:String,forgetProcess:Bool=false){
  generation+=1;geometry=nil;windowNumber=nil;confirmedAt = -.infinity;status=message
  if forgetProcess {processIdentifier=nil;processIdentity=nil}
 }
 mutating func lifecycle(_ change:HostLifecycleChange){
  switch change {
  case .refresh: break // A request for new data is not evidence of a hidden window.
  case .invalidate: invalidate("正在重新确认 Codex 窗口")
  case .restart: invalidate("正在重新确认 Codex 进程",forgetProcess:true)
  }
 }
 @discardableResult mutating func accept(_ result:HostResolution,token:HostReadToken,now:TimeInterval)->Bool {
  guard self.token==token else{return false}
  status=result.reason
  if let next=result.geometry {
   // A newly confirmed window replaces every old anchor atomically.
   geometry=next;windowNumber=next.windowNumber;confirmedAt=now
  }else if result.kind != .temporarilyUnavailable || result.observedWindowNumber == nil || result.observedWindowNumber != windowNumber {
   geometry=nil;windowNumber=nil;confirmedAt = -.infinity
  }
  return true
 }
 func current(now:TimeInterval)->HostGeometry? {
  guard now-confirmedAt < Self.continuityLimit else{return nil}
  return geometry
 }
}
