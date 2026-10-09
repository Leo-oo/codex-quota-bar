using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// The selected option-2 artwork in shared/icons/quota-mark-black.svg and
// shared/icons/quota-mark-white.svg is embedded as ICOs at build time.
// Standard tray sizes decode their own DIB frames. No client artwork is loaded
// or extra corner badge is drawn at runtime.
internal static class NativeTrayAssets
{
    internal const string DarkResource="QuotaBar.Tray.Dark.ico";
    internal const string LightResource="QuotaBar.Tray.Light.ico";
    static readonly int[] sourceSizes={16,18,20,22,24,32,40,48,64,256};
    static readonly byte[] dark=ReadResource(DarkResource),light=ReadResource(LightResource);
    static byte[] ReadResource(string name)
    {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)) {
            if(stream==null) throw new InvalidOperationException("Missing embedded native tray icon: "+name);
            using(var result=new MemoryStream()) { stream.CopyTo(result); return result.ToArray(); }
        }
    }
    internal static int SourceSizeFor(int size)
    {
        if(size<1 || size>256) throw new ArgumentOutOfRangeException("size");
        foreach(int original in sourceSizes) if(original>=size) return original;
        return 256;
    }
    internal static string PolicyFor(int size)
    {
        int original=SourceSizeFor(size);
        return original==size?"Exact original "+size+"px DIB; no interpolation":
            "Nonstandard "+size+"px: one bicubic downsample from original "+original+"px frame";
    }
    internal static byte[] OriginalPixels(int size,bool useDark)
    {
        byte[] data=useDark?dark:light;
        if(data.Length<6 || BitConverter.ToUInt16(data,0)!=0 || BitConverter.ToUInt16(data,2)!=1)
            throw new InvalidDataException("Invalid native tray ICO.");
        int count=BitConverter.ToUInt16(data,4);
        for(int index=0;index<count;index++) {
            int entry=6+16*index;
            if(entry+16>data.Length) throw new InvalidDataException("Truncated native tray directory.");
            int width=data[entry]==0?256:data[entry],height=data[entry+1]==0?256:data[entry+1];
            if(width!=size || height!=size) continue;
            int length=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12);
            int pixels=checked(size*size*4),mask=checked(((size+31)/32)*4*size);
            if(offset<6+16*count || length<40+pixels+mask || offset>data.Length-length ||
                BitConverter.ToInt32(data,offset)!=40 || BitConverter.ToInt32(data,offset+4)!=size ||
                BitConverter.ToInt32(data,offset+8)!=2*size || BitConverter.ToUInt16(data,offset+12)!=1 ||
                BitConverter.ToUInt16(data,offset+14)!=32 || BitConverter.ToInt32(data,offset+16)!=0)
                throw new InvalidDataException("Unsupported native tray DIB.");
            var result=new byte[pixels];
            for(int y=0;y<size;y++) Buffer.BlockCopy(data,offset+40+(size-1-y)*size*4,result,y*size*4,size*4);
            return result;
        }
        throw new InvalidDataException("Native tray frame missing: "+size);
    }
    internal static Bitmap FromPixels(int size,byte[] pixels)
    {
        if(pixels==null || pixels.Length!=size*size*4) throw new ArgumentException("Invalid tray pixel buffer.");
        var result=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        try {
            var locked=result.LockBits(new Rectangle(0,0,size,size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
            try { for(int y=0;y<size;y++) Marshal.Copy(pixels,y*size*4,IntPtr.Add(locked.Scan0,y*locked.Stride),size*4); }
            finally { result.UnlockBits(locked); }
            return result;
        } catch { result.Dispose();throw; }
    }
    internal static byte[] Pixels(Bitmap image)
    {
        var result=new byte[image.Width*image.Height*4];
        var locked=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try { for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(locked.Scan0,y*locked.Stride),result,y*image.Width*4,image.Width*4); }
        finally { image.UnlockBits(locked); }
        return result;
    }
    internal static Bitmap Original(int size,bool useDark)
    {
        int original=SourceSizeFor(size);
        if(original==size) return FromPixels(size,OriginalPixels(size,useDark));
        using(var native=FromPixels(original,OriginalPixels(original,useDark))) {
            var scaled=new Bitmap(size,size,PixelFormat.Format32bppArgb);
            try {
                using(var graphics=Graphics.FromImage(scaled))
                using(var attributes=new ImageAttributes()) {
                    graphics.CompositingMode=CompositingMode.SourceCopy;
                    graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    attributes.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(native,new Rectangle(0,0,size,size),0,0,original,original,GraphicsUnit.Pixel,attributes);
                }
                return scaled;
            } catch { scaled.Dispose();throw; }
        }
    }
}
