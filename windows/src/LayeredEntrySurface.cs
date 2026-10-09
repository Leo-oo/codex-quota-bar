using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Only submits pixels to an already-created WS_EX_LAYERED form. Visibility,
// input routing, clipping and native-popup z-order remain the host's concern.
internal static class LayeredEntrySurface
{
    [StructLayout(LayoutKind.Sequential)] struct PointNative { internal int X,Y;internal PointNative(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential)] struct SizeNative { internal int Width,Height;internal SizeNative(int w,int h){Width=w;Height=h;} }
    [StructLayout(LayoutKind.Sequential)] struct BitmapInfo { internal int Size,Width,Height;internal short Planes,BitCount;internal int Compression,SizeImage,XPels,YPels,ClrUsed,ClrImportant; }
    [StructLayout(LayoutKind.Sequential,Pack=1)] struct Blend { internal byte Operation,Flags,ConstantAlpha,AlphaFormat; }
    [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll",SetLastError=true)]static extern IntPtr CreateDIBSection(IntPtr dc,ref BitmapInfo info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
    [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr window,IntPtr destination,ref PointNative position,ref SizeNative size,IntPtr source,ref PointNative origin,uint key,ref Blend blend,uint flags);
    internal static int LastError {get;private set;}
    internal static byte[] PremultipliedPixels(Bitmap frame)
    {
        if(frame.PixelFormat!=PixelFormat.Format32bppArgb)throw new ArgumentException("Entry frame must be straight 32bpp ARGB.");
        var output=new byte[checked(frame.Width*frame.Height*4)];
        var data=frame.LockBits(new Rectangle(Point.Empty,frame.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try { for(int y=0;y<frame.Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),output,y*frame.Width*4,frame.Width*4); }
        finally {frame.UnlockBits(data);}
        for(int index=0;index<output.Length;index+=4) {
            int alpha=output[index+3];
            for(int color=0;color<3;color++)output[index+color]=(byte)((output[index+color]*alpha+127)/255);
        }
        return output;
    }
    internal static bool Apply(Form form,Bitmap frame)
    {
        LastError=0;
        if(form==null||form.IsDisposed||!form.IsHandleCreated||frame==null){LastError=87;return false;}
        byte[] pixels=PremultipliedPixels(frame);
        IntPtr screen=IntPtr.Zero,memory=IntPtr.Zero,bitmap=IntPtr.Zero,previous=IntPtr.Zero;
        try {
            screen=GetDC(IntPtr.Zero);memory=CreateCompatibleDC(screen);
            if(screen==IntPtr.Zero||memory==IntPtr.Zero){LastError=Marshal.GetLastWin32Error();if(LastError==0)LastError=8;return false;}
            var info=new BitmapInfo {Size=Marshal.SizeOf(typeof(BitmapInfo)),Width=frame.Width,Height=-frame.Height,Planes=1,BitCount=32,SizeImage=pixels.Length};
            IntPtr bits;bitmap=CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);
            if(bitmap==IntPtr.Zero||bits==IntPtr.Zero){LastError=Marshal.GetLastWin32Error();if(LastError==0)LastError=8;return false;}
            Marshal.Copy(pixels,0,bits,pixels.Length);previous=SelectObject(memory,bitmap);
            if(previous==IntPtr.Zero||previous==new IntPtr(-1)){LastError=8;return false;}
            var position=new PointNative(form.Left,form.Top);var size=new SizeNative(frame.Width,frame.Height);var origin=new PointNative(0,0);
            var blend=new Blend {Operation=0,Flags=0,ConstantAlpha=255,AlphaFormat=1};
            bool success=UpdateLayeredWindow(form.Handle,screen,ref position,ref size,memory,ref origin,0,ref blend,2);
            if(!success)LastError=Marshal.GetLastWin32Error();return success;
        } finally {
            if(previous!=IntPtr.Zero&&previous!=new IntPtr(-1)&&memory!=IntPtr.Zero)SelectObject(memory,previous);
            if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);
            if(memory!=IntPtr.Zero)DeleteDC(memory);
            if(screen!=IntPtr.Zero)ReleaseDC(IntPtr.Zero,screen);
        }
    }
}
