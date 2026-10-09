using System;
using System.Drawing;

// All arguments are physical screen pixels, after WinForms lays out the popup.
internal static class SubmenuPlacement
{
    internal static int CenteredY(Rectangle itemScreen,int popupHeight,Rectangle workingArea)
    {
        double centered=itemScreen.Top+(itemScreen.Height-(double)popupHeight)/2;
        long requested=(long)Math.Round(centered,MidpointRounding.ToEven);
        long lastTop=Math.Max((long)workingArea.Top,(long)workingArea.Top+workingArea.Height-popupHeight);
        // An oversized popup cannot fit; pin its top to the working-area top.
        return (int)Math.Max(workingArea.Top,Math.Min(requested,lastTop));
    }
    internal static Point CenteredLocation(Rectangle itemScreen,Rectangle actualPopupBounds,Rectangle workingArea)
    {
        // Keep the existing/native horizontal side, gap and screen-edge flip.
        return new Point(actualPopupBounds.Left,CenteredY(itemScreen,actualPopupBounds.Height,workingArea));
    }
}

