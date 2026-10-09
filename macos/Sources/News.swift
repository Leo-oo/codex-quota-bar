import Foundation
import CryptoKit
struct NewsItem: Codable {
 let id:String,version:String,type:String,status:String,scope:String,time:String,url:String,occurred:String,basis:String
 let explicit:Bool,inferred:Bool
 var confirmed:String? = nil
 var historyText:String? {NewsHistory.text(self)}
 var knownType:Bool {["direct_reset","reset_credit"].contains(type)}
 var title:String {if status=="withdrawn" {return "重置消息"};guard knownType else{return "消息（类型未明确）"};guard explicit else{return "重置（形式未明确）"};return type=="direct_reset" ? "直接额度重置":"发放重置卡"}
 var state:String {
  switch status {
  case "confirmed":
   if explicit && knownType && ["source_post","receipt_review"].contains(basis) {return "已发生"}
   return basis=="source_post" ? "已落地（来源确认）":basis=="receipt_review" ? "已落地（来源核验）":"尚未确认（确认依据未提供）"
  case "announced":return "预告（尚未确认）"
  case "in_progress":return "预告（进行中，尚未确认）"
  case "expired_unconfirmed","likely_completed":return "尚未确认（预计时间已过）"
  case "withdrawn":return "已撤回（来源更正）"
  default:return "尚未确认"
  }
 }
 var scopeValue:String {scope.isEmpty ? "未明确":scope}
 // Keep legacy cache compatibility: time remains the serialized source field.
 var timeField:(label:String,value:String) {
  guard !time.isEmpty else{return (status=="announced" ? "预计生效时间":"消息时间","未公布")}
  guard let colon=time.firstIndex(of:"：") else{return ("时间信息",time)}
  let source=String(time[..<colon]),value=String(time[time.index(after:colon)...])
  if source.hasPrefix("来源") {return ("来源记录时间",value)}
  if source.hasPrefix("消息发布") {return ("消息发布时间",value)}
  if source.hasPrefix("发生日期") {return (source,value)}
  if source.hasPrefix("预计") || source.hasPrefix("预告") {
   return (status=="announced" ? "预计生效时间":"预告窗口 · 尚未确认",value+(source.contains("AIHOT") ? "（AIHOT 推算）":""))
  }
  return (source,value)
 }

}
struct NewsSnapshot:Codable {
 var items:[NewsItem];let history:NewsItem?;let checked:String;let day:String
 func current(now:Date=Date())->NewsSnapshot {guard day != NewsParser.day(now) else{return self};return NewsSnapshot(items:items.filter{["announced","in_progress","expired_unconfirmed","likely_completed"].contains($0.status)},history:history,checked:checked,day:NewsParser.day(now))}
}
enum NewsParser {
 static func day(_ now:Date)->String {let f=DateFormatter();f.timeZone=TimeZone(secondsFromGMT:28800);f.dateFormat="yyyy-MM-dd";return f.string(from:now)}
 static func date(_ s:String)->String {
  if s.isEmpty{return "未提供"}
  if s.count==10 {return s} // Do not turn a calendar date into midnight.
  let iso=ISO8601DateFormatter()
  iso.formatOptions=[.withInternetDateTime,.withFractionalSeconds]
  let parsed=iso.date(from:s);iso.formatOptions=[.withInternetDateTime]
  if let d=parsed ?? iso.date(from:s) {let f=DateFormatter();f.dateFormat="M月d日 HH:mm";return f.string(from:d)}
  return s + (s.contains("T") && !s.contains("Z") && !s.contains("+") ? "（时区未提供）":"")
 }
 static func safeURL(_ s:String)->String {guard let u=URLComponents(string:s),u.scheme=="https",["x.com","twitter.com","aihot.news"].contains(u.host ?? ""),u.user==nil,u.password==nil,u.port==nil || u.port==443 else {return "https://aihot.news/codex-reset"};return s}
 static func parse(_ data:Data,now:Date=Date()) throws -> NewsSnapshot {
  guard data.count<=2_097_152,let root=try JSONSerialization.jsonObject(with:data) as? [String:Any],root["schemaVersion"] as? Int==1,let events=root["events"] as? [[String:Any]],events.count<=1000 else {throw QuotaError(message:"消息格式暂时无法读取")}
  func text(_ d:[String:Any],_ key:String)->String {String((d[key] as? String ?? "").prefix(600))}
  let today=text(root,"today").isEmpty ? day(now):text(root,"today")
  var items:[NewsItem]=[],history:[NewsItem]=[],seen=Set<String>()
  for e in events {
   let id=text(e,"id");if id.isEmpty || seen.contains(id){continue};seen.insert(id)
   let p=e["presentation"] as? [String:Any] ?? [:],s=e["schedule"] as? [String:Any] ?? [:],estimate=e["estimate"] as? [String:Any] ?? [:]
   let status=text(p,"status").isEmpty ? text(e,"status"):text(p,"status")
   let type=text(e,"type"),occurred=text(e,"occurredOn"),basis=text(e,"confirmationBasis")
   var time=""
   if status=="confirmed" {
    if !text(e,"confirmedAt").isEmpty {time="来源记录时间："+date(text(e,"confirmedAt"))}
    else if !occurred.isEmpty {time="发生日期："+date(occurred)+"（北京时间）"}
   } else if status != "withdrawn" && !text(estimate,"from").isEmpty {time="预计窗口（AIHOT 推算）："+date(text(estimate,"from"))+(text(estimate,"through").isEmpty ? "":"–"+date(text(estimate,"through")))}
   else if status != "withdrawn" && !text(s,"label").isEmpty {time="预告时间："+text(s,"label")}
   else if status != "withdrawn" && !text(s,"from").isEmpty {time="预告时间："+date(text(s,"from"))}
   if p["timeInferred"] as? Bool==true && !time.isEmpty {time += "（推算）"}
   let material:[String:Any] = ["type":type,"status":status,"presentation":p,"schedule":s,"estimate":estimate,"occurred":occurred,"basis":basis,"confirmedAt":text(e,"confirmedAt")]
   let bytes=try JSONSerialization.data(withJSONObject:material,options:.sortedKeys)
   let version=SHA256.hash(data:bytes).map{String(format:"%02x",$0)}.joined()
   let n=NewsItem(id:id,version:version,type:type,status:status,scope:([text(p,"audienceZh").isEmpty ? "未明确":text(p,"audienceZh"),text(p,"productsZh")].filter{!$0.isEmpty}.joined(separator:" · ")),time:time,url:safeURL(text(e,"url")),occurred:occurred,basis:basis,explicit:p["kindExplicit"] as? Bool==true,inferred:p["timeInferred"] as? Bool==true,confirmed:text(e,"confirmedAt"))
   if NewsHistory.stamp(n,now:now) != nil {history.append(n)}
   let eventDay=[occurred,text(e,"confirmedAt"),text(estimate,"from"),text(s,"from"),text(e,"createdAt")].first{!$0.isEmpty}.map{NewsHistory.calendarDay($0)} ?? ""
   if eventDay==today || ["announced","in_progress","expired_unconfirmed","likely_completed"].contains(status) {items.append(n)}
  }
  for a in root["activities"] as? [[String:Any]] ?? [] where text(a,"kind")=="event_update" && text(a,"action")=="withdraw" && a["statusChanged"] as? Bool==true && String(text(a,"publishedAt").prefix(10))==today {
   for id in a["eventIds"] as? [String] ?? [] {
    history.removeAll{$0.id==id}
    if !items.contains(where:{$0.id==id}) { items.append(NewsItem(id:id,version:"withdraw:"+text(a,"id"),type:"",status:"withdrawn",scope:"",time:"",url:"https://aihot.news/codex-reset",occurred:"",basis:"",explicit:false,inferred:false)) }
   }
  }
  guard items.count<=100 else {throw QuotaError(message:"消息数量异常")}
  return NewsSnapshot(items:items,history:history.sorted{NewsHistory.newer($0,$1,now:now)}.first,checked:text(root,"checkedAt"),day:today)
 }
}
final class NewsClient {
 static func read(_ complete:@escaping(Result<NewsSnapshot,Error>)->Void) {
  let config=URLSessionConfiguration.ephemeral;config.timeoutIntervalForRequest=15;config.httpCookieStorage=nil;config.urlCredentialStorage=nil
  let session=URLSession(configuration:config)
  var request=URLRequest(url:URL(string:"https://aihot.news/api/v1/codex-resets/recent")!);request.setValue("CodexUsageMac/0.11.6",forHTTPHeaderField:"User-Agent")
  session.dataTask(with:request){data,response,error in
   defer{session.finishTasksAndInvalidate()}
   guard error==nil,let response=response as? HTTPURLResponse,response.statusCode==200,let data else {complete(.failure(QuotaError(message:"消息暂时无法更新")));return}
   complete(Result{try NewsParser.parse(data)})
  }.resume()
 }
}
