import AppKit
struct NewsBlock {
 let item:NewsItem
 let titleWidth:CGFloat,stateWidth:CGFloat
 let titleHeight:CGFloat,stateHeight:CGFloat,scopeHeight:CGFloat,timeHeight:CGFloat,labelHeight:CGFloat
 let complete:Bool
 var headerHeight:CGFloat {max(titleHeight,stateHeight+6)}
 var scopeLabelY:CGFloat {headerHeight+14.4}
 var scopeY:CGFloat {scopeLabelY+14.4}
 var timeLabelY:CGFloat {scopeY+scopeHeight+9.6}
 var timeY:CGFloat {timeLabelY+labelHeight+2.4}
 var moreY:CGFloat {timeY+timeHeight+4}
 var height:CGFloat {timeY+timeHeight+12+(complete ? 0:18)}
}
enum NewsLayout {
 static func attributes(_ size:CGFloat)->[NSAttributedString.Key:Any] {
  let style=NSMutableParagraphStyle();style.lineBreakMode = .byWordWrapping
  return [.font:NSFont(name:"PingFangSC-Regular",size:size) ?? NSFont.systemFont(ofSize:size),.paragraphStyle:style]
 }
 static func height(_ text:String,_ width:CGFloat,_ font:CGFloat)->CGFloat {
  guard !text.isEmpty else{return 0}
  return ceil((text as NSString).boundingRect(with:NSSize(width:max(1,width),height:10000),options:[.usesLineFragmentOrigin,.usesFontLeading],attributes:attributes(font)).height)+2
 }
 static func blocks(_ items:[NewsItem],width:CGFloat,budget:CGFloat)->[NewsBlock] {
  var result:[NewsBlock]=[],used:CGFloat=0
  for item in items {
   let stateWidth=min(width/2,ceil((item.state as NSString).size(withAttributes:attributes(8)).width)+16)
   let titleWidth=width-stateWidth-6.4
   let title=height(item.title,titleWidth,12),state=height(item.state,stateWidth-14,8)
   let scope=height(item.scopeValue,width,9.6),time=height(item.timeField.value,width,9.6)
   let block=NewsBlock(item:item,titleWidth:titleWidth,stateWidth:stateWidth,titleHeight:title,stateHeight:state,scopeHeight:min(scope,60),timeHeight:min(time,45),labelHeight:height(item.timeField.label,width,8),complete:scope<=60 && time<=45)
   guard used+block.height<=budget else{break}
   result.append(block);used+=block.height
  }
  return result
 }
 static func visibleVersions(_ blocks:[NewsBlock])->[(String,String)] {blocks.filter{$0.complete}.map{($0.item.id,$0.item.version)}}
}
