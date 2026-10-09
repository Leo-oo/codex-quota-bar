using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

// Pure geometry: rectangles, contentHeight and signed upwardOffset are screen pixels.
// Only the named DIP dimensions below are multiplied by the target window scale.
internal static class RailLayout
{
    // This is the agreed reference convention: 64/24 physical pixels at 125%.
    // Other DPI values round the final avatar-to-bar gap, not cumulative steps.
    internal const double InitialAvatarGapDip = 51.2;
    internal const double MinimumAvatarGapDip = 19.2;
    internal static int UpwardOffsetPixels(int signedDownwardDip,double scale)
    {
        return checked(Pixels(InitialAvatarGapDip-signedDownwardDip,scale)-
            Pixels(InitialAvatarGapDip,scale));
    }
    internal static bool TryPlace(Rectangle host, double scale, Rectangle avatar,
        Rectangle update, Rectangle[] obstacles, int contentHeight, int upwardOffset,
        out Rectangle result)
    {
        string conflict;
        return TryPlace(host,scale,avatar,update,obstacles,contentHeight,upwardOffset,out result,out conflict);
    }
    internal static bool TryPlace(Rectangle host,double scale,Rectangle avatar,
        Rectangle update,Rectangle[] obstacles,int contentHeight,int upwardOffset,
        out Rectangle result,out string conflict)
    {
        result = Rectangle.Empty;
        Rectangle candidate,path;
        if(!TryGeometry(host,scale,avatar,contentHeight,upwardOffset,out candidate,out path,out conflict)) return false;
        int gap=Pixels(8,scale);
        // A command must either reach its requested rectangle or explicitly fail.
        if(TooClose(candidate,update,gap)) { conflict="update"; return false; }
        if(Usable(update) && (long)candidate.Top >= (long)update.Bottom+gap && TooClose(path,update,gap))
        { conflict="update"; return false; }
        if(obstacles!=null)
            foreach(Rectangle obstacle in obstacles)
            {
                if(obstacle==avatar || obstacle==update) continue;
                if(TooClose(path,obstacle,gap)) { conflict="navigation"; return false; }
            }
        result=candidate; conflict=null; return true;
    }
    // Tracking may temporarily avoid a newly appearing update control. The
    // requested DIP preference is owned by the caller and is never rewritten.
    internal static bool TryResolve(Rectangle host,double scale,Rectangle avatar,
        Rectangle update,Rectangle[] obstacles,int contentHeight,int upwardOffset,
        out Rectangle effective,out string conflict)
    {
        if(TryPlace(host,scale,avatar,update,obstacles,contentHeight,upwardOffset,out effective,out conflict)) return true;
        if(conflict!="update") return false;
        long baselineBottom=(long)avatar.Top-Pixels(InitialAvatarGapDip,scale);
        long safeBottom=(long)update.Top-Pixels(8,scale);
        long safeOffset=baselineBottom-safeBottom;
        if(safeOffset<Int32.MinValue || safeOffset>Int32.MaxValue) return false;
        string safeConflict;
        if(!TryPlace(host,scale,avatar,update,obstacles,contentHeight,(int)safeOffset,out effective,out safeConflict))
        { effective=Rectangle.Empty; return false; }
        // Success with an update conflict means effective differs from requested.
        conflict="update"; return true;
    }
    static bool TryGeometry(Rectangle host,double scale,Rectangle avatar,int contentHeight,
        int upwardOffset,out Rectangle candidate,out Rectangle path,out string conflict)
    {
        candidate=path=Rectangle.Empty; conflict="invalid-geometry";
        if (!Usable(host) || !Usable(avatar) || !host.Contains(avatar) ||
            contentHeight <= 0 || Double.IsNaN(scale) || Double.IsInfinity(scale) ||
            scale <= 0 || scale * 60 > Int32.MaxValue) return false;

        int idealWidth = Pixels(48, scale);
        int gap = Pixels(8, scale);
        int initialGap=Pixels(InitialAvatarGapDip,scale);
        int minimumGap=Pixels(MinimumAvatarGapDip,scale);
        if (idealWidth<=0 || initialGap<=0 || minimumGap<=0) return false;

        double center = avatar.Left + avatar.Width / 2.0;
        double railLeft = Math.Max(host.Left, center - 30 * scale);
        double railRight = Math.Min(host.Right, center + 30 * scale);
        // If a rail edge is clipped, preserve the avatar center and narrow the bar.
        int width = Math.Min(idealWidth, (int)Math.Floor(
            2 * Math.Min(center - railLeft, railRight - center)));
        if (width <= 0) return false;
        // Round halves consistently across zero so moving to a left-hand monitor
        // cannot change the bar's alignment by one pixel.
        int left = (int)Math.Floor(center - width / 2.0 + .5);
        if (left < railLeft || (double)left + width > railRight) return false;

        long baselineBottom=(long)avatar.Top-initialGap;
        long safeBottom=(long)avatar.Top-minimumGap;
        long bottom = baselineBottom - upwardOffset;
        if (bottom > safeBottom) { conflict="minimum-avatar-gap"; return false; }
        long top = bottom - contentHeight;
        if (top < host.Top || bottom > host.Bottom || top < Int32.MinValue ||
            bottom > Int32.MaxValue) { conflict="target-space"; return false; }

        candidate=new Rectangle(left,(int)top,width,contentHeight);
        if(!host.Contains(candidate)) { conflict="target-space"; return false; }
        if(TooClose(candidate,avatar,gap)) { conflict="avatar"; return false; }

        // Check the whole movement path, not just the destination: an offset must
        // never jump across an intervening navigation button into another gap.
        // A known update is validated separately; moving above a newly appearing
        // update remains possible even if the reference baseline is now blocked.
        long pathTop = Math.Min(top,baselineBottom-contentHeight);
        long pathBottom = Math.Max(bottom,baselineBottom);
        long pathHeight = pathBottom - pathTop;
        if (pathHeight <= 0 || pathHeight > Int32.MaxValue) return false;
        if(pathTop < Int32.MinValue || pathTop > Int32.MaxValue) return false;
        path=new Rectangle(left,(int)pathTop,width,(int)pathHeight);
        conflict=null;
        return true;
    }

    static bool Usable(Rectangle bounds) { return bounds.Width > 0 && bounds.Height > 0; }
    static int Pixels(double dip, double scale)
    {
        return (int)Math.Round(dip * scale, MidpointRounding.AwayFromZero);
    }
    static bool TooClose(Rectangle area, Rectangle obstacle, int gap)
    {
        if (!Usable(obstacle)) return false;
        return (long)area.Left < (long)obstacle.Left + obstacle.Width &&
            (long)area.Left + area.Width > obstacle.Left &&
            (long)area.Top < (long)obstacle.Top + obstacle.Height + gap &&
            (long)area.Top + area.Height > (long)obstacle.Top - gap;
    }

    // Independent, deterministic checks; no windows, account data or UI access.
    // Returns the number of failed checks. The caller chooses the report path.
    internal static int RunChecks(string path)
    {
        var lines = new List<string>();
        int failed = 0;
        Action<string, bool> check = delegate(string name, bool ok)
        {
            lines.Add((ok ? "PASS " : "FAIL ") + name);
            if (!ok) failed++;
        };
        foreach (double scale in new double[] { 1,1.25,1.5,2,104.0/96,106.0/96 })
        {
            string tag = "scale=" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + " ";
            Func<int, int> px = delegate(int dip) { return Pixels(dip, scale); };
            Rectangle host = new Rectangle(0, 0, px(1000), px(900));
            Rectangle avatar = new Rectangle(px(8), px(800), px(44), px(40));
            Rectangle update=new Rectangle(avatar.Left,avatar.Top-px(44),avatar.Width,px(36));
            Rectangle baseline,shifted,effective,negative;
            string conflict;
            bool placed=TryPlace(host,scale,avatar,Rectangle.Empty,null,px(60),0,out baseline,out conflict);
            check(tag + "fits fixed rail without update", placed && host.Contains(baseline));
            check(tag + "width and center follow avatar", placed && baseline.Width == px(48) &&
                Math.Abs(baseline.Left + baseline.Width / 2.0 - avatar.Left - avatar.Width / 2.0) <= .5);
            check(tag + "zero DIP offset has zero physical conversion",UpwardOffsetPixels(0,scale)==0 && conflict==null);
            int[] referenceGaps=new int[] { 64,54,44,34,24 };
            for(int step=0;step<referenceGaps.Length;step++)
            {
                int offset=step*8;
                int expectedGap=(int)Math.Round(referenceGaps[step]*scale/1.25,MidpointRounding.AwayFromZero);
                bool ok=TryPlace(host,scale,avatar,Rectangle.Empty,null,px(60),UpwardOffsetPixels(offset,scale),out shifted,out conflict);
                check(tag+"down "+offset+" DIP reaches exact requested gap",ok && conflict==null && avatar.Top-shifted.Bottom==expectedGap);
                check(tag+"down "+offset+" DIP retains safe contained rectangle",ok && host.Contains(shifted) && !shifted.IntersectsWith(avatar));
            }
            check(tag+"tracking exact placement has no conflict",TryResolve(host,scale,avatar,Rectangle.Empty,null,px(60),0,out effective,out conflict) && effective==baseline && conflict==null);
            check(tag+"fifth down step explicitly rejects avatar minimum",!TryPlace(host,scale,avatar,Rectangle.Empty,null,px(60),UpwardOffsetPixels(40,scale),out shifted,out conflict) && shifted.IsEmpty && conflict=="minimum-avatar-gap");
            check(tag+"raw pixel offset is not scaled twice",TryPlace(host,scale,avatar,Rectangle.Empty,null,px(60),17,out shifted,out conflict) && shifted.Top==baseline.Top-17);
            check(tag+"actual update can conflict even with initial reference",!TryPlace(host,scale,avatar,update,null,px(60),0,out shifted,out conflict) && shifted.IsEmpty && conflict=="update");
            check(tag+"actual update explicitly rejects lowest command",!TryPlace(host,scale,avatar,update,null,px(60),UpwardOffsetPixels(32,scale),out shifted,out conflict) && conflict=="update");
            bool resolved=TryResolve(host,scale,avatar,update,new Rectangle[] { avatar,update },px(60),0,out effective,out conflict);
            check(tag+"initial update conflict yields nearest safe edge",resolved && conflict=="update" && effective.Bottom==update.Top-px(8) && effective!=baseline);
            Rectangle initialAvoidance=effective;
            check(tag+"stored lowest offset resolves same safe edge without rewriting request",TryResolve(host,scale,avatar,update,new Rectangle[] { avatar,update },px(60),UpwardOffsetPixels(32,scale),out effective,out conflict) && conflict=="update" && effective==initialAvoidance);
            check(tag+"update disappearance restores requested lowest gap",TryResolve(host,scale,avatar,Rectangle.Empty,null,px(60),UpwardOffsetPixels(32,scale),out effective,out conflict) && conflict==null && avatar.Top-effective.Bottom==(int)Math.Round(24*scale/1.25,MidpointRounding.AwayFromZero));
            Rectangle adjacentUpdate=update; adjacentUpdate.Offset(px(80),0);
            check(tag+"nonoverlapping update does not shift or block rail",TryPlace(host,scale,avatar,adjacentUpdate,null,px(60),0,out shifted,out conflict) && shifted==baseline && conflict==null);
            Rectangle below=new Rectangle(avatar.Left,baseline.Bottom+px(8),avatar.Width,px(10));
            check(tag + "down path cannot cross native navigation", TryPlace(host,scale,avatar,
                Rectangle.Empty,new Rectangle[] { below },px(60),0,out shifted) &&
                !TryPlace(host,scale,avatar,Rectangle.Empty,new Rectangle[] { below },px(60),UpwardOffsetPixels(8,scale),out shifted,out conflict) && conflict=="navigation");
            Rectangle navigation = new Rectangle(avatar.Left, baseline.Top + px(10), avatar.Width, px(30));
            check(tag + "intersecting navigation explicitly rejects", !TryPlace(host,scale,avatar,Rectangle.Empty,
                new Rectangle[] { navigation },px(60),0,out shifted,out conflict) && shifted.IsEmpty && conflict=="navigation");
            Rectangle crossed = new Rectangle(avatar.Left, baseline.Top - px(35), avatar.Width, px(20));
            check(tag + "offset cannot jump across navigation", !TryPlace(host,scale,avatar,Rectangle.Empty,
                new Rectangle[] { crossed },px(60),px(130),out shifted,out conflict) && conflict=="navigation");
            Rectangle adjacent = new Rectangle(px(65), baseline.Top, px(200), px(60));
            check(tag + "adjacent sidebar content does not block rail", TryPlace(host,scale,avatar,Rectangle.Empty,
                new Rectangle[] { adjacent }, px(60), 0, out shifted));
            Rectangle negativeHost=host,negativeAvatar=avatar;
            negativeHost.Offset(-px(1200), -px(300));
            negativeAvatar.Offset(-px(1200), -px(300));
            Rectangle expectedNegative = baseline; expectedNegative.Offset(-px(1200), -px(300));
            check(tag + "negative screen coordinates preserve placement", TryPlace(negativeHost,scale,
                negativeAvatar,Rectangle.Empty,null,px(60),0,out negative) && negative==expectedNegative);
            check(tag + "large upward offset cannot leave window", !TryPlace(host, scale, avatar,
                Rectangle.Empty,null,px(60),px(900),out shifted,out conflict) && conflict=="target-space");
            Rectangle smallHost=new Rectangle(0,0,px(1000),px(160));
            Rectangle smallAvatar=new Rectangle(px(8),px(100),px(44),px(40));
            Rectangle smallUpdate=new Rectangle(px(8),px(20),px(44),px(50));
            check(tag+"update with no safe space cannot yield a fake rectangle",!TryResolve(smallHost,scale,smallAvatar,smallUpdate,null,px(60),UpwardOffsetPixels(32,scale),out effective,out conflict) && effective.IsEmpty && conflict=="update");
            Rectangle thinUpdate=new Rectangle(avatar.Left,avatar.Top-px(40),avatar.Width,px(1));
            check(tag+"down command cannot jump a thin update",!TryPlace(host,scale,avatar,thinUpdate,null,px(1),UpwardOffsetPixels(32,scale),out shifted,out conflict) && conflict=="update");
        }
        Rectangle answer;
        Rectangle normalHost = new Rectangle(0, 0, 1000, 900);
        Rectangle normalAvatar = new Rectangle(8, 800, 44, 40);
        check("missing avatar hides quota", !TryPlace(normalHost, 1, Rectangle.Empty,
            Rectangle.Empty, null, 60, 0, out answer));
        check("avatar outside host hides quota", !TryPlace(normalHost, 1, new Rectangle(-20, 800, 44, 40),
            Rectangle.Empty, null, 60, 0, out answer));
        check("empty host hides quota", !TryPlace(Rectangle.Empty, 1, normalAvatar,
            Rectangle.Empty, null, 60, 0, out answer));
        check("invalid scale hides quota", !TryPlace(normalHost, Double.NaN, normalAvatar,
            Rectangle.Empty, null, 60, 0, out answer) && !TryPlace(normalHost, 0, normalAvatar,
            Rectangle.Empty, null, 60, 0, out answer));
        check("empty content hides quota", !TryPlace(normalHost, 1, normalAvatar,
            Rectangle.Empty, null, 0, 0, out answer));
        lines.Add("Checks=" + (lines.Count) + " Failed=" + failed);
        File.WriteAllLines(path, lines.ToArray());
        return failed;
    }
}
