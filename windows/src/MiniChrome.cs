using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Shared drawing primitives for the approved v15 surfaces. All bounds are pixels.
internal static class MiniChrome
{
    internal static GraphicsPath RoundRect(RectangleF bounds,float radius)
    {
        var path=new GraphicsPath();
        float diameter=Math.Min(Math.Min(bounds.Width,bounds.Height),Math.Max(0,radius*2));
        if(diameter<=0) { path.AddRectangle(bounds); return path; }
        path.AddArc(bounds.Left,bounds.Top,diameter,diameter,180,90);
        path.AddArc(bounds.Right-diameter,bounds.Top,diameter,diameter,270,90);
        path.AddArc(bounds.Right-diameter,bounds.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(bounds.Left,bounds.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure(); return path;
    }
    internal static void PaintSurface(Graphics graphics,Rectangle bounds,Appearance theme,float radius)
    {
        if(bounds.Width<2 || bounds.Height<2) return;
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=RoundRect(new RectangleF(bounds.X+.5f,bounds.Y+.5f,bounds.Width-1,bounds.Height-1),radius))
        using(var fill=new SolidBrush(theme.Surface))
        using(var border=new Pen(theme.Border,1)) { graphics.FillPath(fill,path); graphics.DrawPath(border,path); }
    }
    internal static void SetRoundedRegion(Control control,float radius)
    {
        if(control.Width<=0 || control.Height<=0) return;
        using(var path=RoundRect(new RectangleF(0,0,control.Width,control.Height),radius))
        {
            var previous=control.Region; control.Region=new Region(path);
            if(previous!=null) previous.Dispose();
        }
    }
    internal static void Icon(Graphics graphics,string kind,Rectangle bounds,Color color)
    {
        var saved=graphics.Save();
        graphics.SmoothingMode=SmoothingMode.AntiAlias;
        graphics.TranslateTransform(bounds.Left,bounds.Top);
        graphics.ScaleTransform(bounds.Width/24f,bounds.Height/24f);
        using(var pen=new Pen(color,1.55f))
        {
            pen.StartCap=pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;
            if(kind=="news") {
                using(var outline=RoundRect(new RectangleF(4,4,16,16),3)) graphics.DrawPath(pen,outline); graphics.DrawLine(pen,8,8,16,8);
                graphics.DrawLine(pen,8,12,16,12); graphics.DrawLine(pen,8,16,13,16);
            } else if(kind=="clock") {
                graphics.DrawEllipse(pen,4,4,16,16); graphics.DrawLine(pen,12,7,12,12); graphics.DrawLine(pen,12,12,15,14);
            } else if(kind=="calendar") {
                graphics.DrawRectangle(pen,4,5,16,15); graphics.DrawLine(pen,4,10,20,10);
                graphics.DrawLine(pen,8,3,8,7); graphics.DrawLine(pen,16,3,16,7);
                graphics.DrawLine(pen,8,14,10,14); graphics.DrawLine(pen,14,14,16,14);
            } else if(kind=="hide") {
                graphics.DrawArc(pen,3,7,18,10,180,180); graphics.DrawArc(pen,3,7,18,10,0,180);
                graphics.DrawEllipse(pen,9,9,6,6); graphics.DrawLine(pen,3,3,21,21);
            } else if(kind=="move") {
                graphics.DrawLine(pen,12,3,12,21);
                graphics.DrawLines(pen,new[]{new PointF(8,7),new PointF(12,3),new PointF(16,7)});
                graphics.DrawLines(pen,new[]{new PointF(8,17),new PointF(12,21),new PointF(16,17)});
            } else if(kind=="exit") {
                graphics.DrawLines(pen,new[]{new PointF(10,4),new PointF(5,4),new PointF(4,5),new PointF(4,19),new PointF(5,20),new PointF(10,20)});
                graphics.DrawLine(pen,9,12,19,12);
                graphics.DrawLines(pen,new[]{new PointF(15,8),new PointF(19,12),new PointF(15,16)});
            } else if(kind=="up" || kind=="down") {
                bool up=kind=="up";
                graphics.DrawLines(pen,up?new[]{new PointF(7,14),new PointF(12,9),new PointF(17,14)}:
                    new[]{new PointF(7,10),new PointF(12,15),new PointF(17,10)});
            } else if(kind=="reset") {
                graphics.DrawArc(pen,4,4,16,16,-60,300);
                graphics.DrawLines(pen,new[]{new PointF(4,3),new PointF(4,9),new PointF(10,9)});
            } else if(kind=="external") {
                graphics.DrawLines(pen,new[]{new PointF(11,4),new PointF(4,4),new PointF(4,20),new PointF(20,20),new PointF(20,13)});
                graphics.DrawLines(pen,new[]{new PointF(14,3),new PointF(21,3),new PointF(21,10)});
                graphics.DrawLine(pen,11,13,21,3);
            } else if(kind=="help") {
                graphics.DrawEllipse(pen,3,3,18,18); graphics.DrawArc(pen,9,7,6,6,180,250);
                graphics.DrawLine(pen,12,12,12,14); graphics.DrawEllipse(pen,11.5f,17,.8f,.8f);
            }
        }
        graphics.Restore(saved);
    }
}

internal sealed class MiniMenuRenderer : ToolStripProfessionalRenderer
{
    readonly Appearance theme;
    readonly float scale;
    internal MiniMenuRenderer(Appearance value,double dpi):base(new QuotaMenuColors(value))
    { theme=value; scale=(float)dpi; RoundedEdges=false; }
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    { MiniChrome.PaintSurface(e.Graphics,e.ToolStrip.ClientRectangle,theme,12*scale); }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=MiniChrome.RoundRect(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),12*scale))
        using(var pen=new Pen(theme.Border)) e.Graphics.DrawPath(pen,path);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if(!e.Item.Selected || !e.Item.Enabled) return;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float inset=(float)Math.Round(6*scale);
        float left=inset-e.Item.Bounds.Left;
        float right=e.ToolStrip.ClientSize.Width-inset-e.Item.Bounds.Left;
        using(var path=MiniChrome.RoundRect(new RectangleF(left,2*scale,Math.Max(0,right-left),e.Item.Height-4*scale),5*scale))
        using(var fill=new SolidBrush(theme.Dark?Color.FromArgb(58,58,58):Color.FromArgb(243,243,243))) e.Graphics.FillPath(fill,path);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using(var pen=new Pen(theme.Dark?Color.FromArgb(65,65,65):Color.FromArgb(237,237,237)))
            e.Graphics.DrawLine(pen,13*scale,e.Item.Height/2f,e.Item.Width-13*scale,e.Item.Height/2f);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        UiTypography.Prepare(e.Graphics);
        var bounds=new Rectangle((int)Math.Round(31*scale),0,e.Item.Width-(int)Math.Round(53*scale),e.Item.Height);
        var color=e.Item.Enabled?(theme.Dark?theme.Ink:Color.FromArgb(51,51,51)):(theme.Dark?theme.Secondary:Color.FromArgb(128,128,128));
        UiTypography.DrawMenuText(e.Graphics,e.Text,e.TextFont,bounds,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
        int icon=(int)Math.Round(14*scale);
        MiniChrome.Icon(e.Graphics,e.Item.Name,new Rectangle((int)Math.Round(12*scale),(e.Item.Height-icon)/2,icon,icon),e.Item.Enabled?(theme.Dark?Color.FromArgb(183,183,183):Color.FromArgb(104,104,104)):color);
        if(e.Item.AccessibleDescription=="unread") using(var brush=new SolidBrush(Color.FromArgb(211,75,69)))
            e.Graphics.FillEllipse(brush,e.Item.Width-18.5f*scale,(e.Item.Height-5*scale)/2,5*scale,5*scale);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        float x=e.Item.Width-17*scale,y=e.Item.Height/2f;
        using(var pen=new Pen(theme.Dark?Color.FromArgb(183,183,183):Color.FromArgb(104,104,104),1.4f*scale))
            e.Graphics.DrawLines(pen,new[]{new PointF(x-2*scale,y-3*scale),new PointF(x+2*scale,y),new PointF(x-2*scale,y+3*scale)});
    }
}
