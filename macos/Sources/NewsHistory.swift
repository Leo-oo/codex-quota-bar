import Foundation
// History is not an unread activity. All calendar/format operations use UTC+8.
enum NewsHistory {
 static let zone=TimeZone(secondsFromGMT:28800)!
 struct Stamp {let day:Date;let ordering:Date;let value:Date;let confirmation:Bool;let exact:Bool}
 static func dayDate(_ value:String)->Date? {
  guard value.count==10 else{return nil}
  let f=DateFormatter();f.locale=Locale(identifier:"en_US_POSIX");f.calendar=Calendar(identifier:.gregorian);f.timeZone=zone;f.dateFormat="yyyy-MM-dd";f.isLenient=false
  guard let d=f.date(from:value),f.string(from:d)==value else{return nil};return d
 }
 static func instant(_ value:String)->Date? {
  guard value.range(of:#"(Z|[+-][0-9]{2}:[0-9]{2})$"#,options:.regularExpression) != nil else{return nil}
  let f=ISO8601DateFormatter();f.formatOptions=[.withInternetDateTime,.withFractionalSeconds]
  if let d=f.date(from:value){return d};f.formatOptions=[.withInternetDateTime];return f.date(from:value)
 }
 static func calendarDay(_ value:String)->String {if let d=instant(value){return NewsParser.day(d)};return String(value.prefix(10))}
 static func stamp(_ n:NewsItem,now:Date=Date())->Stamp? {
  guard n.status=="confirmed",n.explicit,!n.inferred,n.knownType,["source_post","receipt_review"].contains(n.basis) else{return nil}
  let raw=n.confirmed ?? "",precise=instant(raw),recorded=precise ?? dayDate(raw)
  if let occurrence=dayDate(n.occurred) {
   guard occurrence<=now else{return nil}
   // Never elevate an occurrence day with a later confirmation day.
   let tie=precise.flatMap{$0<=now && NewsParser.day($0)==n.occurred ? $0:nil} ?? occurrence
   return Stamp(day:occurrence,ordering:tie,value:occurrence,confirmation:false,exact:false)
  }
  guard let recorded,recorded<=now,let day=dayDate(NewsParser.day(recorded)) else{return nil}
  return Stamp(day:day,ordering:recorded,value:recorded,confirmation:true,exact:precise != nil)
 }
 static func newer(_ a:NewsItem,_ b:NewsItem,now:Date)->Bool {
  guard let x=stamp(a,now:now),let y=stamp(b,now:now) else{return false}
  if x.day != y.day{return x.day>y.day};if x.ordering != y.ordering{return x.ordering>y.ordering};return a.id<b.id
 }
 static func text(_ n:NewsItem,now:Date=Date())->String? {
  guard let s=stamp(n,now:now) else{return nil}
  let f=DateFormatter();f.locale=Locale(identifier:"zh_CN");f.timeZone=zone;f.dateFormat=s.exact ? "M月d日 HH:mm":"M月d日"
  let source=s.confirmation ? (s.exact ? "确认时间":"确认日期"):"发生日期"
  return (n.type=="direct_reset" ? "上次额度重置：":"上次发放重置卡：")+f.string(from:s.value)+"（"+source+"）"
 }
}
