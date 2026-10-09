using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// A non-activating popup: only fully painted, visible event versions become read.
internal sealed class ResetNewsCard : ToolStripDropDown
{
    sealed class CardSurface : Panel
    {
        internal CardSurface() { DoubleBuffered=true; }
    }
    sealed class DetailLink : Button
    {
        internal Appearance Theme;
        internal double UiScale=1;
        bool hovered;
        static readonly TextFormatFlags LinkFlags=TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
        int Px(double value) { return Math.Max(1,(int)Math.Round(value*UiScale)); }
        int TextWidth(Graphics graphics)
        { return UiTypography.MeasureText(graphics,Text,Font,Size.Empty,LinkFlags).Width; }
        internal Size ContentSize
        {
            get {
                // Measure on the same kind of pixel surface used for painting;
                // context-free GDI measurement can reserve different text overhang.
                using(var bitmap=new Bitmap(1,1)) using(var graphics=Graphics.FromImage(bitmap)) {
                    UiTypography.Prepare(graphics);
                    int icon=Px(11),iconInset=(int)Math.Round(icon/6.0);
                    return new Size(TextWidth(graphics)+Px(4)+icon-iconInset+2*Px(4),Px(24));
                }
            }
        }
        internal DetailLink()
        {
            TabStop=false; FlatStyle=FlatStyle.Flat; FlatAppearance.BorderSize=0;
            Cursor=Cursors.Hand; Text="详情"; AccessibleName="查看 AIHOT 详情";
            AccessibleRole=AccessibleRole.Link;
            SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
        }
        protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered=false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            if(Theme==null) return;
            UiTypography.Prepare(e.Graphics);
            e.Graphics.Clear(Theme.Surface);
            if(hovered) using(var path=MiniChrome.RoundRect(ClientRectangle,Px(6)))
                using(var fill=new SolidBrush(Theme.Dark?Color.FromArgb(58,58,58):Color.FromArgb(242,242,244))) e.Graphics.FillPath(fill,path);
            int icon=Px(11),gap=Px(4),left=Px(4),textWidth=TextWidth(e.Graphics),iconInset=(int)Math.Round(icon/6.0);
            var text=new Rectangle(left,0,textWidth,Height);
            Color ink=hovered?Theme.Ink:(Theme.Dark?Color.FromArgb(174,174,174):Color.FromArgb(104,104,104));
            UiTypography.DrawText(e.Graphics,Text,Font,text,ink,LinkFlags|TextFormatFlags.VerticalCenter|TextFormatFlags.Left);
            // The shared icon's 24-unit view box starts at x=4. Remove that
            // intrinsic inset so the visible stroke has the intended compact gap.
            MiniChrome.Icon(e.Graphics,"external",new Rectangle(text.Right+gap-iconInset,(Height-icon)/2,icon,icon),ink);
        }
    }
    sealed class EventRow
    {
        internal ResetNewsItem Item;
        internal string Title,Status,Scope,TimeLabel,TimeValue;
        internal Rectangle Bounds,TitleBounds,StatusBounds,ScopeLabelBounds,ScopeBounds,TimeLabelBounds,TimeBounds;
        internal double LogicalBottom;
        internal int SeparatorY;
        internal bool Painted;
    }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    static readonly TextFormatFlags TextFlags=TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
    readonly Panel panel=new CardSurface();
    readonly DetailLink website=new DetailLink();
    readonly NewsHelpPopup helpPopup=new NewsHelpPopup();
    readonly MenuDismissal dismissal;
    readonly PopupShadow shadow;
    readonly Action<string,string> markRead;
    readonly Timer escapeTimer=new Timer { Interval=50 };
    readonly List<EventRow> rows=new List<EventRow>();
    readonly HashSet<string> readThisOpen=new HashSet<string>();
    List<ResetNewsItem> items=new List<ResetNewsItem>();
    ResetNewsItem history;
    Font headingFont,titleFont,bodyFont,emptyFont,labelFont,sourceFont,checkFont,historyFont;
    Appearance appearance;
    Form anchor;
    Point? trayAnchor;
    Rectangle placementHost;
    DateTimeOffset? previewClock;
    double uiScale=1;
    string warning,checkText,historyText,notice,overflowText;
    Rectangle headingBounds,newsIconBounds,helpBounds,emptyBounds,emptyIconBounds,historyBounds,warningBounds,checkBounds,sourceBounds;
    int footerLine;
    bool escapeDown,disposingCard,drawingPreview;
    int paintCount;
    Rectangle lastPaintClip;

    internal int DisplayedCount { get { return rows.Count; } }
    internal int TotalCount { get { return items.Count; } }
    internal object MessageDiagnostic
    {
        get {
            var versions=new List<object>();
            foreach(var item in items) versions.Add(new { id=item.Id,version=item.Version });
            return new { visible=Visible,count=items.Count,displayed=rows.Count,paintCount=paintCount,
                acknowledged=readThisOpen.Count,newDataNotice=notice=="有更新，请重新打开消息卡",versions=versions };
        }
    }
    internal string OverflowDiagnostic { get; private set; }
    internal string ReadDiagnostic
    {
        get {
            var text=new System.Text.StringBuilder();
            text.Append("paint=").Append(paintCount).Append(" clip=").Append(lastPaintClip).Append(" rows=").Append(rows.Count)
                .Append(" read=").Append(readThisOpen.Count).Append(" visible=").Append(Visible).Append(" nativeVisible=").Append(IsHandleCreated && Native.IsWindowVisible(Handle))
                .Append(" hook=").Append(dismissal.Active).Append(" card=").Append(Bounds).Append(" panel=").Append(panel.Bounds).Append(" client=").Append(panel.ClientRectangle);
            if(anchor!=null && !anchor.IsDisposed) text.Append(" work=").Append(WorkingArea(anchor));
            foreach(var row in rows) {
                Rectangle screen=panel.RectangleToScreen(row.Bounds);
                Point center=new Point(screen.Left+screen.Width/2,screen.Top+screen.Height/2);
                IntPtr hit=Native.WindowFromPoint(center);
                text.Append("\nrow=").Append(row.Item.Id).Append(" painted=").Append(row.Painted).Append(" bounds=").Append(row.Bounds)
                    .Append(" screen=").Append(screen).Append(" clientContains=").Append(panel.ClientRectangle.Contains(row.Bounds))
                    .Append(" workContains=").Append(anchor!=null && !anchor.IsDisposed && WorkingArea(anchor).Contains(screen))
                    .Append(" hit=").Append(hit).Append(" root=").Append(Native.Root(hit)).Append(" cardHandle=").Append(Handle);
            }
            return text.ToString();
        }
    }
    internal string VisibleText
    {
        get {
            var text=new System.Text.StringBuilder("Tibo 重置消息\n");
            foreach(var row in rows) text.AppendLine(row.Title).AppendLine(row.Status).AppendLine("适用范围").AppendLine(row.Scope).AppendLine(row.TimeLabel).AppendLine(row.TimeValue);
            if(items.Count==0) {
                text.AppendLine("暂无新消息");
                if(!String.IsNullOrEmpty(historyText)) text.AppendLine(historyText);
            }
            string message=WarningText(overflowText);
            if(message.Length>0) text.AppendLine(message);
            return text.AppendLine(checkText).Append("来源：AIHOT\n详情").ToString();
        }
    }
    Rectangle WorkingArea(Form owner)
    { return trayAnchor.HasValue?Screen.FromPoint(trayAnchor.Value).WorkingArea:Screen.FromControl(owner).WorkingArea; }
    Point PopupLocation(Form owner)
    {
        Rectangle screen=WorkingArea(owner);
        if(!trayAnchor.HasValue) return PopupPlacement.ForBar(owner.Bounds,placementHost,Size,screen,uiScale);
        int x=trayAnchor.Value.X,y=trayAnchor.Value.Y;
        return new Point(Math.Max(screen.Left,Math.Min(x,screen.Right-Width)),Math.Max(screen.Top,Math.Min(y,screen.Bottom-Height)));
    }
    // The second argument remains for compatibility; the card has no mark-all action.
    internal ResetNewsCard(Action<string,string> read,Action all)
    {
        markRead=read; AutoSize=false; Padding=Padding.Empty; Margin=Padding.Empty;
        DropShadowEnabled=false; shadow=PopupShadow.Attach(this,1,20);
        panel.Margin=Padding.Empty; panel.AutoScroll=false; panel.Paint+=PaintCard;
        panel.MouseMove+=delegate(object sender,MouseEventArgs e) {
            panel.Cursor=helpBounds.Contains(e.Location)?Cursors.Help:Cursors.Default;
            UpdateHelp();
        };
        panel.MouseLeave+=delegate { UpdateHelp(); };
        website.Click+=delegate { OpenLink(); };
        panel.Controls.Add(website);
        var host=new ToolStripControlHost(panel) { AutoSize=false,Margin=Padding.Empty,Padding=Padding.Empty };
        // Keep the accepted explicit dismissal. Native menu mode must not hide this no-activate card.
        Items.Add(host); dismissal=new MenuDismissal(this,false);
        Opened+=delegate { escapeDown=(GetAsyncKeyState(0x1B)&0x8000)!=0; escapeTimer.Start(); };
        Closed+=delegate { escapeTimer.Stop(); helpPopup.Hide(); };
        escapeTimer.Tick+=delegate {
            if(!Visible || disposingCard) { escapeTimer.Stop(); helpPopup.Hide(); return; }
            AcknowledgePaintedRows(); UpdateHelp();
            bool down=(GetAsyncKeyState(0x1B)&0x8000)!=0;
            if(down && !escapeDown) Close();
            escapeDown=down;
        };
    }
    protected override CreateParams CreateParams
    { get { var p=base.CreateParams; p.ExStyle|=0x80|0x08000000; return p; } }
    protected override bool TopMost { get { return false; } }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
        base.WndProc(ref m);
    }
    protected override bool ProcessCmdKey(ref Message m,Keys keyData)
    {
        if((keyData&Keys.KeyCode)==Keys.Escape) { Close(); return true; }
        return base.ProcessCmdKey(ref m,keyData);
    }
    void UpdateHelp()
    {
        if(!Visible || disposingCard || anchor==null || anchor.IsDisposed) { helpPopup.Hide(); return; }
        Point cursor=Cursor.Position;
        Rectangle target=panel.RectangleToScreen(helpBounds);
        if(target.Contains(cursor) && Native.Root(Native.WindowFromPoint(cursor))==Handle)
            helpPopup.ShowHelp(this,target,WorkingArea(anchor),appearance,uiScale);
        else if(!helpPopup.Visible || !helpPopup.Bounds.Contains(cursor)) helpPopup.Hide();
    }
    void OpenLink()
    {
        try { Process.Start(new ProcessStartInfo("https://aihot.news/codex-reset") { UseShellExecute=true }); Close(); }
        catch(System.ComponentModel.Win32Exception) { notice="无法打开浏览器，请稍后重试"; Reflow(); }
        catch(InvalidOperationException) { notice="无法打开浏览器，请稍后重试"; Reflow(); }
    }
    static string Compact(string text)
    { return ResetNewsPresentation.Compact(text); }
    static string CardWarning(ResetNewsState state)
    {
        var messages=new List<string>();
        if(!String.IsNullOrEmpty(state.Error)) messages.Add(state.Error);
        else if(!state.Cache.Initialized) messages.Add("正在读取消息…");
        if(state.Cache.Initialized && state.Cache.Snapshot.Day!=ResetNewsParser.BeijingToday) messages.Add("缓存消息 · 等待更新");
        if(state.Cache.Snapshot.Monitor!="healthy" && !String.IsNullOrEmpty(state.Cache.Snapshot.Monitor)) messages.Add("上游采集有延迟，消息可能不是最新");
        if(!String.IsNullOrEmpty(state.StorageError)) messages.Add(state.StorageError);
        return String.Join("\n",messages.ToArray());
    }
    internal void Open(Form owner,ResetNewsState state,Appearance theme,double scale)
    { Open(owner,state,theme,scale,null); }
    internal void Open(Form owner,ResetNewsState state,Appearance theme,double scale,Point? trayLocation)
    { Open(owner,state,theme,scale,trayLocation,Rectangle.Empty); }
    internal void Open(Form owner,ResetNewsState state,Appearance theme,double scale,Point? trayLocation,Rectangle hostBounds)
    {
        if(Visible) Close();
        placementHost=hostBounds;
        anchor=owner; trayAnchor=trayLocation; appearance=theme; uiScale=scale; notice=null; readThisOpen.Clear(); paintCount=0; lastPaintClip=Rectangle.Empty;
        previewClock=null;
        items=new List<ResetNewsItem>(state.Cache.Snapshot.Items);
        // Freeze displayed versions, history and source check time until the next open.
        items.Sort((a,b)=> { int unread=state.Unread(b).CompareTo(state.Unread(a)); return unread!=0?unread:String.CompareOrdinal(b.Published,a.Published); });
        history=state.Cache.Snapshot.LatestHistory;
        string historyLabel=ResetNewsParser.HistoryLabel(history),historyTime=ResetNewsParser.HistoryTime(history);
        historyText=String.IsNullOrEmpty(historyLabel) || String.IsNullOrEmpty(historyTime)?null:historyLabel+"："+historyTime;
        checkText=ResetNewsParser.CheckTime(state.Cache.Snapshot.VerifiedAt,DateTimeOffset.Now);
        if(String.IsNullOrEmpty(checkText)) checkText="消息检查时间未提供";
        warning=CardWarning(state); Reflow();
        if(trayAnchor.HasValue) Show(PopupLocation(owner));
        else Show(owner,owner.PointToClient(PopupLocation(owner)));
        PositionAbove(owner);
        panel.Invalidate(); panel.Update();
        AcknowledgePaintedRows();
    }
    // Offline visual fixtures supply an unshown owner. No popup, hook or read acknowledgement is started.
    internal void PreparePreview(Form owner,ResetNewsSnapshot snapshot,Appearance theme,double scale,DateTimeOffset now)
    {
        if(Visible) throw new InvalidOperationException("可见消息卡不能用于离线预览");
        anchor=owner; trayAnchor=null; placementHost=Rectangle.Empty; appearance=theme; uiScale=scale; previewClock=now;
        notice=warning=null; readThisOpen.Clear(); paintCount=0; lastPaintClip=Rectangle.Empty;
        items=new List<ResetNewsItem>(snapshot.Items);
        history=snapshot.LatestHistory;
        string label=ResetNewsParser.HistoryLabel(history),time=ResetNewsParser.HistoryTime(history);
        historyText=String.IsNullOrEmpty(label) || String.IsNullOrEmpty(time)?null:label+"："+time;
        checkText=ResetNewsParser.CheckTime(snapshot.VerifiedAt,now);
        if(String.IsNullOrEmpty(checkText)) checkText="消息检查时间未提供";
        Reflow();
    }
    int Px(double value) { return Math.Max(1,(int)Math.Round(value*uiScale)); }
    static int TextHeight(string text,Font font,int width)
    { return UiTypography.MeasureText(text,font,new Size(Math.Max(1,width),Int32.MaxValue),TextFlags).Height; }
    void ReplaceFonts()
    {
        website.Font=Control.DefaultFont; DisposeFonts();
        headingFont=UiTypography.Font(items.Count==0?16:14,uiScale);
        titleFont=UiTypography.Font(15,uiScale);
        bodyFont=UiTypography.Font(12,uiScale);
        emptyFont=UiTypography.Font(13,uiScale);
        labelFont=UiTypography.Font(10,uiScale);
        sourceFont=UiTypography.Font(10,uiScale);
        checkFont=UiTypography.Font(9,uiScale);
        historyFont=UiTypography.Font(11,uiScale);
        website.Font=sourceFont; website.UiScale=uiScale;
    }
    void DisposeFonts()
    {
        if(headingFont!=null) headingFont.Dispose(); if(titleFont!=null) titleFont.Dispose();
        if(bodyFont!=null) bodyFont.Dispose(); if(emptyFont!=null) emptyFont.Dispose();
        if(labelFont!=null) labelFont.Dispose(); if(sourceFont!=null) sourceFont.Dispose();
        if(checkFont!=null) checkFont.Dispose(); if(historyFont!=null) historyFont.Dispose();
    }
    string WarningText(string overflow)
    {
        var messages=new List<string>();
        if(!String.IsNullOrEmpty(notice)) messages.Add(notice);
        if(!String.IsNullOrEmpty(warning)) messages.Add(warning);
        if(!String.IsNullOrEmpty(overflow)) messages.Add(overflow);
        return String.Join("\n",messages.ToArray());
    }
    int Y(double logical) { return (int)Math.Round(logical*uiScale); }
    static int SingleLineHeight(Font font)
    { return UiTypography.MeasureText("国Ag",font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height; }
    double WrappedExtra(string text,Font font,int width)
    { return Math.Max(0,TextHeight(text,font,width)-SingleLineHeight(font))/uiScale; }
    Rectangle TextBox(int x,double top,int width,double slot,int measured)
    { return new Rectangle(x,Y(top),width,Math.Max(measured,Y(top+slot)-Y(top))); }
    EventRow MakeRow(ResetNewsItem item,double top,int margin,int textWidth,DateTimeOffset now)
    {
        var view=ResetNewsPresentation.Valid(item.View)?item.View:ResetNewsPresentation.Create(item,now);
        string title=view.Title,status=view.Status;
        int chipWidth=Math.Min(textWidth/2,UiTypography.MeasureText(status,labelFont,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width+Px(20));
        int statusMeasured=TextHeight(status,labelFont,chipWidth-Px(16));
        double chipHeight=Math.Max(25,statusMeasured/uiScale+8);
        int titleWidth=Math.Max(1,textWidth-chipWidth-Px(8)),titleMeasured=TextHeight(title,titleFont,titleWidth);
        double titleHeight=Math.Max(25,titleMeasured/uiScale);
        double current=top+Math.Max(chipHeight,titleHeight)+18;
        var row=new EventRow { Item=item,Title=title,Status=status,
            TitleBounds=TextBox(margin,top,titleWidth,titleHeight,titleMeasured),
            StatusBounds=TextBox(margin+textWidth-chipWidth,top,chipWidth,chipHeight,Y(top+chipHeight)-Y(top)),
            ScopeLabelBounds=TextBox(margin,current,textWidth,15,SingleLineHeight(labelFont)) };
        current+=18;
        row.Scope=view.Scope;
        // Keep the approved logical rhythm for a single line. GDI's leading may
        // extend into the following gap; every measured glyph box remains intact.
        // Additional wrapped lines grow the layout rather than being cropped.
        double scopeSlot=18+WrappedExtra(row.Scope,bodyFont,textWidth);
        row.ScopeBounds=TextBox(margin,current,textWidth,scopeSlot,TextHeight(row.Scope,bodyFont,textWidth));
        current+=scopeSlot+12;
        row.TimeLabel=view.TimeLabel; row.TimeValue=view.TimeValue;
        double timeLabelSlot=15+WrappedExtra(row.TimeLabel,labelFont,textWidth);
        row.TimeLabelBounds=TextBox(margin,current,textWidth,timeLabelSlot,TextHeight(row.TimeLabel,labelFont,textWidth));
        current+=timeLabelSlot+3;
        double timeSlot=18+WrappedExtra(row.TimeValue,bodyFont,textWidth);
        row.TimeBounds=TextBox(margin,current,textWidth,timeSlot,TextHeight(row.TimeValue,bodyFont,textWidth));
        row.LogicalBottom=current+timeSlot;
        row.Bounds=new Rectangle(margin,Y(top),textWidth,Math.Max(row.TimeBounds.Bottom,Y(row.LogicalBottom))-Y(top));
        row.SeparatorY=Y(row.LogicalBottom+15);
        return row;
    }
    void Reflow()
    {
        if(anchor==null || anchor.IsDisposed || disposingCard) return;
        helpPopup.Hide();
        Rectangle screen=WorkingArea(anchor);
        int baseWidth=items.Count==0?320:300;
        uiScale=Math.Max(0.5,Math.Min(uiScale,screen.Width/(double)baseWidth));
        ReplaceFonts(); rows.Clear(); overflowText=null; OverflowDiagnostic=null;
        int width=Math.Min(Px(baseWidth),screen.Width),margin=Px(items.Count==0?20:18),textWidth=Math.Max(1,width-2*margin);
        int headerHeight=Px(22),iconSize=Px(items.Count==0?19:17);
        newsIconBounds=new Rectangle(margin,Px(21),iconSize,iconSize);
        helpBounds=new Rectangle(width-margin-Px(14),Px(23),Px(14),Px(14));
        headingBounds=new Rectangle(margin+Px(items.Count==0?29:26),margin,
            Math.Max(1,helpBounds.Left-margin-Px(items.Count==0?37:34)),headerHeight);
        emptyBounds=emptyIconBounds=historyBounds=Rectangle.Empty;
        double contentBottom;
        var proposed=new List<EventRow>();
        if(items.Count==0) {
            emptyIconBounds=new Rectangle(margin,Y(51),Px(17),Px(17));
            emptyBounds=TextBox(margin+Px(25),51,textWidth-Px(25),21,TextHeight("暂无新消息",emptyFont,textWidth-Px(25)));
            contentBottom=78;
            if(!String.IsNullOrEmpty(historyText)) {
                double historySlot=18+WrappedExtra(historyText,historyFont,textWidth);
                historyBounds=TextBox(margin,78,textWidth,historySlot,TextHeight(historyText,historyFont,textWidth));
                contentBottom=106+historySlot-18;
            }
        } else {
            double top=55; DateTimeOffset now=previewClock??DateTimeOffset.Now;
            foreach(var item in items) {
                var row=MakeRow(item,top,margin,textWidth,now);
                proposed.Add(row); top=row.LogicalBottom+32;
            }
            contentBottom=proposed[proposed.Count-1].LogicalBottom+15;
        }
        double footerHeight=items.Count==0?59:65;
        string message=WarningText(null);
        double warningHeight=message.Length==0?0:TextHeight(message,historyFont,textWidth)/uiScale+10;
        if(items.Count>0 && Y(contentBottom+warningHeight+footerHeight)>screen.Height) {
            string overflowMessage="另有 "+items.Count+" 条未展示，请在 AIHOT 查看";
            double reserve=TextHeight(WarningText(overflowMessage),historyFont,textWidth)/uiScale+10+footerHeight;
            contentBottom=55;
            foreach(var row in proposed) {
                if(Y(row.LogicalBottom+15+reserve)>screen.Height) break;
                rows.Add(row); contentBottom=row.LogicalBottom+15;
            }
            overflowText="另有 "+(items.Count-rows.Count)+" 条未展示，请在 AIHOT 查看";
            OverflowDiagnostic="工作区 "+screen.Width+"×"+screen.Height+"，消息 "+items.Count+" 条，完整展示 "+rows.Count+" 条";
            message=WarningText(overflowText);
            warningHeight=TextHeight(message,historyFont,textWidth)/uiScale+10;
        } else rows.AddRange(proposed);
        warningBounds=warningHeight==0?Rectangle.Empty:TextBox(margin,contentBottom,textWidth,warningHeight-10,TextHeight(message,historyFont,textWidth));
        double footerTop=contentBottom+warningHeight;
        footerLine=Y(footerTop);
        checkBounds=TextBox(margin,footerTop+10,textWidth,15,SingleLineHeight(checkFont));
        double linkTop=footerTop+(items.Count==0?23:26);
        Size linkSize=website.ContentSize;
        website.Bounds=new Rectangle(width-margin-linkSize.Width,Y(linkTop),linkSize.Width,Y(linkTop+24)-Y(linkTop));
        sourceBounds=TextBox(margin,footerTop+(items.Count==0?29:32),Math.Max(1,website.Left-margin-Px(12)),17,SingleLineHeight(sourceFont));
        // Scale the complete frame once, so fractional DPI does not accumulate
        // independent rounding from each section. Wrapped fields still add height.
        int requiredHeight=Y(footerTop+footerHeight);
        int height=Math.Min(screen.Height,requiredHeight);
        if(requiredHeight>screen.Height)
            OverflowDiagnostic=(OverflowDiagnostic==null?"工作区 "+screen.Width+"×"+screen.Height+"；":"工作区内容过高；"+OverflowDiagnostic+"；")+"说明或底栏未能完整展示";
        Size=panel.Size=new Size(width,height); Items[0].Size=panel.Size;
        MiniChrome.SetRoundedRegion(this,Px(20));
        MiniChrome.SetRoundedRegion(panel,Px(20));
        ApplyTheme(appearance);
        if(Visible) { Location=PopupLocation(anchor); panel.Invalidate(); panel.Update(); }
    }
    Color Secondary { get { return appearance.Dark?Color.FromArgb(168,168,168):Color.FromArgb(104,104,104); } }
    Color Muted { get { return appearance.Dark?Color.FromArgb(143,143,143):Color.FromArgb(138,138,138); } }
    Color ValueInk { get { return appearance.Dark?appearance.Ink:Color.FromArgb(71,71,71); } }
    Color FieldInk { get { return appearance.Dark?Color.FromArgb(151,151,151):Color.FromArgb(146,146,146); } }
    Color StatusInk { get { return appearance.Dark?Color.FromArgb(171,171,171):Color.FromArgb(119,119,119); } }
    void PaintCard(object sender,PaintEventArgs e)
    {
        if(appearance==null || headingFont==null || disposingCard) return;
        UiTypography.Prepare(e.Graphics);
        if(!drawingPreview) { paintCount++; lastPaintClip=e.ClipRectangle; }
        MiniChrome.PaintSurface(e.Graphics,panel.ClientRectangle,appearance,Px(20));
        UiTypography.DrawText(e.Graphics,"Tibo 重置消息",headingFont,headingBounds,appearance.Ink,TextFlags);
        MiniChrome.Icon(e.Graphics,"news",newsIconBounds,Secondary);
        MiniChrome.Icon(e.Graphics,"help",helpBounds,Muted);
        using(var border=new Pen(appearance.Border)) {
            foreach(var row in rows) {
                UiTypography.DrawText(e.Graphics,row.Title,titleFont,row.TitleBounds,appearance.Ink,TextFlags);
                using(var chip=MiniChrome.RoundRect(row.StatusBounds,Px(12))) e.Graphics.DrawPath(border,chip);
                var statusText=Rectangle.Inflate(row.StatusBounds,-Px(8),-Px(3));
                UiTypography.DrawText(e.Graphics,row.Status,labelFont,statusText,StatusInk,TextFlags|TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                UiTypography.DrawText(e.Graphics,"适用范围",labelFont,row.ScopeLabelBounds,FieldInk,TextFlags);
                UiTypography.DrawText(e.Graphics,row.Scope,bodyFont,row.ScopeBounds,ValueInk,TextFlags);
                UiTypography.DrawText(e.Graphics,row.TimeLabel,labelFont,row.TimeLabelBounds,FieldInk,TextFlags);
                UiTypography.DrawText(e.Graphics,row.TimeValue,bodyFont,row.TimeBounds,ValueInk,TextFlags);
                if(row!=rows[rows.Count-1]) e.Graphics.DrawLine(border,row.Bounds.Left,row.SeparatorY,row.Bounds.Right,row.SeparatorY);
                if(!drawingPreview && Visible && e.ClipRectangle.Contains(row.Bounds)) row.Painted=true;
            }
            if(!emptyBounds.IsEmpty) {
                MiniChrome.Icon(e.Graphics,"calendar",emptyIconBounds,Muted);
                UiTypography.DrawText(e.Graphics,"暂无新消息",emptyFont,emptyBounds,Secondary,TextFlags);
                if(!historyBounds.IsEmpty) UiTypography.DrawText(e.Graphics,historyText,historyFont,historyBounds,Secondary,TextFlags);
            }
            if(!warningBounds.IsEmpty) UiTypography.DrawText(e.Graphics,WarningText(overflowText),historyFont,warningBounds,Secondary,TextFlags);
            e.Graphics.DrawLine(border,newsIconBounds.Left,footerLine,panel.Width-newsIconBounds.Left,footerLine);
        }
        UiTypography.DrawText(e.Graphics,checkText,checkFont,checkBounds,Muted,TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);
        UiTypography.DrawText(e.Graphics,"来源：AIHOT",sourceFont,sourceBounds,Muted,TextFlags);
        if(!drawingPreview) AcknowledgePaintedRows();
    }
    void AcknowledgePaintedRows()
    {
        // Native hit testing can lag the first paint until Show/SetWindowPos returns.
        if(!Visible || !Native.IsWindowVisible(Handle) || !dismissal.Active) return;
        Rectangle screen=WorkingArea(anchor);
        foreach(var row in rows) {
            if(!row.Painted || !panel.ClientRectangle.Contains(row.Bounds) || !screen.Contains(panel.RectangleToScreen(row.Bounds))) continue;
            Point center=panel.PointToScreen(new Point(row.Bounds.Left+row.Bounds.Width/2,row.Bounds.Top+row.Bounds.Height/2));
            if(Native.Root(Native.WindowFromPoint(center))!=Handle) continue;
            string key=row.Item.Id+"\n"+row.Item.Version;
            if(readThisOpen.Add(key)) markRead(row.Item.Id,row.Item.Version);
        }
    }
    internal void PositionAbove(Form owner)
    { PositionAbove(owner,placementHost); }
    internal void PositionAbove(Form owner,Rectangle hostBounds)
    {
        if(trayAnchor.HasValue) {
            Native.SetWindowPos(Handle,new IntPtr(-2),0,0,0,0,0x01|0x02|0x10);
            return;
        }
        placementHost=hostBounds;
        Location=PopupLocation(owner);
        if(Native.IsTopmost(Handle)) Native.PlaceNormal(Handle,IntPtr.Zero,0,0,0,0,0x01|0x02|0x10);
        IntPtr above=Native.GetWindow(owner.Handle,3);
        if(above!=Handle) Native.PlaceNormal(Handle,above,0,0,0,0,0x01|0x02|0x10);
    }
    internal void ApplyTheme(Appearance theme)
    {
        appearance=theme; BackColor=panel.BackColor=website.BackColor=theme.Surface;
        website.Theme=theme; website.Invalidate(); panel.Invalidate();
        helpPopup.ApplyTheme(theme);
        shadow.UpdatePlacement(uiScale,20);
    }
    internal void SavePreview(string path)
    {
        drawingPreview=true;
        try {
            using(var bitmap=new Bitmap(panel.Width,panel.Height)) {
                panel.DrawToBitmap(bitmap,panel.ClientRectangle);
                // An unopened preview's hidden ancestor suppresses child rendering.
                // Paint only that missing child; live visible cards already include it.
                if(!website.Visible && website.Width>0 && website.Height>0) {
                    using(var link=new Bitmap(website.Width,website.Height)) {
                        website.DrawToBitmap(link,website.ClientRectangle);
                        using(var graphics=Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(link,website.Location);
                    }
                }
                bitmap.Save(path);
            }
        }
        finally { drawingPreview=false; }
    }
    internal bool ShouldNotifySnapshot(ResetNewsSnapshot snapshot)
    {
        string nextHistory=ResetNewsPresentation.EmptyHistory(snapshot);
        return !ResetNewsPresentation.SameActivities(items,snapshot.Items) ||
            (items.Count==0 && (historyText??"")!=nextHistory);
    }
    internal void ShowNewDataNotice(ResetNewsSnapshot snapshot)
    { if(ShouldNotifySnapshot(snapshot)) ShowNewDataNotice(); }
    internal void ShowNewDataNotice() { if(Visible) { notice="有更新，请重新打开消息卡"; Reflow(); } }
    internal void CheckOutside(Point point) { dismissal.ProcessLeftPress(point); }
    internal bool DismissalActive { get { return dismissal.Active; } }
    protected override void Dispose(bool disposing)
    {
        if(disposing && !disposingCard) {
            disposingCard=true; escapeTimer.Stop(); escapeTimer.Dispose(); dismissal.Dispose(); helpPopup.Dispose(); shadow.Dispose();
            website.Font=Control.DefaultFont; DisposeFonts();
        }
        base.Dispose(disposing);
    }
}
