using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Explicit own-data entry point. Nothing is shown and no production entry point runs.
internal static class HistoryLayout110Probe
{
    const double Scale=1.25;
    const string Expected="上次发放重置卡：12月31日 23:59（确认时间）";
    const TextFormatFlags Flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix;
    static int checks;
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc,int index);
    sealed class OracleSurface : Panel
    {
        internal string GlyphText; internal Font GlyphFont; internal Color Surface,Ink; internal Rectangle GlyphBounds;
        internal int ActualHdcDpi;
        internal OracleSurface() { DoubleBuffered=true; }
        protected override void OnPaint(PaintEventArgs e) {
            e.Graphics.Clear(Surface);
            IntPtr dc=e.Graphics.GetHdc(); try {ActualHdcDpi=GetDeviceCaps(dc,90);} finally {e.Graphics.ReleaseHdc(dc);}
            UiTypography.DrawText(e.Graphics,GlyphText,GlyphFont,GlyphBounds,Ink,Flags);
        }
    }
    static void Check(bool pass,string name) { if(!pass) throw new InvalidOperationException(name); checks++; Console.WriteLine("PASS "+name); }
    static T Field<T>(object instance,string name)
    { return (T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(instance); }
    static object Box(Rectangle r) { return new {x=r.X,y=r.Y,width=r.Width,height=r.Height}; }
    static Rectangle Ink(Bitmap image,Color background)
    {
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++)
            if(image.GetPixel(x,y).ToArgb()!=background.ToArgb()) { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x); bottom=Math.Max(bottom,y); }
        return right<0?Rectangle.Empty:Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    static object Case(string output,bool dark)
    {
        string name=dark?"dark":"light";
        int acknowledged=0;
        var history=new ResetNewsItem {Id="own-long-confirmation",Type="reset_credit",Status="confirmed",KindExplicit=true,
            ConfirmationBasis="source_post",ConfirmedAt="2025-12-31T23:59:59+08:00"};
        var snapshot=new ResetNewsSnapshot {Items=new List<ResetNewsItem>(),LatestHistory=history,VerifiedAt="2026-10-09T14:00:00+08:00"};
        var palette=Appearance.Palette(dark);
        using(var owner=new Form()) using(var card=new ResetNewsCard(delegate(string id,string version){acknowledged++;},delegate{})) {
            card.PreparePreview(owner,snapshot,palette,Scale,DateTimeOffset.Parse("2026-10-09T14:00:00+08:00"));
            var panel=Field<Panel>(card,"panel");
            var historyBounds=Field<Rectangle>(card,"historyBounds");
            var checkBounds=Field<Rectangle>(card,"checkBounds");
            var sourceBounds=Field<Rectangle>(card,"sourceBounds");
            var website=Field<Control>(card,"website");
            var historyFont=Field<Font>(card,"historyFont");
            int footer=Field<int>(card,"footerLine");
            string text=Field<string>(card,"historyText");
            string png=Path.Combine(output,"history-empty-"+name+"-125.png");
            card.SavePreview(png);
            Check(text==Expected && card.VisibleText.Contains(Expected) && card.TotalCount==0,name+" longest history is retained");
            Check(!owner.Visible && !card.Visible && !card.DismissalActive && acknowledged==0,name+" preview is unshown without hook or read");
            Color ink=dark?Color.FromArgb(168,168,168):Color.FromArgb(104,104,104);
            using(var independent=new Bitmap(panel.Width,Math.Max(panel.Height,historyBounds.Top+256))) using(var graphics=Graphics.FromImage(independent)) {
                graphics.Clear(palette.Surface);
                Size measured=UiTypography.MeasureText(graphics,Expected,historyFont,new Size(historyBounds.Width,256),Flags);
                int hdcDpi; IntPtr dc=graphics.GetHdc(); try {hdcDpi=GetDeviceCaps(dc,90);} finally {graphics.ReleaseHdc(dc);}
                int oracleHdcDpi;
                using(var oracle=new OracleSurface()) {
                    oracle.Size=independent.Size; oracle.GlyphText=Expected; oracle.GlyphFont=historyFont;
                    oracle.Surface=palette.Surface; oracle.Ink=ink;
                    oracle.GlyphBounds=new Rectangle(historyBounds.X,historyBounds.Y,historyBounds.Width,256);
                    oracle.DrawToBitmap(independent,oracle.ClientRectangle); oracleHdcDpi=oracle.ActualHdcDpi;
                }
                using(var reference=independent.Clone(new Rectangle(historyBounds.X,historyBounds.Y,historyBounds.Width,256),independent.PixelFormat)) {
                var expectedInk=Ink(reference,palette.Surface);
                Check(measured.Height<=historyBounds.Height && panel.ClientRectangle.Contains(historyBounds),name+" history slot contains measured text");
                Check(historyBounds.Bottom<footer && panel.ClientRectangle.Contains(checkBounds) && panel.ClientRectangle.Contains(sourceBounds) && panel.ClientRectangle.Contains(website.Bounds),name+" history and footer fit the frame");
                using(var actual=new Bitmap(png)) {
                    int mismatches=0;
                    using(var roi=actual.Clone(historyBounds,actual.PixelFormat)) {
                        for(int y=0;y<roi.Height;y++) for(int x=0;x<roi.Width;x++)
                            if(roi.GetPixel(x,y).ToArgb()!=reference.GetPixel(x,y).ToArgb()) mismatches++;
                        var actualInk=Ink(roi,palette.Surface);
                        Console.WriteLine("METRIC "+name+" mismatch="+mismatches+" actual="+actualInk+" independent="+expectedInk+" bounds="+historyBounds);
                        Check(mismatches==0 && actualInk==expectedInk,name+" card glyphs match the independent tall canvas");
                        Check(!expectedInk.IsEmpty && expectedInk.Bottom<=historyBounds.Height && expectedInk.Right<reference.Width,name+" full glyph plane has no bottom or right clipping");
                        return new {theme=name,uiScaleParameter=Scale,bitmapDpiX=graphics.DpiX,bitmapDpiY=graphics.DpiY,actualHdcDpi=hdcDpi,
                            oraclePaintHdcDpi=oracleHdcDpi,
                            history=text,historyBounds=Box(historyBounds),measuredWidth=measured.Width,measuredHeight=measured.Height,
                            independentGlyphBounds=Box(expectedInk),actualGlyphBounds=Box(actualInk),pixelMismatches=mismatches,
                            footerLine=footer,checkBounds=Box(checkBounds),sourceBounds=Box(sourceBounds),linkBounds=Box(website.Bounds),
                            clientBounds=Box(panel.ClientRectangle),fontFamily=historyFont.Name,fontSize=historyFont.Size,fontUnit=historyFont.Unit.ToString(),
                            image=Path.GetFileName(png),ownerShown=false,cardShown=false,inputHookActive=card.DismissalActive,readCallbacks=acknowledged};
                    }
                }
                }
            }
        }
    }
    [STAThread] static int Main(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        string output=Path.GetFullPath(args[0]); Directory.CreateDirectory(output);
        var cases=new List<object> {Case(output,false),Case(output,true)};
        File.WriteAllText(Path.Combine(output,"layout-metrics.json"),new JavaScriptSerializer().Serialize(new {schema=1,pass=checks,fail=0,cases=cases,
            scope="actual control bitmap rendering; not a live desktop screenshot",systemDpiChanged=false,normalProgramMainStarted=false}));
        Console.WriteLine("12 history layout checks passed"); return 0;
    }
}
