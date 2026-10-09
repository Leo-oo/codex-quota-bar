using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Windows.Forms;

// Draws the built controls with synthetic fixtures. No normal UsageBar, network,
// desktop capture, Show/Open, user input, or real-account data is required.
// Expectations use the approved Library SVG version 15 plus the later approved
// real-machine menu/bar/help calibration, independently of layout helpers.
internal static class VisualChecks
{
    const string ApprovedSvg="private-design-reference-removed";
    static readonly double[] Scales={1.0,1.25,1.5,2.0};
    static readonly DateTimeOffset QuotaNow=Local(2026,10,3,0,5,2),NewsNow=Local(2026,10,3,23,50,0);
    static readonly List<string> Log=new List<string>();
    static readonly Dictionary<string,string> Pictures=new Dictionary<string,string>();
    static string folder; static int failed,checks;
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc,int index);
    static DateTimeOffset Local(int y,int m,int d,int h,int n,int s)
    {
        var date=new DateTime(y,m,d,h,n,s,DateTimeKind.Unspecified);
        return new DateTimeOffset(date,TimeZoneInfo.Local.GetUtcOffset(date));
    }
    static int Px(double dip,double scale) { return (int)Math.Round(dip*scale); }
    static string Tag(double scale) { return "dpi"+Px(100,scale).ToString(CultureInfo.InvariantCulture); }
    static void Check(string name,bool ok) { checks++; Log.Add((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; }
    static void Scenario(string name,Action action)
    {
        try { action(); }
        catch(Exception e) { Check(name+" render completes",false); Log.Add("  "+e.GetType().FullName+": "+e.Message); }
    }
    static QuotaSnapshot Quota(bool dual)
    {
        var value=new QuotaSnapshot { AccountKey="visual-fixture-v15-only",Fetched=QuotaNow.AddSeconds(-2) };
        if(dual) value.Windows.Add(new QuotaWindow { Minutes=300,Remaining=72,Reset=QuotaNow.AddHours(2).AddMinutes(18) });
        value.Windows.Add(new QuotaWindow { Minutes=10080,Remaining=dual?51:24,Reset=QuotaNow.AddDays(6).AddHours(14) });
        return value;
    }
    static ResetNewsItem Item(string type,bool confirmed)
    {
        return new ResetNewsItem {
            Id="visual-fixture-"+type,Version="fixture-v15",Type=type,KindExplicit=true,
            Status=confirmed?"confirmed":"announced",Scope="夹具套餐用户",Products="",
            Body="仅供控件渲染的虚构夹具数据，不代表个人到账。",
            Published=Local(2026,10,3,12,0,0).ToString("o",CultureInfo.InvariantCulture),
            ConfirmedAt=confirmed?Local(2026,10,3,12,0,0).ToString("o",CultureInfo.InvariantCulture):null,
            ConfirmationBasis=confirmed?"source_post":null,
            ExpectedFrom=confirmed?null:Local(2026,10,5,0,0,0).ToString("o",CultureInfo.InvariantCulture),
            Url="https://aihot.news/codex-reset"
        };
    }
    static ResetNewsSnapshot News(string kind)
    {
        var value=new ResetNewsSnapshot {
            DisplayPolicy=3,Day="2026-10-03",Monitor="healthy",VerifiedAt=NewsNow.ToString("o",CultureInfo.InvariantCulture),
            Items=new List<ResetNewsItem>()
        };
        if(kind=="empty-history") {
            value.LatestHistory=Item("direct_reset",true); value.LatestHistory.Id="visual-fixture-history";
            value.LatestHistory.ConfirmedAt=Local(2026,10,2,12,0,0).ToString("o",CultureInfo.InvariantCulture);
            value.LatestHistory.OccurredOn="2026-10-02"; value.LatestHistory.TimeInferred=false;
        } else if(kind=="credit-announced") value.Items.Add(Item("reset_credit",false));
        else if(kind=="direct-confirmed") value.Items.Add(Item("direct_reset",true));
        return value;
    }
    static Bitmap Render(Control control)
    {
        control.CreateControl(); control.PerformLayout();
        if(control.Width<1 || control.Height<1) throw new InvalidOperationException("Empty control render bounds.");
        var image=new Bitmap(control.Width,control.Height,PixelFormat.Format32bppArgb);
        try { control.DrawToBitmap(image,control.ClientRectangle); return image; }
        catch { image.Dispose(); throw; }
    }
    static void Save(Bitmap image,string name)
    {
        string path=Path.Combine(folder,name+"-fixture.png"); image.Save(path,ImageFormat.Png); Pictures[name]=path;
        Log.Add("IMAGE "+name+" "+image.Width+"x"+image.Height);
    }
    static void SizeCheck(string name,Size actual,int width,int height,double scale)
    {
        var expected=new Size(Px(width,scale),Px(height,scale));
        Check(name+" expected control bounds "+expected.Width+"x"+expected.Height,actual==expected);
        if(actual!=expected) Log.Add("  actual="+actual.Width+"x"+actual.Height);
    }
    static bool SameValueMask(Bitmap normal,Bitmap hover,int rows,double scale,Color ink)
    {
        int found=0;
        for(int row=0;row<rows;row++) {
            int top=Px(35*row+3,scale),bottom=Math.Min(normal.Height,Px(35*row+27,scale));
            // Values now follow the measured caption instead of a fixed column.
            // The full literal oracle separately proves the numeric glyph, so
            // retaining the whole row here cannot let a caption hide a failure.
            for(int y=top;y<bottom;y++) for(int x=0;x<normal.Width;x++) {
                bool a=normal.GetPixel(x,y).ToArgb()==ink.ToArgb(),b=hover.GetPixel(x,y).ToArgb()==ink.ToArgb();
                if(a) found++; if(a!=b) return false;
            }
        }
        return found>0;
    }
    static int Pixels(Bitmap image,Rectangle region,Func<Color,bool> predicate)
    {
        region=Rectangle.Intersect(new Rectangle(Point.Empty,image.Size),region); int count=0;
        for(int y=region.Top;y<region.Bottom;y++) for(int x=region.Left;x<region.Right;x++)
            if(predicate(image.GetPixel(x,y))) count++;
        return count;
    }
    static int InkHeight(Bitmap image,Rectangle region,Func<Color,bool> predicate)
    {
        region=Rectangle.Intersect(new Rectangle(Point.Empty,image.Size),region); int first=-1,last=-1;
        for(int y=region.Top;y<region.Bottom;y++) for(int x=region.Left;x<region.Right;x++)
            if(predicate(image.GetPixel(x,y))) { if(first<0) first=y; last=y; break; }
        return first<0?0:last-first+1;
    }
    static void Labels(string themeName,Appearance theme,double scale)
    {
        foreach(bool dual in new[]{false,true}) {
            string name="quota-"+(dual?"dual":"weekly")+"-"+themeName+"-"+Tag(scale); var value=Quota(dual);
            using(var label=new UsageLabel { Size=new Size(Px(48,scale),UsageLabel.HeightFor(value,false,false,scale)),BackColor=theme.Rail }) {
                label.Configure(value,theme,true,false,false,scale); label.CreateControl(); label.SetPreviewHover(false);
                using(var normal=Render(label)) {
                    Save(normal,name+"-normal");
                    Check(name+" independently expected rail height",label.Height==Px(dual?78:43,scale));
                    if(!dual) VerifyEntryGlyph(normal,name,"周","24%",14,theme,scale,0);
                    else {
                        VerifyEntryGlyph(normal,name+"-5h","5h","72%",14,theme,scale,0);
                        VerifyEntryGlyph(normal,name+"-weekly","周","51%",14,theme,scale,1);
                    }
                    label.SetPreviewHover(true);
                    using(var hover=Render(label)) {
                        Save(hover,name+"-hover");
                        var ink=theme.Dark?Color.FromArgb(184,184,191):Color.FromArgb(122,122,130);
                        Check(name+" hover keeps value ink positions",SameValueMask(normal,hover,dual?2:1,scale,ink));
                        int x=Px(24,scale),y=Px(35,scale);
                        Check(name+" hover changes blank background",normal.GetPixel(x,y).ToArgb()!=hover.GetPixel(x,y).ToArgb());
                        var hoverFill=theme.Dark?Color.FromArgb(53,53,59):Color.FromArgb(221,219,227);
                        Check(name+" approved calibrated hover fill",hover.GetPixel(x,y).ToArgb()==hoverFill.ToArgb());
                        Check(name+" hover preserves two-DIP outer gutter",hover.GetPixel(0,Px(20,scale)).ToArgb()==normal.GetPixel(0,Px(20,scale)).ToArgb());
                    }
                }
            }
        }
    }

    static void DrawLiteral(Graphics graphics,string text,Font font,Color ink,float x,float baseline)
    {
        using(var brush=new SolidBrush(ink))
        using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.NoClip;
            float ascent=font.Size*font.FontFamily.GetCellAscent(font.Style)/font.FontFamily.GetEmHeight(font.Style);
            graphics.DrawString(text,font,brush,new PointF(x,baseline-ascent),format);
        }
    }
    static bool HasInk(Color color,Color background)
    { return Math.Max(Math.Abs(color.R-background.R),Math.Max(Math.Abs(color.G-background.G),Math.Abs(color.B-background.B)))>4; }
    static Rectangle LiteralInkBounds(Bitmap bitmap,Rectangle region,Color background)
    {
        int left=bitmap.Width,top=bitmap.Height,right=-1,bottom=-1;
        region=Rectangle.Intersect(new Rectangle(Point.Empty,bitmap.Size),region);
        for(int y=region.Top;y<region.Bottom;y++) for(int x=region.Left;x<region.Right;x++)
            if(HasInk(bitmap.GetPixel(x,y),background)) { left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y); }
        return right<left?Rectangle.Empty:Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    static void VerifyEntryGlyph(Bitmap actual,string name,string caption,string expected,float logical,Appearance theme,double scale,int row)
    {
        Color ink=theme.Dark?Color.FromArgb(184,184,191):Color.FromArgb(122,122,130);
        using(var reference=new Bitmap(Px(112,scale),actual.Height,PixelFormat.Format32bppArgb))
        using(var captionOnly=new Bitmap(reference.Width,reference.Height,PixelFormat.Format32bppArgb))
        using(var valueOnly=new Bitmap(reference.Width,reference.Height,PixelFormat.Format32bppArgb))
        using(var graphics=Graphics.FromImage(reference))
        using(var captionGraphics=Graphics.FromImage(captionOnly))
        using(var valueGraphics=Graphics.FromImage(valueOnly))
        using(var font=new Font("Segoe UI",(float)(logical*scale),FontStyle.Regular,GraphicsUnit.Pixel,0))
        using(var captionFont=new Font("Microsoft YaHei UI",(float)(10*scale),FontStyle.Regular,GraphicsUnit.Pixel,134))
        using(var format=new StringFormat(StringFormat.GenericTypographic)) {
            foreach(var canvas in new[]{graphics,captionGraphics,valueGraphics}) {
                canvas.Clear(theme.Rail);canvas.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                canvas.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            }
            format.FormatFlags|=StringFormatFlags.NoWrap|StringFormatFlags.NoClip;
            // Complete known literals, explicit installed faces and approved
            // sizes/baseline; no application formatter/font/Caption/layout API.
            float captionX=(float)scale,baseline=(float)((35*row+20)*scale);
            float advance=graphics.MeasureString(caption,captionFont,Int32.MaxValue,format).Width;
            float valueX=captionX+advance+(expected=="—"?(float)scale:0);
            DrawLiteral(graphics,caption,captionFont,ink,captionX,baseline);
            DrawLiteral(graphics,expected,font,ink,valueX,baseline);
            DrawLiteral(captionGraphics,caption,captionFont,ink,captionX,baseline);
            DrawLiteral(valueGraphics,expected,font,ink,valueX,baseline);
            int painted=0,missing=0,outside=0;
            var rowArea=new Rectangle(0,Px(35*row+2,scale),reference.Width,Px(25,scale));
            for(int y=rowArea.Top;y<Math.Min(reference.Height,rowArea.Bottom);y++) for(int x=0;x<reference.Width;x++) {
                Color wanted=reference.GetPixel(x,y);
                if(wanted.ToArgb()==theme.Rail.ToArgb()) continue;
                painted++;
                if(x>=actual.Width || y>=actual.Height) { outside++;continue; }
                Color got=actual.GetPixel(x,y);
                if(Math.Max(Math.Abs(got.R-wanted.R),Math.Max(Math.Abs(got.G-wanted.G),Math.Abs(got.B-wanted.B)))>2) missing++;
            }
            Rectangle captionInk=LiteralInkBounds(captionOnly,rowArea,theme.Rail),valueInk=LiteralInkBounds(valueOnly,rowArea,theme.Rail);
            int overlap=0;
            for(int y=rowArea.Top;y<Math.Min(reference.Height,rowArea.Bottom);y++) for(int x=0;x<reference.Width;x++)
                if(HasInk(captionOnly.GetPixel(x,y),theme.Rail) && HasInk(valueOnly.GetPixel(x,y),theme.Rail)) overlap++;
            int gap=valueInk.IsEmpty || captionInk.IsEmpty?-1:valueInk.Left-captionInk.Right;
            Check(name+" independent complete caption and value reference paints",painted>0 && !captionInk.IsEmpty && !valueInk.IsEmpty);
            Check(name+" complete "+caption+expected+" glyph fits the 48-DIP canvas",outside==0);
            Check(name+" painted entry equals the complete independent glyphs",missing==0);
            Check(name+" caption and percentage glyphs do not overlap",overlap==0 && gap>=0);
            Check(name+" naturally adjacent caption and percentage ink",gap>=0 && gap<=(int)Math.Ceiling(1.5*scale));
            Save(reference,name+"-literal-reference");
            Log.Add("GLYPH "+name+" literal="+caption+expected+" logicalSize="+logical.ToString(CultureInfo.InvariantCulture)+
                " referenceInk="+painted+" outsideCanvas="+outside+" mismatched="+missing+" captionInk="+captionInk+" valueInk="+valueInk+
                " naturalInkGap="+gap+" overlap="+overlap+" valueOrigin="+valueX.ToString(CultureInfo.InvariantCulture));
        }
    }
    static void LabelBoundaries(string themeName,Appearance theme,double scale)
    {
        double[] values={23,100,99.9,Double.NaN}; string[] literals={"23%","100%","99.9%","—"};
        float[] sizes={14,12,11.5f,14}; string[] tags={"23","100","99-point-9","unknown"};
        for(int index=0;index<values.Length;index++) {
            string name="quota-value-"+tags[index]+"-"+themeName+"-"+Tag(scale); var snapshot=Quota(false);
            snapshot.Windows[0].Remaining=values[index];
            using(var label=new UsageLabel { Size=new Size(Px(48,scale),Px(43,scale)),BackColor=theme.Rail }) {
                label.Configure(snapshot,theme,true,false,false,scale); label.CreateControl(); label.SetPreviewHover(false);
                using(var normal=Render(label)) {
                    Save(normal,name+"-normal"); VerifyEntryGlyph(normal,name,"周",literals[index],sizes[index],theme,scale,0);
                    label.SetPreviewHover(true);
                    using(var hover=Render(label)) {
                        Save(hover,name+"-hover");
                        Color ink=theme.Dark?Color.FromArgb(184,184,191):Color.FromArgb(122,122,130);
                        Check(name+" fixed value ink positions survive hover",SameValueMask(normal,hover,1,scale,ink));
                    }
                }
            }
        }
    }
    static void MenuAnchors(double scale)
    {
        string name="bar-menu-anchor-"+Tag(scale);
        var host=new Rectangle(100,50,900,700); var area=new Rectangle(0,0,1400,900);
        var popup=new Size(Px(229,scale),Px(178,scale));
        var bar=new Rectangle(110,600,Px(48,scale),Px(43,scale));
        Point expected=new Point(bar.Right+Px(4,scale),host.Bottom-Px(8,scale)-popup.Height);
        Point actual=UsageBar.BarMenuLocation(bar,host,popup,area,scale);
        Check(name+" right edge plus four DIP and host bottom minus eight DIP",actual==expected);
        var moved=bar; moved.Y=350;
        Check(name+" rail vertical movement leaves host-bottom anchoring intact",UsageBar.BarMenuLocation(moved,host,popup,area,scale)==expected);
        if(Math.Abs(scale-1.25)<.001) Check(name+" 125-percent bottom gap is ten physical pixels",host.Bottom-(actual.Y+popup.Height)==10);
        Log.Add("ANCHOR "+name+" actual="+actual+" expected="+expected+" bottomGap="+(host.Bottom-actual.Y-popup.Height));
        var row=new Rectangle(250,370,Px(227,scale),Px(29,scale));
        var submenu=new Size(Px(155,scale),Px(103,scale));
        int expectedY=(int)Math.Round(row.Top+row.Height/2.0-submenu.Height/2.0,MidpointRounding.ToEven);
        Point placed=UsageBar.SubmenuLocation(new Rectangle(200,200,popup.Width,popup.Height),row,submenu,area,scale);
        Check(name+" submenu center follows the actual 29-DIP row center",placed.Y==expectedY);
        Check(name+" submenu center is within one physical pixel",Math.Abs((placed.Y+submenu.Height/2.0)-(row.Top+row.Height/2.0))<=.5);
        Log.Add("SUBMENU ANCHOR "+name+" row="+row+" popup="+submenu+" actual="+placed+" independentY="+expectedY);
    }

    static void Tooltips(string themeName,Appearance theme,double scale)
    {
        foreach(bool dual in new[]{false,true}) {
            string name="tooltip-"+(dual?"dual":"weekly")+"-"+themeName+"-"+Tag(scale);
            using(var popup=new DetailsPopup()) {
                popup.PrepareQuotaPreview(Quota(dual),false,false,null,theme,scale,QuotaNow);
                Check(name+" remains unopened",!popup.Visible);
                using(var image=Render(popup)) { Save(image,name); SizeCheck(name,image.Size,dual?304:258,dual?217:142,scale); }
                string text=popup.VisibleText;
                Check(name+" weekly fixture and countdown",text.Contains(dual?"51%":"24%") && text.Contains("6天14小时后重置"));
                Check(name+" last-success age",text.Contains("更新于00:05（2s前）"));
                Check(name+" supplied windows only",dual?(text.Contains("72%") && text.Contains("2小时18分钟后重置")):!text.Contains("5小时"));
                Check(name+" no mockup badge",!text.Contains("示例内容") && !text.Contains("演示"));
                Log.Add("TEXT "+name+" "+text.Replace("\r","").Replace("\n"," | "));
            }
        }
    }
    static void NewsCards(string themeName,Appearance theme,double scale,Form owner)
    {
        foreach(string kind in new[]{"empty-none","empty-history","credit-announced","direct-confirmed"}) {
            string name="news-"+kind+"-"+themeName+"-"+Tag(scale); int reads=0;
            using(var card=new ResetNewsCard(delegate(string id,string version) { reads++; },delegate { reads++; })) {
                card.PreparePreview(owner,News(kind),theme,scale,NewsNow);
                Check(name+" no popup or input observer",!card.Visible && !card.DismissalActive);
                string path=Path.Combine(folder,name+"-fixture.png"); card.SavePreview(path); Pictures[name]=path;
                using(var image=new Bitmap(path)) {
                    int baseWidth=kind.StartsWith("empty",StringComparison.Ordinal)?320:300;
                    int baseHeight=kind=="empty-none"?137:kind=="empty-history"?165:262;
                    SizeCheck(name,image.Size,baseWidth,baseHeight,scale);
                    // The approved SVG puts Details and the external-link icon in
                    // the bottom-right footer. Read pixels in that independent
                    // source area: VisibleText alone cannot prove the button drew.
                    var linkArea=new Rectangle(Px(baseWidth-74,scale),Px(baseHeight-42,scale),Px(61,scale),Px(30,scale));
                    int ink=Pixels(image,linkArea,delegate(Color c) {
                        return c.A>100 && (theme.Dark?(c.R>100 && c.G>100 && c.B>100):(c.R<160 && c.G<160 && c.B<160));
                    });
                    Check(name+" Details glyphs paint in SVG footer area",ink>=Px(3,scale));
                    Log.Add("IMAGE "+name+" "+image.Width+"x"+image.Height);
                }
                Check(name+" render does not acknowledge versions",reads==0); string text=card.VisibleText;
                Check(name+" public source/detail link",text.Contains("来源：AIHOT") && text.Contains("详情"));
                Check(name+" no original mockup status badge",!text.Contains("示例内容") && !text.Contains("· 示例"));
                if(kind=="empty-none") Check(name+" no invented history",text.Contains("暂无新消息") && !text.Contains("上次"));
                if(kind=="empty-history") Check(name+" reliable history date only",text.Contains("上次额度重置：10月2日") && !text.Contains("10月2日 12:00"));
                if(kind=="credit-announced") Check(name+" credit announcement semantics",text.Contains("发放重置卡") && text.Contains("预告") && text.Contains("预计生效时间") && !text.Contains("已发生"));
                if(kind=="direct-confirmed") Check(name+" confirmed direct reset/source-time semantics",text.Contains("直接额度重置") && text.Contains("已发生") && text.Contains("来源记录时间"));
                Check(name+" fixture content fits",card.OverflowDiagnostic==null && card.DisplayedCount==card.TotalCount);
                Log.Add("TEXT "+name+" "+text.Replace("\r","").Replace("\n"," | "));
            }
        }
    }
    static void Menus(string themeName,Appearance theme,double scale)
    {
        string name="menu-"+themeName+"-"+Tag(scale);
        using(var menu=UsageBar.CreatePreviewMenu(theme,scale,"更新于00:05（2s前）",true)) {
            // The production style now fixes the approved outer size. Asking a
            // native menu for its preferred width would replace that real size.
            menu.PerformLayout(); menu.CreateControl();
            var labels=new List<string>(); ToolStripMenuItem position=null,news=null,hide=null;
            foreach(ToolStripItem item in menu.Items) {
                if(item is ToolStripSeparator) continue; labels.Add(item.Text);
                if(item.Text=="位置调整") position=item as ToolStripMenuItem;
                if(item.Text=="Tibo 重置消息") news=item as ToolStripMenuItem;
                if(item.Text=="暂时隐藏") hide=item as ToolStripMenuItem;
            }
            Check(name+" approved command order",String.Join("|",labels.ToArray())=="更新于00:05（2s前）|Tibo 重置消息|暂时隐藏|位置调整|退出额度条");
            Check(name+" stays unopened",!menu.Visible);
            if(position==null || news==null || hide==null) throw new InvalidOperationException("Missing calibrated menu command.");
            if(Math.Abs(scale-1.25)<.001) {
                bool paintLogged=false;
                position.Paint+=delegate(object sender,PaintEventArgs args) {
                    if(paintLogged) return; paintLogged=true;
                    float dpi=args.Graphics.DpiY; var matrix=args.Graphics.Transform.Elements; var dc=args.Graphics.GetHdc();
                    try { Log.Add("PAINT "+name+" graphicsDpiY="+dpi.ToString(CultureInfo.InvariantCulture)+" HdcLogPixelsY="+GetDeviceCaps(dc,90)+" itemFont="+position.Font.Size.ToString(CultureInfo.InvariantCulture)+" itemPoints="+position.Font.SizeInPoints.ToString(CultureInfo.InvariantCulture)+" transformScale="+matrix[0].ToString(CultureInfo.InvariantCulture)+","+matrix[3].ToString(CultureInfo.InvariantCulture)); }
                    finally { args.Graphics.ReleaseHdc(dc); }
                };
                using(var fontProbe=new Bitmap(Px(120,scale),Px(48,scale))) using(var graphics=Graphics.FromImage(fontProbe)) {
                    graphics.Clear(theme.Surface);
                    UiTypography.DrawMenuText(graphics,"位置调整",position.Font,new Rectangle(0,0,fontProbe.Width,fontProbe.Height),theme.Ink,TextFormatFlags.NoPadding|TextFormatFlags.SingleLine|TextFormatFlags.VerticalCenter);
                    int probeHeight=InkHeight(fontProbe,new Rectangle(Point.Empty,fontProbe.Size),delegate(Color c) { return Math.Max(Math.Abs(c.R-theme.Surface.R),Math.Max(Math.Abs(c.G-theme.Surface.G),Math.Abs(c.B-theme.Surface.B)))>4; });
                    Log.Add("METRIC "+name+" directWrapperHanInkHeight="+probeHeight+" graphicsDpiY="+graphics.DpiY.ToString(CultureInfo.InvariantCulture)+" fontPoints="+position.Font.SizeInPoints.ToString(CultureInfo.InvariantCulture));
                    Save(fontProbe,"menu-font-reference-"+themeName+"-"+Tag(scale));
                }
            }
            var finalItem=menu.Items[menu.Items.Count-1];
            Check(name+" final command retains five-DIP bottom gutter",finalItem.Bounds.Bottom+Px(5,scale)<=menu.Height);
            Log.Add("METRIC "+name+" finalCommandBottom="+finalItem.Bounds.Bottom+" surfaceHeight="+menu.Height+" bottomGutter="+(menu.Height-finalItem.Bounds.Bottom));
            position.Select();
            using(var image=Render(menu)) {
                Save(image,name); SizeCheck(name,image.Size,229,178,scale);
                // The real-machine calibration keeps the unread dot in the
                // right side of the news row, within the new 229-DIP frame.
                var badgeArea=new Rectangle(Px(197,scale),Px(37,scale),Px(27,scale),Px(29,scale));
                int dot=Pixels(image,badgeArea,delegate(Color c) { return c.A>100 && c.R>170 && c.G<120 && c.B<120 && c.R-c.G>60; });
                Check(name+" unread dot paints in calibrated news-row area",dot>=Px(4,scale));
                Check(name+" command uses Segoe UI 13.5 logical pixels",news.Font.Name=="Segoe UI" && news.Font.Unit==GraphicsUnit.Pixel && Math.Abs(news.Font.Size-13.5*scale)<.02);
                Check(name+" explicit drawing selects verified Latin and sans Han faces",UiTypography.MenuDiagnostic.Contains("Latin{selectedFace=Segoe UI;") && UiTypography.MenuDiagnostic.Contains("CJK{selectedFace="+UiTypography.FamilyName+";") && UiTypography.MenuDiagnostic.Contains("weight=400; charset=134") && UiTypography.MenuDiagnostic.Contains("missing=0"));
                Log.Add("MENU FONT "+name+" "+UiTypography.MenuDiagnostic);
                int y=position.Bounds.Top+position.Bounds.Height/2;
                var selected=image.GetPixel(Math.Min(image.Width-1,Px(9,scale)),Math.Min(image.Height-1,y));
                int first=-1,last=-1;
                for(int x=0;x<image.Width;x++) if(image.GetPixel(x,y).ToArgb()==selected.ToArgb()) { if(first<0) first=x; last=x; }
                Check(name+" hover changes the menu surface",selected.ToArgb()!=theme.Surface.ToArgb());
                int left=first,right=image.Width-1-last;
                Check(name+" hover left/right insets differ by at most one pixel",first>=0 && Math.Abs(left-right)<=1);
                Check(name+" hover keeps calibrated six-DIP outer inset",Math.Abs(left-Px(6,scale))<=1 && Math.Abs(right-Px(6,scale))<=1);
                Log.Add("METRIC "+name+" font="+news.Font.Name+" pixelSize="+news.Font.Size.ToString(CultureInfo.InvariantCulture)+" hoverInsets="+left+","+right+" selectedItemBounds="+position.Bounds);
                if(Math.Abs(scale-1.25)<.001) {
                    Check(name+" matches real-machine native 286px width within three percent",Math.Abs(image.Width-286)<=286*.03);
                    int pitch=position.Bounds.Top-hide.Bounds.Top;
                    Check(name+" 125-percent command pitch is 36px plus/minus one",Math.Abs(pitch-36)<=1);
                    // This region isolates Han text after the 31-DIP text column. Exclude its left
                    // icon, right arrow and rounded hover margins; count painted
                    // pixels instead of trusting Font.Size or measurement APIs.
                    var han=new Rectangle(position.Bounds.Left+Px(31,scale),position.Bounds.Top+Px(4,scale),Px(96,scale),position.Bounds.Height-Px(8,scale));
                    int hanHeight=InkHeight(image,han,delegate(Color c) { return c.A>100 && (theme.Dark?(c.R>130 && c.G>130 && c.B>130):(c.R<180 && c.G<180 && c.B<180)); });
                    Check(name+" 125-percent Han ink matches the approved 16-17px range",hanHeight>=16 && hanHeight<=17);
                    Log.Add("METRIC "+name+" nativeReferenceWidth=286 toolWidth="+image.Width+" commandPitch="+pitch+" HanInkHeight="+hanHeight);
                }
            }
            var submenu=position.DropDown; submenu.PerformLayout(); submenu.CreateControl();
            labels.Clear(); foreach(ToolStripItem item in submenu.Items) if(!(item is ToolStripSeparator)) labels.Add(item.Text);
            Check(name+" approved submenu order",String.Join("|",labels.ToArray())=="上微调|下微调|恢复初始");
            Check(name+" submenu unopened",!submenu.Visible);
            var subLast=submenu.Items[submenu.Items.Count-1];
            Check(name+" submenu final command retains five-DIP bottom gutter",subLast.Bounds.Bottom+Px(5,scale)<=submenu.Height);
            Log.Add("METRIC "+name+" submenuFinalBottom="+subLast.Bounds.Bottom+" submenuHeight="+submenu.Height+" bottomGutter="+(submenu.Height-subLast.Bounds.Bottom));
            using(var image=Render(submenu)) { Save(image,"position-"+themeName+"-"+Tag(scale)); SizeCheck(name+" position",image.Size,155,103,scale); }
        }
    }
    static void Help(string themeName,Appearance theme,double scale)
    {
        string name="help-"+themeName+"-"+Tag(scale);
        using(var popup=new NewsHelpPopup()) {
            popup.PreparePreview(new Rectangle(120,80,Px(16,scale),Px(16,scale)),new Rectangle(0,0,1200,1000),theme,scale);
            Check(name+" remains unopened",!popup.Visible);
            Check(name+" uses calibrated 280-DIP wrap width",popup.Width==Px(280,scale));
            Check(name+" explains public-source scope without implying personal arrival",popup.AccessibleName.Contains("AIHOT公开活动") && popup.AccessibleName.Contains("不代表本人额度") && popup.AccessibleName.Contains("北京时间") && popup.AccessibleName.Contains("确认时间不是实际到账时间"));
            using(var image=Render(popup)) {
                Save(image,name);
                var body=new Rectangle(Px(14,scale),Px(14,scale),image.Width-Px(28,scale),image.Height-Px(28,scale));
                int ink=Pixels(image,body,delegate(Color c) { return c.A>100 && (theme.Dark?(c.R>100 && c.G>100 && c.B>100):(c.R<160 && c.G<160 && c.B<160)); });
                Check(name+" wrapped explanation actually paints",ink>=Px(20,scale));
            }
        }
    }

    static byte[] EmbeddedIcon(bool dark)
    {
        string name=dark?"QuotaBar.Tray.Dark.ico":"QuotaBar.Tray.Light.ico";
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
        using(var data=new MemoryStream()) {
            if(stream==null) throw new InvalidDataException("Missing embedded fixture icon "+name);
            stream.CopyTo(data); return data.ToArray();
        }
    }
    static byte[] IndependentIconPixels(byte[] data,int size)
    {
        if(data.Length<6 || BitConverter.ToUInt16(data,2)!=1) throw new InvalidDataException("Invalid ICO fixture resource.");
        int count=BitConverter.ToUInt16(data,4);
        for(int index=0;index<count;index++) {
            int entry=6+16*index; int width=data[entry]==0?256:data[entry],height=data[entry+1]==0?256:data[entry+1];
            if(width!=size || height!=size) continue;
            int offset=BitConverter.ToInt32(data,entry+12),header=BitConverter.ToInt32(data,offset);
            if(header!=40 || BitConverter.ToUInt16(data,offset+14)!=32 || BitConverter.ToInt32(data,offset+8)!=2*size)
                throw new InvalidDataException("Expected independent 32bpp native DIB.");
            byte[] result=new byte[size*size*4];
            for(int y=0;y<size;y++) Buffer.BlockCopy(data,offset+40+(size-1-y)*size*4,result,y*size*4,size*4);
            return result;
        }
        throw new InvalidDataException("Missing independent native ICO frame "+size);
    }
    static byte[] IndependentBitmapPixels(Bitmap image)
    {
        var result=new byte[image.Width*image.Height*4];
        var data=image.LockBits(new Rectangle(Point.Empty,image.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try { for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),result,y*image.Width*4,image.Width*4); }
        finally { image.UnlockBits(data); }
        return result;
    }
    static void NativeIconCheck(Bitmap image,string name,bool dark)
    {
        int size=image.Width;
        byte[] wanted=IndependentIconPixels(EmbeddedIcon(dark),size),got=IndependentBitmapPixels(image);
        int differences=0,transparent=0;
        for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
            int offset=(y*size+x)*4;
            for(int channel=0;channel<4;channel++) if(got[offset+channel]!=wanted[offset+channel]) {differences++;break;}
            if(wanted[offset+3]==0) transparent++;
        }
        Check(name+" full embedded RGBA including hidden RGB unchanged without extra gauge",differences==0);
        Check(name+" native resource retains transparent pixels",transparent>0);
        Log.Add("NATIVE ICON "+name+" frame="+size+" changedPixels="+differences+" transparent="+transparent);
    }
    static void NativeIconComposites(string themeName,Appearance theme)
    {
        foreach(int size in new[]{16,18,20,22,24,32,40,48,64}) using(var icon=ToolIcon.Create(theme.Dark,size)) using(var drawn=ToolIcon.Draw(size,theme.Dark)) {
            string name="tray-native-"+size+"px-"+themeName;
            Check(name+" selected native HICON size",icon.Width==size && icon.Height==size);
            foreach(bool white in new[]{false,true}) using(var actual=new Bitmap(size,size,PixelFormat.Format32bppArgb))
            using(var expected=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
                Color background=white?Color.White:Color.Black;
                using(var graphics=Graphics.FromImage(actual)) { graphics.Clear(background); graphics.DrawIconUnstretched(icon,new Rectangle(0,0,size,size)); }
                using(var graphics=Graphics.FromImage(expected)) { graphics.Clear(background); graphics.DrawImageUnscaled(drawn,0,0); }
                int differences=0;
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) {
                    Color a=actual.GetPixel(x,y),b=expected.GetPixel(x,y);
                    if(Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)))>2) differences++;
                }
                Check(name+" alpha compositing on "+(white?"white":"black")+" matches decoded bitmap",differences==0);
                Save(actual,name+"-"+(white?"white":"black")+"-composite");
                Log.Add("NATIVE HICON "+name+" background="+background.Name+" mismatched="+differences);
            }
        }
    }

    static void TrayIcons(string themeName,Appearance theme,double scale)
    {
        foreach(int baseSize in new[]{16,32}) {
            int size=Px(baseSize,scale); string name="tray-"+baseSize+"dip-"+themeName+"-"+Tag(scale);
            using(var image=ToolIcon.Draw(size,theme.Dark)) {
                Save(image,name); Check(name+" requested pixels",image.Width==size && image.Height==size); NativeIconCheck(image,name,theme.Dark); int painted=0,transparent=0;
                for(int y=0;y<size;y++) for(int x=0;x<size;x++) { if(image.GetPixel(x,y).A==0) transparent++; else painted++; }
                Check(name+" glyph and transparent surrounding",painted>0 && transparent>0);
            }
        }
    }
    static void Overview(string themeName,Appearance theme,double scale)
    {
        string[] names={
            "quota-dual-"+themeName+"-dpi100-normal","quota-dual-"+themeName+"-dpi100-hover",
            "quota-weekly-"+themeName+"-dpi100-normal","quota-weekly-"+themeName+"-dpi100-hover",
            "tooltip-dual-"+themeName+"-dpi100","tooltip-weekly-"+themeName+"-dpi100",
            "menu-"+themeName+"-dpi100","position-"+themeName+"-dpi100",
            "news-empty-none-"+themeName+"-dpi100","news-empty-history-"+themeName+"-dpi100",
            "news-credit-announced-"+themeName+"-dpi100","news-direct-confirmed-"+themeName+"-dpi100",
            "tray-16dip-"+themeName+"-dpi100","tray-32dip-"+themeName+"-dpi100",
            "help-"+themeName+"-dpi100"
        };
        for(int i=0;i<names.Length;i++) names[i]=names[i].Replace("dpi100",Tag(scale));
        int column=Px(320,scale)+65,row=Px(262,scale)+48;
        using(var canvas=new Bitmap(column*4+20,65+row*4+30)) using(var graphics=Graphics.FromImage(canvas))
        using(var heading=new Font("Microsoft YaHei UI",16,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var caption=new Font("Microsoft YaHei UI",11,FontStyle.Regular,GraphicsUnit.Pixel))
        using(var ink=new SolidBrush(theme.Ink)) {
            graphics.Clear(theme.Dark?Color.FromArgb(24,24,24):Color.FromArgb(247,247,248));
            graphics.DrawString("v0.11.3 calibrated controls / FIXTURE DATA / no desktop capture / "+themeName+" / "+Tag(scale),heading,ink,20,18);
            for(int i=0;i<names.Length;i++) {
                int x=20+(i%4)*column,y=65+(i/4)*row; string path; graphics.DrawString(names[i],caption,ink,x,y);
                if(Pictures.TryGetValue(names[i],out path)) using(var image=new Bitmap(path)) graphics.DrawImageUnscaled(image,x,y+25);
                else graphics.DrawString("Unavailable: see report",caption,ink,x,y+25);
            }
            canvas.Save(Path.Combine(folder,"overview-"+themeName+"-"+Tag(scale)+"-fixture.png"),ImageFormat.Png);
        }
    }
    internal static int RunStatic()
    {
        folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"visual-evidence-v0113"); Directory.CreateDirectory(folder);
        Log.Clear(); Pictures.Clear(); failed=checks=0;
        Log.Add("Base design=Library "+ApprovedSvg+" version=15; v0.11.3 uses the later approved five-point calibration, natural caption/value adjacency and direct embedded tray frames without an additional gauge.");
        Log.Add("All quota and event values are synthetic fixtures: weekly24%, dual72%/51%,23%,100%,99.9%,unknown. No account/auth/public API/runtime cache is read.");
        Log.Add("The six user screenshots have no materialized original pixels in this engineering environment; no 1:1 user-image comparison is claimed.");
        Log.Add("Only fixture DrawToBitmap, independent glyph references, embedded-icon pixels and memory HICON composites: no desktop capture, Show/Open, SendKeys, global input or monitor changes.");
        Log.Add("Scales=1,1.25,1.5,2 are control renders, not multi-monitor hardware verification.");
        Log.Add("Geometry: rail48x43/78, Tooltip258x142/304x217, empty320x137/165, event300x262; calibrated menu229x178, position155x103, helpWrap280 DIP.");
        Log.Add("Calibration evidence: desktop GetDpiForWindow=120 (1.25) for both native Codex and running v0.11.0 tool; supplied native menu width about286 physical pixels.");
        Log.Add("Typography correction uses the actual pixel/point font basis and HDC DPI; explicit static initialization prevents fonts being seeded before PMv2/application startup. Menu-only Latin/CJK exact HFONT path, command nominal size=13.5logical/status11; 125-percent Han ink target=16-17px.");
        Log.Add("Entry glyph oracle uses explicit Microsoft YaHei UI caption10 and Segoe UI14/12/11.5 pixels; shared baseline20, caption origin1 and actual caption advance, without space. Unknown em dash uses the approved one-DIP overhang correction.");
        Log.Add("FONT "+UiTypography.ResolutionDiagnostic);
        Log.Add("NUMERIC FONT "+UiTypography.NumericDiagnostic);
        using(var probe=new Bitmap(1,1)) using(var graphics=Graphics.FromImage(probe)) {
            float bitmapDpi=graphics.DpiY; var dc=graphics.GetHdc();
            try { Log.Add("RUNTIME bitmapDpiY="+bitmapDpi.ToString(CultureInfo.InvariantCulture)+" GdiHdcLogPixelsY="+GetDeviceCaps(dc,90)); }
            finally { graphics.ReleaseHdc(dc); }
        }
        Log.Add("These PNGs are offscreen control renders, not same-screen real-app comparisons or desktop screenshots.");
        Log.Add("Rail render width is explicitly configured as 48 DIP; this does not independently validate the normal application's layout width.");
        Log.Add("Menus share production styling but use inert fixture commands; these images do not validate real entry points or click dispatch.");
        Log.Add("History prefers occurred-on dates and labels source-confirmation fallbacks in Beijing time; confirmation does not assert delivery.");
        Log.Add("ConfirmedAt fixture time is labelled source-record time; no tool verification or exact execution is implied.");
        Log.Add("Windows font fallback may differ from SVG Noto Sans CJK SC. No pixel-identity claim is made for the unavailable approved PNG pixels.");
        foreach(double scale in Scales) Scenario("menu anchors "+Tag(scale),delegate { MenuAnchors(scale); });
        using(var owner=new Form { AutoScaleMode=AutoScaleMode.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(100,100,80,80),ShowInTaskbar=false }) {
            foreach(bool dark in new[]{false,true}) {
                string themeName=dark?"dark":"light"; var theme=Appearance.Palette(dark);
                if(!dark) {
                    Check("light surface SVG #ffffff",theme.Surface.ToArgb()==Color.White.ToArgb());
                    Check("light border SVG #e0e0e0",theme.Border.ToArgb()==Color.FromArgb(224,224,224).ToArgb());
                    Check("light ink SVG #242424",theme.Ink.ToArgb()==Color.FromArgb(36,36,36).ToArgb());
                }
                foreach(double scale in Scales) {
                    string group=themeName+" "+Tag(scale);
                    Scenario("labels "+group,delegate { Labels(themeName,theme,scale); });
                    Scenario("full value boundaries "+group,delegate { LabelBoundaries(themeName,theme,scale); });
                    Scenario("tooltips "+group,delegate { Tooltips(themeName,theme,scale); });
                    Scenario("news "+group,delegate { NewsCards(themeName,theme,scale,owner); });
                    Scenario("menus "+group,delegate { Menus(themeName,theme,scale); });
                    Scenario("help "+group,delegate { Help(themeName,theme,scale); });
                    Scenario("tray "+group,delegate { TrayIcons(themeName,theme,scale); });
                }
                Scenario("native icon HICON "+themeName,delegate { NativeIconComposites(themeName,theme); });
                Scenario("overview "+themeName+" dpi100",delegate { Overview(themeName,theme,1.0); });
                Scenario("overview "+themeName+" dpi125",delegate { Overview(themeName,theme,1.25); });
            }
            Check("preview owner never shown",!owner.Visible);
        }
        Log.Add("Images="+Pictures.Count+" plus four overview sheets"); Log.Add("Checks="+checks+" Failed="+failed);
        File.WriteAllLines(Path.Combine(folder,"visual-check-v0113.txt"),Log.ToArray()); return failed==0?0:1;
    }
    // Compatibility only. The current --visual-test calls RunStatic before any bar.
    internal static void Run(FixtureWindow host,UsageBar bar)
    {
        host.Shown+=delegate { host.BeginInvoke((Action)delegate {
            bar.Dispose(); host.Hide(); Environment.ExitCode=RunStatic(); host.Close();
        }); };
    }
}
