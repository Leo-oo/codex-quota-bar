using System;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// One installed CJK sans family for GDI and GDI+. Logical pixel sizes are scaled once.
internal static class UiTypography
{
    // Do not let beforefieldinit start GDI+ font resolution before Main has
    // configured process DPI awareness.
    static UiTypography() { }
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern uint GetGlyphIndices(IntPtr dc,string text,int count,[Out] ushort[] glyphs,uint flags);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int GetTextFace(IntPtr dc,int count,StringBuilder text);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc,int index);
    static readonly string family=ResolveFamily();
    static string resolution;
    static readonly string numericFamily=NumericFamily(false);
    static readonly string numericStrongFamily=NumericFamily(true);
    internal static string FamilyName { get { return family; } }
    internal static string ResolutionDiagnostic { get { return resolution; } }
    static string ResolveFamily()
    {
        string[] candidates={"Microsoft YaHei UI","Microsoft YaHei","Microsoft JhengHei UI","DengXian","Noto Sans SC"};
        const string sample="重置消息暂时隐藏位置调整退出额度条每周剩余";
        foreach(string candidate in candidates) {
            try {
                using(var installed=new FontFamily(candidate))
                using(var font=new Font(installed,12,FontStyle.Regular,GraphicsUnit.Pixel,134))
                using(var bitmap=new Bitmap(8,8))
                using(var graphics=Graphics.FromImage(bitmap)) {
                    IntPtr dc=graphics.GetHdc(),native=font.ToHfont(),previous=SelectObject(dc,native);
                    try {
                        ushort[] glyphs=new ushort[sample.Length];
                        uint result=GetGlyphIndices(dc,sample,sample.Length,glyphs,1);
                        bool covered=result!=0xffffffff;
                        foreach(ushort glyph in glyphs) if(glyph==0xffff) covered=false;
                        if(!covered) continue;
                        var face=new StringBuilder(64); GetTextFace(dc,64,face);
                        resolution="family="+font.FontFamily.Name+"; gdiFace="+face+"; sampleHan="+sample.Length+"; missingHan=0; unit=Pixel; scaleAppliedOnce";
                        return font.FontFamily.Name;
                    } finally { SelectObject(dc,previous); DeleteObject(native); graphics.ReleaseHdc(dc); }
                }
            } catch(ArgumentException) { }
        }
        resolution="No verified CJK sans family; using system message font";
        return SystemFonts.MessageBoxFont.FontFamily.Name;
    }
    internal static Font Font(float logical,double scale,bool semibold=false)
    {
        float pixels=(float)(logical*scale);
        if(Single.IsNaN(pixels) || Single.IsInfinity(pixels) || pixels<=0) pixels=12;
        // GDI+ exposes Regular/Bold styles; stronger roles use the family's native Bold.
        return new Font(family,pixels,semibold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel,134);
    }
    static string NumericFamily(bool semibold)
    {
        string name=semibold?"Segoe UI Semibold":"Segoe UI";
        try { using(var installed=new FontFamily(name)) return installed.Name; }
        catch(ArgumentException) { return family; }
    }
    internal static Font Numeric(float logical,double scale,bool semibold=false)
    {
        float pixels=(float)(logical*scale);
        if(Single.IsNaN(pixels) || Single.IsInfinity(pixels) || pixels<=0) pixels=12;
        return new Font(semibold?numericStrongFamily:numericFamily,pixels,FontStyle.Regular,GraphicsUnit.Pixel,0);
    }
    internal static string NumericDiagnostic { get { return "regular="+numericFamily+"; semibold="+numericStrongFamily+"; unit=Pixel; scaleAppliedOnce; numeric-only"; } }
    static Font ForTextRenderer(Graphics graphics,Font font)
    {
        // Framework TextRenderer converts Font.SizeInPoints using its GDI HDC
        // DPI. The Pixel-to-point basis depends on GDI+ initialization, so a
        // fixed 96/DPI correction can shrink fonts in the real application.
        // Compare this font's actual conversion with its requested pixel size;
        // leave the caller's already-scaled font unchanged for GDI+ DrawString.
        int dpi=96;
        IntPtr dc=graphics.GetHdc();
        try { dpi=GetDeviceCaps(dc,90); }
        finally { graphics.ReleaseHdc(dc); }
        if(dpi<=0) dpi=96;
        float size=font.Size;
        if(font.Unit==GraphicsUnit.Pixel) {
            float converted=font.SizeInPoints*dpi/72f;
            if(converted>0 && !Single.IsNaN(converted) && !Single.IsInfinity(converted))
                size*=font.Size/converted;
        }
        return new Font(font.FontFamily,size,font.Style,font.Unit,font.GdiCharSet,font.GdiVerticalFont);
    }
    internal static void DrawText(Graphics graphics,string text,Font font,Rectangle bounds,Color ink,TextFormatFlags flags)
    {
        Prepare(graphics);
        using(var gdiFont=ForTextRenderer(graphics,font))
            TextRenderer.DrawText(graphics,text,gdiFont,bounds,ink,flags);
    }
    internal static Size MeasureText(Graphics graphics,string text,Font font,Size proposed,TextFormatFlags flags)
    {
        Prepare(graphics);
        using(var gdiFont=ForTextRenderer(graphics,font))
            return TextRenderer.MeasureText(graphics,text,gdiFont,proposed,flags);
    }
    internal static Size MeasureText(string text,Font font,Size proposed,TextFormatFlags flags)
    {
        // Use the same explicit GDI context as drawing, rather than Framework's
        // context-free measurement cache and its default padding/font quality.
        using(var bitmap=new Bitmap(1,1))
        using(var graphics=Graphics.FromImage(bitmap))
            return MeasureText(graphics,text,font,proposed,flags);
    }

    // Menu text has explicit Latin and CJK faces. This path selects the exact
    // HFONT used by DrawTextW; other UI keeps the shared TextRenderer correction.
    internal static Font MenuFont(float logical,double scale)
    { return Numeric(logical,scale,false); }
    static string menuDiagnostic="menu not drawn";
    internal static string MenuDiagnostic { get { return menuDiagnostic; } }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    sealed class MenuLogFont
    {
        public int Height,Width,Escapement,Orientation,Weight;
        public byte Italic,Underline,StrikeOut,Charset,OutPrecision,ClipPrecision,Quality,PitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Face;
    }
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct MenuMetrics
    {
        public int Height,Ascent,Descent,InternalLeading,ExternalLeading,AveCharWidth,MaxCharWidth,Weight,Overhang,AspectX,AspectY;
        public char FirstChar,LastChar,DefaultChar,BreakChar;
        public byte Italic,Underlined,StruckOut,PitchAndFamily,Charset;
    }
    [StructLayout(LayoutKind.Sequential)] struct MenuSize { public int Width,Height; }
    [StructLayout(LayoutKind.Sequential)]
    struct MenuRect
    {
        public int Left,Top,Right,Bottom;
        public MenuRect(Rectangle value) { Left=value.Left;Top=value.Top;Right=value.Right;Bottom=value.Bottom; }
    }
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateFontIndirect(MenuLogFont font);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern bool GetTextMetrics(IntPtr dc,out MenuMetrics metrics);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern bool GetTextExtentPoint32(IntPtr dc,string text,int length,out MenuSize size);
    [DllImport("gdi32.dll",CharSet=CharSet.Unicode)] static extern int GetObject(IntPtr obj,int bytes,[Out] MenuLogFont font);
    [DllImport("gdi32.dll")] static extern IntPtr GetCurrentObject(IntPtr dc,uint kind);
    [DllImport("gdi32.dll")] static extern int SaveDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool RestoreDC(IntPtr dc,int saved);
    [DllImport("gdi32.dll")] static extern int SetBkMode(IntPtr dc,int mode);
    [DllImport("gdi32.dll")] static extern uint SetTextColor(IntPtr dc,uint color);
    [DllImport("gdi32.dll")] static extern int IntersectClipRect(IntPtr dc,int left,int top,int right,int bottom);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int DrawTextW(IntPtr dc,string text,int length,ref MenuRect bounds,uint flags);
    sealed class MenuFace : IDisposable
    {
        internal IntPtr Handle;
        internal MenuMetrics Metrics;
        internal string Face,Diagnostic;
        internal MenuFace(IntPtr dc,string name,float pixels,FontStyle style,byte charset,string sample)
        {
            var value=new MenuLogFont {
                Height=-Math.Max(1,(int)Math.Round(pixels)),Weight=(style&FontStyle.Bold)!=0?700:400,
                Italic=(byte)((style&FontStyle.Italic)!=0?1:0),Charset=charset,Quality=5,Face=name
            };
            Handle=CreateFontIndirect(value);
            if(Handle==IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
            IntPtr previous=SelectObject(dc,Handle);
            try {
                GetTextMetrics(dc,out Metrics);
                var face=new StringBuilder(64); GetTextFace(dc,64,face); Face=face.ToString();
                var selected=new MenuLogFont();
                bool readLogFont=GetObject(Handle,Marshal.SizeOf(typeof(MenuLogFont)),selected)>0;
                ushort[] glyphs=new ushort[sample.Length];
                uint result=GetGlyphIndices(dc,sample,sample.Length,glyphs,1);
                int missing=result==0xffffffff?sample.Length:0;
                if(result!=0xffffffff) foreach(ushort glyph in glyphs) if(glyph==0xffff) missing++;
                Diagnostic="selectedFace="+Face+"; requestedFace="+name+"; sameHfont="+(GetCurrentObject(dc,6)==Handle)+
                    "; logFontRead="+readLogFont+"; lfHeight="+(readLogFont?selected.Height:value.Height)+
                    "; weight="+(readLogFont?selected.Weight:value.Weight)+"; charset="+(readLogFont?selected.Charset:charset)+
                    "; quality="+(readLogFont?selected.Quality:value.Quality)+"; sample="+sample+"; missing="+missing;
            } finally { SelectObject(dc,previous); }
        }
        public void Dispose() { if(Handle!=IntPtr.Zero) { DeleteObject(Handle); Handle=IntPtr.Zero; } }
    }
    sealed class MenuRun
    {
        internal string Text;
        internal MenuFace Font;
        internal int Width;
    }
    static bool IsMenuLatin(char value) { return value<=0x036f || value=='\u2026' || value=='\u2013' || value=='\u2014'; }
    static System.Collections.Generic.List<MenuRun> MenuRuns(IntPtr dc,string text,MenuFace latin,MenuFace cjk)
    {
        var result=new System.Collections.Generic.List<MenuRun>();
        for(int start=0;start<text.Length;) {
            bool isLatin=IsMenuLatin(text[start]); int end=start+1;
            while(end<text.Length && IsMenuLatin(text[end])==isLatin) end++;
            var font=isLatin?latin:cjk; string part=text.Substring(start,end-start);
            IntPtr previous=SelectObject(dc,font.Handle);
            try {
                MenuSize measured; GetTextExtentPoint32(dc,part,part.Length,out measured);
                result.Add(new MenuRun { Text=part,Font=font,Width=measured.Width });
            } finally { SelectObject(dc,previous); }
            start=end;
        }
        return result;
    }
    static int MenuWidth(System.Collections.Generic.List<MenuRun> runs)
    { int width=0; foreach(var run in runs) width+=run.Width; return width; }
    internal static Size MeasureMenuText(Graphics graphics,string text,Font font)
    {
        if(String.IsNullOrEmpty(text)) return Size.Empty;
        IntPtr dc=graphics.GetHdc();
        try {
            float pixels=font.Unit==GraphicsUnit.Pixel?font.Size:font.SizeInPoints*GetDeviceCaps(dc,90)/72f;
            using(var latin=new MenuFace(dc,numericFamily,pixels,font.Style,0,"Tibo"))
            using(var cjk=new MenuFace(dc,family,pixels,font.Style,134,"重置消息")) {
                var runs=MenuRuns(dc,text,latin,cjk);
                return new Size(MenuWidth(runs),Math.Max(latin.Metrics.Ascent,cjk.Metrics.Ascent)+Math.Max(latin.Metrics.Descent,cjk.Metrics.Descent));
            }
        } finally { graphics.ReleaseHdc(dc); }
    }
    internal static void DrawMenuText(Graphics graphics,string text,Font font,Rectangle bounds,Color ink,TextFormatFlags flags)
    {
        if(String.IsNullOrEmpty(text) || bounds.Width<=0 || bounds.Height<=0) return;
        IntPtr dc=graphics.GetHdc(); int saved=SaveDC(dc);
        try {
            float pixels=font.Unit==GraphicsUnit.Pixel?font.Size:font.SizeInPoints*GetDeviceCaps(dc,90)/72f;
            using(var latin=new MenuFace(dc,numericFamily,pixels,font.Style,0,"Tibo"))
            using(var cjk=new MenuFace(dc,family,pixels,font.Style,134,"重置消息")) {
                var runs=MenuRuns(dc,text,latin,cjk);
                if((flags&TextFormatFlags.EndEllipsis)!=0 && MenuWidth(runs)>bounds.Width) {
                    string visible=text;
                    do {
                        if(visible.Length==0) break;
                        int remove=Char.IsLowSurrogate(visible[visible.Length-1]) && visible.Length>1?2:1;
                        visible=visible.Substring(0,visible.Length-remove);
                        runs=MenuRuns(dc,visible+"\u2026",latin,cjk);
                    } while(MenuWidth(runs)>bounds.Width);
                }
                int width=MenuWidth(runs),ascent=Math.Max(latin.Metrics.Ascent,cjk.Metrics.Ascent),
                    height=ascent+Math.Max(latin.Metrics.Descent,cjk.Metrics.Descent);
                int x=bounds.Left;
                if((flags&TextFormatFlags.HorizontalCenter)!=0) x+=(bounds.Width-width)/2;
                else if((flags&TextFormatFlags.Right)!=0) x+=bounds.Width-width;
                int y=bounds.Top;
                if((flags&TextFormatFlags.VerticalCenter)!=0) y+=(bounds.Height-height)/2;
                else if((flags&TextFormatFlags.Bottom)!=0) y+=bounds.Height-height;
                SetBkMode(dc,1); SetTextColor(dc,(uint)(ink.R|(ink.G<<8)|(ink.B<<16)));
                IntersectClipRect(dc,bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);
                foreach(var run in runs) {
                    IntPtr previous=SelectObject(dc,run.Font.Handle);
                    try {
                        var part=new MenuRect(Rectangle.FromLTRB(x,y+ascent-run.Font.Metrics.Ascent,bounds.Right,bounds.Bottom));
                        DrawTextW(dc,run.Text,run.Text.Length,ref part,0x20|0x100|0x800);
                    } finally { SelectObject(dc,previous); }
                    x+=run.Width;
                }
                // These faces were observed while our exact HFONTs were selected
                // and those same handles were passed to DrawTextW above.
                menuDiagnostic="text="+text+"; path=explicit-HFONT/DrawTextW; requestedPixelSize="+pixels+
                    "; hdcDpi="+GetDeviceCaps(dc,90)+"; baseline="+(y+ascent)+"; advance="+width+
                    "; Latin{"+latin.Diagnostic+"}; CJK{"+cjk.Diagnostic+"}";
            }
        } finally { if(saved!=0) RestoreDC(dc,saved); graphics.ReleaseHdc(dc); }
    }
    internal static void Prepare(Graphics graphics)
    {
        // Popup/text surfaces are opaque. Explicit quality avoids default bitmap
        // hinting changing the visual weight between GDI TextRenderer and GDI+.
        graphics.TextRenderingHint=TextRenderingHint.ClearTypeGridFit;
    }
}
