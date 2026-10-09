import AppKit

// User-approved tradeoff: ordinary host popups may be behind this floating entry.
// Host availability still comes from the existing foreground/lifecycle resolver.
// Occlusion is deliberately NOT a reason to raise, lower, hide, or disable input.
struct FloatingAttachment {
 static let level=NSWindow.Level.floating
 private(set) var attachedHost:Int?
 @discardableResult mutating func apply(to panel:NSWindow,host:Int?,hidden:Bool)->Bool {
  guard let host,!hidden else {
   attachedHost=nil
   if panel.isVisible {panel.orderOut(nil)}
   return false
  }
  if panel.level != Self.level {panel.level=Self.level}
  guard attachedHost != host || !panel.isVisible else{return false}
  attachedHost=host
  panel.orderFrontRegardless() // No activation or key-window request.
  return true
 }
}
