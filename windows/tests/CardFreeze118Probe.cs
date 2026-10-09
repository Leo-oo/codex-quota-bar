using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Timer=System.Windows.Forms.Timer;

// Native, non-activating own-window fixture. It creates no UsageBar and never
// calls Program.Main, a client/network API, browser or global input API.
internal static class CardFreeze118Probe
{
    sealed class OwnHost:Form
    {
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000080;return p;}}
        internal OwnHost(){AutoScaleMode=AutoScaleMode.None;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(217,219,224);}
    }
    sealed class ReadEvidence
    {
        public string Stage,Version;
        public bool CardVisible,NativeVisible,DismissalActive,RowPainted,ClientContains,WorkContains,NativeHitCard;
        public object RowBounds,ScreenBounds;
        public long HitRoot,CardHwnd;
    }
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=1048576};
    static readonly List<object> Checks=new List<object>();
    static readonly List<string> Cases=new List<string>();
    static readonly List<object> Observations=new List<object>();
    static readonly List<ReadEvidence> Reads=new List<ReadEvidence>();
    static readonly StringBuilder Lines=new StringBuilder();
    static readonly Stopwatch Budget=Stopwatch.StartNew();
    static OwnHost host;static ResetNewsCard card;static ResetNewsState state;static Timer timer;
    static string output,cachePath,readStage="",version1,version2,visible1,visible2,check1;
    static int phase,pass,fail,paintBefore;static bool finished,completed;static DateTimeOffset initialClock;static IntPtr initialForeground;
    static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Hidden).GetValue(target);}
    static object Rect(Rectangle r){return new{x=r.X,y=r.Y,width=r.Width,height=r.Height};}
    static void Check(string id,bool good)
    {Checks.Add(new{id=id,passed=good});Lines.AppendLine((good?"PASS ":"FAIL ")+id);if(good)pass++;else fail++;}
    static void Require(bool good,string message){if(!good)throw new InvalidOperationException(message);}
    static ResetNewsSnapshot Snapshot(string scope,DateTimeOffset checkedAt,DateTimeOffset projectionClock)
    {
        var snapshot=new ResetNewsSnapshot {DisplayPolicy=3,Day=ResetNewsParser.BeijingToday,Monitor="healthy",VerifiedAt=checkedAt.ToString("o"),Items=new List<ResetNewsItem>()};
        if(scope!=null){
            var item=new ResetNewsItem {Id="own-fixture-activity",Type="direct_reset",KindExplicit=true,Status="announced",Scope=scope,Products="Codex",
                Published=initialClock.ToString("o"),ExpectedFrom=initialClock.AddMinutes(5).ToString("o"),Through=initialClock.AddMinutes(10).ToString("o"),
                Schedule="own fixture expected window",Url="https://aihot.news/codex-reset"};
            ResetNewsPresentation.Initialize(item,projectionClock);snapshot.Items.Add(item);
        }return snapshot;
    }
    static int PaintCount {get{return Field<int>(card,"paintCount");}}
    static bool NativeVisible {get{return card.Visible&&card.IsHandleCreated&&Native.IsWindowVisible(card.Handle)&&card.DismissalActive;}}
    static void Capture(string stage)
    {
        Observations.Add(new{stage=stage,utc=DateTime.UtcNow.ToString("o"),message=card.MessageDiagnostic,visibleText=card.VisibleText,
            readDiagnostic=card.ReadDiagnostic,contentChanged=state.ContentChanged,hasUnread=state.HasUnread,readCallbackCount=Reads.Count,
            checkTime=ResetNewsParser.CheckTime(state.Cache.Snapshot.VerifiedAt,DateTimeOffset.Now)});
    }
    static void MarkRead(string id,string version)
    {
        // Capture the exact native oracle at the production callback; no mark
        // action is initiated by this fixture outside this card callback.
        var panel=Field<Panel>(card,"panel");
        foreach(object row in Field<IEnumerable>(card,"rows")){
            var item=Field<ResetNewsItem>(row,"Item");if(item.Id!=id||item.Version!=version)continue;
            Rectangle bounds=Field<Rectangle>(row,"Bounds"),screen=panel.RectangleToScreen(bounds);
            Point center=new Point(screen.Left+screen.Width/2,screen.Top+screen.Height/2);
            IntPtr hit=Native.Root(Native.WindowFromPoint(center));
            Reads.Add(new ReadEvidence {Stage=readStage,Version=version,CardVisible=card.Visible,NativeVisible=Native.IsWindowVisible(card.Handle),DismissalActive=card.DismissalActive,
                RowPainted=Field<bool>(row,"Painted"),ClientContains=panel.ClientRectangle.Contains(bounds),WorkContains=Screen.FromControl(host).WorkingArea.Contains(screen),
                NativeHitCard=hit==card.Handle,HitRoot=hit.ToInt64(),CardHwnd=card.Handle.ToInt64(),RowBounds=Rect(bounds),ScreenBounds=Rect(screen)});break;
        }
        state.MarkRead(id,version);
    }
    static bool ValidRead(int index,string version,string stage)
    {
        if(Reads.Count<=index)return false;var r=Reads[index];
        return r.Version==version&&r.Stage==stage&&r.CardVisible&&r.NativeVisible&&r.DismissalActive&&r.RowPainted&&r.ClientContains&&r.WorkContains&&r.NativeHitCard;
    }
    static void Open(string stage)
    {readStage=stage;card.Open(host,state,Appearance.Palette(false),Native.Scale(host.Handle),null,host.Bounds);}
    static void ForceOwnPaint()
    {var panel=Field<Panel>(card,"panel");panel.Invalidate();panel.Update();}
    static void Tick(object sender,EventArgs args)
    {
        if(finished)return;
        try{
            if(Budget.ElapsedMilliseconds>18000)throw new TimeoutException("Bounded18s own card fixture; native paint/read was not established");
            if(phase==0){
                Check("empty-open-native-visible-hook",NativeVisible);
                Check("empty-open-real-paint",PaintCount>0);
                Check("empty-open-zero-items-visible-empty-text",card.TotalCount==0&&card.DisplayedCount==0&&card.VisibleText.Contains("暂无新消息"));
                Check("empty-open-no-read-no-unread",Reads.Count==0&&!state.HasUnread);Capture("empty-open");Cases.Add("empty-open");
                var next=Snapshot("own fixture audience v1",initialClock.AddMinutes(1),initialClock);state.Apply(next,null);version1=next.Items[0].Version;
                Check("empty-arrival-content-changed",state.ContentChanged);
                Check("empty-arrival-should-notify",card.ShouldNotifySnapshot(next));
                paintBefore=PaintCount;card.ShowNewDataNotice(next);phase=1;
            }else if(phase==1){
                ForceOwnPaint();Check("empty-arrival-remains-native-visible-painted",NativeVisible&&PaintCount>paintBefore);
                Check("empty-arrival-old-card-frozen-zero-items",card.TotalCount==0&&card.DisplayedCount==0&&card.VisibleText.Contains("暂无新消息")&&!card.VisibleText.Contains("own fixture audience v1"));
                Check("empty-arrival-visible-update-notice",card.VisibleText.Contains("有更新，请重新打开消息卡"));
                Check("empty-arrival-new-version-remains-unread",state.HasUnread&&Reads.Count==0&&!state.Cache.Read.ContainsKey("own-fixture-activity"));Capture("empty-arrival");Cases.Add("empty-arrival");
                card.Close();Check("empty-arrival-close-releases-native-popup",!card.Visible&&!Native.IsWindowVisible(card.Handle)&&!card.DismissalActive);Open("reopen-v1");phase=2;
            }else if(phase==2){
                ForceOwnPaint();if(Reads.Count==0)return;
                Check("reopen-v1-native-visible-real-paint",NativeVisible&&PaintCount>0);
                Check("reopen-v1-current-content-no-notice",card.TotalCount==1&&card.DisplayedCount==1&&card.VisibleText.Contains("own fixture audience v1")&&!card.VisibleText.Contains("有更新"));
                Check("reopen-v1-exact-read-native-oracle",Reads.Count==1&&ValidRead(0,version1,"reopen-v1"));
                Check("reopen-v1-current-version-read",!state.HasUnread&&state.Cache.Read["own-fixture-activity"]==version1);
                Check("reopen-v1-read-persisted-own-cache",new ResetNewsState(cachePath).Cache.Read["own-fixture-activity"]==version1);Capture("reopen-v1");Cases.Add("reopen-v1");
                visible1=card.VisibleText;var next=Snapshot("own fixture audience v2",initialClock.AddMinutes(2),initialClock);state.Apply(next,null);version2=next.Items[0].Version;
                Check("same-id-update-visible-version-changed",version2!=version1&&state.ContentChanged);
                Check("same-id-update-should-notify",card.ShouldNotifySnapshot(next));card.ShowNewDataNotice(next);phase=3;
            }else if(phase==3){
                ForceOwnPaint();Check("same-id-update-old-card-native-visible",NativeVisible);
                Check("same-id-update-old-projection-frozen",card.TotalCount==1&&card.VisibleText.Contains("own fixture audience v1")&&!card.VisibleText.Contains("own fixture audience v2"));
                Check("same-id-update-new-version-not-read-by-old-card",state.HasUnread&&Reads.Count==1&&state.Cache.Read["own-fixture-activity"]==version1);
                Check("same-id-update-visible-update-notice",card.VisibleText.Contains("有更新，请重新打开消息卡"));Capture("same-id-update");Cases.Add("same-id-update");
                card.Close();Check("same-id-close-releases-native-popup",!card.Visible&&!Native.IsWindowVisible(card.Handle)&&!card.DismissalActive);Open("reopen-v2");phase=4;
            }else if(phase==4){
                ForceOwnPaint();if(Reads.Count<2)return;
                Check("reopen-v2-native-visible-real-paint",NativeVisible&&PaintCount>0);
                Check("reopen-v2-new-projection-no-notice",card.TotalCount==1&&card.VisibleText.Contains("own fixture audience v2")&&!card.VisibleText.Contains("own fixture audience v1")&&!card.VisibleText.Contains("有更新"));
                Check("reopen-v2-exact-read-native-oracle",Reads.Count==2&&ValidRead(1,version2,"reopen-v2"));
                Check("reopen-v2-current-version-read",!state.HasUnread&&state.Cache.Read["own-fixture-activity"]==version2);
                Check("reopen-v2-read-persisted-own-cache",new ResetNewsState(cachePath).Cache.Read["own-fixture-activity"]==version2);Capture("reopen-v2");Cases.Add("reopen-v2");
                visible2=card.VisibleText;check1=Field<string>(card,"checkText");var clockOnly=Snapshot("own fixture audience v2",initialClock.AddMinutes(3),initialClock);state.Apply(clockOnly,null);
                Check("checked-only-content-changed-false",!state.ContentChanged);
                Check("checked-only-should-notify-false",!card.ShouldNotifySnapshot(clockOnly));card.ShowNewDataNotice(clockOnly);phase=5;
            }else if(phase==5){
                ForceOwnPaint();Check("checked-only-native-card-content-clock-frozen",NativeVisible&&card.VisibleText==visible2&&Field<string>(card,"checkText")==check1);
                Check("checked-only-no-notice-no-unread-no-extra-read",!card.VisibleText.Contains("有更新")&&!state.HasUnread&&Reads.Count==2&&state.Cache.Snapshot.Items[0].Version==version2);
                Check("checked-only-state-reports-new-check-time",state.Summary.Contains(ResetNewsParser.CheckTime(state.Cache.Snapshot.VerifiedAt,DateTimeOffset.Now)));Capture("checked-only");Cases.Add("checked-only");
                var crossing=Snapshot("own fixture audience v2",initialClock.AddHours(2),initialClock.AddHours(2));
                string candidateStatus=crossing.Items[0].View.Status,priorStatus=state.Cache.Snapshot.Items[0].View.Status;
                Check("crossing-fixture-really-crosses-status-clock",candidateStatus!=priorStatus);state.Apply(crossing,null);
                Check("crossing-content-changed-false",!state.ContentChanged);
                Check("crossing-should-notify-false",!card.ShouldNotifySnapshot(crossing));card.ShowNewDataNotice(crossing);phase=6;
            }else if(phase==6){
                ForceOwnPaint();Check("crossing-native-projection-retains-status",NativeVisible&&card.VisibleText==visible2&&state.Cache.Snapshot.Items[0].Version==version2);
                Check("crossing-no-notice-no-unread-no-extra-read",!card.VisibleText.Contains("有更新")&&!state.HasUnread&&Reads.Count==2);
                Check("crossing-check-time-only-in-state",state.Summary.Contains(ResetNewsParser.CheckTime(state.Cache.Snapshot.VerifiedAt,DateTimeOffset.Now)));Capture("status-clock-crossing");Cases.Add("status-clock-crossing");
                card.Close();Open("reopen-clock-only");phase=7;
            }else if(phase==7){
                ForceOwnPaint();if(Reads.Count<3)return;
                Check("reopen-clock-current-check-time-displayed",NativeVisible&&Field<string>(card,"checkText")==ResetNewsParser.CheckTime(state.Cache.Snapshot.VerifiedAt,DateTimeOffset.Now)&&Field<string>(card,"checkText")!=check1);
                Check("reopen-clock-activity-version-remains-read",!state.HasUnread&&state.Cache.Snapshot.Items[0].Version==version2&&state.Cache.Read["own-fixture-activity"]==version2);
                Check("reopen-clock-exact-native-read-same-version",Reads.Count==3&&ValidRead(2,version2,"reopen-clock-only"));
                Check("reopen-clock-no-update-notice",!card.VisibleText.Contains("有更新"));Capture("reopen-clock-only");Cases.Add("reopen-clock-only");completed=true;Finish();
            }
        }catch(Exception error){Lines.AppendLine("EXCEPTION "+error);Check("fixture-no-exception",false);Finish();}
    }
    static void Finish()
    {
        if(finished)return;finished=true;if(timer!=null)timer.Stop();
        try{if(card!=null&&!card.IsDisposed){card.Close();Check("cleanup-native-hidden-hook-timer-released",!card.Visible&&!Native.IsWindowVisible(card.Handle)&&!card.DismissalActive&&!Field<Timer>(card,"escapeTimer").Enabled);card.Dispose();}}
        catch(Exception error){Lines.AppendLine("CLEANUP "+error);Check("cleanup-no-exception",false);}
        Check("own-fixture-foreground-unchanged",Native.GetForegroundWindow()==initialForeground);
        Check("all-eight-native-cases-complete",completed&&Cases.Count==8);
        File.WriteAllText(Path.Combine(output,"card-freeze.txt"),Lines.ToString(),new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output,"result.json"),Json.Serialize(new{completed=completed,pass=pass,fail=fail,cases=Cases,checks=Checks,readEvidence=Reads,observations=Observations,
            elapsedMilliseconds=Budget.ElapsedMilliseconds,usageBarCreated=false,normalProgramMainStarted=false,networkOrRealCodexRead=false,physicalGlobalInputSent=false,userProcessTouched=false,
            nativeVisiblePaintAcknowledgementRequired=true,cachePath=cachePath}),new UTF8Encoding(false));
        if(host!=null&&!host.IsDisposed)host.Close();Application.ExitThread();
    }
    [STAThread]static int Main(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);cachePath=Path.Combine(output,"own-fixture-runtime","news-cache.json");Require(!File.Exists(cachePath),"Fresh own fixture runtime required");
        initialClock=DateTimeOffset.Now;initialForeground=Native.GetForegroundWindow();state=new ResetNewsState(cachePath);state.Apply(Snapshot(null,initialClock,initialClock),null);
        var work=Screen.PrimaryScreen.WorkingArea;host=new OwnHost {Bounds=new Rectangle(work.Left+60,work.Top+60,360,500)};
        host.Shown+=delegate{Native.ShowWindow(host.Handle,4);card=new ResetNewsCard(MarkRead,delegate{});Open("empty-open");timer=new Timer{Interval=100};timer.Tick+=Tick;timer.Start();};
        Application.Run(host);if(timer!=null)timer.Dispose();host.Dispose();return finished&&completed&&fail==0?0:1;
    }
}
