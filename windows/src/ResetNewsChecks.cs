using System;
using System.Collections.Generic;
using System.IO;

internal static class ResetNewsChecks
{
    internal static ResetNewsSnapshot Sample(string id,string status,string scope,string body)
    {
        return ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {
            schemaVersion=1, today="2026-10-03", checkedAt="2026-10-03T16:00:00+08:00",monitor=new {status="healthy"},
            events=new[]{new { id=id,type="direct_reset",displayLabel="额度重置（测试消息）",status=status,
                presentation=new {status=status,audienceZh=scope,productsZh="Codex",kindExplicit=true},
                confirmedAt=status=="confirmed"?"2026-10-03T18:15:00+08:00":null,confirmationBasis=status=="confirmed"?"source_post":null,
                schedule=new {from="2026-10-03T17:00:00+08:00",through="2026-10-03T18:00:00+08:00",label="预计今天 17:00–18:00"},
                posts=new[]{new {text=body,publishedAt="2026-10-03T16:00:00+08:00",url="https://aihot.news/codex-reset"}} }}
        }));
    }
    internal static int Run()
    {
        var lines=new List<string>(); int failed=0;
        Action<string,bool> check=(name,ok)=>{lines.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++;};
        string dir=Path.Combine(Path.GetTempPath(),"CodexUsageMini-NewsChecks-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); string file=Path.Combine(dir,"state.json");
        var state=new ResetNewsState(file);
        var initial=Sample("a","announced","Plus 用户","测试预告");
        state.Apply(initial,"etag1");
        check("first sync establishes quiet baseline",state.Cache.Initialized && !state.HasUnread);
        state.Apply(Sample("a","announced","Plus 用户","文字改写，不改变含义"),"etag2");
        check("body or ETag change alone does not alert",!state.HasUnread);
        var second=Sample("b","announced","Plus 用户","另一事件的相似预告");
        second.Items.Add(initial.Items[0]); state.Apply(second,"etag3");
        check("new event creates unread badge",state.HasUnread && state.Unread(second.Items[0]));
        state=new ResetNewsState(file);
        check("unread survives restart",state.HasUnread);
        state.MarkRead(second.Items[0].Id,second.Items[0].Version);
        check("reading version clears badge",!state.HasUnread);
        state=new ResetNewsState(file); check("read persists across restart",!state.HasUnread);
        var confirmed=Sample("a","confirmed","Plus 用户","已确认"); state.Apply(confirmed,"etag4");
        check("confirmation alerts again",state.HasUnread);
        state.MarkRead("a",initial.Items[0].Version);
        check("acknowledging old visible version preserves newer unread",state.HasUnread);
        state.MarkAllRead(); check("mark all clears current versions",!state.HasUnread);
        state.Apply(Sample("a","confirmed","Pro 用户","范围修正"),"etag5");
        check("scope change alerts",state.HasUnread);
        string version=state.Cache.Snapshot.Items[0].Version;
        state.Error="离线，显示缓存";
        check("failure retains content and read state",state.HasUnread && state.Cache.Snapshot.Items.Count==1 && state.Summary.Contains("离线"));
        state.Success(); check("304 success preserves unread and clears error",state.HasUnread && state.Error==null && state.Cache.Snapshot.Items[0].Version==version);
        state.MarkAllRead(); state.Apply(Sample("regrouped","confirmed","Pro 用户","重新分组"),"etag6");
        check("identical regrouped event stays read",!state.HasUnread);
        var withdrawal=ResetNewsParser.Parse("{\"schemaVersion\":1,\"today\":\"2026-10-03\",\"events\":[],\"activities\":[{\"id\":\"w1\",\"publishedAt\":\"2026-10-03T16:00:00+08:00\",\"kind\":\"event_update\",\"action\":\"withdraw\",\"statusChanged\":true,\"eventIds\":[\"regrouped\"],\"text\":\"已撤回\"}]}");
        state.Apply(withdrawal,"etag7");
        check("explicit withdrawal retained and unread",state.HasUnread && withdrawal.Items[0].Status=="withdrawn");
        state.Apply(ResetNewsParser.Parse("{\"schemaVersion\":1,\"events\":[],\"activities\":[{\"kind\":\"related\",\"action\":\"withdraw\",\"statusChanged\":false,\"eventIds\":[\"a\"]}]}"),"etag8");
        check("related activity and ageing-out do not fabricate withdrawal",state.Cache.Snapshot.Items.Count==0 && !state.HasUnread);
        bool rejected=false; try { ResetNewsParser.Parse("{\"schemaVersion\":2,\"events\":[]}"); } catch(InvalidDataException) { rejected=true; }
        check("unknown schema rejected",rejected);
        check("source HTTPS link accepted",ResetNewsParser.SafeLink("https://x.com/thsottiaux/status/1"));
        check("unsafe links rejected",!ResetNewsParser.SafeLink("file:///C:/test") && !ResetNewsParser.SafeLink("https://x.com.evil.test/") && !ResetNewsParser.SafeLink("https://user@x.com/"));
        File.WriteAllText(file,"broken json"); state=new ResetNewsState(file);
        check("corrupt cache reported",state.StorageError!=null && !state.Cache.Initialized);
        state.Apply(initial,null); check("corrupt cache recovery rebaselines quietly",!state.HasUnread && state.StorageError==null);
        state.Cache.Snapshot.Monitor="delayed"; check("upstream delay is visible",state.Summary.Contains("上游采集有延迟"));
        Func<string,string,string,string,object> entry=(id,status,confirmation,occurred)=>new {
            id=id,status=status,createdAt="2026-09-27T01:00:00+08:00",confirmedAt=confirmation,occurredOn=occurred,
            posts=new[]{new {text="当前主原帖",publishedAt="2026-10-03T12:30:00+08:00"},new {text="折叠旧原帖",publishedAt="2026-09-27T01:00:00+08:00"}}
        };
        object[] five={entry("today1","confirmed","2026-10-03T05:00:00+08:00",null),entry("today2","confirmed","2026-10-03T12:00:00+08:00",null),
            entry("old1","confirmed","2026-09-30T10:00:00+08:00",null),entry("old2","confirmed",null,"2026-09-30"),entry("old3","confirmed","2026-09-27T02:00:00+08:00",null)};
        var filtered=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=five}));
        check("five recent events reduce to two current-day events",filtered.Items.Count==2 && filtered.Items.TrueForAll(x=>x.Id.StartsWith("today")));
        check("post bodies are not imported and newest source timestamp is retained",filtered.Items.TrueForAll(x=>x.Body==null && x.Published=="2026-10-03T12:30:00+08:00"));
        var pending=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=new[]{entry("pending","announced",null,null),entry("expired","expired_unconfirmed",null,null),entry("old","confirmed","2026-09-27T02:00:00+08:00",null)}}));
        check("outstanding announcements survive date filtering",pending.Items.Count==2);
        var precedence=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=new[]{entry("receipt","confirmed","2026-10-03T12:00:00+08:00","2026-09-30")}}));
        check("verified occurrence date takes precedence over confirmation date",precedence.Items.Count==0);
        state.Apply(filtered,"before-midnight"); state.MarkAllRead();
        filtered.DisplayPolicy=0; File.WriteAllText(file,ResetNewsParser.Json.Serialize(state.Cache));
        state=new ResetNewsState(file);
        check("old policy cache forces refetch and hides history",state.Cache.ETag==null && state.Cache.Snapshot.Items.Count==0);
        var migrated=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=five}));
        state.Apply(migrated,"new-policy"); check("upgrade preserves read versions without false badge",!state.HasUnread && state.Cache.Snapshot.Items.Count==2);
        var tomorrow=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-04",events=five}));
        state.Apply(tomorrow,"next-day"); check("day rollover removes old events and their badge",state.Cache.Snapshot.Day=="2026-10-04" && state.Cache.Snapshot.Items.Count==0 && !state.HasUnread);
        var direct=Sample("compact","confirmed","Pro 500 用户","你已收到 7 张卡，有效期 30 天，额度已恢复 100%").Items[0];
        check("explicit direct-reset type is source-qualified",ResetNewsParser.TypeLabel(direct)=="额度重置" && ResetNewsParser.StatusLabel(direct,DateTimeOffset.Now)=="已落地（来源确认）");
        check("confirmation timestamp is not the retained announced execution window",ResetNewsParser.KeyTime(direct).Contains("18:15") && !ResetNewsParser.KeyTime(direct).Contains("17:00"));
        check("body cannot fabricate quantity validity or personal account receipt",direct.Body==null && !ResetNewsParser.KeyTime(direct).Contains("100%"));
        direct.KindExplicit=false;
        check("legacy event type does not override an unspecified reset form",ResetNewsParser.TypeLabel(direct)=="重置（形式未明确）");
        direct.KindExplicit=true; direct.Type="reset_credit";
        check("explicit reset-credit grant is distinct from quota reset",ResetNewsParser.TypeLabel(direct)=="发放重置卡");
        direct.ConfirmationBasis="";
        check("missing confirmation evidence is not presented as confirmed landing",ResetNewsParser.StatusLabel(direct,DateTimeOffset.Now).StartsWith("尚未确认"));
        direct.ConfirmationBasis="receipt_review"; direct.ConfirmedAt=""; direct.OccurredOn="2026-10-03";
        check("reviewed occurrence day does not invent an exact landing time",ResetNewsParser.StatusLabel(direct,DateTimeOffset.Now).Contains("来源核验") && ResetNewsParser.KeyTime(direct)=="发生日期：10月3日（北京时间）");
        check("a date-only source record never gains midnight",ResetNewsParser.LocalTime("2026-10-03")=="10月3日");
        check("a timestamp without a timezone retains that uncertainty",ResetNewsParser.LocalTime("2026-10-03T18:15:00")=="2026-10-03T18:15:00（时区未提供）");
        check("invalid source dates remain unclear",ResetNewsParser.LocalTime("2026-02-30")=="时间未明确");
        var recorded=new ResetNewsItem {Status="confirmed",ConfirmedAt="2026-10-03T18:15:00+08:00",ConfirmationBasis="source_post"};
        string recordedTime="来源记录时间："+DateTimeOffset.Parse("2026-10-03T18:15:00+08:00").ToLocalTime().ToString("M月d日 HH:mm");
        check("source-post confirmation time is a source record not an execution receipt",ResetNewsParser.KeyTime(recorded)==recordedTime);
        recorded.ConfirmationBasis="receipt_review";
        check("receipt-review timestamps are not relabelled as tool verification",ResetNewsParser.KeyTime(recorded)==recordedTime);
        recorded.ConfirmedAt="2026-10-03";
        check("a date-only confirmedAt retains only its source-record date",ResetNewsParser.KeyTime(recorded)=="来源记录时间：10月3日");
        recorded.ConfirmedAt="2026-10-03T18:15:00";
        check("source-record timestamps with no timezone are not interpreted locally",ResetNewsParser.KeyTime(recorded)=="来源记录时间：2026-10-03T18:15:00（时区未提供）");
        direct.TimeInferred=true;
        check("an inferred occurrence date is explicitly labelled and excluded from history",ResetNewsParser.KeyTime(direct)=="发生日期（推算）：10月3日（北京时间）" && ResetNewsParser.HistoryLabel(direct)=="");
        direct.TimeInferred=false;
        var announced=Sample("pending-display","announced","Plus 用户","预告").Items[0];
        check("a passed announced window remains unconfirmed",ResetNewsParser.StatusLabel(announced,DateTimeOffset.Parse("2026-10-03T20:00:00+08:00"))=="尚未确认（预计时间已过）");
        announced.Status="likely_completed";
        check("likely-completed presentation never becomes source confirmation",ResetNewsParser.StatusLabel(announced,DateTimeOffset.Now).StartsWith("尚未确认"));
        var qualifiers=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new { schemaVersion=1,today="2026-10-03",events=new[]{new {
            id="qualified",type="direct_reset",status="confirmed",confirmedAt="2026-10-03T12:00:00+08:00",confirmationBasis="source_post",scope="所有付费订阅",
            presentation=new {status="confirmed",audienceZh="Pro 500 用户",productsZh="Codex",kindExplicit=false},posts=new object[0]
        }} }));
        check("compact display uses current audience product and form qualifiers",qualifiers.Items[0].Scope=="Pro 500 用户" && qualifiers.Items[0].Products=="Codex" && ResetNewsParser.TypeLabel(qualifiers.Items[0]).Contains("未明确"));
        var unspecified=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=new[]{new {
            id="unspecified",type="direct_reset",status="confirmed",confirmedAt="2026-10-03T12:00:00+08:00",scope="所有用户",posts=new object[0]
        }} }));
        check("absent presentation does not imply all accounts or an explicit reset form",unspecified.Items[0].Scope=="" && ResetNewsParser.TypeLabel(unspecified.Items[0]).Contains("未明确"));
        check("missing confirmation basis keeps status and timestamp unconfirmed",ResetNewsParser.StatusLabel(unspecified.Items[0],DateTimeOffset.Now).StartsWith("尚未确认") && ResetNewsParser.KeyTime(unspecified.Items[0]).StartsWith("来源记录时间："));
        Func<string,ResetNewsSnapshot> estimated=from=>ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=new[]{new {
            id="estimated",type="direct_reset",status="announced",createdAt="2026-10-03T12:00:00+08:00",
            presentation=new {status="announced",audienceZh="Plus 用户",kindExplicit=true},
            estimate=new {from=from,through="2026-10-03T21:00:00+08:00",basis="model"},posts=new object[0]
        }} }));
        var estimatedFirst=estimated("2026-10-03T20:00:00+08:00"); var estimatedChanged=estimated("2026-10-03T20:30:00+08:00");
        check("AIHOT estimate is labelled as an estimate",ResetNewsParser.KeyTime(estimatedFirst.Items[0]).StartsWith("预计窗口（AIHOT 推算）："));
        check("estimate correction changes the semantic read version",estimatedFirst.Items[0].Version!=estimatedChanged.Items[0].Version);
        var likely=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {schemaVersion=1,today="2026-10-03",events=new[]{new {
            id="likely",type="direct_reset",status="announced",createdAt="2026-09-30T12:00:00+08:00",presentation=new {status="likely_completed",kindExplicit=true},posts=new object[0]
        }} }));
        check("an old unconfirmed likely-completed event remains available",likely.Items.Count==1 && ResetNewsParser.StatusLabel(likely.Items[0],DateTimeOffset.Now).StartsWith("尚未确认"));
        state.Apply(qualifiers,"compact-fields"); state.MarkAllRead();
        var legacy=ResetNewsParser.Json.Deserialize<ResetNewsCache>(ResetNewsParser.Json.Serialize(state.Cache));
        legacy.Snapshot.DisplayPolicy=1; legacy.Snapshot.Day=ResetNewsParser.BeijingToday;
        legacy.Snapshot.Items[0].Type=null; legacy.Snapshot.Items[0].KindExplicit=null; legacy.Snapshot.Items[0].Products=null;
        legacy.Snapshot.Items[0].ConfirmedAt=null; legacy.Snapshot.Items[0].OccurredOn=null; legacy.Snapshot.Items[0].ConfirmationBasis=null;
        legacy.ReadPolicy=0; legacy.Snapshot.Items[0].View=null;
        legacy.Snapshot.Items[0].Version="legacy-compact-version";
        legacy.Read[legacy.Snapshot.Items[0].Id]=legacy.Snapshot.Items[0].Version;
        File.WriteAllText(file,ResetNewsParser.Json.Serialize(legacy)); state=new ResetNewsState(file);
        check("v0.9.1 current-day cache is retained while forcing source refetch",state.Cache.ETag==null && state.Cache.Snapshot.Items.Count==1 && !state.HasUnread);
        check("missing legacy qualifiers remain unknown instead of claiming landing",ResetNewsParser.TypeLabel(state.Cache.Snapshot.Items[0]).Contains("未明确") && ResetNewsParser.StatusLabel(state.Cache.Snapshot.Items[0],DateTimeOffset.Now).StartsWith("尚未确认"));
        state.Error="离线，保留升级前消息";
        check("offline upgrade retains readable content and semantic read records",state.Cache.Snapshot.Items.Count==1 && state.Summary.Contains("离线") && state.Cache.Read[qualifiers.Items[0].Id]==state.Cache.Snapshot.Items[0].Version);
        state.Apply(qualifiers,"compact-refetched"); check("visible qualifier correction after compact cache upgrade remains unread",state.HasUnread && state.Cache.Snapshot.DisplayPolicy==3);
        legacy.Snapshot.Day="2000-01-01"; File.WriteAllText(file,ResetNewsParser.Json.Serialize(legacy)); state=new ResetNewsState(file);
        check("legacy cache from another day hides stale events but preserves read versions",state.Cache.ETag==null && state.Cache.Snapshot.Items.Count==0 && state.Cache.Read.ContainsKey(qualifiers.Items[0].Id));
        var visibleOnly=Sample("visible","announced","Plus 用户","第一条"); visibleOnly.Items.Add(Sample("not-visible","announced","Plus 用户","第二条").Items[0]);
        state.Apply(visibleOnly,"partial-view"); state.MarkRead(visibleOnly.Items[0].Id,visibleOnly.Items[0].Version);
        check("acknowledging one visible row leaves undisplayed versions unread",!state.Unread(visibleOnly.Items[0]) && state.Unread(visibleOnly.Items[1]) && state.HasUnread);
        var historyNow=DateTimeOffset.Parse("2026-10-04T12:00:00+08:00");
        Func<string,string,string,bool,string,string,bool,object> historical=(id,type,status,explicitKind,confirmedAt,occurredOn,inferred)=>new {
            id=id,type=type,status=status,createdAt="2026-09-27T01:00:00+08:00",updatedAt="2026-10-04T11:59:00+08:00",
            confirmedAt=confirmedAt,occurredOn=occurredOn,confirmationBasis="source_post",
            presentation=new {status=status,kindExplicit=explicitKind,timeInferred=inferred},
            schedule=new {from="2026-10-04T09:00:00+08:00",label="预计今天 09:00"},
            posts=new[]{new {publishedAt="2026-10-04T11:59:00+08:00"}}
        };
        Func<object[],ResetNewsSnapshot> historyParse=events=>ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {
            schemaVersion=1,today="2026-10-04",checkedAt="2026-10-04T11:30:00+08:00",events=events
        }),historyNow);
        var latestHistory=historyParse(new[]{
            historical("earlier-grant","reset_credit","confirmed",true,"2026-09-30T13:49:00+08:00","2026-09-30",false),
            historical("last-reset","direct_reset","confirmed",true,"2026-10-03T05:18:00+08:00","2026-10-03",false),
            historical("last-grant","reset_credit","confirmed",true,"2026-10-03T12:00:00+08:00","2026-10-03",false)
        });
        check("history is extracted before current-day filtering",latestHistory.Items.Count==0 && latestHistory.LatestHistory!=null);
        check("one latest confirmed event is selected across both explicit types",latestHistory.LatestHistory.Id=="last-grant" && ResetNewsParser.HistoryLabel(latestHistory.LatestHistory)=="上次发放重置卡");
        check("a late confirmation of an older occurrence cannot replace a newer occurrence",historyParse(new[]{
            historical("old-late-confirmation","direct_reset","confirmed",true,"2026-10-04T11:00:00+08:00","2026-09-30",false),
            historical("newer-occurrence","reset_credit","confirmed",true,"2026-10-03T12:00:00+08:00","2026-10-03",false)
        }).LatestHistory.Id=="newer-occurrence");
        check("same-day candidates select one stable record without claiming an execution time",latestHistory.LatestHistory.Id=="last-grant" &&
            ResetNewsParser.HistoryTime(latestHistory.LatestHistory)=="10月3日（发生日期）");
        var unknownHistory=historyParse(new[]{
            historical("unknown-form","direct_reset","confirmed",false,"2026-10-03T13:00:00+08:00","2026-10-03",false),
            historical("unknown-type","future_kind","confirmed",true,"2026-10-03T14:00:00+08:00","2026-10-03",false),
            historical("announced-history","direct_reset","announced",true,"2026-10-03T15:00:00+08:00","2026-10-03",false)
        });
        check("unknown forms unknown types and announcements never become history",unknownHistory.LatestHistory==null);
        check("an unknown explicit type is not labelled as a quota reset",ResetNewsParser.TypeLabel(new ResetNewsItem {Type="future_kind",KindExplicit=true,Status="confirmed"})=="消息（类型未明确）");
        check("created updated published and scheduled times cannot substitute for occurrence",historyParse(new[]{
            historical("no-occurrence","direct_reset","confirmed",true,null,null,false)
        }).LatestHistory==null);
        check("future confirmations cannot fabricate missing occurrences and future days are excluded",historyParse(new[]{
            historical("future-time","direct_reset","confirmed",true,"2026-10-04T12:01:00+08:00",null,false),
            historical("future-day","reset_credit","confirmed",true,null,"2026-10-05",false)
        }).LatestHistory==null);
        check("inferred dates are not explicit historical occurrences",historyParse(new[]{
            historical("inferred-day","reset_credit","confirmed",true,null,"2026-09-30",true)
        }).LatestHistory==null);
        check("reliable confirmation fallback preserves source meaning",historyParse(new[]{
            historical("date-in-timestamp","direct_reset","confirmed",true,"2026-09-30",null,false),
            historical("no-zone","direct_reset","confirmed",true,"2026-09-30T12:00:00",null,false),
            historical("exact-confirmation-only","direct_reset","confirmed",true,"2026-09-30T12:00:00+08:00",null,false)
        }).LatestHistory.Id=="exact-confirmation-only");
        var dateHistory=historyParse(new[]{historical("known-day","reset_credit","confirmed",true,null,"2026-09-30",false)});
        check("historical date-only precision is retained without midnight",dateHistory.LatestHistory!=null && ResetNewsParser.HistoryTime(dateHistory.LatestHistory)=="9月30日（发生日期）");
        var timestampHistory=historyParse(new[]{historical("known-day-with-confirmation","direct_reset","confirmed",true,"2026-10-03T05:18:00+08:00","2026-10-03",false)});
        check("confirmation HH:mm is not displayed as an occurrence time",ResetNewsParser.HistoryLabel(timestampHistory.LatestHistory)=="上次额度重置" &&
            ResetNewsParser.HistoryTime(timestampHistory.LatestHistory)=="10月3日（发生日期）");
        var missingBasis=historyParse(new object[]{new {id="no-basis",type="direct_reset",status="confirmed",confirmedAt="2026-10-03T05:18:00+08:00",occurredOn="2026-10-03",presentation=new {kindExplicit=true}}});
        check("history requires a known confirmation basis",missingBasis.LatestHistory==null);
        var withdrawnHistory=ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {
            schemaVersion=1,today="2026-10-04",events=new[]{
                historical("kept-history","direct_reset","confirmed",true,"2026-10-03T05:18:00+08:00","2026-10-02",false),
                historical("withdrawn-history","reset_credit","confirmed",true,"2026-10-03T12:00:00+08:00","2026-10-03",false)
            },activities=new[]{new {id="withdraw-history",kind="event_update",action="withdraw",statusChanged=true,publishedAt="2026-10-04T11:00:00+08:00",eventIds=new[]{"withdrawn-history"}}}
        }),historyNow);
        check("an explicit withdrawal removes its historical candidate",withdrawnHistory.LatestHistory.Id=="kept-history");
        var historyState=new ResetNewsState(Path.Combine(dir,"history-state.json")); historyState.Apply(timestampHistory,"history-first");
        historyState.Apply(latestHistory,"history-newer");
        check("historical summaries do not create unread message badges",!historyState.HasUnread && historyState.Cache.Snapshot.Items.Count==0);
        var checkAt=historyNow.AddMinutes(-12);
        check("same-local-day check time is a single compact line",ResetNewsParser.CheckTime(checkAt.ToString("o"),historyNow)=="消息检查于 "+checkAt.ToLocalTime().ToString("HH:mm"));
        checkAt=historyNow.AddDays(-1);
        check("cross-local-day check time includes its date",ResetNewsParser.CheckTime(checkAt.ToString("o"),historyNow)=="消息检查于 "+checkAt.ToLocalTime().ToString("M月d日 HH:mm"));
        check("missing check time is not replaced with local fetch time",ResetNewsParser.CheckTime(null,historyNow)=="" && ResetNewsParser.CheckTime("invalid",historyNow)=="");
        var summaryState=new ResetNewsState(Path.Combine(dir,"summary-state.json")); summaryState.Apply(timestampHistory,"summary-source");
        summaryState.Cache.LastSuccess="2099-01-01T00:00:00+08:00";
        string summary=summaryState.Summary;
        check("legacy summary identifies source check time without claiming verification or local fetch",summary.StartsWith("消息检查于 ") &&
            !summary.Contains("来源核验") && !summary.Contains("最近读取") && !summary.Contains("2099") && !summary.Contains("\n"));
        summaryState.Cache.Snapshot.VerifiedAt="";
        check("legacy summary does not invent check time when checkedAt is absent",summaryState.Summary=="");
        var policyTwo=ResetNewsParser.Json.Deserialize<ResetNewsCache>(ResetNewsParser.Json.Serialize(state.Cache));
        policyTwo.Snapshot.DisplayPolicy=2; policyTwo.Snapshot.Day=ResetNewsParser.BeijingToday;
        policyTwo.Snapshot.LatestHistory=latestHistory.LatestHistory; policyTwo.ETag="old-policy-etag";
        string previouslyRead=policyTwo.Read[visibleOnly.Items[0].Id];
        File.WriteAllText(file,ResetNewsParser.Json.Serialize(policyTwo)); state=new ResetNewsState(file);
        check("policy two preserves same-day messages and semantic read versions while forcing refetch",state.Cache.Snapshot.Items.Count==2 &&
            state.Cache.ETag==null && state.Cache.Read[visibleOnly.Items[0].Id]==previouslyRead && !state.Unread(state.Cache.Snapshot.Items[0]) && state.Unread(state.Cache.Snapshot.Items[1]));
        check("legacy cache migration never invents a historical summary",state.Cache.Snapshot.LatestHistory==null);
        lines.Add("Failed="+failed); File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-check.txt"),lines.ToArray());
        return failed==0?0:1;
    }
    internal static int Probe()
    {
        var lines=new List<string>(); bool ok=false;
        using(var client=new ResetNewsClient()) {
            var first=client.Read(null);
            lines.Add("CheckedAt="+DateTimeOffset.Now.ToString("o"));
            lines.Add("FirstRead="+(first.Snapshot!=null?"200":"failed: "+first.Error));
            if(first.Diagnostic!=null) lines.Add(first.Diagnostic);
            if(first.Snapshot!=null) {
                lines.Add("Events="+first.Snapshot.Items.Count); lines.Add("Upstream="+first.Snapshot.Monitor);
                lines.Add("ETagPresent="+!String.IsNullOrEmpty(first.ETag));
                var next=client.Read(first.ETag);
                lines.Add("ConditionalRead="+(next.Unchanged?"304":next.Snapshot!=null?"200 changed":"failed: "+next.Error));
                ok=next.Unchanged || next.Snapshot!=null;
            }
        }
        File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-probe.txt"),lines.ToArray());
        return ok?0:1;
    }
}
