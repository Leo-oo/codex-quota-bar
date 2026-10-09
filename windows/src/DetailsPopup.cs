using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

// Hit-testable, non-activating quota details; the legacy text API remains a fallback.
internal sealed class DetailsPopup : Form
{
    sealed class QuotaRow
    {
        internal string Name,Percent,Reset;
        internal double Fraction;
        internal bool HasFraction;
        internal float TitleBaseline,PercentBaseline,ResetBaseline,Separator;
        internal RectangleF Progress;
    }
    readonly Label content=new Label();
    readonly List<QuotaRow> rows=new List<QuotaRow>();
    readonly PopupShadow shadow;
    Appearance appearance;
    Font headingFont,percentFont,resetFont,footerFont;
    double scale=1,fontScale;
    Rectangle placementHost;
    bool structured;
    string footer="",status="",emptyMessage="";
    float footerBaseline,statusTop;
    int statusHeight,emptyHeight;
    static readonly TextFormatFlags Wrapped=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding;
    internal string VisibleText
    {
        get {
            if(!structured) return content.Text;
            var text=new StringBuilder();
            foreach(var row in rows) text.Append(row.Name).Append("：").Append(row.Percent).Append("\n").Append(row.Reset).Append("\n");
            if(rows.Count==0) text.Append("额度\n").Append(emptyMessage).Append("\n");
            if(status.Length>0) text.Append(status).Append("\n");
            return text.Append(footer).ToString().TrimEnd();
        }
    }
    internal DetailsPopup()
    {
        AutoScaleMode=AutoScaleMode.None; FormBorderStyle=FormBorderStyle.None;
        ShowInTaskbar=false; StartPosition=FormStartPosition.Manual;
        SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        content.AutoSize=false; content.BackColor=Color.Transparent; Controls.Add(content);
        shadow=PopupShadow.Attach(this,1,15);
    }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    {
        get { var p=base.CreateParams; p.ExStyle|=0x80|0x08000000; p.ClassStyle&=~0x00020000; return p; }
    }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x84) { m.Result=new IntPtr(1); return; }
        if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
        base.WndProc(ref m);
    }
    int Px(double value) { return Math.Max(1,(int)Math.Round(value*scale)); }
    static double ValidScale(double value)
    { return Double.IsNaN(value)||Double.IsInfinity(value)||value<=0?1:value; }
    void EnsureFonts(double dpi)
    {
        scale=ValidScale(dpi);
        if(headingFont!=null && Math.Abs(scale-fontScale)<.001) return;
        content.Font=Control.DefaultFont; DisposeFonts();
        headingFont=UiTypography.Font(14,scale);
        percentFont=UiTypography.Numeric(21,scale,true);
        resetFont=UiTypography.Font(13,scale);
        footerFont=UiTypography.Font(12,scale);
        content.Font=resetFont; fontScale=scale;
    }
    void DisposeFonts()
    {
        if(headingFont!=null) headingFont.Dispose(); if(percentFont!=null) percentFont.Dispose();
        if(resetFont!=null) resetFont.Dispose(); if(footerFont!=null) footerFont.Dispose();
        headingFont=percentFont=resetFont=footerFont=null;
    }
    internal void ApplyTheme(Appearance theme)
    {
        appearance=theme; BackColor=theme.Surface; content.ForeColor=theme.Ink; Invalidate();
        if(shadow!=null) shadow.UpdatePlacement(scale,15);
    }
    // Uses the same layout and painting as the live popup without Show or desktop input.
    internal void PrepareQuotaPreview(QuotaSnapshot snapshot,bool old,bool demo,string error,Appearance theme,double dpi,DateTimeOffset now)
    {
        EnsureFonts(dpi); structured=true; content.Visible=false; rows.Clear();
        footer=status=emptyMessage=""; statusHeight=emptyHeight=0;
        ApplyTheme(theme);
        int count=snapshot==null?0:snapshot.Windows.Count;
        int width=Px(count>1?304:258),padding=Px(21),textWidth=width-2*padding;
        double height=count<=1?142:217+88.0*(count-2);
        var notices=new List<string>();
        if(demo) notices.Add("演示数据 · 非真实额度");
        else if(old && count>0) notices.Add("旧值 · 等待更新");
        if(count>0 && !String.IsNullOrEmpty(error)) notices.Add(error);
        for(int i=0;i<count;i++) {
            var window=snapshot.Windows[i];
            float title=(float)((i==0?34:32+88*i)*scale);
            float percentage=(float)((i==0?36:34+88*i)*scale);
            float progress=(float)((count==1?49:47+88*i)*scale);
            float reset=(float)((count==1?77:i==0?73:74+88*i)*scale);
            float separator=(float)((count==1?93:89+88*i)*scale);
            double fraction; bool known=QuotaDetails.TryFraction(window,out fraction);
            rows.Add(new QuotaRow {
                Name=QuotaDetails.Name(window)+"剩余",Percent=QuotaDetails.DisplayPercent(window),
                Reset=QuotaDetails.ResetText(window,now),Fraction=fraction,HasFraction=known,
                TitleBaseline=title,PercentBaseline=percentage,ResetBaseline=reset,Separator=separator,
                Progress=new RectangleF(padding,progress,textWidth,(float)(4*scale))
            });
        }
        if(count==0) {
            emptyMessage=String.IsNullOrEmpty(error)?"正在通过本机 Codex 读取额度…":error;
            emptyHeight=UiTypography.MeasureText(emptyMessage,resetFont,new Size(textWidth,Int32.MaxValue),Wrapped).Height;
            height=Math.Max(height,(53*scale+emptyHeight+21*scale)/scale);
            statusTop=(float)(53*scale+emptyHeight+8*scale);
        } else {
            footer=QuotaDetails.UpdatedText(snapshot,now);
            footerBaseline=(float)((count==1?121:201+88*(count-2))*scale);
            statusTop=rows[rows.Count-1].Separator+(float)(10*scale);
        }
        status=String.Join("\n",notices.ToArray());
        if(status.Length>0) {
            statusHeight=UiTypography.MeasureText(status,footerFont,new Size(textWidth,Int32.MaxValue),Wrapped).Height;
            int extra=statusHeight+Px(8);
            height+=extra/scale;
            if(count>0) footerBaseline+=extra;
        }
        Size=new Size(width,Px(height));
        MiniChrome.SetRoundedRegion(this,(float)(15*scale));
        Invalidate();
    }
    internal void ShowQuotaDetails(Form bar,bool vertical,QuotaSnapshot snapshot,bool old,bool demo,string error,Appearance theme,double dpi,DateTimeOffset now)
    { ShowQuotaDetails(bar,vertical,snapshot,old,demo,error,theme,dpi,now,Rectangle.Empty); }
    internal void ShowQuotaDetails(Form bar,bool vertical,QuotaSnapshot snapshot,bool old,bool demo,string error,Appearance theme,double dpi,DateTimeOffset now,Rectangle hostBounds)
    {
        placementHost=hostBounds;
        PrepareQuotaPreview(snapshot,old,demo,error,theme,dpi,now);
        PositionFor(bar);
        if(!Visible) Show(bar);
        PositionAbove(bar);
    }
    internal void ShowDetails(Form bar,bool vertical,string text,Appearance theme,double dpi)
    { ShowDetails(bar,vertical,text,theme,dpi,Rectangle.Empty); }
    internal void ShowDetails(Form bar,bool vertical,string text,Appearance theme,double dpi,Rectangle hostBounds)
    {
        placementHost=hostBounds;
        EnsureFonts(dpi); structured=false; rows.Clear(); content.Visible=false;
        content.Text=(text??"").TrimEnd(); ApplyTheme(theme);
        Rectangle screen=Screen.FromControl(bar).WorkingArea;
        int padding=Px(21),maxWidth=Math.Max(1,Math.Min(Px(330),screen.Width-2*padding));
        Size measured=UiTypography.MeasureText(content.Text,content.Font,new Size(maxWidth,Int32.MaxValue),Wrapped);
        Size=new Size(measured.Width+2*padding,measured.Height+2*padding);
        content.Bounds=new Rectangle(padding,padding,measured.Width,measured.Height);
        MiniChrome.SetRoundedRegion(this,(float)(15*scale));
        PositionFor(bar);
        if(!Visible) Show(bar);
        PositionAbove(bar);
    }
    void PositionFor(Form bar)
    {
        Rectangle screen=Screen.FromControl(bar).WorkingArea;
        Location=PopupPlacement.ForBar(bar.Bounds,placementHost,Size,screen,scale);
    }
    internal void PositionAbove(Form bar)
    { PositionAbove(bar,placementHost); }
    internal void PositionAbove(Form bar,Rectangle hostBounds)
    {
        placementHost=hostBounds;
        PositionFor(bar);
        if(Native.IsTopmost(Handle)) Native.PlaceNormal(Handle,IntPtr.Zero,0,0,0,0,0x01|0x02|0x10);
        IntPtr above=Native.GetWindow(bar.Handle,3);
        if(above==Handle || (shadow!=null && shadow.IsLayer(above))) return;
        Native.PlaceNormal(Handle,above,0,0,0,0,0x01|0x02|0x10);
    }
    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if(Width>0 && Height>0) MiniChrome.SetRoundedRegion(this,(float)(15*scale));
        if(shadow!=null) shadow.UpdatePlacement(scale,15);
    }
    static void Baseline(Graphics g,string text,Font font,Color color,RectangleF line,float baseline,bool right)
    {
        float ascent=font.Size*font.FontFamily.GetCellAscent(font.Style)/font.FontFamily.GetEmHeight(font.Style);
        using(var brush=new SolidBrush(color))
        using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.NoClip;
            format.Alignment=right?StringAlignment.Far:StringAlignment.Near;
            g.DrawString(text,font,brush,new RectangleF(line.X,baseline-ascent,line.Width,font.Height*2),format);
        }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if(appearance==null) return;
        MiniChrome.PaintSurface(e.Graphics,ClientRectangle,appearance,(float)(15*scale));
        if(!structured) { UiTypography.DrawText(e.Graphics,content.Text,content.Font,content.Bounds,appearance.Ink,Wrapped); return; }
        if(headingFont==null) return;
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; UiTypography.Prepare(g);
        float padding=(float)(21*scale);
        var line=new RectangleF(padding,0,Width-2*padding,Height);
        Color secondary=appearance.Dark?Color.FromArgb(178,178,183):Color.FromArgb(96,96,96);
        Color muted=appearance.Dark?Color.FromArgb(154,154,161):Color.FromArgb(130,130,130);
        Color track=appearance.Dark?Color.FromArgb(63,63,67):Color.FromArgb(237,237,237);
        Color fill=appearance.Dark?Color.FromArgb(182,182,190):Color.FromArgb(112,112,112);
        using(var separator=new Pen(track,Math.Max(1,(float)scale))) {
            foreach(var row in rows) {
                Baseline(g,row.Name,headingFont,appearance.Ink,line,row.TitleBaseline,false);
                Baseline(g,row.Percent,percentFont,appearance.Ink,line,row.PercentBaseline,true);
                using(var path=MiniChrome.RoundRect(row.Progress,(float)(2*scale)))
                using(var brush=new SolidBrush(track)) g.FillPath(brush,path);
                if(row.HasFraction && row.Fraction>0) {
                    RectangleF remaining=row.Progress; remaining.Width=(float)(remaining.Width*row.Fraction);
                    using(var path=MiniChrome.RoundRect(remaining,Math.Min((float)(2*scale),remaining.Width/2)))
                    using(var brush=new SolidBrush(fill)) g.FillPath(brush,path);
                }
                Baseline(g,row.Reset,resetFont,secondary,line,row.ResetBaseline,false);
                g.DrawLine(separator,padding,row.Separator,Width-padding,row.Separator);
            }
        }
        if(rows.Count==0) {
            Baseline(g,"额度",headingFont,appearance.Ink,line,(float)(34*scale),false);
            UiTypography.DrawText(g,emptyMessage,resetFont,new Rectangle(Px(21),Px(53),Width-2*Px(21),emptyHeight),secondary,Wrapped);
        }
        if(status.Length>0) UiTypography.DrawText(g,status,footerFont,
            new Rectangle(Px(21),(int)Math.Round(statusTop),Width-2*Px(21),statusHeight),appearance.Muted,Wrapped);
        if(footer.Length>0) Baseline(g,footer,footerFont,muted,line,footerBaseline,false);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) { if(shadow!=null) shadow.Dispose(); content.Font=Control.DefaultFont; DisposeFonts(); }
        base.Dispose(disposing);
    }
}
