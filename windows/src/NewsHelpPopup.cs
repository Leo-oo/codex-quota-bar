using System;
using System.Drawing;
using System.Windows.Forms;

// Hover explanation only. This window never takes focus or installs another input hook.
internal sealed class NewsHelpPopup : Form
{
    internal const string Explanation="AIHOT公开活动不代表本人额度已重置或收到卡。历史按北京时间显示；确认时间不是实际到账时间。";
    static readonly TextFormatFlags Flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
    Appearance appearance;
    Font bodyFont;
    readonly PopupShadow shadow;
    Rectangle textBounds;
    Rectangle lastTarget,lastWork;
    double scale=1;
    internal NewsHelpPopup()
    {
        FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual;
        AutoScaleMode=AutoScaleMode.None; SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint,true);
        AccessibleName=Explanation;
        shadow=PopupShadow.Attach(this,1,12);
    }
    int Px(double value) { return Math.Max(1,(int)Math.Round(value*scale)); }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams
    { get { var p=base.CreateParams; p.ExStyle|=0x80|0x08000000; return p; } }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
        base.WndProc(ref m);
    }
    internal void ShowHelp(Control owner,Rectangle target,Rectangle work,Appearance theme,double dpi)
    {
        if(IsDisposed || owner.IsDisposed) return;
        if(Visible && appearance==theme && target==lastTarget && work==lastWork && Math.Abs(scale-dpi)<.01) return;
        LayoutHelp(target,work,theme,dpi);
        if(!Visible) Show(owner);
        shadow.UpdatePlacement(scale,12);
        Invalidate();
    }
    // Offline fixtures can measure and paint the production layout without Show or input hooks.
    internal void PreparePreview(Rectangle target,Rectangle work,Appearance theme,double dpi)
    {
        if(Visible) throw new InvalidOperationException("可见说明不能用于离线预览");
        LayoutHelp(target,work,theme,dpi);
    }
    void LayoutHelp(Rectangle target,Rectangle work,Appearance theme,double dpi)
    {
        lastTarget=target; lastWork=work;
        scale=dpi; appearance=theme;
        int width=Math.Max(1,Math.Min(Px(280),work.Width)),margin=Math.Min(Px(14),Math.Max(0,width/4)),textWidth=Math.Max(1,width-2*margin);
        if(bodyFont!=null) bodyFont.Dispose();
        bodyFont=UiTypography.Font(12,scale);
        int height=Math.Max(1,Math.Min(work.Height,Math.Max(Px(64),UiTypography.MeasureText(Explanation,bodyFont,new Size(textWidth,Int32.MaxValue),Flags).Height+2*margin)));
        textBounds=new Rectangle(margin,margin,textWidth,Math.Max(1,height-2*margin));
        Size=new Size(width,height);
        int x=target.Right-width,y=target.Bottom+Px(8);
        if(y+height>work.Bottom) y=target.Top-height-Px(8);
        Location=new Point(Math.Max(work.Left,Math.Min(x,work.Right-width)),Math.Max(work.Top,Math.Min(y,work.Bottom-height)));
        MiniChrome.SetRoundedRegion(this,Px(12)); BackColor=theme.Surface;
        shadow.UpdatePlacement(scale,12);
    }
    internal void ApplyTheme(Appearance theme)
    { appearance=theme; BackColor=theme.Surface; shadow.UpdatePlacement(scale,12); if(Visible) Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(appearance==null || bodyFont==null) return;
        UiTypography.Prepare(e.Graphics);
        MiniChrome.PaintSurface(e.Graphics,ClientRectangle,appearance,Px(12));
        Color ink=appearance.Dark?Color.FromArgb(174,174,174):Color.FromArgb(104,104,104);
        UiTypography.DrawText(e.Graphics,Explanation,bodyFont,textBounds,ink,Flags);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) {
            shadow.Dispose();
            if(bodyFont!=null) { bodyFont.Dispose(); bodyFont=null; }
        }
        base.Dispose(disposing);
    }
}
