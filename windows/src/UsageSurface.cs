using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

// 48-DIP rail and 35-DIP rows remain stable. Caption and complete value share
// one baseline; longer percentages use fixed size tiers, independent of hover.
internal sealed class UsageLabel : Control
{
    internal ContentAlignment TextAlign { get; set; }
    internal bool AutoEllipsis { get; set; }
    internal Action ContextMenuOpening;
    internal Action FrameInvalidated;
    readonly Timer hoverTimer=new Timer { Interval=15 };
    QuotaSnapshot snapshot;
    Appearance theme;
    bool vertical,old,demo;
    double scale=1,hoverAmount,hoverFrom,hoverTarget;
    long hoverStarted;
    Font captionFont,valueFont,inlineCaptionFont,mediumValueFont,longValueFont;
    internal UsageLabel()
    {
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        AccessibleRole=AccessibleRole.StaticText;
        hoverTimer.Tick+=delegate {
            UpdateHoverAmount();
            Invalidate();
            RequestFrame();
            if(Math.Abs(hoverAmount-hoverTarget)<.0001) hoverTimer.Stop();
        };
    }
    internal static int HeightFor(QuotaSnapshot value,bool stale,bool sample,double dpi)
    {
        int count=value==null?0:value.Windows.Count;
        double height=35.0*Math.Max(1,count)+8;
        if(sample || stale) height+=14;
        return Math.Max(1,(int)Math.Round(height*ValidScale(dpi)));
    }
    static double ValidScale(double value)
    { return Double.IsNaN(value)||Double.IsInfinity(value)||value<=0?1:value; }
    protected override void WndProc(ref Message m)
    {
        if(m.Msg==0x7B && ContextMenuOpening!=null) ContextMenuOpening();
        base.WndProc(ref m);
    }
    internal void Configure(QuotaSnapshot value,Appearance palette,bool upright,bool stale,bool sample,double dpi)
    {
        snapshot=value; theme=palette; vertical=upright; old=stale; demo=sample;
        double next=ValidScale(dpi);
        if(captionFont==null || Math.Abs(next-scale)>.001) {
            if(captionFont!=null) captionFont.Dispose();
            if(valueFont!=null) valueFont.Dispose();
            if(inlineCaptionFont!=null) inlineCaptionFont.Dispose();
            if(mediumValueFont!=null) mediumValueFont.Dispose();
            if(longValueFont!=null) longValueFont.Dispose();
            captionFont=UiTypography.Font(11,next);
            valueFont=UiTypography.Numeric(14,next);
            inlineCaptionFont=UiTypography.Font(10,next);
            mediumValueFont=UiTypography.Numeric(12,next);
            longValueFont=UiTypography.Numeric(11.5f,next);
        }
        scale=next;
        Invalidate();
        RequestFrame();
    }
    void UpdateHoverAmount()
    {
        if(hoverStarted==0) return;
        double elapsed=(Stopwatch.GetTimestamp()-hoverStarted)*1000.0/Stopwatch.Frequency;
        double amount=Math.Min(1,Math.Max(0,elapsed/120));
        double eased=amount*amount*(3-2*amount);
        hoverAmount=hoverFrom+(hoverTarget-hoverFrom)*eased;
    }
    void AnimateHover(bool inside)
    {
        UpdateHoverAmount();
        hoverFrom=hoverAmount; hoverTarget=inside?1:0; hoverStarted=Stopwatch.GetTimestamp();
        hoverTimer.Start(); Invalidate();
        RequestFrame();
    }
    void RequestFrame() { if(FrameInvalidated!=null) FrameInvalidated(); }
    internal void SetPointerHover(bool inside)
    {
        if(hoverTarget==(inside?1:0)) return;
        AnimateHover(inside);
    }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); AnimateHover(true); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); AnimateHover(false); }
    // Control-only evidence: set an animation endpoint without displaying a desktop window.
    internal void SetPreviewHover(bool inside)
    {
        hoverTimer.Stop(); hoverStarted=0; hoverAmount=hoverFrom=hoverTarget=inside?1:0; Invalidate();
        RequestFrame();
    }
    protected override void OnVisibleChanged(EventArgs e)
    {
        if(!Visible) { hoverTimer.Stop(); hoverStarted=0; hoverAmount=hoverFrom=hoverTarget=0; }
        base.OnVisibleChanged(e);
        RequestFrame();
    }
    static Color Blend(Color from,Color to,double amount)
    {
        return Color.FromArgb((int)Math.Round(from.R+(to.R-from.R)*amount),
            (int)Math.Round(from.G+(to.G-from.G)*amount),(int)Math.Round(from.B+(to.B-from.B)*amount));
    }
    static void Baseline(Graphics g,string text,Font font,Color color,float x,float baseline)
    {
        float ascent=font.Size*font.FontFamily.GetCellAscent(font.Style)/font.FontFamily.GetEmHeight(font.Style);
        using(var brush=new SolidBrush(color))
        using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.NoClip;
            g.DrawString(text,font,brush,new PointF(x,baseline-ascent),format);
        }
    }
    float Caption(Graphics g,string text,Color color,float baseline)
    {
        float x=(float)scale,width=(float)(12.5*scale);
        using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.NoClip;
            string shown=text;
            float advance=g.MeasureString(shown,inlineCaptionFont,Int32.MaxValue,format).Width;
            if(advance>width && text!="周" && text!="5h") {
                int[] elements=StringInfo.ParseCombiningCharacters(text);
                shown="…";
                for(int count=elements.Length-1;count>0;count--) {
                    string candidate=text.Substring(0,elements[count])+"…";
                    if(g.MeasureString(candidate,inlineCaptionFont,Int32.MaxValue,format).Width<=width) { shown=candidate; break; }
                }
                advance=g.MeasureString(shown,inlineCaptionFont,Int32.MaxValue,format).Width;
            }
            Baseline(g,shown,inlineCaptionFont,color,x,baseline);
            return x+advance;
        }
    }
    Font InlineValueFont(string text)
    {
        return text.Length<=3?valueFont:text.Length==4?mediumValueFont:longValueFont;
    }
    void Progress(Graphics g,QuotaWindow window,RectangleF bounds,Color track,Color fill)
    {
        using(var path=MiniChrome.RoundRect(bounds,(float)scale))
        using(var brush=new SolidBrush(track)) g.FillPath(brush,path);
        double fraction;
        if(!QuotaDetails.TryFraction(window,out fraction) || fraction<=0) return;
        RectangleF filled=bounds; filled.Width=(float)(bounds.Width*fraction);
        using(var path=MiniChrome.RoundRect(filled,Math.Min((float)scale,filled.Width/2)))
        using(var brush=new SolidBrush(fill)) g.FillPath(brush,path);
    }
    static void TransparentQuality(Graphics graphics)
    {
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        graphics.CompositingMode=CompositingMode.SourceOver;
    }
    static Rectangle InkBounds(Bitmap image)
    {
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        var locked=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {
            var row=new byte[image.Width*4];
            for(int y=0;y<image.Height;y++) {
                Marshal.Copy(IntPtr.Add(locked.Scan0,y*locked.Stride),row,0,row.Length);
                for(int x=0;x<image.Width;x++) if(row[x*4+3]!=0) {
                    left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                }
            }
        } finally { image.UnlockBits(locked); }
        return right<0?Rectangle.Empty:Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    void CenterGlyphPlane(Graphics destination,Bitmap glyphs,float cellLeft,float cellWidth,int y)
    {
        Rectangle ink=InkBounds(glyphs);
        if(ink.IsEmpty) return;
        int x=(int)Math.Round(cellLeft+(cellWidth-ink.Width)/2.0-ink.X,MidpointRounding.AwayFromZero);
        destination.DrawImageUnscaled(glyphs,x,y);
    }
    // Authoritative live frame: normal background is alpha-zero; only hover
    // fades in the rounded surface. Glyph planes remain independently centered.
    internal Bitmap CreateTransparentBitmap(bool unread)
    {
        UpdateHoverAmount();
        var frame=new Bitmap(Math.Max(1,Width),Math.Max(1,Height),PixelFormat.Format32bppArgb);
        try {
            using(var graphics=Graphics.FromImage(frame)) {
                graphics.Clear(Color.FromArgb(0,0,0,0));TransparentQuality(graphics);
                if(theme!=null && hoverAmount>0) {
                    Color hover=theme.Dark?Color.FromArgb(49,48,62):Color.FromArgb(231,231,237);
                    float inset=(float)(2*scale);
                    using(var path=MiniChrome.RoundRect(new RectangleF(inset,inset,Math.Max(1,Width-2*inset),Math.Max(1,Height-2*inset)),(float)(8*scale)))
                    using(var brush=new SolidBrush(Color.FromArgb((int)Math.Round(255*hoverAmount),hover)))
                        graphics.FillPath(brush,path);
                }
                Color normal=theme==null?ForeColor:theme.Dark?Color.FromArgb(184,184,191):Color.FromArgb(122,122,130);
                Color active=theme==null?normal:theme.Dark?Color.FromArgb(220,220,226):Color.FromArgb(91,91,100);
                Color textColor=Blend(normal,active,hoverAmount);
                if(snapshot==null || snapshot.Windows.Count==0 || theme==null || captionFont==null) {
                    using(var glyphs=new Bitmap(frame.Width,frame.Height,PixelFormat.Format32bppArgb)) {
                    using(var g=Graphics.FromImage(glyphs))
                    using(var brush=new SolidBrush(textColor))
                    using(var format=new StringFormat(StringFormat.GenericTypographic)) {
                        g.Clear(Color.FromArgb(0,0,0,0));TransparentQuality(g);
                        format.Alignment=StringAlignment.Center;format.LineAlignment=StringAlignment.Center;
                        format.Trimming=StringTrimming.EllipsisCharacter;format.FormatFlags|=StringFormatFlags.LineLimit;
                        g.DrawString(Text,Font,brush,new RectangleF((float)(2*scale),0,Math.Max(1,frame.Width-(float)(4*scale)),frame.Height),format);
                    }
                    CenterGlyphPlane(graphics,glyphs,0,frame.Width,0);
                    }
                } else {
                    Color track=theme.Dark?Color.FromArgb(65,65,73):Color.FromArgb(225,225,229);
                    Color fill=Blend(theme.Dark?Color.FromArgb(149,149,160):Color.FromArgb(143,143,151),
                        theme.Dark?Color.FromArgb(180,180,190):Color.FromArgb(116,116,124),hoverAmount);
                    int offset=(int)Math.Round((old||demo?14:0)*scale);
                    if(vertical && (old||demo)) {
                        using(var plane=new Bitmap(frame.Width+32,(int)Math.Ceiling(14*scale+8),PixelFormat.Format32bppArgb)) {
                        using(var g=Graphics.FromImage(plane)) {
                            g.Clear(Color.FromArgb(0,0,0,0));TransparentQuality(g);
                            Baseline(g,demo?"演示":"旧值",captionFont,demo?textColor:theme.Muted,8,(float)(11*scale));
                        }
                        CenterGlyphPlane(graphics,plane,0,frame.Width,0);
                        }
                    }
                    for(int index=0;index<snapshot.Windows.Count;index++) {
                        var window=snapshot.Windows[index];
                        float cell=vertical?frame.Width:frame.Width/(float)snapshot.Windows.Count;
                        int row=vertical?offset+(int)Math.Round(35*index*scale):0;
                        using(var plane=new Bitmap((int)Math.Ceiling(cell+32*scale),(int)Math.Ceiling(35*scale),PixelFormat.Format32bppArgb)) {
                        using(var g=Graphics.FromImage(plane)) {
                            g.Clear(Color.FromArgb(0,0,0,0));TransparentQuality(g);
                            string percent=QuotaDetails.DisplayPercent(window);
                            float padding=(float)(16*scale);
                            if(vertical) {
                                g.TranslateTransform(padding-(float)scale,0);
                                float valueX=Caption(g,window.Label??"窗口",textColor,(float)(20*scale));
                                if(percent=="—")valueX+=(float)scale;
                                Baseline(g,percent,InlineValueFont(percent),textColor,valueX,(float)(20*scale));
                            } else {
                                string title=(index==0&&(demo||old)?(demo?"演示 · ":"旧值 · "):"")+(window.Label??"窗口")+"剩余";
                                float advance;
                                using(var format=new StringFormat(StringFormat.GenericTypographic))advance=g.MeasureString(title,captionFont,Int32.MaxValue,format).Width;
                                Baseline(g,title,captionFont,textColor,padding,(float)(17*scale));
                                Baseline(g,percent,valueFont,textColor,padding+advance+(float)(4*scale),(float)(17*scale));
                            }
                        }
                        CenterGlyphPlane(graphics,plane,vertical?0:index*cell,cell,row);
                        }
                        Progress(graphics,window,new RectangleF(vertical?(float)(7*scale):index*cell+(float)(7*scale),
                            row+(float)((vertical?32:25)*scale),vertical?(float)(34*scale):Math.Max(1,cell-(float)(14*scale)),(float)(2*scale)),track,fill);
                    }
                }
                if(unread)using(var brush=new SolidBrush(Color.FromArgb(207,101,92)))
                    graphics.FillEllipse(brush,frame.Width-(float)(6.4*scale),(float)(.6*scale),(float)(4.8*scale),(float)(4.8*scale));
            }
            return frame;
        } catch { frame.Dispose();throw; }
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        UpdateHoverAmount();
        var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; UiTypography.Prepare(g);
        if(theme!=null && hoverAmount>0) {
            Color target=theme.Dark?Color.FromArgb(53,53,59):Color.FromArgb(221,219,227);
            float inset=(float)(2*scale);
            using(var path=MiniChrome.RoundRect(new RectangleF(inset,inset,Math.Max(1,Width-2*inset),Math.Max(1,Height-2*inset)),(float)(8*scale)))
            using(var brush=new SolidBrush(Blend(BackColor,target,hoverAmount))) g.FillPath(brush,path);
        }
        if(snapshot==null || snapshot.Windows.Count==0 || theme==null || captionFont==null) {
            UiTypography.DrawText(g,Text,Font,ClientRectangle,ForeColor,
                TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
            base.OnPaint(e); return;
        }
        Color caption=theme.Dark?Color.FromArgb(184,184,191):Color.FromArgb(122,122,130);
        Color value=caption;
        Color track=theme.Dark?Color.FromArgb(65,65,73):Color.FromArgb(225,225,229);
        Color normalFill=theme.Dark?Color.FromArgb(149,149,160):Color.FromArgb(143,143,151);
        Color hoverFill=theme.Dark?Color.FromArgb(180,180,190):Color.FromArgb(116,116,124);
        Color fill=Blend(normalFill,hoverFill,hoverAmount);
        if(vertical) {
            float offset=(float)((demo||old?14:0)*scale);
            if(demo || old) {
                string status=demo?"演示":"旧值";
                using(var format=new StringFormat(StringFormat.GenericTypographic)) {
                    float measured=g.MeasureString(status,captionFont,Int32.MaxValue,format).Width;
                    Baseline(g,status,captionFont,demo?caption:theme.Muted,(Width-measured)/2f,(float)(11*scale));
                }
            }
            for(int i=0;i<snapshot.Windows.Count;i++) {
                var window=snapshot.Windows[i];
                float row=offset+(float)(35*i*scale);
                string percent=QuotaDetails.DisplayPercent(window);
                float valueX=Caption(g,window.Label??"窗口",caption,row+(float)(20*scale));
                // The missing-value em dash extends left of its typographic
                // origin; a one-DIP overhang correction keeps both glyphs whole.
                if(percent=="—") valueX+=(float)scale;
                Baseline(g,percent,InlineValueFont(percent),value,valueX,row+(float)(20*scale));
                Progress(g,window,new RectangleF((float)(7*scale),row+(float)(32*scale),(float)(34*scale),(float)(2*scale)),track,fill);
            }
        } else {
            float cell=Width/(float)snapshot.Windows.Count;
            for(int i=0;i<snapshot.Windows.Count;i++) {
                var window=snapshot.Windows[i];
                string title=(i==0 && (demo||old)?(demo?"演示 · ":"旧值 · "):"")+(window.Label??"窗口")+"剩余";
                SizeF measured;
                using(var format=new StringFormat(StringFormat.GenericTypographic)) measured=g.MeasureString(title,captionFont,Int32.MaxValue,format);
                float left=i*cell+(float)(7*scale);
                Baseline(g,title,captionFont,old&&!demo?theme.Muted:caption,left,(float)(17*scale));
                Baseline(g,QuotaDetails.DisplayPercent(window),valueFont,value,left+measured.Width+(float)(4*scale),(float)(17*scale));
                Progress(g,window,new RectangleF(left,(float)(25*scale),Math.Max(1,cell-(float)(14*scale)),(float)(2*scale)),track,fill);
            }
        }
        // The existing unread badge is painted by the owner after the stable quota rows.
        base.OnPaint(e);
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing) {
            FrameInvalidated=null;
            hoverTimer.Stop(); hoverTimer.Dispose();
            if(captionFont!=null) captionFont.Dispose();
            if(valueFont!=null) valueFont.Dispose();
            if(inlineCaptionFont!=null) inlineCaptionFont.Dispose();
            if(mediumValueFont!=null) mediumValueFont.Dispose();
            if(longValueFont!=null) longValueFont.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class QuotaMenuColors : ProfessionalColorTable
{
    readonly Appearance theme;
    internal QuotaMenuColors(Appearance palette) { theme=palette; UseSystemColors=false; }
    Color Background { get { return theme.Dark?theme.Surface:Color.White; } }
    Color Border { get { return theme.Dark?theme.Border:Color.FromArgb(224,224,224); } }
    public override Color ToolStripDropDownBackground { get { return Background; } }
    public override Color ImageMarginGradientBegin { get { return Background; } }
    public override Color ImageMarginGradientMiddle { get { return Background; } }
    public override Color ImageMarginGradientEnd { get { return Background; } }
    public override Color MenuBorder { get { return Border; } }
    public override Color MenuItemBorder { get { return Border; } }
    public override Color MenuItemSelected { get { return theme.Dark?Color.FromArgb(52,53,57):Color.FromArgb(243,243,243); } }
    public override Color MenuItemSelectedGradientBegin { get { return MenuItemSelected; } }
    public override Color MenuItemSelectedGradientEnd { get { return MenuItemSelected; } }
    public override Color SeparatorDark { get { return theme.Dark?theme.Border:Color.FromArgb(237,237,237); } }
    public override Color SeparatorLight { get { return Background; } }
}

internal static class ToolIcon
{
    internal static bool SystemTaskbarDark
    {
        get {
            try {
                using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",false)) {
                    object value=key==null?null:key.GetValue("SystemUsesLightTheme");
                    return value is int && (int)value==0;
                }
            } catch(UnauthorizedAccessException) { return false; }
              catch(System.Security.SecurityException) { return false; }
              catch(IOException) { return false; }
        }
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string name,string caption);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int GetSystemMetricsForDpi(int index,uint dpi);
    internal static int CurrentTaskbarPixelSize
    {
        get {
            try {
                IntPtr tray=FindWindow("Shell_TrayWnd",null);
                uint dpi=tray==IntPtr.Zero?96:GetDpiForWindow(tray);
                int size=GetSystemMetricsForDpi(49,dpi==0?96:dpi);
                if(size>=8 && size<=64) return size;
            } catch(EntryPointNotFoundException) { }
            return Math.Max(8,Math.Min(64,SystemInformation.SmallIconSize.Width));
        }
    }
    internal static Bitmap Draw(int size) { return Draw(size,SystemTaskbarDark); }
    internal static Bitmap Draw(int size,bool dark) { return NativeTrayAssets.Original(size,dark); }
    internal static Icon Create() { return Create(SystemTaskbarDark); }
    static byte[] DibFrame(Bitmap image)
    {
        int size=image.Width,maskStride=((size+31)/32)*4;
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream)) {
            writer.Write(40);writer.Write(size);writer.Write(size*2);
            writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(0);
            writer.Write(size*size*4+maskStride*size);
            writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
            for(int y=size-1;y>=0;y--) for(int x=0;x<size;x++) {
                Color pixel=image.GetPixel(x,y);
                writer.Write(pixel.B);writer.Write(pixel.G);writer.Write(pixel.R);writer.Write(pixel.A);
            }
            for(int y=size-1;y>=0;y--) {
                var mask=new byte[maskStride];
                for(int x=0;x<size;x++) if(image.GetPixel(x,y).A==0) mask[x/8]|=(byte)(1<<(7-x%8));
                writer.Write(mask);
            }
            writer.Flush();return stream.ToArray();
        }
    }
    internal static Icon Create(bool dark) { return Create(dark,CurrentTaskbarPixelSize); }
    internal static Icon Create(bool dark,int pixelSize)
    {
        if(pixelSize<8 || pixelSize>64) throw new ArgumentOutOfRangeException("pixelSize");
        var nativeSizes=new List<int>(new[]{16,20,24,32,40,48,64});
        if(!nativeSizes.Contains(pixelSize)) { nativeSizes.Add(pixelSize);nativeSizes.Sort(); }
        int[] sizes=nativeSizes.ToArray();
        var frames=new List<byte[]>();
        foreach(int size in sizes)
            using(var image=Draw(size,dark))
                frames.Add(DibFrame(image));
        using(var stream=new MemoryStream())
        using(var writer=new BinaryWriter(stream)) {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset);
                offset+=frames[i].Length;
            }
            foreach(var frame in frames) writer.Write(frame);
            writer.Flush(); stream.Position=0;
            using(var icon=new Icon(stream,new Size(pixelSize,pixelSize))) return (Icon)icon.Clone();
        }
    }
}
