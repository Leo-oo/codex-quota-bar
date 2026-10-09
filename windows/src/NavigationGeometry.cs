using System;
using System.Drawing;

// Temporary, pure projection of a confirmed fixed rail for the same host HWND.
// This is estimated geometry: the original Fetched time is deliberately retained.
internal static class NavigationGeometry
{
    internal static bool TryProject(NavigationSnapshot source, Rectangle host,
        double scale, out NavigationSnapshot projected)
    {
        projected = null;
        if (source == null || source.State != 2 || !Usable(source.Host) ||
            !Usable(host) || !Contains(source.Host, source.Avatar) ||
            !ValidScale(source.Scale) || !ValidScale(scale)) return false;
        double ratio = scale / source.Scale;
        if (!ValidScale(ratio)) return false;

        Rectangle avatar, update;
        if (!TryRectangle(source.Avatar, source.Host, host, ratio, true, out avatar) ||
            !Contains(host, avatar)) return false;
        if (!TryRectangle(source.Update, source.Host, host, ratio, true, out update)) return false;

        var input = source.Obstacles ?? new Rectangle[0];
        var obstacles = new Rectangle[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            // Choose the anchor from the confirmed OLD bounds. Rechoosing from
            // a shrinking new host would flip the anchor during a resize.
            long topGap = (long)input[i].Top - source.Host.Top;
            long bottomGap = (long)source.Host.Top + source.Host.Height -
                ((long)input[i].Top + input[i].Height);
            bool bottom = bottomGap < topGap; // A tie is always top anchored.
            if (!TryRectangle(input[i], source.Host, host, ratio, bottom, out obstacles[i])) return false;
        }

        projected = new NavigationSnapshot {
            State = 2, Host = host, Scale = scale, Avatar = avatar, Update = update,
            Obstacles = obstacles, Fetched = source.Fetched,
            // Popup bounds remain actual screen coordinates from their own read.
            // Their separate lifetime/occlusion handling belongs to the caller.
            NativePopupVisible = source.NativePopupVisible,
            NativePopupBounds = source.NativePopupBounds == null ? new Rectangle[0] :
                (Rectangle[])source.NativePopupBounds.Clone(),
            NativePopupAncestors = ClonePaths(source.NativePopupAncestors),
            NativePopupFetched = source.NativePopupFetched
        };
        return true;
    }

    static bool TryRectangle(Rectangle input, Rectangle oldHost, Rectangle host,
        double ratio, bool bottom, out Rectangle output)
    {
        output = Rectangle.Empty;
        if (input == Rectangle.Empty) return true;
        if (!Usable(input)) return false;
        long width, height, leftGap, verticalGap;
        if (!TryRound(input.Width * ratio, out width) || width <= 0 ||
            !TryRound(input.Height * ratio, out height) || height <= 0 ||
            !TryRound(((long)input.Left - oldHost.Left) * ratio, out leftGap)) return false;
        double oldGap = bottom ?
            ((long)oldHost.Top + oldHost.Height - ((long)input.Top + input.Height)) :
            ((long)input.Top - oldHost.Top);
        if (!TryRound(oldGap * ratio, out verticalGap)) return false;
        long left = (long)host.Left + leftGap;
        long top = bottom ? (long)host.Top + host.Height - verticalGap - height :
            (long)host.Top + verticalGap;
        if (width > Int32.MaxValue || height > Int32.MaxValue ||
            left < Int32.MinValue || top < Int32.MinValue ||
            left + width > Int32.MaxValue || top + height > Int32.MaxValue) return false;
        output = new Rectangle((int)left, (int)top, (int)width, (int)height);
        return true;
    }

    static bool TryRound(double value, out long rounded)
    {
        rounded = 0;
        if (Double.IsNaN(value) || Double.IsInfinity(value)) return false;
        double result = Math.Round(value, MidpointRounding.AwayFromZero);
        // All eventual offsets and extents must fit a physical screen rectangle.
        if (result < Int32.MinValue || result > Int32.MaxValue) return false;
        rounded = (long)result;
        return true;
    }

    static bool ValidScale(double scale)
    { return scale > 0 && !Double.IsNaN(scale) && !Double.IsInfinity(scale); }

    static bool Usable(Rectangle rectangle)
    {
        return rectangle.Width > 0 && rectangle.Height > 0 &&
            (long)rectangle.Left + rectangle.Width <= Int32.MaxValue &&
            (long)rectangle.Top + rectangle.Height <= Int32.MaxValue;
    }

    static bool Contains(Rectangle outer, Rectangle inner)
    {
        return Usable(outer) && Usable(inner) && inner.Left >= outer.Left &&
            inner.Top >= outer.Top &&
            (long)inner.Left + inner.Width <= (long)outer.Left + outer.Width &&
            (long)inner.Top + inner.Height <= (long)outer.Top + outer.Height;
    }

    static IntPtr[][] ClonePaths(IntPtr[][] paths)
    {
        if (paths == null) return new IntPtr[0][];
        var copy = new IntPtr[paths.Length][];
        for (int i = 0; i < paths.Length; i++)
            copy[i] = paths[i] == null ? null : (IntPtr[])paths[i].Clone();
        return copy;
    }
}
