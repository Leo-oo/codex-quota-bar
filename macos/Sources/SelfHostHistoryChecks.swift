#if SELF_HOST_ONLY
import AppKit
extension SelfHostInputChecks {
 func historyChecks() async {
  let original=app.news;defer{app.news=original;app.closePanels();app.update()}
  func fields(_ view:NSView)->[NSTextField] {view.subviews.flatMap{($0 as? NSTextField).map{[$0]} ?? fields($0)}}
  for type in ["reset_credit","direct_reset"] {
   let n=NewsItem(id:"own-history-fixture",version:"fixture",type:type,status:"confirmed",scope:"",time:"",url:"https://aihot.news/codex-reset",occurred:"",basis:"source_post",explicit:true,inferred:false,confirmed:"2026-10-08T11:44:55+08:00")
   app.news=NewsSnapshot(items:[],history:n,checked:"2026-10-09T14:10:00+08:00",day:NewsParser.day(Date()));app.closePanels();app.update();await pause(0.4)
   click(app.entryView,right:true);await pause();if let b=button("Tibo 重置消息",in:app.mainPanel?.contentView){click(b)};await pause()
   check(app.surface=="news","history card opens through queued menu input "+type)
   let label=app.mainPanel?.contentView.flatMap{fields($0).first{$0.stringValue==n.historyText}}
   check(label != nil,"empty-state shows labelled confirmation "+type)
   if let label {let h=label.attributedStringValue.boundingRect(with:NSSize(width:label.frame.width,height:1000),options:[.usesLineFragmentOrigin,.usesFontLeading]).height
    check(h<=label.frame.height && !label.isHidden,"history text fits unchanged slot "+type)
   }else{check(false,"history text fits unchanged slot "+type)}
   check(!app.unread,"history-only fixture has no unread dot "+type)
   if let view=app.mainPanel?.contentView,let bitmap=view.bitmapImageRepForCachingDisplay(in:view.bounds) {view.cacheDisplay(in:view.bounds,to:bitmap);try? bitmap.representation(using:.png,properties:[:])?.write(to:app.runtime.appendingPathComponent("history-"+type+".png"))}
   app.news=NewsSnapshot(items:[],history:n,checked:"2026-10-09T14:30:00+08:00",day:NewsParser.day(Date()));app.update();check(!app.unread,"history check-time remains quiet "+type)
  }
  app.news=NewsSnapshot(items:[],history:nil,checked:"",day:NewsParser.day(Date()));app.closePanels();app.showNews()
  check(app.mainPanel?.contentView.map{fields($0).allSatisfy{!$0.stringValue.hasPrefix("上次")}}==true,"no-history card omits history label")
 }
}
#endif
