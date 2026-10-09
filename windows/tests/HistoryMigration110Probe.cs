using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

// Explicit own-file migration fixture. No UI, clients, network or real cache.
internal static class HistoryMigration110Probe
{
    sealed class Result { public string id; public bool passed; }
    static readonly List<Result> checks=new List<Result>();
    static int failures;
    static void Check(string id,bool value) { checks.Add(new Result { id=id,passed=value }); if(!value)failures++; }
    static ResetNewsItem Pending(string id,DateTimeOffset clock)
    {
        var item=new ResetNewsItem { Id=id,Status="announced",KindExplicit=true,Type="reset_credit",
            Scope="fixture "+id,ExpectedFrom=clock.AddHours(2).ToString("o") };
        ResetNewsPresentation.Initialize(item,clock);
        return item;
    }
    static ResetNewsItem History(string day)
    {
        return new ResetNewsItem { Id="own-past-history",Status="confirmed",KindExplicit=true,
            Type="direct_reset",OccurredOn=day,ConfirmationBasis="source_post" };
    }
    static ResetNewsCache Cache(string day,int policy,DateTimeOffset clock,ResetNewsItem history)
    {
        var a=Pending("a",clock); var b=Pending("b",clock);
        return new ResetNewsCache { Schema=1,ReadPolicy=1,Initialized=true,LastSuccess=clock.ToString("o"),
            ETag="own-old-etag",Read=new Dictionary<string,string> { {"a",a.Version},{"orphan","own-orphan-version"} },
            Snapshot=new ResetNewsSnapshot { DisplayPolicy=policy,Day=day,VerifiedAt=clock.ToString("o"),
                Items=new List<ResetNewsItem> { a,b },LatestHistory=history } };
    }
    static ResetNewsState Load(string dir,string name,ResetNewsCache cache)
    {
        string path=Path.Combine(dir,name+".json");
        File.WriteAllText(path,ResetNewsParser.Json.Serialize(cache));
        return new ResetNewsState(path);
    }
    static bool ItemsSame(ResetNewsState state,ResetNewsCache prior)
    {
        if(state.Cache.Snapshot.Items.Count!=prior.Snapshot.Items.Count)return false;
        foreach(var wanted in prior.Snapshot.Items) {
            var actual=state.Cache.Snapshot.Items.Find(x=>x.Id==wanted.Id);
            if(actual==null || actual.Version!=wanted.Version)return false;
        }
        return true;
    }
    static bool HistorySame(ResetNewsState state,ResetNewsItem prior)
    {
        var actual=state.Cache.Snapshot.LatestHistory;
        return actual!=null && actual.Id==prior.Id && actual.OccurredOn==prior.OccurredOn &&
            ResetNewsParser.HistoryLabel(actual)=="上次额度重置" &&
            ResetNewsParser.HistoryTime(actual)==ResetNewsParser.HistoryTime(prior);
    }
    [STAThread] static int Main(string[] args)
    {
        if(args.Length!=1)return 2;
        string output=Path.GetFullPath(args[0]),dir=Path.Combine(output,"own-fixture-runtime");
        Directory.CreateDirectory(dir);
        try {
            string today=ResetNewsParser.BeijingToday;
            DateTime current=DateTime.ParseExact(today,"yyyy-MM-dd",CultureInfo.InvariantCulture);
            string yesterday=current.AddDays(-1).ToString("yyyy-MM-dd");
            string earlier=current.AddDays(-2).ToString("yyyy-MM-dd");
            DateTimeOffset now=DateTimeOffset.Now;
            DateTimeOffset oldClock=DateTimeOffset.Parse(yesterday+"T14:00:00+08:00",CultureInfo.InvariantCulture);
            var history=History(earlier);
            var sameCache=Cache(today,3,now,history);
            var same=Load(dir,"same-day-policy3",sameCache);
            Check("same-day policy3 preserves valid occurrence history",HistorySame(same,history) && ItemsSame(same,sameCache));
            Check("same-day policy3 drops ETag for refetch",same.Cache.ETag==null);
            var priorCache=Cache(yesterday,3,oldClock,history);
            var prior=Load(dir,"cross-day-policy3",priorCache);
            Check("cross-day policy3 preserves pending item IDs and versions",ItemsSame(prior,priorCache) && prior.Cache.Snapshot.Day==yesterday);
            Check("cross-day policy3 preserves reliable prior history",HistorySame(prior,history));
            var a=prior.Cache.Snapshot.Items.Find(x=>x.Id=="a"); var b=prior.Cache.Snapshot.Items.Find(x=>x.Id=="b");
            Check("cross-day policy3 preserves exact read unread and drops ETag",a!=null && b!=null &&
                prior.Cache.Read.Count==priorCache.Read.Count && prior.Cache.Read["a"]==priorCache.Read["a"] &&
                prior.Cache.Read["orphan"]=="own-orphan-version" && !prior.Cache.Read.ContainsKey("b") &&
                !prior.Unread(a) && prior.Unread(b) && prior.HasUnread && prior.Cache.ETag==null);
            var invalid=History(earlier); invalid.TimeInferred=true;
            var future=History(current.AddDays(1).ToString("yyyy-MM-dd"));
            var invalidCache=Cache(yesterday,3,oldClock,invalid); var futureCache=Cache(yesterday,3,oldClock,future);
            var invalidState=Load(dir,"invalid-policy3",invalidCache); var futureState=Load(dir,"future-policy3",futureCache);
            Check("policy3 clears invalid future history without clearing activities",
                invalidState.Cache.Snapshot.LatestHistory==null && futureState.Cache.Snapshot.LatestHistory==null &&
                ItemsSame(invalidState,invalidCache) && ItemsSame(futureState,futureCache));
            var policy2=Load(dir,"old-policy2",Cache(yesterday,2,oldClock,history));
            Check("prior-day policy2 keeps legacy clearing behavior",policy2.Cache.Snapshot.Items.Count==0 && policy2.Cache.Snapshot.LatestHistory==null && policy2.Cache.ETag==null);
            var unknown=Load(dir,"unknown-policy",Cache(today,999,now,history));
            Check("unknown policy clears activities and history",unknown.Cache.Snapshot.Items.Count==0 && unknown.Cache.Snapshot.LatestHistory==null && unknown.Cache.ETag==null);
        } catch(Exception error) {
            Check("fixture completed without exception",false);
            File.WriteAllText(Path.Combine(output,"exception.txt"),error.GetType().FullName+"\n"+error.Message);
        }
        bool completed=checks.Count==8 && failures==0;
        File.WriteAllText(Path.Combine(output,"result.json"),ResetNewsParser.Json.Serialize(new {
            expectedAssertions=8,pass=checks.Count-failures,fail=failures,completed=completed,checks=checks,
            normalProgramMainStarted=false,networkAccessed=false,accountFilesRead=false,
            actualRuntimeCacheRead=false,userProcessTouched=false,physicalGlobalInputSent=false,ownWindowsShown=false }));
        return completed?0:1;
    }
}

