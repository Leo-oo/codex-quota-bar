using System;
using System.Drawing;

// Physical screen pixels: all bar-origin main popups share the host-bottom anchor.
internal static class PopupPlacement
{
    internal static Point ForBar(Rectangle bar,Rectangle host,Size popup,Rectangle workingArea,double scale)
    {
        int gap=Math.Max(1,(int)Math.Round(4*scale));
        long x=(long)bar.Right+gap;
        if(x+popup.Width>workingArea.Right) x=(long)bar.Left-popup.Width-gap;
        long lastLeft=Math.Max((long)workingArea.Left,(long)workingArea.Right-popup.Width);
        x=Math.Max(workingArea.Left,Math.Min(x,lastLeft));
        long bottom=host.IsEmpty?bar.Bottom:(long)host.Bottom-(int)Math.Round(8*scale);
        long lastTop=Math.Max((long)workingArea.Top,(long)workingArea.Bottom-popup.Height);
        long y=Math.Max(workingArea.Top,Math.Min(bottom-popup.Height,lastTop));
        // If an oversized popup cannot fit, pin its origin to the working-area edge.
        return new Point((int)x,(int)y);
    }
}
