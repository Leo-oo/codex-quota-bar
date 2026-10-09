using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Web.Script.Serialization;

// Own synthetic data, own files and unshown controls only. No Program.Main,
// network client, UsageBar, global input, native hit test or account/cache read.
internal static class NewsUnreadRegression118
{
    sealed class Result
    {
        public string name;
        public bool passed;
        public object details;
    }
    static readonly List<Result> results=new List<Result>();
    static readonly List<object> observations=new List<object>();
    static readonly List<object> knownIssues=new List<object>();
    static string output, fixtures;
    static int failed;
    static DateTimeOffset clock;
    static string today, yesterday;
    static void Check(string name,bool ok,object details=null)
    { results.Add(new Result {name=name,passed=ok,details=details}); if(!ok) failed++; }
    static string StatePath(string name) { return Path.Combine(fixtures,name+".json"); }
    static ResetNewsSnapshot Parse(object[] events,string day=null)
    {
        return ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {
            schemaVersion=1,today=day??today,checkedAt=(day??today)+"T14:10:00+08:00",
            monitor=new {status="healthy"},events=events
        }),clock);
    }
    static object Event(string id,string status="announced",string scope="Plus 用户",string label="今天20:00–21:00",
        string from=null,string through=null,string occurred=null,string published=null,string product="Codex")
    {
        return new {
            id=id,type="direct_reset",status=status,createdAt=today+"T09:00:00+08:00",
            confirmedAt=status=="confirmed"?today+"T11:00:00+08:00":null,
            occurredOn=occurred,confirmationBasis=status=="confirmed"?"source_post":null,
            presentation=new {status=status,audienceZh=scope,productsZh=product,kindExplicit=true},
            schedule=new {label=label,from=from,through=through},
            posts=new[]{new {text="Synthetic fixture only",publishedAt=published??today+"T09:00:00+08:00"}}
        };
    }
    static ResetNewsSnapshot One(string id,string status="announced",string scope="Plus 用户",string label="今天20:00–21:00",
        string from=null,string through=null,string occurred=null,string published=null,string product="Codex")
    { return Parse(new[]{Event(id,status,scope,label,from,through,occurred,published,product)}); }
    static string PreviewText(Form owner,ResetNewsSnapshot snapshot)
    {
        using(var card=new ResetNewsCard(delegate(string id,string version){throw new InvalidOperationException("Preview must not acknowledge.");},delegate{})) {
            card.PreparePreview(owner,snapshot,Appearance.Palette(false),1.25,clock);
            return card.VisibleText;
        }
    }
    static void StateCases()
    {
        var state=new ResetNewsState(StatePath("empty-residue"));
        state.Cache.Read["retired-synthetic-id"]="retired-synthetic-version";
        state.Apply(Parse(new object[0]),"empty");
        Check("empty snapshot with read residues has no unread badge",!state.HasUnread && state.Cache.Read.Count==1);
        state=new ResetNewsState(StatePath("empty-residue"));
        Check("empty snapshot remains quiet after own-file restart",!state.HasUnread && state.Cache.Snapshot.Items.Count==0 && state.Cache.Read.Count==1);

        state=new ResetNewsState(StatePath("initial"));
        var a=One("a",from:today+"T20:00:00+08:00",through:today+"T21:00:00+08:00");
        state.Apply(a,"initial");
        Check("first successful nonempty fetch creates quiet baseline",state.Cache.Initialized && !state.HasUnread && state.Cache.Read[a.Items[0].Id]==a.Items[0].Version);
        string version=a.Items[0].Version;
        var unchanged=One("a",from:today+"T20:00:00+08:00",through:today+"T21:00:00+08:00");
        unchanged.VerifiedAt=today+"T14:30:00+08:00";
        state.Apply(unchanged,"different-etag");
        Check("check time and ETag alone do not create unread",!state.HasUnread && unchanged.Items[0].Version==version);
        var both=Parse(new[]{Event("a",from:today+"T20:00:00+08:00",through:today+"T21:00:00+08:00"),Event("b",scope:"Pro 用户")});
        state.Apply(both,"new-id");
        var existingA=both.Items.Find(item=>item.Id=="a");
        var addedB=both.Items.Find(item=>item.Id=="b");
        Check("a/b fixture lookup resolves distinct IDs and expected versions",existingA!=null && addedB!=null &&
            existingA.Id=="a" && addedB.Id=="b" && existingA.Version==version && addedB.Version!=version);
        Check("new ID alongside existing event becomes unread",!state.Unread(existingA) && state.Unread(addedB) && state.HasUnread);
        state.MarkRead(addedB.Id,addedB.Version);
        Check("acknowledging the current event version clears its badge",!state.HasUnread &&
            state.Cache.Read[addedB.Id]==addedB.Version && state.Cache.Read[existingA.Id]==existingA.Version);
        state=new ResetNewsState(StatePath("initial"));
        Check("version read record persists in own state file",!state.HasUnread &&
            state.Cache.Read[addedB.Id]==addedB.Version && state.Cache.Read[existingA.Id]==existingA.Version);
        var confirmed=One("a","confirmed",occurred:today);
        state.Apply(confirmed,"confirmed");
        Check("announced to confirmed transition becomes unread",state.HasUnread && state.Unread(confirmed.Items[0]));
        state.MarkRead("a",version);
        Check("old frozen-version acknowledgement does not read the new version",state.HasUnread && state.Cache.Read["a"]==version);
        state.MarkRead("a",confirmed.Items[0].Version);
        state.Apply(Parse(new object[0]),"aged-out");
        Check("nonempty to empty drops badge without deleting read residues",!state.HasUnread && state.Cache.Read.Count==2);
        state=new ResetNewsState(StatePath("initial"));
        Check("empty after replacement persists across own-file restart",!state.HasUnread && state.Cache.Snapshot.Items.Count==0);

        var emptyBaseline=new ResetNewsState(StatePath("empty-baseline"));
        emptyBaseline.Apply(Parse(new object[0]),"first-empty");
        emptyBaseline.Apply(One("after-empty"),"new-after-empty");
        Check("event after an initialized empty baseline is unread",emptyBaseline.HasUnread);

        var history=Parse(new[]{Event("history","confirmed",occurred:yesterday)});
        var historyState=new ResetNewsState(StatePath("history"));
        historyState.Apply(history,"history");
        Check("explicit prior-day history is separate from current unread items",history.Items.Count==0 && history.LatestHistory!=null && !historyState.HasUnread);

        var dayState=new ResetNewsState(StatePath("rollover"));
        dayState.Apply(Parse(new object[0]),"quiet-baseline");
        var priorDay=One("yesterday-event","confirmed",occurred:yesterday);
        // Supply a real previous source calendar snapshot, then simulate 304/failure.
        priorDay.Day=yesterday; priorDay.VerifiedAt=yesterday+"T14:10:00+08:00"; priorDay.Items.Add(history.LatestHistory);
        dayState.Apply(priorDay,"old-day");
        bool priorUnread=dayState.HasUnread;
        dayState.Error="Synthetic offline failure"; dayState.Success();
        Check("304-like success preserves cached content and unread without inventing source check time",
            priorUnread && dayState.HasUnread && dayState.Cache.Snapshot.Day==yesterday && dayState.Error==null &&
            dayState.Cache.Snapshot.VerifiedAt==priorDay.VerifiedAt);
        dayState.Error="Synthetic offline failure";
        Check("failure retains marked cache rather than silently acknowledging",dayState.HasUnread && dayState.Cache.Snapshot.Items.Count==1);
        var retained=new ResetNewsState(StatePath("rollover"));
        Check("policy-three old-day cache retains unread and same display list on restart",
            retained.HasUnread && retained.Cache.Snapshot.Items.Count==1 && retained.Cache.Snapshot.Day==yesterday);
        dayState.Apply(history,"fresh-current-day");
        Check("fresh source current-day filtering clears an aged-out event badge",!dayState.HasUnread && dayState.Cache.Snapshot.Items.Count==0);
    }
    static Dictionary<string,object> EventObject(object value)
    { return ResetNewsParser.Obj(ResetNewsParser.Json.DeserializeObject(ResetNewsParser.Json.Serialize(value))); }
    static ResetNewsSnapshot At(object[] events,DateTimeOffset now)
    {
        return ResetNewsParser.Parse(ResetNewsParser.Json.Serialize(new {
            schemaVersion=1,today=today,checkedAt=today+"T14:10:00+08:00",
            monitor=new {status="healthy"},events=events
        }),now);
    }
    static string Activity(ResetNewsItem item)
    {
        var view=item.View;
        return ResetNewsParser.Json.Serialize(new[]{view.Title,view.Status,view.Scope,view.TimeLabel,view.TimeValue});
    }
    static ResetNewsItem Find(ResetNewsSnapshot snapshot,string id)
    { return snapshot.Items.Find(item=>item.Id==id); }
    static void PairCase(Form owner,string name,ResetNewsSnapshot first,ResetNewsSnapshot next,bool visibleChange)
    {
        var state=new ResetNewsState(StatePath(name));
        state.Apply(first,"baseline"); state.Apply(next,"changed-source");
        string firstText=PreviewText(owner,first),nextText=PreviewText(owner,next);
        var a=first.Items[0]; var b=next.Items[0];
        Check(name,(firstText!=nextText)==visibleChange && (a.Version!=b.Version)==visibleChange &&
            state.HasUnread==visibleChange && state.ContentChanged==visibleChange &&
            a.Version==ResetNewsPresentation.Version(a.View) && b.Version==ResetNewsPresentation.Version(b.View),
            new {expectedVisibleChange=visibleChange,displayChanged=firstText!=nextText,
                versionChanged=a.Version!=b.Version,unreadAfterChange=state.HasUnread,contentChanged=state.ContentChanged,
                firstVersion=a.Version,nextVersion=b.Version,firstDisplay=firstText,nextDisplay=nextText});
    }
    static void FingerprintCases(Form owner)
    {
        var first=One("label-only",label:"今天20:00–21:00");
        var changed=One("label-only",label:"今天21:00–22:00");
        var state=new ResetNewsState(StatePath("semantic-label"));
        state.Apply(first,"first-label"); state.Apply(changed,"changed-label");
        bool visibleChanged=PreviewText(owner,first)!=PreviewText(owner,changed);
        Check("source-only schedule label changes the actual displayed key time",visibleChanged);
        Check("displayed schedule label correction changes shared version and becomes unread",
            visibleChanged && first.Items[0].Version!=changed.Items[0].Version && state.HasUnread && state.ContentChanged,
            new {firstDisplay=PreviewText(owner,first),nextDisplay=PreviewText(owner,changed),
                firstVersion=first.Items[0].Version,nextVersion=changed.Items[0].Version,unreadAfterChange=state.HasUnread});
        var scope=One("scope",scope:"Plus 用户");
        var corrected=One("scope",scope:"Pro 用户");
        state=new ResetNewsState(StatePath("semantic-scope")); state.Apply(scope,"a"); state.Apply(corrected,"b");
        Check("visible audience correction changes version and alerts",scope.Items[0].Version!=corrected.Items[0].Version && state.HasUnread);

        PairCase(owner,"displayed publication fallback correction alerts",
            One("fallback-publication",label:null,published:today+"T09:00:00+08:00"),
            One("fallback-publication",label:null,published:today+"T10:00:00+08:00"),true);
        PairCase(owner,"normalized audience whitespace stays read",
            One("scope-space",scope:"Plus 用户"),One("scope-space",scope:"  Plus  用户  "),false);
        PairCase(owner,"normalized product whitespace stays read",
            One("product-space",product:"Codex App"),One("product-space",product:"  Codex   App "),false);
        PairCase(owner,"confirmed hidden announcement schedule stays read",
            One("confirmed-schedule","confirmed",from:today+"T09:00:00+08:00",through:today+"T10:00:00+08:00",occurred:today),
            One("confirmed-schedule","confirmed",from:today+"T20:00:00+08:00",through:today+"T21:00:00+08:00",occurred:today),false);

        var absent=EventObject(Event("estimate-empty"));
        var empty=EventObject(Event("estimate-empty")); empty["estimate"]=new Dictionary<string,object>();
        PairCase(owner,"absent versus empty estimate has identical display and stays read",Parse(new object[]{absent}),Parse(new object[]{empty}),false);
        var metadataA=EventObject(Event("metadata"));
        var metadataB=EventObject(Event("metadata"));
        metadataB["displayLabel"]="Changed hidden source heading";
        metadataB["url"]="https://aihot.news/another-public-path";
        metadataB["posts"]=new[]{new {text="Different synthetic undisplayed body",publishedAt=today+"T10:00:00+08:00"}};
        PairCase(owner,"undisplayed title URL body and nonfallback publication stay read",Parse(new object[]{metadataA}),Parse(new object[]{metadataB}),false);
        var basisA=EventObject(Event("basis","confirmed",occurred:today));
        var basisB=EventObject(Event("basis","confirmed",occurred:today)); basisB["confirmationBasis"]="receipt_review";
        PairCase(owner,"equivalent explicit confirmation bases keep the same displayed status and stay read",Parse(new object[]{basisA}),Parse(new object[]{basisB}),false);
        var timeA=EventObject(Event("confirmed-time","confirmed",occurred:today));
        var timeB=EventObject(Event("confirmed-time","confirmed",occurred:today)); timeB["confirmedAt"]=today+"T12:00:00+08:00";
        PairCase(owner,"displayed source record time correction alerts",Parse(new object[]{timeA}),Parse(new object[]{timeB}),true);
        var kindA=EventObject(Event("kind"));
        var kindB=EventObject(Event("kind")); kindB["type"]="reset_credit";
        PairCase(owner,"displayed activity form correction alerts",Parse(new object[]{kindA}),Parse(new object[]{kindB}),true);
        var explicitA=EventObject(Event("explicit"));
        var explicitB=EventObject(Event("explicit")); ResetNewsParser.Obj(explicitB["presentation"])["kindExplicit"]=false;
        PairCase(owner,"displayed unknown form correction alerts",Parse(new object[]{explicitA}),Parse(new object[]{explicitB}),true);
        var statusA=EventObject(Event("source-status"));
        var statusB=EventObject(Event("source-status")); statusB["status"]="withdrawn"; ResetNewsParser.Obj(statusB["presentation"])["status"]="withdrawn";
        PairCase(owner,"source status correction alerts",Parse(new object[]{statusA}),Parse(new object[]{statusB}),true);

        var view=first.Items[0].View;
        var changedClock=new ResetNewsView {Title=view.Title,Status=view.Status,Scope=view.Scope,TimeLabel=view.TimeLabel,TimeValue=view.TimeValue,AsOf=clock.AddHours(4).ToString("o")};
        Check("display fingerprint excludes projection clock",ResetNewsPresentation.Version(view)==ResetNewsPresentation.Version(changedClock));
        Check("parsed version contains exactly the five shared card fields",
            first.Items[0].Version.StartsWith("view1:",StringComparison.Ordinal) &&
            first.Items[0].Version==ResetNewsPresentation.Version(first.Items[0].View) &&
            Activity(first.Items[0]).Contains(first.Items[0].View.TimeValue));
    }
    static string LegacyVersion(object value)
    {
        var item=EventObject(value);
        var presentation=ResetNewsParser.Obj(ResetNewsParser.Get(item,"presentation"));
        var schedule=ResetNewsParser.Obj(ResetNewsParser.Get(item,"schedule"));
        var estimate=ResetNewsParser.Obj(ResetNewsParser.Get(item,"estimate"));
        string status=ResetNewsParser.Text(presentation,"status");
        if(status.Length==0) status=ResetNewsParser.Text(item,"status");
        string material=ResetNewsParser.Json.Serialize(new object[] {
            ResetNewsParser.Text(item,"type"),status,ResetNewsParser.Text(presentation,"audienceZh"),
            ResetNewsParser.Text(presentation,"productsZh"),ResetNewsParser.Get(presentation,"kindExplicit"),
            ResetNewsParser.Text(schedule,"from"),ResetNewsParser.Text(schedule,"through"),
            ResetNewsParser.Text(item,"confirmedAt"),ResetNewsParser.Text(item,"occurredOn"),ResetNewsParser.Text(item,"confirmationBasis")});
        if(estimate!=null) material+=ResetNewsParser.Json.Serialize(new object[] {
            ResetNewsParser.Text(estimate,"from"),ResetNewsParser.Text(estimate,"through"),ResetNewsParser.Text(estimate,"basis")});
        if(Object.Equals(ResetNewsParser.Get(presentation,"timeInferred"),true)) material+="|time-inferred";
        using(var sha=SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(material)));
    }
    static void WriteLegacy(string name,ResetNewsSnapshot snapshot,Dictionary<string,string> read)
    {
        var cache=new ResetNewsCache { Schema=1,Initialized=true,ETag="synthetic-legacy",LastSuccess=clock.ToString("o"),
            Snapshot=snapshot,Read=read,ReadPolicy=0 };
        var document=ResetNewsParser.Obj(ResetNewsParser.Json.DeserializeObject(ResetNewsParser.Json.Serialize(cache)));
        document.Remove("ReadPolicy");
        var stored=ResetNewsParser.Obj(document["Snapshot"]);
        foreach(var value in (object[])stored["Items"]) ResetNewsParser.Obj(value).Remove("View");
        File.WriteAllText(StatePath(name),ResetNewsParser.Json.Serialize(document),Encoding.UTF8);
    }
    static void MigrationCases(Form owner)
    {
        var eventA=Event("legacy-read"); var eventB=Event("legacy-unread",scope:"Pro 用户"); var eventC=Event("legacy-missing",product:"Codex App");
        var snapshot=Parse(new[]{eventA,eventB,eventC});
        Find(snapshot,"legacy-read").Version=LegacyVersion(eventA);
        Find(snapshot,"legacy-unread").Version=LegacyVersion(eventB);
        Find(snapshot,"legacy-missing").Version=LegacyVersion(eventC);
        string oldRead=Find(snapshot,"legacy-read").Version,oldMismatch="synthetic-older-read-version",orphan="synthetic-orphan-legacy-version";
        WriteLegacy("migration",snapshot,new Dictionary<string,string> {{"legacy-read",oldRead},{"legacy-unread",oldMismatch},{"orphan",orphan}});
        var state=new ResetNewsState(StatePath("migration"));
        var read=Find(state.Cache.Snapshot,"legacy-read"); var unread=Find(state.Cache.Snapshot,"legacy-unread"); var missing=Find(state.Cache.Snapshot,"legacy-missing");
        Check("legacy matching read version migrates without becoming unread",
            state.Cache.ReadPolicy==1 && read.Version!=oldRead && !state.Unread(read) && state.Cache.Read[read.Id]==read.Version);
        Check("legacy mismatched and missing read versions remain unread",
            state.Unread(unread) && state.Unread(missing) && state.Cache.Read[unread.Id]==oldMismatch && !state.Cache.Read.ContainsKey(missing.Id));
        Check("legacy orphan read is preserved without guessing a display version",state.Cache.Read["orphan"]==orphan);
        Check("legacy migration projects every retained card from LastSuccess",
            state.Cache.Snapshot.Items.TrueForAll(item=>ResetNewsPresentation.Valid(item.View) && item.View.AsOf==clock.ToString("o") &&
                item.Version==ResetNewsPresentation.Version(item.View)) &&
            PreviewText(owner,state.Cache.Snapshot).Contains(read.View.TimeValue));
        var persisted=ResetNewsParser.Json.Deserialize<ResetNewsCache>(File.ReadAllText(StatePath("migration")));
        Check("legacy migration immediately persists policy projection and correct read mapping",
            persisted.ReadPolicy==1 && Find(persisted.Snapshot,read.Id).Version==read.Version &&
            persisted.Read[read.Id]==read.Version && persisted.Snapshot.Items.TrueForAll(item=>ResetNewsPresentation.Valid(item.View)));
        string saved=File.ReadAllText(StatePath("migration"));
        state=new ResetNewsState(StatePath("migration"));
        Check("second cache construction preserves migration and read distinctions",
            File.ReadAllText(StatePath("migration"))==saved && !state.Unread(Find(state.Cache.Snapshot,"legacy-read")) &&
            state.Unread(Find(state.Cache.Snapshot,"legacy-unread")) && state.Unread(Find(state.Cache.Snapshot,"legacy-missing")));
        var same=Parse(new[]{eventA,eventB,eventC}); state.Apply(same,"fresh-source-after-migration");
        Check("unchanged source after migration creates no new content update",
            !state.ContentChanged && !state.Unread(Find(same,"legacy-read")) &&
            state.Unread(Find(same,"legacy-unread")) && state.Cache.Read["orphan"]==orphan);
        var empty=Parse(new object[0]); WriteLegacy("migration-empty",empty,new Dictionary<string,string>{{"orphan",orphan}});
        var emptyState=new ResetNewsState(StatePath("migration-empty"));
        Check("empty legacy cache persists the new policy while preserving orphan reads and no badge",
            emptyState.Cache.ReadPolicy==1 && !emptyState.HasUnread && emptyState.Cache.Read["orphan"]==orphan &&
            ResetNewsParser.Json.Deserialize<ResetNewsCache>(File.ReadAllText(StatePath("migration-empty"))).ReadPolicy==1);
        var absent=EventObject(Event("estimate-migration")); var emptyEstimate=EventObject(Event("estimate-migration")); emptyEstimate["estimate"]=new Dictionary<string,object>();
        var legacySnapshot=Parse(new object[]{emptyEstimate}); legacySnapshot.Items[0].Version=LegacyVersion(emptyEstimate);
        WriteLegacy("migration-empty-estimate",legacySnapshot,new Dictionary<string,string>{{"estimate-migration",legacySnapshot.Items[0].Version}});
        var estimateState=new ResetNewsState(StatePath("migration-empty-estimate"));
        estimateState.Apply(Parse(new object[]{absent}),"estimate-removed");
        Check("legacy empty estimate presence migrates and stays read when the source omits it",
            !estimateState.HasUnread && !estimateState.ContentChanged);
    }
    static void ClockAndContentCases(Form owner)
    {
        var source=Event("clock-only",from:today+"T20:00:00+08:00",through:today+"T21:00:00+08:00");
        var initial=At(new[]{source},clock);
        var late=At(new[]{source},clock.AddHours(11));
        Check("standalone late projection honestly distinguishes the elapsed announcement window",
            initial.Items[0].View.Status!=late.Items[0].View.Status);
        var state=new ResetNewsState(StatePath("clock-only")); state.Apply(initial,"before");
        string initialVersion=initial.Items[0].Version,initialText=PreviewText(owner,initial),asOf=initial.Items[0].View.AsOf;
        late.VerifiedAt=today+"T23:00:00+08:00"; state.Apply(late,"after");
        Check("checkedAt-only threshold crossing reuses the persisted view and never creates unread",
            !state.HasUnread && !state.ContentChanged && late.Items[0].Version==initialVersion &&
            late.Items[0].View.AsOf==asOf && Activity(initial.Items[0])==Activity(late.Items[0]),
            new {persistedStatus=late.Items[0].View.Status,asOf=asOf,scope="source-stable display retained; no proof of native refresh"});
        state=new ResetNewsState(StatePath("clock-only"));
        Check("source-stable projection survives restart and card uses its stored status",
            state.Cache.Snapshot.Items[0].Version==initialVersion && state.Cache.Snapshot.Items[0].View.AsOf==asOf &&
            PreviewText(owner,state.Cache.Snapshot).Contains(state.Cache.Snapshot.Items[0].View.Status));
        var revised=EventObject(source); ResetNewsParser.Obj(revised["presentation"])["audienceZh"]="Pro 用户";
        var sourceChange=At(new object[]{revised},clock.AddHours(11)); state.Apply(sourceChange,"actual-correction");
        Check("actual source correction after a clock threshold uses the new truthful projection and alerts",
            state.HasUnread && state.ContentChanged && sourceChange.Items[0].View.AsOf==clock.AddHours(11).ToString("o") &&
            sourceChange.Items[0].View.Status!="预告");
        var tzSource=Event("timezone-stable","confirmed",occurred:today);
        var tzState=new ResetNewsState(StatePath("timezone-stable")); var tzFirst=At(new[]{tzSource},clock); tzState.Apply(tzFirst,"before-zone-change");
        var tzItem=tzState.Cache.Snapshot.Items[0];
        string currentLocalValue=tzItem.View.TimeValue;
        var record=DateTimeOffset.Parse(tzItem.ConfirmedAt,CultureInfo.InvariantCulture);
        string formerLocalValue=record.ToOffset(TimeSpan.FromHours(9)).ToString("M月d日 HH:mm",CultureInfo.InvariantCulture);
        if(formerLocalValue==currentLocalValue) formerLocalValue=record.ToOffset(TimeSpan.FromHours(-4)).ToString("M月d日 HH:mm",CultureInfo.InvariantCulture);
        // Represent an already-persisted former-zone display. Never alter the
        // machine's timezone or process/system settings to produce this case.
        tzItem.View.TimeValue=formerLocalValue; tzItem.Version=ResetNewsPresentation.Version(tzItem.View);
        tzState.Cache.Read[tzItem.Id]=tzItem.Version;
        File.WriteAllText(StatePath("timezone-stable"),ResetNewsParser.Json.Serialize(tzState.Cache),Encoding.UTF8);
        string formerVersion=tzItem.Version;
        tzState=new ResetNewsState(StatePath("timezone-stable"));
        var tzNext=At(new[]{tzSource},clock.AddHours(4)); tzState.Apply(tzNext,"after-zone-change");
        var tzRestart=new ResetNewsState(StatePath("timezone-stable"));
        Check("a retained former-zone display remains read when unchanged source is reprojected in the current zone",
            formerLocalValue!=currentLocalValue && !tzState.HasUnread && !tzState.ContentChanged &&
            tzNext.Items[0].Version==formerVersion && tzNext.Items[0].View.TimeValue==formerLocalValue &&
            !tzRestart.HasUnread && tzRestart.Cache.Snapshot.Items[0].View.TimeValue==formerLocalValue,
            new {scope="synthetic former-zone cache; system timezone never changed",currentLocalValue=currentLocalValue,
                formerLocalValue=formerLocalValue,retainedVersion=formerVersion});
        var orderState=new ResetNewsState(StatePath("order-only"));
        var two=Parse(new[]{Event("order-a"),Event("order-b",scope:"Pro 用户")}); orderState.Apply(two,"first");
        var reverse=Parse(new[]{Event("order-a"),Event("order-b",scope:"Pro 用户")}); reverse.Items.Reverse(); orderState.Apply(reverse,"reverse");
        Check("activity order alone is quiet",!orderState.ContentChanged && !orderState.HasUnread);
        var regroupState=new ResetNewsState(StatePath("regroup")); regroupState.Apply(One("old-id"),"first");
        regroupState.Apply(One("replacement-id"),"new-id");
        Check("regrouped identical activity preserves read while the frozen ID set is detectably changed",
            !regroupState.HasUnread && regroupState.ContentChanged);
        var historyState=new ResetNewsState(StatePath("history-content")); historyState.Apply(Parse(new object[0]),"empty");
        var history=Parse(new[]{Event("prior-history","confirmed",occurred:yesterday)}); historyState.Apply(history,"history");
        Check("empty history addition is content change without an unread activity",
            historyState.ContentChanged && !historyState.HasUnread && ResetNewsPresentation.EmptyHistory(history).Length>0);
        historyState.Apply(Parse(new object[0]),"remove-history");
        Check("empty history removal is content change without an unread activity",historyState.ContentChanged && !historyState.HasUnread);
    }
    static void FreezeAndPreviewCases(Form owner)
    {
        int acknowledged=0;
        var state=new ResetNewsState(StatePath("frozen-empty")); state.Apply(Parse(new object[0]),"empty");
        using(var card=new ResetNewsCard(delegate(string id,string version){acknowledged++;state.MarkRead(id,version);},delegate{})) {
            card.PreparePreview(owner,state.Cache.Snapshot,Appearance.Palette(false),1.25,clock);
            string frozen=card.VisibleText;
            Check("unshown empty card and empty state agree",card.TotalCount==0 && frozen.Contains("暂无新消息") && !state.HasUnread);
            Check("unchanged frozen empty snapshot does not request an update notice",!card.ShouldNotifySnapshot(state.Cache.Snapshot));
            var emptyCheckOnly=Parse(new object[0]); emptyCheckOnly.VerifiedAt=today+"T14:30:00+08:00";
            Check("check-time change does not notify a frozen empty card",!card.ShouldNotifySnapshot(emptyCheckOnly));
            var withHistory=Parse(new[]{Event("empty-frozen-history","confirmed",occurred:yesterday)});
            Check("a new history row requests an update for a frozen empty card without creating unread",
                withHistory.Items.Count==0 && card.ShouldNotifySnapshot(withHistory) && ResetNewsPresentation.EmptyHistory(withHistory).Length>0);
            var next=One("new-while-empty"); state.Apply(next,"new");
            Check("new activity requests an update notice for a frozen empty card",card.ShouldNotifySnapshot(next) && state.ContentChanged);
            card.ShowNewDataNotice(next);
            var hiddenDiagnostic=ResetNewsParser.Obj(ResetNewsParser.Json.DeserializeObject(ResetNewsParser.Json.Serialize(card.MessageDiagnostic)));
            Check("the actual notification method leaves an unshown card hidden and without a notice",
                !card.Visible && !card.DismissalActive && Object.Equals(hiddenDiagnostic["newDataNotice"],false) && acknowledged==0);
            Check("snapshot update can coexist with frozen empty preview and an unread current state",
                card.TotalCount==0 && card.VisibleText==frozen && state.HasUnread,
                new {currentItems=state.Cache.Snapshot.Items.Count,frozenItems=card.TotalCount,scope="copy/layout test; not real Open or native acknowledgement"});
            Check("no hidden preview implicitly marks new data read",acknowledged==0 && !card.Visible && !card.DismissalActive);
            card.SavePreview(Path.Combine(output,"frozen-empty-preview.png"));
            card.PreparePreview(owner,state.Cache.Snapshot,Appearance.Palette(false),1.25,clock);
            Check("fresh preview uses latest current snapshot",card.TotalCount==1 && card.DisplayedCount==1 && !card.VisibleText.Contains("暂无新消息"));
            card.SavePreview(Path.Combine(output,"fresh-event-preview.png"));
            var same=One("new-while-empty"); same.VerifiedAt=today+"T14:30:00+08:00"; state.Apply(same,"etag-only");
            Check("check time alone does not notify a frozen activity card",!state.ContentChanged && !card.ShouldNotifySnapshot(same));
            string frozenVersion=next.Items[0].Version;
            var revised=One("new-while-empty",scope:"Pro 用户"); state.Apply(revised,"new-version");
            Check("same ID revised activity notifies while the frozen card keeps the old version",
                card.ShouldNotifySnapshot(revised) && state.HasUnread && state.ContentChanged &&
                ((List<ResetNewsItem>)typeof(ResetNewsCard).GetField("items",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(card))[0].Version==frozenVersion);
            state.MarkRead("new-while-empty",frozenVersion);
            Check("frozen card old-version ack leaves the revised current activity unread",
                state.HasUnread && state.Cache.Read["new-while-empty"]==frozenVersion &&
                revised.Items[0].Version!=frozenVersion);
            state=new ResetNewsState(StatePath("frozen-empty"));
            Check("old-version ack persists without reading a revised activity on restart",
                state.HasUnread && state.Cache.Read["new-while-empty"]==frozenVersion);
            state.Apply(Parse(new object[0]),"empty-again");
            Check("removing an activity notifies a frozen activity card without leaving an unread badge",
                card.ShouldNotifySnapshot(state.Cache.Snapshot) && state.ContentChanged && !state.HasUnread);
            // Restore an unread current activity for the existing invisible-paint guard.
            state.Apply(revised,"restore-current");

            // Verify the production visibility gate without mocking native hit success.
            var rows=(IEnumerable)typeof(ResetNewsCard).GetField("rows",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(card);
            foreach(var row in rows) row.GetType().GetField("Painted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(row,true);
            typeof(ResetNewsCard).GetMethod("AcknowledgePaintedRows",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(card,null);
            Check("painted flag alone cannot acknowledge an unshown card",acknowledged==0 && state.HasUnread && !card.DismissalActive);
        }
    }
    static int RedPixels(Bitmap frame)
    {
        int count=0;
        for(int y=0;y<frame.Height;y++) for(int x=0;x<frame.Width;x++) {
            Color c=frame.GetPixel(x,y);
            if(c.A>0 && Math.Abs(c.R-207)<=2 && Math.Abs(c.G-101)<=2 && Math.Abs(c.B-92)<=2) count++;
        }
        return count;
    }
    static void FrameCases()
    {
        foreach(double scale in new[]{1.0,1.25,1.5,2.0}) {
            using(var label=new UsageLabel()) {
                var quota=new QuotaSnapshot(); quota.Windows.Add(new QuotaWindow {Remaining=13,Minutes=10080});
                label.Size=new Size((int)Math.Round(48*scale),UsageLabel.HeightFor(quota,false,false,scale));
                label.Configure(quota,Appearance.Palette(false),true,false,false,scale);
                using(var unread=label.CreateTransparentBitmap(true)) using(var clear=label.CreateTransparentBitmap(false)) {
                    Check("fresh unread frame contains red marker at scale "+scale.ToString(CultureInfo.InvariantCulture),RedPixels(unread)>0);
                    Check("next fresh clear frame removes all red pixels at scale "+scale.ToString(CultureInfo.InvariantCulture),RedPixels(clear)==0);
                    if(scale==1.25) {unread.Save(Path.Combine(output,"entry-unread-125.png"),ImageFormat.Png);clear.Save(Path.Combine(output,"entry-clear-125.png"),ImageFormat.Png);}
                }
            }
        }
    }
    static bool ContainsSensitiveField(object value)
    {
        var dictionary=value as Dictionary<string,object>;
        if(dictionary!=null) {
            foreach(var pair in dictionary) {
                string key=pair.Key.ToLowerInvariant();
                if(key=="quota" || key=="account" || key=="accountkey" || key=="email" ||
                    key=="rawresponse" || key=="responsebody" || key=="credentials") return true;
                if(ContainsSensitiveField(pair.Value)) return true;
            }
        }
        var array=value as object[];
        if(array!=null) foreach(var item in array) if(ContainsSensitiveField(item)) return true;
        return false;
    }
    static void DiagnosticCases()
    {
        string log=Path.Combine(fixtures,"diagnostic-tail.json");
        var logger=new NewsDiagnostic(log);
        for(int index=1;index<=85;index++) logger.Record("synthetic-badge",new {itemCount=index,unreadCount=index%2,attemptedUnread=index%2!=0});
        var document=ResetNewsParser.Obj(ResetNewsParser.Json.DeserializeObject(File.ReadAllText(log)));
        var events=(object[])ResetNewsParser.Get(document,"events");
        double first,last;
        QuotaParser.Number(ResetNewsParser.Get(ResetNewsParser.Obj(events[0]),"sequence"),out first);
        QuotaParser.Number(ResetNewsParser.Get(ResetNewsParser.Obj(events[events.Length-1]),"sequence"),out last);
        Check("diagnostic keeps the latest 80 events with original sequence values",events.Length==80 && first==6 && last==85);
        Check("measured diagnostic schema contains no quota account or raw response fields",!ContainsSensitiveField(document),
            new {scope="synthetic logger schema; production TraceNews callsite reviewed separately"});

        var state=new ResetNewsState(StatePath("diagnostic-failure-state")); state.Apply(One("logger-fixture"),"logger-fixture");
        string before=ResetNewsParser.Json.Serialize(state.Cache);
        string obstruction=Path.Combine(fixtures,"not-a-directory"); File.WriteAllText(obstruction,"own fixture obstruction");
        var failedLogger=new NewsDiagnostic(Path.Combine(obstruction,"diagnostic.json"));
        bool threw=false; try {failedLogger.Record("synthetic-write-failure",new {itemCount=state.Cache.Snapshot.Items.Count,unreadCount=state.HasUnread?1:0});} catch {threw=true;}
        Check("diagnostic write failure neither escapes nor changes message state",!threw && before==ResetNewsParser.Json.Serialize(state.Cache));
    }
    [STAThread] static int Main(string[] arguments)
    {
        if(arguments.Length!=1) return 2;
        output=Path.GetFullPath(arguments[0]); fixtures=Path.Combine(output,"own-fixtures");
        Directory.CreateDirectory(fixtures);
        today=ResetNewsParser.BeijingToday;
        clock=DateTimeOffset.ParseExact(today+"T12:00:00+08:00","yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture);
        yesterday=clock.AddDays(-1).ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            StateCases();
            using(var owner=new Form {Bounds=new Rectangle(100,100,60,54),ShowInTaskbar=false}) {
                FingerprintCases(owner); MigrationCases(owner); ClockAndContentCases(owner); FreezeAndPreviewCases(owner);
                Check("own preview owner was never shown",!owner.Visible);
            }
            FrameCases(); DiagnosticCases();
        } catch(Exception error) {
            Check("unexpected offline fixture exception",false,new {type=error.GetType().FullName,message=error.Message});
        }
        var report=new {resultScope="synthetic own-file and noShow bitmap/layout regression only",passed=results.Count-failed,failed=failed,
            checks=results,observations=observations,knownIssues=knownIssues,sourceCalendarDay=today,normalApplicationStarted=false,networkAccessed=false,
            accountFilesRead=false,actualRuntimeCacheRead=false,userProcessTouched=false,physicalUserInputTested=false,
            nativeAcknowledgementTested=false,realOpenNotificationTested=false,pureNotificationPredicateTested=true,
            predecessor=new {version="0.11.7",checks=37,knownIssues=1,scope="preserved external baseline; no production or real read-state mutation"}};
        File.WriteAllText(Path.Combine(output,"news-unread-regression-report.json"),new JavaScriptSerializer().Serialize(report));
        return failed==0?0:1;
    }
}
