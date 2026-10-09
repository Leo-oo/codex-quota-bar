#if !DISTRIBUTION
import AppKit
extension AppDelegate {
 func exportVisuals(){
  guard preview else{return}
  let directory=runtime.appendingPathComponent("visual-evidence");try? FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
  func save(_ view:NSView,_ name:String){for scale in [1.0,1.25,2.0]{
   let b=NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:Int(ceil(view.bounds.width*CGFloat(scale))),pixelsHigh:Int(ceil(view.bounds.height*CGFloat(scale))),bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0)!;b.size=view.bounds.size;view.cacheDisplay(in:view.bounds,to:b)
   try? b.representation(using:.png,properties:[:])?.write(to:directory.appendingPathComponent(name+"@\(scale)x.png"))
  }}
  for dark in [false,true] {
   let appearance=NSAppearance(named:dark ? .darkAqua:.aqua)!;NSApp.appearance=appearance
   let theme=dark ? "dark":"light"
   preferences.theme=theme
   appearance.performAsCurrentDrawingAppearance {
    let entryBackdrop=FlippedView(frame:NSRect(x:0,y:0,width:88,height:73));entryBackdrop.wantsLayer=true;entryBackdrop.layer?.backgroundColor=NSColor.hex(dark ? 0x202123:0xe8e8e8).cgColor
    let sampleEntry=EntryView(frame:NSRect(x:20,y:20,width:48,height:33));sampleEntry.appearance=appearance;sampleEntry.snapshot=QuotaSnapshot(windows:[QuotaWindow(remaining:82,minutes:10080,reset:0,fallback:"周")],fetched:Date());entryBackdrop.addSubview(sampleEntry)
    save(entryBackdrop,theme+"-entry-normal");sampleEntry.hovered=true;sampleEntry.needsDisplay=true;save(entryBackdrop,theme+"-entry-hover")
    showMenu();menuButtons[2].hover=true;mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-menu")
    showPosition();subPanel?.contentView?.appearance=appearance;save(subPanel!.contentView!,theme+"-submenu")
    showTheme();subPanel?.contentView?.appearance=appearance;save(subPanel!.contentView!,theme+"-theme-submenu")
    showTooltip();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-tooltip")
    let savedHistoryNews=news
    for kind in ["reset_credit","direct_reset"] {
     let item=NewsItem(id:"history-fixture",version:"fixture",type:kind,status:"confirmed",scope:"",time:"",url:"https://aihot.news/codex-reset",occurred:"",basis:"source_post",explicit:true,inferred:false,confirmed:"2026-10-08T11:44:55+08:00")
     news=NewsSnapshot(items:[],history:item,checked:"2026-10-09T14:10:00+08:00",day:NewsParser.day(Date()));showNews();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-history-"+kind)
    }
    news=savedHistoryNews
    let original=news;news=NewsSnapshot(items:[],history:nil,checked:"2026-10-03T23:50:00+08:00",day:NewsParser.day(Date()));showNews();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-empty")
    if let surface=mainPanel?.contentView as? CardSurface,let details=surface.card.subviews.compactMap({$0 as? PlainButton}).first(where:{$0.icon == .external}) {details.hovered=true;details.needsDisplay=true;save(surface,theme+"-empty-details-hover");details.hovered=false}
    let trayCard=CardView(frame:NSRect(x:0,y:0,width:60,height:40));trayCard.appearance=appearance
    let icon=NSImageView(frame:NSRect(x:20,y:10,width:20,height:20));icon.image=trayImage(dark:dark);trayCard.addSubview(icon);save(trayCard,theme+"-tray")
    news=original;showNews();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-news")
    func fixture(_ id:String,_ type:String="reset_credit",_ status:String="confirmed",_ scope:String="示例 Plus 用户 · Codex",_ time:String="发生日期：2026-10-07（示例日期）",_ explicit:Bool=true,_ basis:String="source_post")->NewsItem {NewsItem(id:id,version:"fixture",type:type,status:status,scope:scope,time:time,url:"https://aihot.news/codex-reset",occurred:"",basis:basis,explicit:explicit,inferred:false)}
    let cases:[(String,[NewsItem],String?)]=[
     ("short",[fixture("short")],nil),
     ("unknown",[fixture("unknown","direct_reset","confirmed","","来源记录时间：10月7日 11:35",false)],nil),
     ("unknown-type",[fixture("unknown-type","other","confirmed","","",false,"receipt_review")],nil),
     ("long-cn",[fixture("long-cn","direct_reset","in_progress",String(repeating:"示例适用范围与产品说明",count:12),"预告时间："+String(repeating:"尚未明确具体时间",count:10))],nil),
     ("long-en",[fixture("long-en","reset_credit","announced",String(repeating:"Example audience and product details ",count:12),"预告时间：Example date pending source confirmation")],nil),
     ("multiple",[fixture("one"),fixture("two","direct_reset","announced","示例 Pro 用户","预告时间：示例日期（未公布时刻）")],nil),
     ("error",[],"消息暂时无法更新")]
    for (name,items,error) in cases {
     news=NewsSnapshot(items:items,history:nil,checked:"2026-10-07",day:NewsParser.day(Date()));newsError=error;showNews();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-fixture-"+name)
    }
    news=nil;newsError="消息暂时无法更新";showNews();mainPanel?.contentView?.appearance=appearance;save(mainPanel!.contentView!,theme+"-fixture-no-cache-error")
    news=original;newsError=nil
   }
  }
  let metadata:[String:Any] = ["build":"0.11.6-mac-candidate.17","viewUnits":"AppKit points","exports":[1,1.25,2],"windowBackingScaleFactor":previewWindow?.backingScaleFactor ?? 0,"hostFramePoints":NSStringFromRect(previewWindow?.frame ?? .zero),"source":"own views only; fictional test fixtures, not actual public events; not real Codex capture","menuCardPoints":[228,195.2],"designScale":0.8,"darkPanelTokens":"supplemental; v16 specifies only dark entry"]
  if let d=try? JSONSerialization.data(withJSONObject:metadata,options:.prettyPrinted){try? d.write(to:directory.appendingPathComponent("metadata.json"))}
  closePanels();NSApp.terminate(nil)
 }
}

#endif
