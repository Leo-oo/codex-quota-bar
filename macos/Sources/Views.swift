import AppKit
import CoreText
extension NSColor { static func hex(_ n:UInt32)->NSColor {NSColor(srgbRed:CGFloat((n>>16)&255)/255,green:CGFloat((n>>8)&255)/255,blue:CGFloat(n&255)/255,alpha:1)} }
enum Visual {
 static let scale:CGFloat = 0.8
 static func color(_ light:UInt32,_ dark:UInt32)->NSColor {NSColor(name:nil){a in a.bestMatch(from:[.aqua,.darkAqua]) == .darkAqua ? .hex(dark):.hex(light)}}
 static let text=color(0x242424,0xececec), secondary=color(0x686868,0xa1a1a1), muted=color(0x929292,0x929292)
 static let border=color(0xe0e0e0,0x444444), line=color(0xededed,0x3f3f43), fill=color(0xffffff,0x282828)
 static func font(_ size:CGFloat,_ weight:NSFont.Weight = .regular)->NSFont {NSFont(name:weight == .semibold ? "PingFangSC-Semibold":weight == .medium ? "PingFangSC-Medium":"PingFangSC-Regular",size:size) ?? NSFont.systemFont(ofSize:size,weight:weight)}
}
func ink(_ text:String,_ rect:NSRect,_ size:CGFloat=12,_ color:NSColor = .labelColor,_ weight:NSFont.Weight = .regular) {
 let style=NSMutableParagraphStyle();style.lineBreakMode = .byWordWrapping
 (text as NSString).draw(in:rect,withAttributes:[.font:Visual.font(size,weight),.foregroundColor:color,.paragraphStyle:style])
}
class FlippedView:NSView {override var isFlipped:Bool{true}}
final class OverlayPanel:NSPanel {
 #if SELF_HOST_ONLY
 var hideRequests=0,frameRequests=0,orderRequests=0,contentAssignments=0
 override func setFrame(_ frameRect:NSRect,display flag:Bool){frameRequests+=1;super.setFrame(frameRect,display:flag)}
 override func order(_ place:NSWindow.OrderingMode,relativeTo otherWin:Int){orderRequests+=1;super.order(place,relativeTo:otherWin);let app=NSApp.delegate as? AppDelegate;app?.recordFollow("after-order");DispatchQueue.main.async {app?.recordFollow("next-turn-after-order")}}
 override func orderFrontRegardless(){orderRequests+=1;super.orderFrontRegardless()}
 override var contentView:NSView? {didSet{contentAssignments+=1}}
 override func orderOut(_ sender:Any?){hideRequests+=1;super.orderOut(sender)}
 #endif
 private let acceptsKey:Bool
 override var canBecomeKey:Bool {acceptsKey}
 override var canBecomeMain:Bool {false}
 func dismiss(){parent?.removeChildWindow(self);orderOut(nil)}
 var escaped:(()->Void)?
 override func cancelOperation(_ sender:Any?){escaped?()}
 init(size:NSSize,entry:Bool=false) {
  acceptsKey = !entry
  super.init(contentRect:NSRect(origin:.zero,size:size),styleMask:[.borderless,.nonactivatingPanel],backing:.buffered,defer:false)
  isOpaque=false;backgroundColor = .clear;hasShadow = false;hidesOnDeactivate=false;isReleasedWhenClosed=false
  level = FloatingAttachment.level;collectionBehavior=[.transient,.fullScreenAuxiliary];animationBehavior = .none
  isFloatingPanel=false;becomesKeyOnlyIfNeeded=true
 }
}
final class EntryView:FlippedView {
 var snapshot:QuotaSnapshot?;var stale=false;var unread=false;var hovered=false;var onRight:(()->Void)?;var onHover:((Bool)->Void)?
 override func updateTrackingAreas(){super.updateTrackingAreas();trackingAreas.forEach(removeTrackingArea);addTrackingArea(NSTrackingArea(rect:bounds,options:[.activeAlways,.mouseEnteredAndExited,.inVisibleRect],owner:self))}
 override func mouseEntered(with event:NSEvent){hovered=true;needsDisplay=true;onHover?(true)}
 override func mouseExited(with event:NSEvent){hovered=false;needsDisplay=true;onHover?(false)}
 override func mouseDown(with event:NSEvent){} // Deliberately no left action.
 override func rightMouseDown(with event:NSEvent){onRight?()}
 override func draw(_ dirtyRect:NSRect){
  if hovered {let dark=effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua;(dark ? NSColor.hex(0x31303e):NSColor.hex(0xd5d5df)).setFill();NSBezierPath(roundedRect:bounds,xRadius:9,yRadius:9).fill()}
  let dark=effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua
  let entryInk=dark ? NSColor.hex(0xc1bfca):NSColor.hex(0x75757c)
  let ws=snapshot?.windows ?? []
  if ws.isEmpty {ink("—",NSRect(x:18,y:12,width:20,height:18),12,.secondaryLabelColor)}
  for (i,w) in ws.enumerated() {
   let y=CGFloat(i)*33+4
   let label=NSAttributedString(string:w.label,attributes:[.font:Visual.font(10),.foregroundColor:entryInk])
   let value=NSAttributedString(string:w.percent,attributes:[.font:Visual.font(9.5),.foregroundColor:entryInk])
   let width=label.size().width+value.size().width;let x=(bounds.width-width)/2
   label.draw(at:NSPoint(x:x,y:y));value.draw(at:NSPoint(x:x+label.size().width,y:y+0.5))
   Visual.color(0xdcdce3,0x464552).setFill();NSBezierPath(roundedRect:NSRect(x:7,y:y+19,width:bounds.width-14,height:2),xRadius:1,yRadius:1).fill()
   Visual.color(hovered ? 0x777780:0x96949f,hovered ? 0xa3a0af:0x96949f).setFill();NSBezierPath(roundedRect:NSRect(x:7,y:y+19,width:(bounds.width-14)*w.remaining/100,height:2),xRadius:1,yRadius:1).fill()
  }
  if unread {NSColor.systemRed.setFill();NSBezierPath(ovalIn:NSRect(x:bounds.maxX-7,y:4,width:4,height:4)).fill()}
  if stale {ink("待更新",NSRect(x:5,y:bounds.height-14,width:bounds.width-10,height:12),8,.secondaryLabelColor)}
 }
}
class CardView:FlippedView {
 var corner:CGFloat=14.4
 override func draw(_ dirtyRect:NSRect){Visual.fill.setFill();let p=NSBezierPath(roundedRect:bounds.insetBy(dx:0.4,dy:0.4),xRadius:corner,yRadius:corner);p.fill();Visual.border.setStroke();p.lineWidth=0.8;p.stroke()}
}
// v16 explicitly draws ten nested black rectangles (alpha .004 each), not a native window shadow.
final class CardSurface:FlippedView {
 let card:CardView
 init(_ card:CardView){self.card=card;super.init(frame:NSRect(x:0,y:0,width:card.frame.width+16,height:card.frame.height+16));card.setFrameOrigin(NSPoint(x:8,y:8));addSubview(card)}
 required init?(coder:NSCoder){fatalError()}
 func containsInteractivePoint(_ point:NSPoint)->Bool {
  CardHitRegion.contains(card.convert(point,from:self),in:card.bounds,radius:card.corner)
 }
 override func draw(_ dirtyRect:NSRect){
  NSColor.black.withAlphaComponent(0.004).setFill()
  for i in 0..<10 {let d=CGFloat(i)*0.4;NSBezierPath(roundedRect:NSRect(x:4+d,y:6.4+d,width:card.frame.width+8-2*d,height:card.frame.height+8-2*d),xRadius:card.corner+4-d,yRadius:card.corner+4-d).fill()}
 }
}
final class RuleView:FlippedView {override func draw(_ dirtyRect:NSRect){Visual.line.setFill();bounds.fill()}}
enum Glyph:String {case theme,lightTheme,darkTheme,clock,news,hide,position,exit,up,down,left,right,reset,external,calendar,help,chevron}
enum GlyphAssets {
 static let paths:[String:String] = [
  "gauge": #"<path d="M4 17a9 9 0 1 1 16 0" /><path d="m12 12 5-5" /><circle cx="12" cy="12" r="1.4" /><path d="M4 17h3m10 0h3" />"#,
  "clock": #"<circle cx="12" cy="12" r="8" /><path d="M12 7v5l3 2" />"#,
  "news": #"<rect x="4" y="4" width="16" height="16" rx="3" /><path d="M8 8h8m-8 4h8m-8 4h5" />"#,
  "hide": #"<path d="M3 3l18 18M10 5.2A12 12 0 0 1 21 12a15 15 0 0 1-3 3M6 6C4 7.5 2.8 10 2 12c3 7 10 9 15 5M10 10a3 3 0 0 0 4 4" />"#,
  "position": #"<path d="M12 3v18m-4-14 4-4 4 4m-8 10 4 4 4-4" />"#,
  "exit": #"<path d="M10 4H5a1 1 0 0 0-1 1v14a1 1 0 0 0 1 1h5m5-12 4 4-4 4m-6-4h10" />"#,
  "chevron": #"<path d="m9 6 6 6-6 6" />"#,
  "lightTheme": #"<circle cx="12" cy="12" r="8"/>"#,
  "darkTheme": #"<circle cx="12" cy="12" r="8" fill="currentColor"/>"#,
  "theme": #"<circle cx="12" cy="12" r="8"/><path d="M12 4v16M12 4a8 8 0 0 1 0 16" fill="currentColor"/>"#,
  "calendar": #"<rect x="4" y="5" width="16" height="15" rx="3" /><path d="M8 3v4m8-4v4M4 10h16m-12 5h8" />"#,
  "external": #"<path d="M14 4h6v6m0-6-9 9M10 5H5a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1h13a1 1 0 0 0 1-1v-5" />"#,
  "up": #"<path d="m7 14 5-5 5 5" />"#,
  "down": #"<path d="m7 10 5 5 5-5" />"#,
  "left": #"<path d="m14 7-5 5 5 5" />"#,
  "right": #"<path d="m10 7 5 5-5 5" />"#,
  "reset": #"<path d="M4 10a8 8 0 1 1 1 7M4 4v6h6" />"#,
  "help": #"<circle cx="12" cy="12" r="10.5" stroke-width="1.5"/><text x="8.1" y="17.5" fill="currentColor" stroke="none" font-family="sans-serif" font-size="15">?</text>"#
 ]
 static var cache:[String:NSImage]=[:]
}
func glyph(_ kind:Glyph,_ rect:NSRect,_ color:NSColor=Visual.secondary){
 let c=color.usingColorSpace(.sRGB) ?? .gray
 let hex=String(format:"#%02x%02x%02x",Int(c.redComponent*255),Int(c.greenComponent*255),Int(c.blueComponent*255))
 let key=kind.rawValue+hex
 if GlyphAssets.cache[key]==nil,let path=GlyphAssets.paths[kind.rawValue] {
  let svg="<svg xmlns='http://www.w3.org/2000/svg' width='24' height='24' viewBox='0 0 24 24'><g fill='none' color='\(hex)' stroke='\(hex)' stroke-width='1.55' stroke-linecap='round' stroke-linejoin='round'>\(path)</g></svg>"
  GlyphAssets.cache[key]=NSImage(data:svg.data(using:.utf8)!)
 }
 GlyphAssets.cache[key]?.draw(in:rect,from:.zero,operation:.sourceOver,fraction:1,respectFlipped:true,hints:nil)
}
final class GlyphView:FlippedView {var kind:Glyph = .news;var color=Visual.secondary;override func draw(_ r:NSRect){glyph(kind,bounds,color)}}
final class DesignText:FlippedView {
 let text:String,size:CGFloat,color:NSColor,weight:NSFont.Weight
 init(_ text:String,x:CGFloat,baseline:CGFloat,width:CGFloat,size:CGFloat,color:NSColor=Visual.secondary,weight:NSFont.Weight = .regular){self.text=text;self.size=size;self.color=color;self.weight=weight;let font=Visual.font(size,weight);super.init(frame:NSRect(x:x,y:baseline-font.ascender,width:width,height:font.ascender-font.descender+3));setAccessibilityElement(true);setAccessibilityRole(.staticText);setAccessibilityLabel(text)}
 required init?(coder:NSCoder){fatalError()}
 override func draw(_ r:NSRect){ink(text,bounds,size,color,weight)}
}
final class PlainButton:NSButton {
 var icon:Glyph?;var textSize:CGFloat=8
 var hovered=false;var onHover:((Bool)->Void)?
 override func updateTrackingAreas(){super.updateTrackingAreas();trackingAreas.forEach(removeTrackingArea);addTrackingArea(NSTrackingArea(rect:bounds,options:[.activeAlways,.mouseEnteredAndExited,.inVisibleRect],owner:self))}
 override func mouseEntered(with event:NSEvent){hovered=true;needsDisplay=true;onHover?(true)}
 override func mouseExited(with event:NSEvent){hovered=false;needsDisplay=true;onHover?(false)}
 override func resetCursorRects(){addCursorRect(bounds,cursor:icon == .help ? .arrow:.pointingHand)}
 override func draw(_ r:NSRect){
  if hovered || isHighlighted {Visual.color(0xf2f2f4,0x3a3a3a).setFill();NSBezierPath(roundedRect:bounds,xRadius:4,yRadius:4).fill()}
  if icon == .help {Visual.color(0xa2a2a2,0x929292).setStroke();let p=NSBezierPath(ovalIn:bounds.insetBy(dx:0.4,dy:0.4));p.lineWidth=0.8;p.stroke();let f=Visual.font(8);ink("?",NSRect(x:3.92,y:8.96-f.ascender,width:8,height:12),8,Visual.color(0x888888,0xa1a1a1));return}
  let font=Visual.font(textSize),textSizeValue=(title as NSString).size(withAttributes:[.font:font])
  let iconWidth:CGFloat=icon == nil ? 0:8.8,gap:CGFloat=icon == nil || title.isEmpty ? 0:4
  let start=(bounds.width-textSizeValue.width-gap-iconWidth)/2
  if !title.isEmpty {ink(title,NSRect(x:start,y:(bounds.height-textSizeValue.height)/2,width:textSizeValue.width+1,height:textSizeValue.height+1),textSize,hovered ? Visual.text:Visual.secondary)}
  if let icon {glyph(icon,NSRect(x:start+textSizeValue.width+gap,y:(bounds.height-iconWidth)/2,width:iconWidth,height:iconWidth),hovered ? Visual.text:Visual.secondary)}
 }
}
final class HoverCard:CardView {
 var onHover:((Bool)->Void)?
 override func updateTrackingAreas(){super.updateTrackingAreas();trackingAreas.forEach(removeTrackingArea);addTrackingArea(NSTrackingArea(rect:bounds,options:[.activeAlways,.mouseEnteredAndExited,.inVisibleRect],owner:self))}
 override func mouseEntered(with event:NSEvent){onHover?(true)}
 override func mouseExited(with event:NSEvent){onHover?(false)}
}
final class QuotaCard:CardView {
 var snapshot:QuotaSnapshot?;var error:String?;var updateText="正在读取额度";var onHover:((Bool)->Void)?
 override func updateTrackingAreas(){super.updateTrackingAreas();trackingAreas.forEach(removeTrackingArea);addTrackingArea(NSTrackingArea(rect:bounds,options:[.activeAlways,.mouseEnteredAndExited,.inVisibleRect],owner:self))}
 override func mouseEntered(with event:NSEvent){onHover?(true)}
 override func mouseExited(with event:NSEvent){onHover?(false)}
 override func draw(_ dirtyRect:NSRect){corner=12;super.draw(dirtyRect)
  let ws=snapshot?.windows ?? []
  for(i,w) in ws.enumerated(){let y=CGFloat(i)*68.8
   let title=w.minutes==10080 ? "每周剩余":w.minutes==300 ? "5小时剩余":w.label+"剩余"
   ink(title,NSRect(x:16.8,y:y+14,width:150,height:18),11.2,Visual.text)
   let value=w.percent as NSString;let attrs:[NSAttributedString.Key:Any]=[.font:Visual.font(16.8,.semibold),.foregroundColor:Visual.text];let width=value.size(withAttributes:attrs).width
   value.draw(at:NSPoint(x:bounds.width-16.8-width,y:y+11),withAttributes:attrs)
   Visual.line.setFill();NSBezierPath(roundedRect:NSRect(x:16.8,y:y+37.6,width:bounds.width-33.6,height:3.2),xRadius:1.6,yRadius:1.6).fill()
   Visual.color(0x707070,0xb6b6be).setFill();NSBezierPath(roundedRect:NSRect(x:16.8,y:y+37.6,width:(bounds.width-33.6)*w.remaining/100,height:3.2),xRadius:1.6,yRadius:1.6).fill()
   var reset="重置时间未提供"
   if let seconds=w.reset {let remaining=Int(seconds-Date().timeIntervalSince1970);if remaining<=0{reset="等待服务更新重置时间"}else{let days=remaining/86400,hours=remaining%86400/3600,mins=remaining%3600/60;reset=(days>0 ? "\(days)天":"")+"\(hours)小时\(mins)分钟后重置"}}
   ink(reset,NSRect(x:16.8,y:y+46,width:bounds.width-33.6,height:18),10.4,Visual.color(0x606060,0xa1a1a1))
   Visual.line.setFill();NSRect(x:16.8,y:y+71.2,width:bounds.width-33.6,height:0.8).fill()
  }
  if ws.isEmpty {ink(error ?? "正在读取额度",NSRect(x:16.8,y:17,width:bounds.width-33.6,height:42),11.2,Visual.secondary)}
  let y=bounds.height-(error==nil ? 23:43)
  ink(updateText,NSRect(x:16.8,y:y,width:bounds.width-33.6,height:18),9.6,Visual.color(0x828282,0x929292))
  if let error {ink(error,NSRect(x:16.8,y:y+19,width:bounds.width-33.6,height:28),9,Visual.secondary)}
 }
}
final class RowButton:NSButton {
 var hover=false;var onEnter:(()->Void)?;var icon:Glyph = .news;var chevron=false
 override func updateTrackingAreas(){super.updateTrackingAreas();trackingAreas.forEach(removeTrackingArea);addTrackingArea(NSTrackingArea(rect:bounds,options:[.activeAlways,.mouseEnteredAndExited,.inVisibleRect],owner:self))}
 override func mouseEntered(with event:NSEvent){hover=true;needsDisplay=true;onEnter?()}
 override func mouseExited(with event:NSEvent){hover=false;needsDisplay=true}
 override func draw(_ dirtyRect:NSRect){if hover || isHighlighted {Visual.color(0xf3f3f3,0x373541).setFill();NSBezierPath(roundedRect:bounds,xRadius:7.2,yRadius:7.2).fill()};glyph(icon,NSRect(x:8.8,y:(bounds.height-14.4)/2,width:14.4,height:14.4));ink(title,NSRect(x:32.8,y:(bounds.height-16)/2,width:bounds.width-54,height:20),11.2,isEnabled ? Visual.text:Visual.muted);if state == .on {ink("✓",NSRect(x:bounds.width-22,y:6,width:16,height:18),11.2,Visual.text)};if chevron{glyph(.chevron,NSRect(x:bounds.width-22,y:(bounds.height-11.2)/2,width:11.2,height:11.2))}}
}
#if !DISTRIBUTION
final class PreviewHost:FlippedView {
 var live=false
 var referenceLayout=false
 var anchorShift:CGFloat=0
 var updateVisible=true
 var updateExpanded=false
 override func draw(_ dirtyRect:NSRect){
  let dark=effectiveAppearance.bestMatch(from:[.darkAqua,.aqua]) == .darkAqua; (dark ? NSColor.hex(0x22222b):NSColor.hex(0xf9f8fc)).setFill();bounds.fill();(dark ? NSColor.hex(0x292832):NSColor.hex(0xeeedf3)).setFill();NSRect(x:0,y:0,width:(referenceLayout ? 52:66)+anchorShift,height:bounds.height).fill()
  NSColor.hex(0xbb5845).setFill();NSBezierPath(ovalIn:NSRect(x:(referenceLayout ? 14:18)+anchorShift,y:bounds.height-(referenceLayout ? 38:48),width:referenceLayout ? 24:30,height:referenceLayout ? 24:30)).fill();ink("LE",NSRect(x:(referenceLayout ? 19:25)+anchorShift,y:bounds.height-(referenceLayout ? 35:42),width:25,height:20),11,.white)
  if updateVisible {NSColor.tertiaryLabelColor.setStroke();let reserved=NSBezierPath(ovalIn:NSRect(x:referenceLayout ? 11:18,y:bounds.height-(updateExpanded ? 175:111),width:30,height:updateExpanded ? 94:30));reserved.setLineDash([3,3],count:2,phase:0);reserved.stroke()
  ink("原生更新入口预留",NSRect(x:84,y:bounds.height-108,width:250,height:20),10,.secondaryLabelColor)}
  ink("自有窗口验证预览",NSRect(x:100,y:32,width:400,height:30),22,.labelColor,.semibold)
  ink((live ? "真实额度来自本机官方接口":"示例额度，非真实账户")+"\n⌘1 打开自有测试控制面板\n\n悬停入口查看额度详情；右键打开菜单。\n拖动或缩放此窗口检查跟随。\n\n头像间距待用户真实宿主观察。\n该窗口不读取、不操控 Codex 界面。",NSRect(x:100,y:88,width:390,height:180),13,.secondaryLabelColor)
  ink("Codex额度条 · 里奥Leo @staley_leo",NSRect(x:100,y:bounds.height-43,width:390,height:25),11,.secondaryLabelColor)
 }
}

#endif

// Align visible glyph bounds, rather than line boxes with font leading.
func centeredGlyphInk(_ text:String,in rect:NSRect,size:CGFloat,color:NSColor,weight:NSFont.Weight = .regular,leftAligned:Bool=false){
 let line=CTLineCreateWithAttributedString(NSAttributedString(string:text,attributes:[.font:Visual.font(size,weight),.foregroundColor:color]))
 let inkBounds=CTLineGetBoundsWithOptions(line,[.useGlyphPathBounds])
 guard !inkBounds.isEmpty,let c=NSGraphicsContext.current?.cgContext else{return}
 c.saveGState();defer{c.restoreGState()}
 c.translateBy(x:leftAligned ? rect.minX-inkBounds.minX:rect.midX-inkBounds.midX,y:rect.midY+inkBounds.midY)
 c.scaleBy(x:1,y:-1);c.textMatrix = .identity;c.textPosition = .zero;CTLineDraw(line,c)
}
final class NewsHeading:FlippedView {
 let size:CGFloat
 init(frame:NSRect,size:CGFloat){self.size=size;super.init(frame:frame);setAccessibilityElement(true);setAccessibilityRole(.staticText);setAccessibilityLabel("Tibo 重置消息")}
 required init?(coder:NSCoder){fatalError()}
 override func draw(_ r:NSRect){centeredGlyphInk("Tibo 重置消息",in:bounds,size:size,color:Visual.text,weight:.medium,leftAligned:true)}
}
final class BadgeView:FlippedView {
 var text=""
 override func draw(_ r:NSRect){Visual.border.setStroke();let p=NSBezierPath(roundedRect:bounds.insetBy(dx:0.4,dy:0.4),xRadius:10,yRadius:10);p.lineWidth=0.8;p.stroke();centeredGlyphInk(text,in:bounds,size:8,color:Visual.secondary)}
}

final class DotView:FlippedView {override func draw(_ r:NSRect){NSColor.hex(0xd34b45).setFill();NSBezierPath(ovalIn:bounds).fill()}}

// Uses the same text container and drawing options as NewsLayout, without NSTextField insets.
final class NewsText:FlippedView {
 let text:String,size:CGFloat,color:NSColor
 init(_ text:String,frame:NSRect,size:CGFloat,color:NSColor=Visual.text){self.text=text;self.size=size;self.color=color;super.init(frame:frame);setAccessibilityElement(true);setAccessibilityRole(.staticText);setAccessibilityLabel(text)}
 required init?(coder:NSCoder){fatalError()}
 override func draw(_ dirtyRect:NSRect){
  let style=NSMutableParagraphStyle();style.lineBreakMode = .byWordWrapping
  (text as NSString).draw(with:bounds,options:[.usesLineFragmentOrigin,.usesFontLeading],attributes:[.font:Visual.font(size),.paragraphStyle:style,.foregroundColor:color])
 }
}
