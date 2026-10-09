// Window-following primitives adapted from Codex Usage Remaining.
// Copyright (c) 2026 Zoey Liew. MIT; see LICENSE.upstream.txt.
// Local quota display. Authentication remains owned by the installed Codex app server.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    internal delegate void EventCallback(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time);
    internal delegate bool WindowCallback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(WindowCallback callback,IntPtr data);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint id);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attr, out Rect rect, int size);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint="SetWindowLongPtr")] internal static extern IntPtr SetOwner64(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint="SetWindowLong")] internal static extern IntPtr SetOwner32(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle64(IntPtr window,int index);
    [DllImport("user32.dll", EntryPoint="GetWindowLongW")] static extern int GetStyle32(IntPtr window,int index);
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, EventCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr hook);

    internal static IntPtr Root(IntPtr h) { IntPtr r = GetAncestor(h, 2); return r == IntPtr.Zero ? h : r; }
    internal static double Scale(IntPtr h) { try { uint d = GetDpiForWindow(h); return d == 0 ? 1 : d / 96.0; } catch (EntryPointNotFoundException) { return 1; } }
    internal static bool BoundsOf(IntPtr h, out Rect b)
    {
        return DwmGetWindowAttribute(h, 9, out b, Marshal.SizeOf(typeof(Rect))) == 0 || GetWindowRect(h, out b);
    }
    internal static bool IsTopmost(IntPtr window)
    {
        if(window==IntPtr.Zero) return false;
        long style=IntPtr.Size==8?GetStyle64(window,-20).ToInt64():GetStyle32(window,-20);
        return (style&8)!=0;
    }
    internal static bool PlaceNormal(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags)
    {
        // Inserting after a topmost HWND silently promotes the inserted window.
        // Keep our existing window in the normal band, below topmost popups.
        if(IsTopmost(window) && !SetWindowPos(window,new IntPtr(-2),0,0,0,0,0x1|0x2|0x10|0x200)) return false;
        if(IsTopmost(after)) after=IntPtr.Zero;
        return SetWindowPos(window,after,x,y,width,height,flags|0x10);
    }
}

internal sealed class UsageBar : Form
{
    readonly UsageLabel label = new UsageLabel();
    readonly DetailsPopup tooltip = new DetailsPopup();
    Appearance appearance;
    DateTime nextThemeRead=DateTime.MinValue;
    internal string ThemePath=Appearance.ConfigPath;
    internal bool DarkTheme { get { return appearance!=null && appearance.Dark; } }
    internal Rectangle PopupBounds { get { return tooltip.Bounds; } }
    internal bool NavigationWorkerStopped { get { return sidebarState.WorkerStopped; } }
    readonly NotifyIcon tray = new NotifyIcon();
    Icon trayIcon=ToolIcon.Create();
    bool taskbarDark=ToolIcon.SystemTaskbarDark;
    int taskbarPixelSize=ToolIcon.CurrentTaskbarPixelSize;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly MenuDismissal menuDismissal;
    readonly ResetNewsClient newsClient=new ResetNewsClient();
    readonly ResetNewsState newsState;
    readonly NewsDiagnostic newsDiagnostic;
    readonly ResetNewsCard newsCard;
    readonly ToolStripMenuItem newsEntry;
    readonly System.Collections.Generic.List<Font> menuFonts=new System.Collections.Generic.List<Font>();
    readonly System.Collections.Generic.List<PopupShadow> menuShadows=new System.Collections.Generic.List<PopupShadow>();
    double menuScale;
    DateTime nextNewsRead=DateTime.MinValue;
    int newsBusy,newsFailures;
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer entryPointerTimer = new System.Windows.Forms.Timer { Interval=30 };
    readonly EntryInput entryInput;
    bool renderingEntry;
    internal int EntrySurfaceError { get; private set; }
    readonly TooltipHover tooltipHover;
    readonly UiPlacementPreferences placementPreferences;
    readonly bool persistPlacement;
    readonly Native.EventCallback eventCallback;
    readonly IntPtr foregroundHook, locationHook, nativeMenuHook;
    bool nativeMenuEventOpen;
    IntPtr nativeMenuEventTarget;
    uint nativeMenuEventThread;
    int nativeMenuDepth;
    readonly System.Collections.Generic.Dictionary<IntPtr,int> nativeMenuSources=new System.Collections.Generic.Dictionary<IntPtr,int>();
    readonly System.Collections.Generic.Dictionary<IntPtr,uint> nativeMenuSourceThreads=new System.Collections.Generic.Dictionary<IntPtr,uint>();
    readonly IntPtr fixtureTarget;
    readonly SidebarState sidebarState=new SidebarState();
    readonly QuotaClient quotaClient=new QuotaClient();
    readonly bool demo;
    readonly ToolStripMenuItem info;
    bool nativePopupActive;
    string positionConflict="",positionAttemptConflict="";
    bool? lastAttemptedNewsUnread,lastAppliedNewsUnread;
    int lastNewsSurfaceError=-1;
    int? rejectedPositionOffsetDip;
    int positionCommandCount;
    readonly PositionTrace positionTrace;
    int trackingSequence;
    bool? lastNativePositionSucceeded;
    int lastNativePositionError;
    Rectangle lastComputedPosition;
    internal string PositionConflict { get { return positionConflict; } }
    internal int ActualAvatarGapPixels { get; private set; }
    internal bool UsingProjectedNavigation { get; private set; }
    internal bool EntryFullyOccluded { get; private set; }
    QuotaSnapshot snapshot;
    string quotaError;
    int quotaBusy, failures, layoutMode;
    DateTime nextQuotaRead=DateTime.MinValue;
    bool stale;
    internal bool VerticalLayout { get; private set; }
    IntPtr owner;
    bool allowVisible, paused, pending, tipVisible;
    bool pendingTrayMenu,menuOpenedFromTray,newsOpenedFromTray;
    int menuGeneration;
    volatile bool closing;
    DateTime nextTipUpdate, nextTargetSearch;
    double fontScale;
    int bottomGap = 42;
    internal int PositionOffsetDip { get { return 42-bottomGap; } }
    internal bool LastPositionSaveSucceeded { get; private set; }
    internal string LastState = "waiting";
    internal Rectangle LastTargetBounds { get; private set; }

    internal UsageBar(IntPtr fixture, bool demoMode,string fixturePlacementPath=null)
    {
        fixtureTarget = fixture;
        demo=demoMode;
        persistPlacement=fixture==IntPtr.Zero || fixturePlacementPath!=null;
        placementPreferences=new UiPlacementPreferences(fixturePlacementPath??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            fixture==IntPtr.Zero?"runtime":"fixture-runtime","ui-placement.json"));
        if(persistPlacement) bottomGap=42-UiPlacementPreferences.Clamp(placementPreferences.Load());
        LastPositionSaveSucceeded=true;
        positionTrace=new PositionTrace(Path.Combine(Path.GetDirectoryName(placementPreferences.FilePath),"position-trace.json"));
        positionTrace.StartupRecord(new { loadedOffsetDip=PositionOffsetDip,persistPlacement=persistPlacement,
            preferencesPath=placementPreferences.FilePath,loadStatus=placementPreferences.LastLoadStatus,
            loadError=placementPreferences.LastLoadError,preferencesExist=File.Exists(placementPreferences.FilePath) });
        positionTrace.RegisterMessageFilter();
        newsState=new ResetNewsState(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fixture==IntPtr.Zero?"runtime":"fixture-runtime","tibo-news.json"));
        newsDiagnostic=new NewsDiagnostic(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,fixture==IntPtr.Zero?"runtime":"fixture-runtime","tibo-diagnostic.json"));
        newsCard=new ResetNewsCard(delegate(string id,string version) { newsState.MarkRead(id,version); UpdateNewsBadge(); TraceNews("message-read"); },delegate { newsState.MarkAllRead(); UpdateNewsBadge(); TraceNews("messages-read"); });
        newsCard.Closed+=delegate { newsOpenedFromTray=false; TraceNews("card-closed"); };
        snapshot=demo?QuotaParser.Demo():null;
        Text = "Codex Usage Mini — 额度条";
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(39, 42, 48);
        Size = new Size(226, 28);
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleCenter;
        label.ForeColor = Color.FromArgb(218, 225, 233);
        label.AutoEllipsis = true;
        label.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular, GraphicsUnit.Pixel);
        Controls.Add(label);
        // A hidden control supplies layout and alpha pixels; the existing form
        // presents them with UpdateLayeredWindow, without a child GDI surface.
        label.Visible=false;
        label.FrameInvalidated=RenderEntrySurface;
        entryInput=new EntryInput(delegate(Point point) {
            return !closing && Visible && EntrySurfaceError==0 && !nativePopupActive && !menu.Visible && !newsCard.Visible && PointerHitsBar(point);
        },delegate(Point point) {
            positionTrace.Record("entry-open-request",new { inputHookActive=entryInput.Active });
            try { BeginInvoke((MethodInvoker)delegate {
                if(closing || !Visible || nativePopupActive) return;
                TracePosition("entry-open-dispatch",null,true);
                pendingTrayMenu=false; menuOpenedFromTray=false;
                menu.Show(this,new Point(Width,0));
            }); } catch(InvalidOperationException) { }
        });
        label.Paint+=delegate(object sender,PaintEventArgs e) {
            if(!newsState.HasUnread) return;
            float scale=(float)(fontScale>0?fontScale:1);
            using(var brush=new SolidBrush(Color.FromArgb(207,101,92))) {
                e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillEllipse(brush,label.Width-6.4f*scale,.6f*scale,4.8f*scale,4.8f*scale);
            }
        };
        UpdateTheme();

        info = new ToolStripMenuItem(demo ? "演示模式 · 非真实额度" : "正在读取额度"); info.Enabled = false; info.Name="clock";
        menu.Items.Add(info);
        menu.DropShadowEnabled=false;
        menuShadows.Add(PopupShadow.Attach(menu,1,12));
        newsEntry=new ToolStripMenuItem("Tibo 重置消息",null,delegate {
            if(closing || IsDisposed) return;
            bool fromTray=menuOpenedFromTray;
            Point location=menu.Location;
            int requestedMenu=menuGeneration;
            menu.Close(); BeginInvoke((MethodInvoker)delegate {
                if(requestedMenu==menuGeneration) OpenNewsFromMenu(fromTray,location);
            });
        });
        newsEntry.Name="news";
        menu.Items.Add(newsEntry); menu.Items.Add(new ToolStripSeparator());
        menuDismissal=new MenuDismissal(menu);
        menuDismissal.ObserveOwnPress=delegate(long kind,string role,string item,bool enabled,bool available) {
            positionTrace.RecordPhysicalMenu(kind,role,item,enabled,available);
        };
        menu.Opening+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {
            // ItemClicked preserves SourceControl until after the item's Click event.
            // Capture the current tray gesture instead of trusting that retained value.
            menuGeneration++;
            menuOpenedFromTray=pendingTrayMenu; pendingTrayMenu=false;
            if(nativePopupActive && !menuOpenedFromTray) { e.Cancel=true; return; }
            CancelTooltip(); newsCard.Close();
            UpdateMenuStatus(); SetMenuMetrics(fontScale>0?fontScale:1); UpdatePositionCommands();
        };
        menu.Opened+=delegate {
            double scale=fontScale>0?fontScale:1;
            MiniChrome.SetRoundedRegion(menu,(float)(12*scale));
            if(!menuOpenedFromTray) menu.Location=BarMenuLocation(Bounds,LastTargetBounds,menu.Size,Screen.FromControl(this).WorkingArea,scale);
            RefreshPopupExclusions();
        };
        tray.MouseDown+=delegate(object sender,MouseEventArgs e) { if(e.Button==MouseButtons.Right) pendingTrayMenu=true; };
        tray.MouseUp+=delegate(object sender,MouseEventArgs e) {
            if(e.Button!=MouseButtons.Right) return;
            pendingTrayMenu=false;
            // NotifyIcon displays its menu before raising MouseUp. Also recognize
            // a right-up notification without a preceding down notification.
            if(menu.Visible) menuOpenedFromTray=true;
        };
        label.ContextMenuOpening=delegate { pendingTrayMenu=false; menuOpenedFromTray=false; };
        ToolStripMenuItem toggle = new ToolStripMenuItem("暂时隐藏"); toggle.Name="hide";
        toggle.Click += delegate { paused = !paused; toggle.Text = paused ? "恢复显示" : "暂时隐藏"; UpdateTracking(); };
        menu.Items.Add(toggle);
        var position=new ToolStripMenuItem("位置调整");
        position.Name="move";
        position.DropDownItems.Add("上微调", null, delegate { PositionCommand(PositionOffsetDip-8,false); }).Name="up";
        position.DropDownItems.Add("下微调", null, delegate { PositionCommand(PositionOffsetDip+8,false); }).Name="down";
        position.DropDownItems.Add(new ToolStripSeparator());
        position.DropDownItems.Add("恢复初始", null, delegate { PositionCommand(0,true); }).Name="reset";
        // A rejected command leaves the position commands available.
        // Accepted commands close explicitly; the outside-click observer remains.
        position.DropDown.AutoClose=false;
        menuShadows.Add(PopupShadow.Attach(position.DropDown,1,12));
        AttachSubmenuAnchor(position,delegate { return fontScale>0?fontScale:1; });
        menu.Items.Add(position);
        AttachMenuChildDismissal(menu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出额度条", null, delegate { Close(); }).Name="exit";
        tray.Icon = trayIcon;
        tray.Text = "Codex Usage Mini · 右键设置或退出";
        tray.ContextMenuStrip = menu;
        label.ContextMenuStrip=menu;
        tray.Visible = fixture == IntPtr.Zero;
        SetMenuMetrics(1); ApplyMenuTheme(menu.Items);
        positionTrace.AttachDropDown(menu,"main");
        positionTrace.AttachDropDown(position.DropDown,"position");
        menu.Opened+=delegate { TracePosition("menu-state",new { surface="main",eventKind="opened" },true); };
        position.DropDown.Opened+=delegate { TracePosition("menu-state",new { surface="position",eventKind="opened" },true); };
        menu.LocationChanged+=delegate { TracePosition("menu-geometry",new { surface="main",eventKind="location" }); };
        menu.SizeChanged+=delegate { TracePosition("menu-geometry",new { surface="main",eventKind="size" }); };
        position.DropDown.LocationChanged+=delegate { TracePosition("menu-geometry",new { surface="position",eventKind="location" }); };
        position.DropDown.SizeChanged+=delegate { TracePosition("menu-geometry",new { surface="position",eventKind="size" }); };
        tooltipHover=new TooltipHover(CanOpenTooltip,CanKeepTooltip,delegate { return tipVisible && tooltip.Visible; },ShowDetails,HideTooltip);
        entryPointerTimer.Tick+=delegate {
            if(closing || !Visible) return;
            label.SetPointerHover(!nativePopupActive && PointerHitsBar(Cursor.Position));
            UpdateHover();
        };
        label.MouseEnter+=delegate { tooltipHover.EnterBar(); };
        label.MouseLeave+=delegate { tooltipHover.LeaveBar(); };
        MouseEnter+=delegate { tooltipHover.EnterBar(); };
        MouseLeave+=delegate { tooltipHover.LeaveBar(); };
        tooltip.MouseEnter+=delegate { tooltipHover.EnterPopup(); };
        tooltip.MouseLeave+=delegate { tooltipHover.LeavePopup(); };

        // Create the hidden native window before registering asynchronous hooks.
        IntPtr initializedHandle = Handle;
        eventCallback = OnWindowEvent;
        foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, eventCallback, 0, 0, 2);
        locationHook = Native.SetWinEventHook(0x800B, 0x800B, IntPtr.Zero, eventCallback, 0, 0, 2);
        nativeMenuHook = Native.SetWinEventHook(6,7,IntPtr.Zero,eventCallback,0,0,2);
        timer.Interval = 150;
        timer.Tick += delegate
        {
            if(Program.ExitSignal!=null && Program.ExitSignal.WaitOne(0)) { Close(); return; }
            RefreshQuota(); RefreshNews(); UpdateTheme(); UpdateTracking(); UpdateHover();
        };
        timer.Start();
        UpdateNewsBadge();
        positionTrace.Record("startup-ready",new { inputHookActive=entryInput.Active,normalHost=fixture==IntPtr.Zero });
        positionTrace.FlushNow();
    }

    // Fixtures dispose the bar directly, so FormClosed alone is not a cleanup path.
    internal void Shutdown()
    {
        if(closing) return;
        closing=true;
        timer.Stop();
        entryPointerTimer.Stop(); entryPointerTimer.Dispose(); entryInput.Dispose();
        if(tooltipHover!=null) tooltipHover.Dispose();
        if(foregroundHook!=IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
        if(locationHook!=IntPtr.Zero) Native.UnhookWinEvent(locationHook);
        if(nativeMenuHook!=IntPtr.Zero) Native.UnhookWinEvent(nativeMenuHook);
        menuDismissal.Dispose();
        sidebarState.Dispose();
        menu.Close(); newsCard.Close(); tooltip.Hide();
        foreach(var shadow in menuShadows) shadow.Dispose(); menuShadows.Clear();
        tray.Visible=false;
        quotaClient.Dispose(); newsClient.Dispose();
        timer.Dispose(); newsCard.Dispose(); tooltip.Dispose(); tray.Dispose(); trayIcon.Dispose(); menu.Dispose();
        foreach(var font in menuFonts) font.Dispose(); menuFonts.Clear();
        label.Font.Dispose();
        positionTrace.Dispose();
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if(!e.Cancel) Shutdown();
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) Shutdown();
        base.Dispose(disposing);
    }

    void UpdateNewsBadge()
    {
        newsEntry.AccessibleDescription=newsState.HasUnread?"unread":null;
        newsEntry.AccessibleName=newsState.HasUnread?"Tibo 重置消息，有未读消息":"Tibo 重置消息";
        label.Invalidate();
        RenderEntrySurface();
        TraceNews("badge-updated");
    }
    void TraceNews(string stage)
    {
        if(newsDiagnostic==null || newsState==null || newsCard==null) return;
        var versions=new System.Collections.Generic.List<object>();
        int unreadCount=0;
        foreach(var item in newsState.Cache.Snapshot.Items) {
            string readVersion;
            newsState.Cache.Read.TryGetValue(item.Id,out readVersion);
            bool unread=newsState.Unread(item);
            if(unread) unreadCount++;
            versions.Add(new { id=item.Id,version=item.Version,readVersion=readVersion,status=item.Status,unread=unread });
        }
        newsDiagnostic.Record(stage,new {
            initialized=newsState.Cache.Initialized,day=newsState.Cache.Snapshot.Day,
            checkedAt=newsState.Cache.Snapshot.VerifiedAt,lastSuccess=newsState.Cache.LastSuccess,
            itemCount=versions.Count,unreadCount=unreadCount,readEntryCount=newsState.Cache.Read.Count,
            items=versions,card=newsCard.MessageDiagnostic,
            menuUnread=newsEntry==null?(bool?)null:newsEntry.AccessibleDescription=="unread",
            attemptedUnread=lastAttemptedNewsUnread,appliedUnread=lastAppliedNewsUnread,surfaceError=EntrySurfaceError
        });
    }
    void OpenNews()
    { OpenNewsFromMenu(false,Point.Empty); }
    void OpenNewsFromMenu(bool fromTray,Point trayLocation)
    {
        if(closing || IsDisposed) return;
        if(nativePopupActive && !fromTray) return;
        CancelTooltip();
        newsCard.Close(); newsOpenedFromTray=fromTray;
        newsCard.Open(this,newsState,appearance,fontScale>0?fontScale:1,fromTray?(Point?)trayLocation:null,LastTargetBounds);
        TraceNews("card-opened");
        RefreshPopupExclusions();
    }
    void RefreshNews()
    {
        if(closing || demo || DateTime.UtcNow<nextNewsRead || Interlocked.CompareExchange(ref newsBusy,1,0)!=0) return;
        nextNewsRead=DateTime.UtcNow.AddMinutes(10);
        string etag=newsState.Cache.Snapshot.Day==ResetNewsParser.BeijingToday?newsState.Cache.ETag:null;
        ThreadPool.QueueUserWorkItem(delegate {
            ResetNewsResponse result=newsClient.Read(etag);
            if(closing || IsDisposed || !IsHandleCreated) { Interlocked.Exchange(ref newsBusy,0); return; }
            try { BeginInvoke((MethodInvoker)delegate {
                if(closing || IsDisposed) return;
                if(result.Snapshot!=null) { TraceNews("snapshot-before"); newsState.Apply(result.Snapshot,result.ETag); newsFailures=0; if(newsState.ContentChanged) newsCard.ShowNewDataNotice(newsState.Cache.Snapshot); TraceNews("snapshot-applied"); }
                else if(result.Unchanged && newsState.Cache.Initialized) { newsState.Success(); newsFailures=0; TraceNews("source-unchanged"); }
                else { newsState.Error=result.Error??"暂时没有消息，请稍后查看"; newsFailures=Math.Min(4,newsFailures+1); TraceNews("source-failed"); }
                double delay=Math.Max(600*Math.Pow(2,newsFailures),result.RetrySeconds);
                if(Double.IsNaN(delay) || Double.IsInfinity(delay)) delay=86400;
                nextNewsRead=DateTime.UtcNow.AddSeconds(Math.Min(delay,(DateTime.MaxValue-DateTime.UtcNow).TotalSeconds-1));
                Interlocked.Exchange(ref newsBusy,0); UpdateNewsBadge();
            }); } catch(InvalidOperationException) { Interlocked.Exchange(ref newsBusy,0); }
        });
    }

    internal void SetLayout(int mode)
    {
        layoutMode=mode;
        UpdateTracking();
    }
    void UpdateTheme()
    {
        if(DateTime.UtcNow<nextThemeRead) return;
        nextThemeRead=DateTime.UtcNow.AddSeconds(1);
        appearance=Appearance.Read(ThemePath,appearance);
        BackColor=appearance.Rail; label.BackColor=appearance.Rail;
        label.ForeColor=OldData&&!demo?appearance.Muted:appearance.Ink;
        tooltip.ApplyTheme(appearance);
        newsCard.ApplyTheme(appearance);
        UpdateMenuStatus();
        ApplyMenuTheme(menu.Items);
        menu.BackColor=appearance.Surface; menu.ForeColor=appearance.Ink;
        menu.Renderer=new MiniMenuRenderer(appearance,fontScale>0?fontScale:1);
        bool dark=ToolIcon.SystemTaskbarDark;
        int pixels=ToolIcon.CurrentTaskbarPixelSize;
        if(dark!=taskbarDark || pixels!=taskbarPixelSize) {
            var previous=trayIcon; trayIcon=ToolIcon.Create(dark,pixels); tray.Icon=trayIcon;
            taskbarDark=dark; taskbarPixelSize=pixels; previous.Dispose();
        }
    }
    void ApplyMenuTheme(ToolStripItemCollection items)
    {
        foreach(ToolStripItem item in items) {
            item.BackColor=appearance.Surface; item.ForeColor=appearance.Ink;
            var group=item as ToolStripDropDownItem;
            if(group!=null && group.DropDownItems.Count>0) {
                group.DropDown.BackColor=appearance.Surface; group.DropDown.ForeColor=appearance.Ink;
                group.DropDown.Renderer=new MiniMenuRenderer(appearance,fontScale>0?fontScale:1);
                ApplyMenuTheme(group.DropDownItems);
            }
        }
    }
    void UpdateMenuStatus()
    {
        if(info==null) return;
        if(snapshot==null) info.Text=quotaError??"正在读取额度";
        else info.Text=(demo?"演示 · ":OldData?"旧值 · ":"")+QuotaDetails.UpdatedText(snapshot,DateTimeOffset.Now);
    }
    void SetMenuMetrics(double scale)
    {
        if(Math.Abs(menuScale-scale)<.01) return;
        foreach(var font in menuFonts) font.Dispose(); menuFonts.Clear(); menuScale=scale;
        StyleMenu(menu,info,appearance,scale,menuFonts);
        foreach(var shadow in menuShadows) shadow.UpdatePlacement(scale,12);
    }
    static void StyleMenu(ContextMenuStrip surface,ToolStripItem statusItem,Appearance theme,double scale,System.Collections.Generic.List<Font> fonts)
    {
        var command=UiTypography.MenuFont(13.5f,scale);
        var status=UiTypography.MenuFont(11,scale);
        fonts.Add(command); fonts.Add(status);
        surface.AutoSize=false; surface.Size=new Size((int)Math.Round(229*scale),(int)Math.Round(178*scale));
        surface.ShowImageMargin=false; surface.Padding=new Padding((int)Math.Round(scale),(int)Math.Round(5*scale),(int)Math.Round(scale),(int)Math.Round(5*scale));
        surface.BackColor=theme.Surface; surface.ForeColor=theme.Ink;
        foreach(ToolStripItem item in surface.Items) {
            item.BackColor=theme.Surface; item.ForeColor=theme.Ink;
            item.AutoSize=false; item.Margin=Padding.Empty; item.Padding=Padding.Empty;
            item.Size=new Size((int)Math.Round(227*scale),(int)Math.Round((item==statusItem?32:item is ToolStripSeparator?10:29)*scale));
            item.Font=item==statusItem?status:command;
            var group=item as ToolStripMenuItem;
            if(group!=null && group.DropDownItems.Count>0) {
                var drop=group.DropDown as ToolStripDropDownMenu;
                if(drop!=null) drop.ShowImageMargin=false;
                group.DropDown.DropShadowEnabled=false;
                group.DropDown.Padding=new Padding((int)Math.Round(scale),(int)Math.Round(5*scale),(int)Math.Round(scale),(int)Math.Round(5*scale));
                group.DropDown.AutoSize=false; group.DropDown.Size=new Size((int)Math.Round(155*scale),(int)Math.Round(103*scale));
                group.DropDown.BackColor=theme.Surface; group.DropDown.ForeColor=theme.Ink;
                group.DropDown.Renderer=new MiniMenuRenderer(theme,scale);
                foreach(ToolStripItem child in group.DropDownItems) {
                    child.BackColor=theme.Surface; child.ForeColor=theme.Ink;
                    child.AutoSize=false; child.Font=command; child.Margin=Padding.Empty; child.Padding=Padding.Empty;
                    child.Size=new Size((int)Math.Round(153*scale),(int)Math.Round((child is ToolStripSeparator?6:29)*scale));
                }
            }
        }
        surface.Renderer=new MiniMenuRenderer(theme,scale);
    }
    // All coordinates are physical screen pixels. The anchor depends on the rail,
    // never the horizontal position of the pointer within the quota label.
    internal static Point BarMenuLocation(Rectangle bar,Rectangle host,Size popup,Rectangle area,double scale)
    {
        return PopupPlacement.ForBar(bar,host,popup,area,scale);
    }
    internal static Point SubmenuLocation(Rectangle parent,Rectangle item,Size popup,Rectangle area,double scale)
    {
        int gap=Math.Max(1,(int)Math.Round(4*scale));
        int x=parent.Right+gap;
        if(x+popup.Width>area.Right) x=parent.Left-popup.Width-gap;
        x=Math.Max(area.Left,Math.Min(x,area.Right-popup.Width));
        int y=SubmenuPlacement.CenteredY(item,popup.Height,area);
        return new Point(x,y);
    }
    static void PositionSubmenu(ToolStripDropDownItem item,double scale)
    {
        var parent=item.Owner;
        if(parent==null) return;
        var row=new Rectangle(parent.PointToScreen(item.Bounds.Location),item.Bounds.Size);
        MiniChrome.SetRoundedRegion(item.DropDown,(float)(12*scale));
        Point location=SubmenuLocation(parent.Bounds,row,item.DropDown.Size,Screen.FromControl(parent).WorkingArea,scale);
        // ToolStripDropDown.SetBoundsCore replaces Location with its standard
        // overlapping bounds. Set the existing native popup's screen position;
        // WM_WINDOWPOSCHANGED keeps managed bounds and the shadow in sync.
        Native.SetWindowPos(item.DropDown.Handle,IntPtr.Zero,location.X,location.Y,0,0,0x1|0x4|0x10);
    }
    static void AttachSubmenuAnchor(ToolStripDropDownItem item,Func<double> getScale)
    {
        bool updating=false;
        EventHandler place=delegate {
            if(updating || !item.DropDown.Visible) return;
            updating=true;
            try { PositionSubmenu(item,getScale()); } finally { updating=false; }
        };
        // WinForms can apply its standard overlapping position after Opened.
        // Observe those later native bounds changes as well as the first open.
        item.DropDown.Opened+=place;
        item.DropDown.LocationChanged+=place;
        item.DropDown.SizeChanged+=place;
    }
    static void AttachMenuChildDismissal(ContextMenuStrip surface)
    {
        surface.Closed+=delegate {
            foreach(ToolStripItem item in surface.Items) {
                var group=item as ToolStripDropDownItem;
                if(group!=null && group.HasDropDownItems) group.DropDown.Close();
            }
        };
    }
    internal static bool IsOwnedBy(IntPtr window,IntPtr target)
    {
        for(int depth=0;depth<32 && window!=IntPtr.Zero;depth++) {
            IntPtr next=Native.GetWindow(window,4);
            if(next==target) return true;
            if(next==window) return false;
            window=next;
        }
        return false;
    }
    // Separate native HWND popups are guarded in addition to embedded UIA menus.
    // Restrict the check to an actual owner chain, so another main window of the
    // same app or unrelated system windows never suppress this rail.
    internal static bool HostHasOwnedPopup(IntPtr target)
    { return HostHasOwnedPopup(target,null); }
    static bool HostHasOwnedPopup(IntPtr target,Predicate<IntPtr> ignore)
    {
        if(target==IntPtr.Zero) return false;
        uint hostProcess; Native.GetWindowThreadProcessId(target,out hostProcess);
        bool found=false;
        Native.EnumWindows(delegate(IntPtr window,IntPtr data) {
            if(window==target || !Native.IsWindowVisible(window) || Native.GetWindow(window,4)==IntPtr.Zero) return true;
            if(ignore!=null && ignore(window)) return true;
            uint process; Native.GetWindowThreadProcessId(window,out process);
            if(process==hostProcess && IsOwnedBy(window,target)) { found=true; return false; }
            return true;
        },IntPtr.Zero);
        return found;
    }
    bool IsToolPopup(IntPtr window)
    {
        if(window==Handle || tooltip.IsHandleCreated && window==tooltip.Handle ||
            newsCard.IsHandleCreated && window==newsCard.Handle || menu.IsHandleCreated && window==menu.Handle ||
            PopupShadow.IsShadowWindow(window)) return true;
        // WinForms can temporarily make our dropdown owned by the active host.
        // Its actual tool handle must not be mistaken for a host popup.
        var control=Control.FromHandle(window);
        if(control is UsageBar || control is DetailsPopup || control is ResetNewsCard || control is NewsHelpPopup) return true;
        foreach(ToolStripItem item in menu.Items) {
            var child=item as ToolStripDropDownItem;
            if(child!=null && child.HasDropDownItems && child.DropDown.IsHandleCreated && child.DropDown.Handle==window) return true;
        }
        return false;
    }
    void RefreshPopupExclusions()
    {
        var handles=new System.Collections.Generic.List<IntPtr>();
        foreach(Control control in new Control[]{this,tooltip,newsCard,menu})
            if(control.IsHandleCreated) handles.Add(control.Handle);
        foreach(ToolStripItem item in menu.Items) {
            var child=item as ToolStripDropDownItem;
            if(child!=null && child.HasDropDownItems && child.DropDown.IsHandleCreated) handles.Add(child.DropDown.Handle);
        }
        foreach(Form form in Application.OpenForms)
            if(form.IsHandleCreated && (form is NewsHelpPopup || PopupShadow.IsShadowWindow(form.Handle))) handles.Add(form.Handle);
        sidebarState.SetPopupWindowExclusions(handles.ToArray());
    }
    // A pure control-rendering fixture: same surface style, no host, hooks or commands.
    internal static ContextMenuStrip CreatePreviewMenu(Appearance theme,double scale,string updateText,bool unread)
    {
        var surface=new ContextMenuStrip();
        var status=new ToolStripMenuItem(updateText) { Enabled=false,Name="clock" };
        surface.Items.Add(status);
        surface.Items.Add(new ToolStripMenuItem("Tibo 重置消息") { Name="news",AccessibleDescription=unread?"unread":null });
        surface.Items.Add(new ToolStripSeparator());
        surface.Items.Add(new ToolStripMenuItem("暂时隐藏") { Name="hide" });
        var position=new ToolStripMenuItem("位置调整") { Name="move" };
        position.DropDownItems.Add(new ToolStripMenuItem("上微调") { Name="up" });
        position.DropDownItems.Add(new ToolStripMenuItem("下微调") { Name="down" });
        position.DropDownItems.Add(new ToolStripSeparator());
        position.DropDownItems.Add(new ToolStripMenuItem("恢复初始") { Name="reset" });
        surface.Items.Add(position); surface.Items.Add(new ToolStripSeparator());
        surface.Items.Add(new ToolStripMenuItem("退出额度条") { Name="exit" });
        var fonts=new System.Collections.Generic.List<Font>(); StyleMenu(surface,status,theme,scale,fonts);
        AttachSubmenuAnchor(position,delegate { return scale; });
        AttachMenuChildDismissal(surface);
        surface.Disposed+=delegate { foreach(var font in fonts) font.Dispose(); };
        return surface;
    }
    void RefreshQuota()
    {
        if(closing || demo || DateTime.UtcNow<nextQuotaRead || Interlocked.CompareExchange(ref quotaBusy,1,0)!=0) return;
        nextQuotaRead=DateTime.UtcNow.AddSeconds(60);
        ThreadPool.QueueUserWorkItem(delegate
        {
            QuotaResult result=quotaClient.Read();
            if(closing || IsDisposed || !IsHandleCreated) { Interlocked.Exchange(ref quotaBusy,0); return; }
            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    if(closing || IsDisposed) return;
                    ApplyQuotaResult(result);
                    Interlocked.Exchange(ref quotaBusy,0);
                    UpdateTracking();
                });
            }
            catch(InvalidOperationException) { Interlocked.Exchange(ref quotaBusy,0); }
        });
    }
    bool OldData { get { return stale || (snapshot!=null && (snapshot.ResetPassed || (DateTimeOffset.Now-snapshot.Fetched).TotalSeconds>120)); } }
    string QuotaText(bool upright)
    { return QuotaText(upright,demo); }
    string QuotaText(bool upright,bool sample)
    {
        if(snapshot==null) return upright ? (quotaError==null?"额度\n···":"额度\n—") : (quotaError==null?"额度 · 读取中…":"额度 · 未获取");
        var parts=new System.Collections.Generic.List<string>();
        if(upright)
        {
            if(sample) parts.Add("演示");
            parts.Add(OldData&&!sample?"旧值":"剩余");
            foreach(var w in snapshot.Windows) { parts.Add(w.Label); parts.Add(w.Percent); }
            return String.Join("\n",parts.ToArray());
        }
        foreach(var w in snapshot.Windows) parts.Add(w.Label+" "+w.Percent);
        return (sample?"演示 · ":"")+(OldData&&!sample?"旧值 · ":"剩余 · ")+String.Join("  ",parts.ToArray());
    }
    string Details()
    {
        if(snapshot==null) return quotaError??"正在通过本机 Codex 读取额度…";
        string text=QuotaDetails.Format(snapshot,DateTimeOffset.Now);
        if(demo) text="演示数据 · 非真实额度\n\n"+text;
        else if(OldData) text="数据已过期，等待更新\n\n"+text;
        if(quotaError!=null) text+="\n\n"+quotaError;
        return text;
    }

    protected override bool ShowWithoutActivation { get { return true; } }
    protected override void SetVisibleCore(bool value) { base.SetVisibleCore(value && allowVisible); }
    protected override CreateParams CreateParams
    {
        get { CreateParams p = base.CreateParams; p.ExStyle |= 0x80 | 0x08000000 | 0x80000; return p; }
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x7B) { pendingTrayMenu=false; menuOpenedFromTray=false; }
        if (m.Msg == 0x84) { m.Result = new IntPtr(1); return; } // HTCLIENT: isolate hover.
        if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    void OnWindowEvent(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint thread, uint time)
    {
        if(!closing && !IsDisposed && IsHandleCreated && (kind==6 || kind==7))
        {
            try { BeginInvoke((MethodInvoker)delegate {
                if(closing || owner==IntPtr.Zero) return;
                // END can arrive after the popup HWND/owner has been destroyed.
                // Use the thread and host accepted at START before querying HWND.
                IntPtr ended=window;
                if(kind==7 && window==IntPtr.Zero && nativeMenuEventTarget==owner) {
                    foreach(var entry in nativeMenuSourceThreads) if(entry.Value==thread) { ended=entry.Key; break; }
                }
                int accepted;
                if(kind==7 && nativeMenuEventTarget==owner && nativeMenuSources.TryGetValue(ended,out accepted) && nativeMenuSourceThreads[ended]==thread) {
                    if(accepted==1) { nativeMenuSources.Remove(ended); nativeMenuSourceThreads.Remove(ended); }
                    else nativeMenuSources[ended]=accepted-1;
                    nativeMenuDepth=Math.Max(0,nativeMenuDepth-1);
                    nativeMenuEventOpen=nativeMenuDepth!=0;
                    if(!nativeMenuEventOpen) nativeMenuEventThread=0;
                    UpdateTracking(); return;
                }
                if(kind==7) return; // An unaccepted source cannot consume another popup's END.
                uint hostProcess,eventProcess;
                Native.GetWindowThreadProcessId(owner,out hostProcess);
                Native.GetWindowThreadProcessId(window,out eventProcess);
                if(hostProcess!=eventProcess || (Native.Root(window)!=owner && !IsOwnedBy(window,owner))) return;
                nativeMenuEventTarget=owner; nativeMenuEventThread=thread;
                nativeMenuSources.TryGetValue(window,out accepted);
                nativeMenuSources[window]=accepted+1; nativeMenuSourceThreads[window]=thread;
                nativeMenuDepth++;
                nativeMenuEventOpen=nativeMenuDepth!=0;
                UpdateTracking();
            }); } catch(InvalidOperationException) { }
            return;
        }
        if (closing || IsDisposed || !IsHandleCreated || pending || (kind == 0x800B && objectId != 0)) return;
        pending = true;
        try { BeginInvoke((MethodInvoker)delegate { pending = false; if(!closing) UpdateTracking(); }); }
        catch (InvalidOperationException) { pending = false; }
    }
    bool MatchesTarget(IntPtr h)
    {
        if (fixtureTarget != IntPtr.Zero) return h == fixtureTarget;
        uint pid; Native.GetWindowThreadProcessId(h, out pid);
        try
        {
            using (Process process = Process.GetProcessById((int)pid))
            {
                string name = process.ProcessName;
                if (!name.Equals("ChatGPT", StringComparison.OrdinalIgnoreCase) && !name.Equals("Codex", StringComparison.OrdinalIgnoreCase)) return false;
                // Match a GUI window rather than any CLI executable with the same name.
                return process.MainWindowHandle == h;
            }
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (System.ComponentModel.Win32Exception) { return false; }
    }
    IntPtr FindTarget()
    {
        IntPtr foreground=Native.Root(Native.GetForegroundWindow());
        if(foreground!=IntPtr.Zero && MatchesTarget(foreground)) return foreground;
        if(owner!=IntPtr.Zero && Native.IsWindow(owner) && MatchesTarget(owner)) return owner;
        if(fixtureTarget!=IntPtr.Zero) return Native.IsWindow(fixtureTarget)?fixtureTarget:IntPtr.Zero;
        if(DateTime.UtcNow<nextTargetSearch) return IntPtr.Zero;
        nextTargetSearch=DateTime.UtcNow.AddSeconds(1);
        foreach(string name in new[]{"ChatGPT","Codex"})
            foreach(Process process in Process.GetProcessesByName(name))
                using(process)
                {
                    try
                    {
                        IntPtr h=process.MainWindowHandle;
                        if(h!=IntPtr.Zero && Native.IsWindowVisible(h) && MatchesTarget(h)) return h;
                    }
                    catch(InvalidOperationException) { }
                    catch(System.ComponentModel.Win32Exception) { }
                }
        return IntPtr.Zero;
    }
    void Conceal(string why)
    {
        LastState = why;
        TracePosition("tracking-conceal",new { reason=why,phase="before-hide" });
        entryPointerTimer.Stop(); entryInput.SetEnabled(false); label.SetPointerHover(false);
        nativePopupActive=false;
        // Tray surfaces stay usable without a visible host or quota bar.
        if(menu.Visible && !menuOpenedFromTray) menu.Close();
        if(newsCard.Visible && !newsOpenedFromTray) newsCard.Close();
        CancelTooltip();
        Hide();
    }
    internal void UpdateTracking()
    {
        if (closing || IsDisposed || !IsHandleCreated) return;
        trackingSequence++; lastNativePositionSucceeded=null; lastNativePositionError=0; lastComputedPosition=Rectangle.Empty;
        if (paused) { Conceal("paused"); return; }
        IntPtr target = FindTarget();
        if (target == IntPtr.Zero || !Native.IsWindowVisible(target) || Native.IsIconic(target)) { Conceal("target-unavailable"); return; }
        Native.Rect b;
        if (!Native.BoundsOf(target, out b)) { Conceal("bounds-unavailable"); return; }
        double s = Native.Scale(target);
        RefreshPopupExclusions();
        sidebarState.Poll(target);
        var navigation=sidebarState.Snapshot;
        if(nativeMenuEventTarget!=target) {
            nativeMenuEventOpen=false; nativeMenuEventTarget=IntPtr.Zero; nativeMenuEventThread=0; nativeMenuDepth=0;
            nativeMenuSources.Clear(); nativeMenuSourceThreads.Clear();
        }
        nativePopupActive=nativeMenuEventOpen || navigation.NativePopupVisible || HostHasOwnedPopup(target,IsToolPopup);
        if(nativePopupActive)
        {
            if(menu.Visible) TracePosition("native-popup-close",new { eventOpen=nativeMenuEventOpen,
                uiaPopup=navigation.NativePopupVisible,eventDepth=nativeMenuDepth },true);
            // Native menus keep their layer and hit targets. Only tool popups
            // are mutually exclusive; the rail itself stays present.
            menuGeneration++;
            if(menu.Visible) menu.Close();
            if(newsCard.Visible) newsCard.Close();
            CancelTooltip();
        }
        if(navigation.State<0)
        { Conceal("page-unavailable"); return; }
        bool rail=navigation.State==2;
        bool upright=rail || layoutMode==2 || (layoutMode==0 && navigation.State!=0);
        if(VerticalLayout!=upright) CancelTooltip();
        VerticalLayout=upright;
        label.Text=QuotaText(upright);
        label.Configure(snapshot,appearance,upright,OldData,demo,s);
        label.ForeColor=OldData&&!demo?appearance.Muted:appearance.Ink;
        // Extend the previous surface left and down while keeping its upper/right edges.
        int width = (int)Math.Round((upright?48:240)*s), height = upright?UsageLabel.HeightFor(snapshot,OldData,demo,s):(int)Math.Round(36*s);
        if (b.Right-b.Left < 330*s || b.Bottom-b.Top < 180*s) { Conceal("target-too-small"); return; }
        int x = b.Left;
        int y = b.Bottom - (int)Math.Round(bottomGap*s) - height;
        LastTargetBounds = Rectangle.FromLTRB(b.Left,b.Top,b.Right,b.Bottom);
        UsingProjectedNavigation=false;
        if(rail)
        {
            // While the async UIA read catches up, project the last confirmed
            // same-HWND rail from its original edge/DPI basis, without hiding.
            NavigationSnapshot projected;
            if(!NavigationGeometry.TryProject(navigation,LastTargetBounds,s,out projected))
            { Conceal("rail-space-unavailable"); return; }
            UsingProjectedNavigation=navigation.Host.Size!=LastTargetBounds.Size || Math.Abs(navigation.Scale-s)>.001;
            Rectangle placement;
            string conflict;
            if(!RailLayout.TryResolve(LastTargetBounds,s,projected.Avatar,projected.Update,projected.Obstacles,height,
                RailLayout.UpwardOffsetPixels(PositionOffsetDip,s),out placement,out conflict))
            { Conceal("rail-space-unavailable"); return; }
            x=placement.X; y=placement.Y; width=placement.Width; height=placement.Height;
            ActualAvatarGapPixels=projected.Avatar.Top-placement.Bottom;
            if(rejectedPositionOffsetDip.HasValue) {
                Rectangle requestedPlacement; string requestedConflict;
                if(TryRailPlacement(navigation,LastTargetBounds,s,height,rejectedPositionOffsetDip.Value,out requestedPlacement,out requestedConflict)) {
                    rejectedPositionOffsetDip=null; positionAttemptConflict="";
                }
            }
            if(positionAttemptConflict.Length>0) SetPositionConflict(positionAttemptConflict);
            else if(conflict=="update") SetPositionConflict("update");
            else { positionAttemptConflict=""; SetPositionConflict(""); }
        }
        if (Math.Abs(fontScale-s) > .01)
        {
            Font old=label.Font; label.Font=UiTypography.Font(11,s); old.Dispose(); fontScale=s; SetMenuMetrics(s);
        }
        if (owner != target)
        {
            // Track z-order ourselves. A native owner destroys owned windows when
            // the host closes, which would also end the tray tool's message loop.
            owner=target;
        }
        // Insert immediately above the owner, below any unrelated foreground window.
        // Capture the insertion point before Show() can change the z-order.
        IntPtr above=Native.GetWindow(target,3);
        while(above==Handle || above==tooltip.Handle || above==newsCard.Handle) above=Native.GetWindow(above,3);
        if (!Visible) { allowVisible=true; Show(); allowVisible=false; }
        lastComputedPosition=new Rectangle(x,y,width,height);
        bool positioned=Native.PlaceNormal(Handle, above, x,y,width,height,0x10 | 0x40);
        lastNativePositionSucceeded=positioned;
        lastNativePositionError=positioned?0:Marshal.GetLastWin32Error();
        if(!positioned) {
            Conceal("position-unavailable"); return;
        }
        ApplyEntryPopupRegion(navigation.NativePopupVisible?navigation.NativePopupBounds:new Rectangle[0]);
        RenderEntrySurface();
        entryInput.SetEnabled(EntrySurfaceError==0);
        if(EntrySurfaceError==0) entryPointerTimer.Start(); else entryPointerTimer.Stop();
        if(menu.Visible && !menuOpenedFromTray) {
            menu.Location=BarMenuLocation(Bounds,LastTargetBounds,menu.Size,Screen.FromControl(this).WorkingArea,s);
            foreach(ToolStripItem item in menu.Items) {
                var child=item as ToolStripDropDownItem;
                if(child!=null && child.HasDropDownItems && child.DropDown.Visible) PositionSubmenu(child,s);
            }
        }
        if(tipVisible) tooltip.PositionAbove(this,LastTargetBounds);
        if(newsCard.Visible && !newsOpenedFromTray) newsCard.PositionAbove(this,LastTargetBounds);
        LastState=EntrySurfaceError==0?"visible":"entry-render-unavailable";
        if(menu.Visible) UpdatePositionCommands();
        TracePosition("tracking-result",null);
    }
    void ApplyEntryPopupRegion(Rectangle[] popups)
    {
        Region clipped=null;
        foreach(Rectangle popup in popups) {
            Rectangle intersection=Rectangle.Intersect(Bounds,popup);
            if(intersection.IsEmpty) continue;
            if(clipped==null) clipped=new Region(ClientRectangle);
            intersection.Offset(-Left,-Top); clipped.Exclude(intersection);
        }
        EntryFullyOccluded=false;
        if(clipped!=null) using(var identity=new System.Drawing.Drawing2D.Matrix())
            EntryFullyOccluded=clipped.GetRegionScans(identity).Length==0;
        // A local hole lets the embedded host menu receive its own pixels and
        // clicks. An adjacent native popup never removes the whole rail.
        var previous=Region; Region=clipped;
        if(previous!=null) previous.Dispose();
    }
    static bool TryRailPlacement(NavigationSnapshot navigation,Rectangle host,double scale,int height,int offsetDip,out Rectangle placement)
    {
        string conflict;
        return TryRailPlacement(navigation,host,scale,height,offsetDip,out placement,out conflict);
    }
    static bool TryRailPlacement(NavigationSnapshot navigation,Rectangle host,double scale,int height,int offsetDip,out Rectangle placement,out string conflict)
    {
        placement=Rectangle.Empty; conflict="navigation";
        NavigationSnapshot projected;
        if(!NavigationGeometry.TryProject(navigation,host,scale,out projected)) return false;
        return RailLayout.TryPlace(host,scale,projected.Avatar,projected.Update,projected.Obstacles,height,
            RailLayout.UpwardOffsetPixels(offsetDip,scale),out placement,out conflict);
    }
    bool CanMovePosition(int offsetDip)
    {
        if(closing || !Visible || LastTargetBounds.IsEmpty || offsetDip==PositionOffsetDip ||
            offsetDip!=UiPlacementPreferences.Clamp(offsetDip)) return false;
        double scale=fontScale>0?fontScale:1;
        var navigation=sidebarState.Snapshot;
        Rectangle placement;
        if(navigation.State==2)
        {
            if(!TryRailPlacement(navigation,LastTargetBounds,scale,Height,offsetDip,out placement)) return false;
        }
        else
        {
            placement=new Rectangle(Left,LastTargetBounds.Bottom-(int)Math.Round((42-offsetDip)*scale)-Height,Width,Height);
            if(!LastTargetBounds.Contains(placement)) return false;
        }
        // A temporarily avoided position can already equal the requested pixel
        // rectangle. Persisting a different preference still counts as success.
        return true;
    }
    void UpdatePositionCommands()
    {
        if(menu==null || menu.IsDisposed) return;
        foreach(string name in new[]{"up","down","reset"})
        {
            var matches=menu.Items.Find(name,true);
            if(matches.Length==0) continue;
            int requested=PositionOffsetDip+(name=="down"?8:-8);
            matches[0].Enabled=name=="reset"?PositionOffsetDip!=0:
                Visible && requested==UiPlacementPreferences.Clamp(requested);
        }
    }
    void MovePosition(int deltaDip)
    { MovePositionTo(PositionOffsetDip+deltaDip,false); }
    void PositionCommand(int offsetDip,bool restoreInitial)
    {
        int before=PositionOffsetDip;
        positionTrace.BeginCommand();
        lastComputedPosition=Rectangle.Empty; lastNativePositionSucceeded=null; lastNativePositionError=0;
        TracePosition("command-enter",new { previousDip=before,requestedDip=offsetDip,restoreInitial=restoreInitial },true);
        try {
            bool accepted=MovePositionTo(offsetDip,restoreInitial);
            RecordPositionCommand(before,offsetDip,accepted);
            TracePosition("command-result",new { previousDip=before,requestedDip=offsetDip,
                accepted=accepted,saveAttempted=accepted&&persistPlacement,
                saved=accepted&&persistPlacement&&LastPositionSaveSucceeded },true);
            if(accepted) menu.Close();
            else if(menu.Visible) {
                var groups=menu.Items.Find("move",false);
                if(groups.Length>0) ((ToolStripMenuItem)groups[0]).ShowDropDown();
            }
        } catch(Exception error) {
            positionTrace.Record("command-exception",new { type=error.GetType().Name,hresult=error.HResult });
            positionTrace.FlushNow();
            throw;
        }
    }
    static object DiagnosticBounds(Rectangle rectangle)
    {
        return new { x=rectangle.X,y=rectangle.Y,width=rectangle.Width,height=rectangle.Height };
    }
    static object DiagnosticWindow(Control surface)
    {
        Native.Rect rectangle=new Native.Rect();
        bool created=surface!=null && surface.IsHandleCreated;
        bool readable=created && Native.GetWindowRect(surface.Handle,out rectangle);
        return new { hwnd=created?surface.Handle.ToInt64():0,visible=surface!=null && surface.Visible,
            nativeVisible=created && Native.IsWindowVisible(surface.Handle),boundsRead=readable,
            nativeBounds=DiagnosticBounds(Rectangle.FromLTRB(rectangle.Left,rectangle.Top,rectangle.Right,rectangle.Bottom)) };
    }
    void RecordPositionCommand(int previousDip,int requestedDip,bool accepted)
    {
        // Only geometry and this tool's placement state; no account/menu text.
        // This last-command record is complemented by position-trace.json,
        // which records input, native placement and the following refreshes.
        positionCommandCount++;
        var navigation=sidebarState.Snapshot;
        var record=new {
            version="0.11.10.0",utc=DateTime.UtcNow.ToString("o"),count=positionCommandCount,
            previousDip=previousDip,requestedDip=requestedDip,desiredDip=PositionOffsetDip,
            accepted=accepted,saveAttempted=accepted&&persistPlacement,
            saved=accepted&&persistPlacement&&LastPositionSaveSucceeded,conflict=positionConflict,
            scale=fontScale,state=LastState,navigationState=navigation.State,
            navigationFetchedUtc=navigation.Fetched.ToString("o"),
            host=DiagnosticBounds(LastTargetBounds),entry=DiagnosticBounds(Bounds),
            avatar=DiagnosticBounds(navigation.Avatar),update=DiagnosticBounds(navigation.Update),
            obstacles=navigation.Obstacles,actualAvatarGapPixels=ActualAvatarGapPixels
        };
        try {
            string path=Path.Combine(Path.GetDirectoryName(placementPreferences.FilePath),"position-last-command.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path,new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(record));
            positionTrace.Record("last-command-write",new { succeeded=true });
        } catch(IOException error) { RecordCommandWriteError(error); }
          catch(UnauthorizedAccessException error) { RecordCommandWriteError(error); }
          catch(System.Security.SecurityException error) { RecordCommandWriteError(error); }
    }
    void RecordCommandWriteError(Exception error)
    { positionTrace.Record("last-command-write",new { succeeded=false,type=error.GetType().Name,hresult=error.HResult }); }
    void TracePosition(string kind,object detail,bool forced=false)
    {
        if(positionTrace==null || (!forced && !positionTrace.CaptureActive)) return;
        var navigation=sidebarState.Snapshot;
        Native.Rect rectangle=new Native.Rect();
        bool actualRead=IsHandleCreated && Native.GetWindowRect(Handle,out rectangle);
        if(!actualRead) rectangle=new Native.Rect();
        var obstacles=navigation.Obstacles??new Rectangle[0];
        var bounded=new object[Math.Min(48,obstacles.Length)];
        for(int i=0;i<bounded.Length;i++) bounded[i]=DiagnosticBounds(obstacles[i]);
        var items=new System.Collections.Generic.List<object>();
        Rectangle childBounds=Rectangle.Empty; bool childVisible=false; ToolStripDropDown childSurface=null;
        var groups=menu.Items.Find("move",false);
        if(groups.Length>0) { var group=groups[0] as ToolStripDropDownItem;
            if(group!=null && group.HasDropDownItems) { childSurface=group.DropDown; childBounds=childSurface.Bounds; childVisible=childSurface.Visible; } }
        foreach(string name in new[]{"up","down","reset"}) {
            var matches=menu.Items.Find(name,true);
            if(matches.Length>0) items.Add(new { command=name,enabled=matches[0].Enabled,available=matches[0].Available });
        }
        positionTrace.Record(kind,new {
            detail=detail,trackingSequence=trackingSequence,desiredOffsetDip=PositionOffsetDip,
            cachedPersistedOffsetDip=placementPreferences.OffsetDip,persistPlacement=persistPlacement,
            saveSucceeded=LastPositionSaveSucceeded,saveStatus=placementPreferences.LastSaveStatus,
            saveError=placementPreferences.LastSaveError,preferencesExist=File.Exists(placementPreferences.FilePath),
            formBounds=DiagnosticBounds(Bounds),nativeBoundsRead=actualRead,
            nativeBounds=DiagnosticBounds(Rectangle.FromLTRB(rectangle.Left,rectangle.Top,rectangle.Right,rectangle.Bottom)),
            computedBounds=DiagnosticBounds(lastComputedPosition),setWindowPosSucceeded=lastNativePositionSucceeded,
            setWindowPosError=lastNativePositionError,host=DiagnosticBounds(LastTargetBounds),
            hostHwnd=owner.ToInt64(),entryHwnd=IsHandleCreated?Handle.ToInt64():0,scale=fontScale,
            visible=Visible,state=LastState,projected=UsingProjectedNavigation,entrySurfaceError=EntrySurfaceError,
            inputHookActive=entryInput!=null && entryInput.Active,inputHookError=entryInput==null?0:entryInput.LastError,
            navigationState=navigation.State,navigationFetchedUtc=navigation.Fetched.ToString("o"),
            avatar=DiagnosticBounds(navigation.Avatar),update=DiagnosticBounds(navigation.Update),
            obstacles=bounded,obstacleCount=obstacles.Length,nativePopupActive=nativePopupActive,
            uiaPopup=navigation.NativePopupVisible,nativeMenuEventOpen=nativeMenuEventOpen,
            actualAvatarGapPixels=ActualAvatarGapPixels,conflict=positionConflict,
            menuVisible=menu.Visible,menuGeneration=menuGeneration,menuOpenedFromTray=menuOpenedFromTray,
            menuBounds=DiagnosticBounds(menu.Bounds),positionMenuBounds=DiagnosticBounds(childBounds),positionMenuVisible=childVisible,
            menuNative=DiagnosticWindow(menu),positionMenuNative=DiagnosticWindow(childSurface),
            commandItems=items
        });
    }
    void SetPositionConflict(string conflict)
    { positionConflict=conflict; }
    bool MovePositionTo(int offsetDip,bool restoreInitial)
    {
        if(closing || offsetDip!=UiPlacementPreferences.Clamp(offsetDip)) return false;
        if(!restoreInitial && !CanMovePosition(offsetDip)) {
            double scale=fontScale>0?fontScale:1;
            Rectangle placement; string conflict;
            TryRailPlacement(sidebarState.Snapshot,LastTargetBounds,scale,Height,offsetDip,out placement,out conflict);
            positionAttemptConflict=conflict=="update"?"update":"navigation";
            SetPositionConflict(positionAttemptConflict); UpdatePositionCommands();
            rejectedPositionOffsetDip=offsetDip;
            return false;
        }
        positionAttemptConflict="";
        rejectedPositionOffsetDip=null;
        bottomGap=42-offsetDip;
        if(persistPlacement) LastPositionSaveSucceeded=placementPreferences.Save(offsetDip);
        UpdateTracking(); UpdatePositionCommands();
        return true;
    }
    bool TooltipAllowed()
    {
        return !closing && !IsDisposed && Visible && EntrySurfaceError==0 && LastState=="visible" &&
            menu!=null && !menu.Visible && !newsCard.Visible && !nativePopupActive && !nativeMenuEventOpen &&
            !sidebarState.Snapshot.NativePopupVisible;
    }
    bool CanOpenTooltip()
    {
        if(!TooltipAllowed()) return false;
        Point cursor=Cursor.Position;
        return Bounds.Contains(cursor) && PointerHitsBar(cursor);
    }
    bool CanKeepTooltip()
    {
        if(!TooltipAllowed()) return false;
        Point cursor=Cursor.Position;
        return Bounds.Contains(cursor) && PointerHitsBar(cursor) ||
            tipVisible && tooltip.Bounds.Contains(cursor) && PointerHitsPopup(cursor);
    }
    void HideTooltip()
    { tooltip.Hide(); tipVisible=false; }
    void CancelTooltip()
    {
        if(tooltipHover!=null) tooltipHover.Cancel();
        else HideTooltip();
    }
    void UpdateHover()
    {
        tooltipHover.Refresh();
        if(tipVisible && CanKeepTooltip() && DateTime.UtcNow>=nextTipUpdate) ShowDetails();
    }
    internal void ShowDetails()
    {
        string status=quotaError;
        tooltip.ShowQuotaDetails(this,VerticalLayout,snapshot,OldData,demo,status,appearance,fontScale>0?fontScale:1,DateTimeOffset.Now,LastTargetBounds);
        tipVisible=true;
        nextTipUpdate=DateTime.UtcNow.AddSeconds(1);
    }
    internal bool PointerHitsBar(Point point)
    {
        if(!Visible || !Bounds.Contains(point) || nativePopupActive) return false;
        if(Region!=null && !Region.IsVisible(point.X-Left,point.Y-Top)) return false;
        IntPtr hit=Native.Root(Native.WindowFromPoint(point));
        return hit==Handle || owner!=IntPtr.Zero && hit==owner;
    }
    void RenderEntrySurface()
    {
        if(renderingEntry || closing || !IsHandleCreated || appearance==null || ClientSize.Width<1 || ClientSize.Height<1) return;
        renderingEntry=true;
        try {
            label.Size=ClientSize;
            bool attemptedUnread=newsState.HasUnread;
            bool? previousApplied=lastAppliedNewsUnread;
            using(var frame=label.CreateTransparentBitmap(attemptedUnread)) {
                bool applied=LayeredEntrySurface.Apply(this,frame);
                EntrySurfaceError=applied?0:Math.Max(1,LayeredEntrySurface.LastError);
                if(applied) lastAppliedNewsUnread=attemptedUnread;
            }
            if(lastAttemptedNewsUnread!=attemptedUnread || previousApplied!=lastAppliedNewsUnread || lastNewsSurfaceError!=EntrySurfaceError) {
                lastAttemptedNewsUnread=attemptedUnread; lastNewsSurfaceError=EntrySurfaceError;
                TraceNews("entry-frame");
            }
            if(EntrySurfaceError!=0) {
                entryInput.SetEnabled(false); entryPointerTimer.Stop(); CancelTooltip();
            }
        } finally { renderingEntry=false; }
    }
    internal bool PointerHitsPopup(Point point) { return Native.Root(Native.WindowFromPoint(point))==tooltip.Handle; }
    internal void CheckNews(Action<string,bool> check,Action complete)
    {
        newsState.Cache.Initialized=false;
        newsState.Apply(ResetNewsChecks.Sample("demo","announced","测试用户","这是独立测试消息，不代表真实重置。"),null);
        newsState.Apply(ResetNewsChecks.Sample("demo","confirmed","测试用户","这是独立测试消息，不代表真实重置。"),null);
        UpdateNewsBadge();
        check("unread menu badge",newsEntry.AccessibleDescription=="unread");
        IntPtr foreground=Native.GetForegroundWindow();
        OpenNews();
        check("news card visible with dismissal hook",newsCard.Visible && newsCard.DismissalActive);
        check("opening preserves foreground window",Native.GetForegroundWindow()==foreground);
        check("card fits working area",Screen.FromControl(this).WorkingArea.Contains(newsCard.Bounds));
        check("quota tooltip is hidden",!tooltip.Visible);
        int step=0;
        var reading=new System.Windows.Forms.Timer { Interval=300 };
        reading.Tick+=delegate {
            if(step++==0) {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-read-diagnostic.txt"),newsCard.ReadDiagnostic);
                check("reading clears current badge after actual paint",!newsState.HasUnread && newsEntry.AccessibleDescription!="unread");
                newsCard.ApplyTheme(Appearance.Palette(true));
                newsCard.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-card-dark-demo.png"));
                newsCard.ApplyTheme(Appearance.Palette(false));
                check("open card follows light theme",newsCard.BackColor==Appearance.Palette(false).Surface);
                newsCard.SavePreview(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-card-light-demo.png"));
                newsState.Apply(ResetNewsChecks.Sample("demo","confirmed","修正后的测试范围","更新测试"),null); UpdateNewsBadge();
                newsCard.ShowNewDataNotice();
                check("new version while open remains unread",newsState.HasUnread && newsEntry.AccessibleDescription=="unread");
                newsCard.CheckOutside(new Point(newsCard.Left+10,newsCard.Top+10));
                check("inside click keeps card open",newsCard.Visible);
                newsCard.CheckOutside(new Point(newsCard.Right+10,newsCard.Bottom+10));
                check("outside left click closes and releases hook",!newsCard.Visible && !newsCard.DismissalActive);
                OpenNews();
            } else {
                check("reopen reads updated version after actual paint",!newsState.HasUnread);
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"news-read-diagnostic.txt"),"\nReopen: "+newsCard.ReadDiagnostic);
                reading.Stop(); reading.Dispose(); newsCard.Close(); complete();
            }
        };
        reading.Start();
    }
    void ApplyQuotaResult(QuotaResult result)
    {
        if(result.Snapshot!=null) {
            snapshot=result.Snapshot; stale=false; quotaError=null; failures=0;
            UpdateMenuStatus();
            nextQuotaRead=DateTime.UtcNow.AddSeconds(60);
        } else {
            if(snapshot==null || String.IsNullOrEmpty(result.VerifiedAccount) || snapshot.AccountKey!=result.VerifiedAccount) snapshot=null;
            stale=true; quotaError=result.Error; info.Text=result.Error;
            failures=Math.Min(4,failures+1);
            nextQuotaRead=DateTime.UtcNow.AddSeconds(Math.Min(120,15*Math.Pow(2,failures-1)));
        }
    }
    internal void CheckQuotaStates(Action<string,bool> check)
    {
        var saved=snapshot; bool savedStale=stale; string savedError=quotaError,savedInfo=info.Text;
        int savedFailures=failures; DateTime savedNext=nextQuotaRead;
        try {
            var weekly=new QuotaSnapshot { AccountKey="fixture-account",Fetched=DateTimeOffset.Now };
            weekly.Windows.Add(new QuotaWindow { Minutes=10080,Remaining=23,Reset=DateTimeOffset.Now.AddHours(2) });
            ApplyQuotaResult(new QuotaResult { Snapshot=weekly });
            check("weekly-only presentation does not invent five-hour quota",snapshot.Windows.Count==1 && !QuotaText(true).Contains("5h") && !Details().Contains("5 小时"));
            weekly.Fetched=DateTimeOffset.Now.AddMinutes(-3);
            check("old snapshot is visibly marked",OldData && QuotaText(true,false).Contains("旧值"));
            ApplyQuotaResult(new QuotaResult { VerifiedAccount="fixture-account",Error="网络连接中断（测试）" });
            check("offline same account preserves marked old value",snapshot==weekly && stale && quotaError!=null);
            ApplyQuotaResult(new QuotaResult { VerifiedAccount="another-fixture-account",Error="账户已变化（测试）" });
            check("account change clears previous account value",snapshot==null);
            ApplyQuotaResult(new QuotaResult { Snapshot=weekly });
            ApplyQuotaResult(new QuotaResult { Error="账户无法确认（测试）" });
            check("unknown account clears previous value",snapshot==null);
            weekly.Fetched=DateTimeOffset.Now; weekly.Windows[0].Reset=DateTimeOffset.Now.AddSeconds(-1);
            ApplyQuotaResult(new QuotaResult { Snapshot=weekly });
            check("elapsed reset waits for data without inventing 100 percent",Details().Contains("等待刷新") && snapshot.Windows[0].Remaining==23);
            weekly.Windows[0].Reset=null;
            check("missing reset time remains explicit",Details().Contains("重置时间未提供"));
            var instant=new DateTimeOffset(2026,10,3,19,29,31,TimeSpan.FromHours(8));
            weekly.Fetched=instant.AddSeconds(-31);
            check("tooltip retains HH:mm and seconds-age format",QuotaDetails.Format(weekly,instant).Contains("更新于"+weekly.Fetched.ToLocalTime().ToString("HH:mm")+"（31s前）"));
        } finally {
            snapshot=saved; stale=savedStale; quotaError=savedError; info.Text=savedInfo;
            failures=savedFailures; nextQuotaRead=savedNext; UpdateTracking();
        }
    }
    internal void CheckMenu(Action<string,bool> check)
    {
        bool legacy=false;
        foreach(ToolStripItem item in menu.Items) if(item.Text.Contains("旧版") || item.Text.Contains("自动定位")) legacy=true;
        check("menu contains no legacy layout choices",!legacy);
        ShowDetails();
        menu.Show(this,new Point(Width,0));
        check("menu opens and outside-click observer is active",menu.Visible && menuDismissal.Active);
        check("opening menu dismisses quota tooltip",!tooltip.Visible);
        menuDismissal.ProcessLeftPress(new Point(menu.Left+10,menu.Top+10));
        check("left press inside menu keeps item interaction",menu.Visible);
        menuDismissal.ProcessLeftPress(new Point(Left+Width/2,Top+Height/2));
        check("left press on bar dismisses menu",!menu.Visible && !menuDismissal.Active);
        menu.Show(this,new Point(Width,0));
        menuDismissal.ProcessLeftPress(new Point(menu.Right+10,menu.Bottom+10));
        check("left press outside bar also dismisses menu",!menu.Visible && !menuDismissal.Active);
        menu.Show(this,new Point(Width,0));
        int before=bottomGap;
        foreach(ToolStripItem item in menu.Items) {
            var group=item as ToolStripDropDownItem;
            if(group!=null) foreach(ToolStripItem child in group.DropDownItems) if(child.Text=="上微调") child.PerformClick();
        }
        check("menu command still runs",bottomGap==Math.Min(300,before+8));
        bottomGap=before; menu.Close(); UpdateTracking();
        check("closing menu releases observer",!menuDismissal.Active);
    }
    internal string PopupDiagnostic
    {
        get { return "popup="+tooltip.Handle+" visible="+tooltip.Visible+" nativeVisible="+Native.IsWindowVisible(tooltip.Handle)+" bounds="+tooltip.Bounds+" hit="+Native.Root(Native.WindowFromPoint(new Point(tooltip.Left+20,tooltip.Top+20)))+" owner="+owner+" bar="+Handle+" previous="+Native.GetWindow(tooltip.Handle,3); }
    }
    internal string LayoutDiagnostic
    {
        get { var n=sidebarState.Snapshot; return "state="+LastState+" visible="+Visible+" vertical="+VerticalLayout+
            " bar="+Bounds+" target="+LastTargetBounds+" navigation="+n.State+" avatar="+n.Avatar+" update="+n.Update; }
    }
}

internal sealed class FixtureWindow : Form
{
    internal readonly Button Underlay;
    internal readonly Panel Sidebar;
    internal readonly Button SidebarToggle;
    internal readonly Button SettingsBack;
    internal readonly Button Avatar, UpdateIndicator, RailObstacle;
    internal bool FixedRail;
    internal bool Collapsed;
    internal int ClickCount;
    internal FixtureWindow()
    {
        Text="Codex Usage Mini — 布局检查窗口";
        StartPosition=FormStartPosition.CenterScreen;
        Size=new Size(820,520); BackColor=Color.FromArgb(29,31,35);
        Label title=new Label { Text="额度条布局检查\n\n这是一扇测试窗口，用于验证布局、主题和悬停隔离。\n演示数字不是账户额度。", AutoSize=true, Location=new Point(290,70), ForeColor=Color.White, Font=new Font("Microsoft YaHei UI",11) };
        Controls.Add(title);
        Panel sidebar=new Panel { Dock=DockStyle.Left, Width=260, BackColor=Color.FromArgb(34,36,40) }; Controls.Add(sidebar); Sidebar=sidebar;
        SidebarToggle=new Button { Text="≡", AccessibleName="隐藏侧边栏", Location=new Point(10,10), Size=new Size(30,28) };
        SidebarToggle.Click+=delegate { ToggleSidebar(); }; sidebar.Controls.Add(SidebarToggle);
        SettingsBack=new Button { Text="返回应用",AccessibleName="返回应用",Location=new Point(10,10),Size=new Size(100,28),Visible=false }; sidebar.Controls.Add(SettingsBack);
        Label footer=new Label { Dock=DockStyle.Bottom, Height=42, Text="  账号                 语音    0%", ForeColor=Color.Silver, TextAlign=ContentAlignment.MiddleLeft }; sidebar.Controls.Add(footer);
        Underlay=new Button { Text="测试：额度条下方按钮", Left=8, Width=244, Height=40, Anchor=AnchorStyles.Left|AnchorStyles.Bottom, Top=ClientSize.Height-85 };
        Underlay.Click+=delegate { ClickCount++; title.Text="底层按钮点击次数："+ClickCount; };
        sidebar.Controls.Add(Underlay);
        Avatar=new Button { AccessibleName="打开个人资料菜单",Text="LE",Visible=false,Anchor=AnchorStyles.Left|AnchorStyles.Bottom };
        UpdateIndicator=new Button { AccessibleName="有可用更新",Text="●",ForeColor=Color.RoyalBlue,Visible=false,Anchor=AnchorStyles.Left|AnchorStyles.Bottom };
        RailObstacle=new Button { AccessibleName="测试导航按钮",Text="导航",Visible=false,Anchor=AnchorStyles.Left|AnchorStyles.Bottom };
        sidebar.Controls.Add(Avatar); sidebar.Controls.Add(UpdateIndicator); sidebar.Controls.Add(RailObstacle);
        var behindTip=new ToolTip { InitialDelay=200,ReshowDelay=200,AutoPopDelay=10000 };
        behindTip.SetToolTip(Underlay,"底层提示：鼠标在额度条上时不应出现");
        FormClosed+=delegate { behindTip.Dispose(); };
    }
    internal void ToggleSidebar()
    {
        Collapsed=!Collapsed; Sidebar.Width=FixedRail?60:Collapsed?52:260;
        SidebarToggle.AccessibleName=Collapsed?"显示侧边栏":"隐藏侧边栏";
        Underlay.Width=Collapsed?40:244;
    }
    internal void SetSettings(bool settings)
    {
        SidebarToggle.Visible=!settings; SettingsBack.Visible=settings;
    }
    internal void SetFixedRail(bool enabled)
    {
        FixedRail=enabled; Sidebar.Width=enabled?60:Collapsed?52:260;
        Underlay.Visible=!enabled; Avatar.Visible=UpdateIndicator.Visible=enabled; RailObstacle.Visible=false;
        Avatar.Bounds=new Rectangle(10,Sidebar.Height-54,38,38);
        UpdateIndicator.Bounds=new Rectangle(10,Sidebar.Height-110,38,38);
        RailObstacle.Bounds=new Rectangle(5,Sidebar.Height-205,46,30);
        Avatar.BringToFront(); UpdateIndicator.BringToFront();
    }
}

internal static class Program
{
    internal static EventWaitHandle ExitSignal;
    [STAThread] static void Main(string[] args)
    {
        if(Array.IndexOf(args,"--exit")>=0)
        {
            try { using(var signal=EventWaitHandle.OpenExisting("Local\\CodexUsageMiniStop")) signal.Set(); }
            catch(WaitHandleCannotBeOpenedException) { }
            return;
        }
        if(Array.IndexOf(args,"--probe")>=0) { DataChecks.Probe(); return; }
        if(Array.IndexOf(args,"--parser-tests")>=0) { DataChecks.Run(); return; }
        if(Array.IndexOf(args,"--news-tests")>=0) { Environment.ExitCode=ResetNewsChecks.Run(); return; }
        if(Array.IndexOf(args,"--news-probe")>=0) { Environment.ExitCode=ResetNewsChecks.Probe(); return; }
        if(Array.IndexOf(args,"--rail-check")>=0)
        { Environment.ExitCode=RailLayout.RunChecks(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rail-check.txt")); return; }
        string executableName=Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        if(executableName.EndsWith(".Check",StringComparison.OrdinalIgnoreCase)) args=new string[]{"--self-test","--auto-start"};
        if(executableName.EndsWith(".Fixture",StringComparison.OrdinalIgnoreCase)) args=new string[]{"--fixture"};
        try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch (EntryPointNotFoundException) { }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if(Array.IndexOf(args,"--visual-test")>=0) { Environment.ExitCode=VisualChecks.RunStatic(); return; }
        if(Array.IndexOf(args,"--orphan-tray-test")>=0) { OrphanTrayChecks.Run(); return; }
        bool owns;
        bool fixtureMode=Array.IndexOf(args,"--fixture")>=0 || Array.IndexOf(args,"--self-test")>=0 || Array.IndexOf(args,"--menu-test")>=0 || Array.IndexOf(args,"--news-ui-test")>=0 || Array.IndexOf(args,"--lifecycle-test")>=0 || Array.IndexOf(args,"--quota-state-test")>=0 || Array.IndexOf(args,"--visual-test")>=0;
        using(Mutex mutex=new Mutex(true,fixtureMode?"Local\\CodexUsageMiniTests":"Local\\CodexUsageMiniPreview",out owns))
        {
            if(!owns) return;
            if(fixtureMode)
            {
                using(FixtureWindow host=new FixtureWindow())
                using(UsageBar bar=new UsageBar(host.Handle,true))
                {
                    bar.ThemePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-theme.toml");
                    File.WriteAllText(bar.ThemePath,"[desktop]\nappearanceTheme = \"dark\"\n");
                    host.Shown+=delegate { bar.UpdateTracking(); };
                    host.FormClosing+=delegate { bar.Shutdown(); };
                    if(Array.IndexOf(args,"--self-test")>=0) Harness.Run(host,bar,Array.IndexOf(args,"--auto-start")>=0);
                    if(Array.IndexOf(args,"--menu-test")>=0) Harness.RunMenu(host,bar,false);
                    if(Array.IndexOf(args,"--news-ui-test")>=0) Harness.RunMenu(host,bar,true);
                    if(Array.IndexOf(args,"--lifecycle-test")>=0) LifecycleChecks.Run(host,bar);
                    if(Array.IndexOf(args,"--quota-state-test")>=0) UiStateChecks.Run(host,bar);
                    if(Array.IndexOf(args,"--visual-test")>=0) VisualChecks.Run(host,bar);
                    Application.Run(host);
                }
            }
            else using(ExitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexUsageMiniStop"))
                using(var bar=new UsageBar(IntPtr.Zero,Array.IndexOf(args,"--demo")>=0))
                {
                    if(Array.IndexOf(args,"--layout-status")>=0)
                    {
                        var statusTimer=new System.Windows.Forms.Timer { Interval=15000 };
                        statusTimer.Tick+=delegate { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-status.txt"),bar.LayoutDiagnostic); statusTimer.Stop(); statusTimer.Dispose(); };
                        statusTimer.Start();
                    }
                    Application.Run(bar);
                }
        }
    }
}

internal static class Harness
{
    internal static void RunMenu(FixtureWindow host,UsageBar bar,bool news)
    {
        var log=new System.Collections.Generic.List<string>();
        int failed=0,attempts=0;
        var timer=new System.Windows.Forms.Timer { Interval=200 };
        timer.Tick+=delegate
        {
            bar.UpdateTracking();
            if(!bar.Visible && ++attempts<30) return;
            timer.Stop();
            Action<string,bool> check=delegate(string name,bool ok) { log.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; };
            Action finish=delegate {
                log.Add("Failed="+failed);
                File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,news?"news-ui-check.txt":"menu-check.txt"),log.ToArray());
                Environment.ExitCode=failed==0?0:1;
                timer.Dispose(); host.Close();
            };
            if(news) bar.CheckNews(check,finish); else { bar.CheckMenu(check); finish(); }
        };
        host.Shown+=delegate { Native.ShowWindow(host.Handle,5); timer.Start(); };
    }
    internal static void Run(FixtureWindow host,UsageBar bar,bool autoStart)
    {
        string report=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-check.txt");
        var log=new System.Collections.Generic.List<string>();
        int step=0, failed=0, activateAttempts=0, transitionHides=0; bool watchingTransition=false; Point oldLocation=Point.Empty; Form other=null;
        bar.VisibleChanged+=delegate { if(watchingTransition && !bar.Visible) transitionHides++; };
        System.Windows.Forms.Timer t=new System.Windows.Forms.Timer { Interval=1200 };
        Action<string,bool> check=delegate(string name,bool ok)
        {
            log.Add((ok?"PASS ":"FAIL ")+name);
            if(!ok) { failed++; log.Add("  state="+bar.LastState+" visible="+bar.Visible+" foreground="+(Native.GetForegroundWindow()==host.Handle)+" bar="+bar.Bounds+" target="+bar.LastTargetBounds); }
        };
        t.Tick+=delegate
        {
            if(step==0 && Native.GetForegroundWindow()!=host.Handle && activateAttempts++<25) { host.Activate(); return; }
            IntPtr focusBeforeTracking=Native.GetForegroundWindow();
            bar.UpdateTracking();
            switch(step++)
            {
                case 0:
                    check("visible for foreground target",bar.Visible);
                    check("inside target",bar.LastTargetBounds.Contains(bar.Bounds));
                    oldLocation=bar.Location; host.Location=new Point(host.Left+35,host.Top+25); break;
                case 1:
                    check("follows move",bar.Left==oldLocation.X+35 && bar.Top==oldLocation.Y+25);
                    oldLocation=new Point(bar.Left,bar.Bottom); host.Height+=60; break;
                case 2:
                    check("bottom anchor follows resize",bar.Bottom==oldLocation.Y+60);
                    host.WindowState=FormWindowState.Maximized; break;
                case 3:
                    check("maximized target",bar.Visible && bar.LastTargetBounds.Contains(bar.Bounds));
                    host.WindowState=FormWindowState.Minimized; break;
                case 4:
                    check("hidden when minimized",!bar.Visible);
                    host.WindowState=FormWindowState.Normal; host.Activate(); break;
                case 5:
                    check("restores after minimize",bar.Visible);
                    check("tracking preserves foreground after restore",Native.GetForegroundWindow()==focusBeforeTracking);
                    other=new Form { Text="Usage Mini unrelated test window",StartPosition=FormStartPosition.Manual,Location=bar.Location, Size=new Size(380,200) }; other.Show(); other.Activate(); break;
                case 6:
                    check("persists when another window is foreground",bar.Visible);
                    check("foreground window remains above quota bar",Native.Root(Native.WindowFromPoint(new Point(bar.Left+20,bar.Top+20)))==other.Handle);
                    check("background tracking does not steal focus",Native.GetForegroundWindow()==focusBeforeTracking);
                    other.Close(); other.Dispose(); host.Activate(); break;
                case 7:
                    check("visible when returning to target",bar.Visible);
                    check("expanded sidebar uses horizontal layout",!bar.VerticalLayout);
                    host.ToggleSidebar(); break;
                case 8:
                    check("collapsed sidebar automatically uses vertical layout",bar.VerticalLayout);
                    check("vertical bar fits 52px rail",bar.Width<=52*Native.Scale(host.Handle));
                    check("vertical left edge has no gap",bar.Left==bar.LastTargetBounds.Left);
                    check("vertical bottom meets account row",bar.Bottom==bar.LastTargetBounds.Bottom-(int)Math.Round(42*Native.Scale(host.Handle)));
                    check("vertical bar isolates pointer from underlying app",bar.PointerHitsBar(new Point(bar.Left+bar.Width/2,bar.Top+bar.Height/2)));
                    host.ToggleSidebar(); break;
                case 9:
                    check("expanding sidebar restores horizontal layout",!bar.VerticalLayout);
                    bar.SetLayout(2); break;
                case 10:
                    check("manual vertical fallback",bar.VerticalLayout);
                    bar.SetLayout(1); break;
                case 11:
                    check("manual horizontal fallback",!bar.VerticalLayout);
                    bar.SetLayout(0); host.Width=250; break;
                case 12:
                    check("hidden for insufficient target space",!bar.Visible && bar.LastState=="target-too-small");
                    host.Width=820; break;
                case 13:
                    check("horizontal left edge has no gap",bar.Left==bar.LastTargetBounds.Left);
                    check("horizontal bottom meets account row",bar.Bottom==bar.LastTargetBounds.Bottom-(int)Math.Round(42*Native.Scale(host.Handle)));
                    check("lower left corner receives pointer",bar.PointerHitsBar(new Point(bar.Left+1,bar.Bottom-1)));
                    check("horizontal bar isolates pointer from underlying app",bar.PointerHitsBar(new Point(bar.Left+bar.Width/2,bar.Top+bar.Height/2)));
                    check("initial dark theme",bar.DarkTheme);
                    File.WriteAllText(bar.ThemePath,"[desktop]\nappearanceTheme = \"light\"\n"); break;
                case 14:
                    check("live light theme switch",!bar.DarkTheme && bar.BackColor.GetBrightness()>.8);
                    File.WriteAllText(bar.ThemePath,"[desktop]\nappearanceTheme = \"dark\"\n"); break;
                case 15:
                    check("live dark theme switch",bar.DarkTheme && bar.BackColor.GetBrightness()<.3);
                    IntPtr focusBeforePopup=Native.GetForegroundWindow();
                    bar.ShowDetails();
                    log.Add("Popup diagnostic: "+bar.PopupDiagnostic);
                    check("details popup isolates pointer",bar.PointerHitsPopup(new Point(bar.PopupBounds.Left+20,bar.PopupBounds.Top+20)));
                    check("details popup stays on screen",Screen.FromControl(host).WorkingArea.Contains(bar.PopupBounds));
                    check("details popup does not take input focus",Native.GetForegroundWindow()==focusBeforePopup);
                    host.SetSettings(true); break;
                case 16:
                    check("legacy settings keeps last confirmed placement",bar.Visible);
                    host.SetSettings(false); break;
                case 17:
                    check("returning from settings restores quota bar",bar.Visible);
                    host.SetFixedRail(true); break;
                case 18:
                    check("modern rail is visible and always vertical",bar.Visible && bar.VerticalLayout);
                    check("modern rail aligns with avatar center",Math.Abs(bar.Left+bar.Width/2.0-host.Avatar.PointToScreen(new Point(host.Avatar.Width/2,0)).X)<=1);
                    check("modern bar clears update button",bar.Bottom<host.UpdateIndicator.PointToScreen(Point.Empty).Y);
                    oldLocation=bar.Location; host.UpdateIndicator.Visible=false; break;
                case 19:
                    check("removing update indicator keeps position stable",bar.Visible && bar.Location==oldLocation);
                    host.UpdateIndicator.Visible=true; host.ToggleSidebar(); break;
                case 20:
                    check("sidebar toggle does not move modern rail",bar.Visible && bar.VerticalLayout && bar.Location==oldLocation);
                    bar.SetLayout(1); break;
                case 21:
                    check("legacy horizontal mode cannot cover modern sidebar",bar.VerticalLayout && bar.Width<=60*Native.Scale(host.Handle) && bar.Location==oldLocation);
                    host.RailObstacle.Visible=true; break;
                case 22:
                    check("rail collision safely hides bar",!bar.Visible && bar.LastState=="rail-space-unavailable");
                    host.RailObstacle.Visible=false; host.SetSettings(true); break;
                case 23:
                    check("modern settings stays visible without sidebar toggle",bar.Visible && bar.VerticalLayout);
                    host.SetSettings(false); break;
                case 24:
                    check("modern main page restores quota bar",bar.Visible && bar.VerticalLayout);
                    oldLocation=bar.Location; host.Location=new Point(host.Left+20,host.Top+15); break;
                case 25:
                    check("modern rail follows window movement",bar.Left==oldLocation.X+20 && bar.Top==oldLocation.Y+15);
                    oldLocation=bar.Location; watchingTransition=true; host.Avatar.Visible=false; host.SidebarToggle.Visible=false; break;
                case 26:
                    check("transient navigation loss keeps fixed rail",bar.Visible && bar.VerticalLayout && bar.Location==oldLocation); break;
                case 27: case 28: case 29:
                    check("continued navigation transition stays visible "+step,bar.Visible && bar.Location==oldLocation); break;
                case 30:
                    check("no brief hide during transition",transitionHides==0);
                    watchingTransition=false; host.Avatar.Visible=true; host.SidebarToggle.Visible=true; break;
                case 31:
                    check("modern avatar recovery retains safe placement",bar.Visible && bar.VerticalLayout && bar.Bottom<host.UpdateIndicator.PointToScreen(Point.Empty).Y);
                    bar.CheckMenu(check);
                    log.Add("Failed="+failed); File.WriteAllLines(report,log.ToArray()); Environment.ExitCode=failed==0?0:1; t.Stop(); t.Dispose(); host.Close(); break;
            }
        };
        // Start after desktop activation so the automation tool cannot restore a window
        // in the middle of the minimize assertion.
        var start=new Button { Text="开始自动检查",Location=new Point(290,240),Size=new Size(160,36) };
        host.Controls.Add(start); host.AcceptButton=start;
        start.Click+=delegate { start.Enabled=false; t.Start(); };
        if(autoStart) host.Shown+=delegate { Native.ShowWindow(host.Handle,5); host.Activate(); start.PerformClick(); };
    }
}
