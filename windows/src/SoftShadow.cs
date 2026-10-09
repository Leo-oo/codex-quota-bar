using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// One alpha layer behind the popup. It never owns the content or receives desktop input.
internal sealed class PopupShadow : IDisposable
{
    readonly Control popup;
    readonly ShadowWindow layer=new ShadowWindow();
    readonly PopupTracker tracker;
    double scale;
    float radius;
    bool disposed,updating;
    Size renderedSize;
    double renderedScale;
    float renderedRadius;
    Bitmap bitmap;
    Rectangle uploadedBounds;
    Point uploadedSource;
    bool uploadDirty=true;
    internal bool IsLayer(IntPtr handle) { return !disposed && layer.IsHandleCreated && layer.Handle==handle; }
    internal static bool IsShadowWindow(IntPtr handle) { return Control.FromHandle(handle) is ShadowWindow; }
    internal static PopupShadow Attach(Control popup,double scale,float radius)
    {
        if(popup==null) throw new ArgumentNullException("popup");
        return new PopupShadow(popup,scale,radius);
    }
    PopupShadow(Control value,double dpi,float corner)
    {
        popup=value; scale=ValidScale(dpi); radius=Math.Max(0,corner);
        tracker=new PopupTracker(this);
        popup.VisibleChanged+=Changed; popup.LocationChanged+=Changed; popup.SizeChanged+=Changed;
        popup.HandleCreated+=HandleCreated; popup.HandleDestroyed+=HandleDestroyed; popup.Disposed+=ContentDisposed;
        if(popup.IsHandleCreated) tracker.Attach(popup.Handle);
    }
    static double ValidScale(double value)
    { return Double.IsNaN(value)||Double.IsInfinity(value)||value<=0?1:value; }
    internal void UpdatePlacement(double dpi,float corner)
    {
        scale=ValidScale(dpi); radius=Math.Max(0,corner); UpdatePlacement();
    }
    internal void UpdatePlacement()
    {
        if(disposed || updating) return;
        if(popup.IsDisposed || !popup.IsHandleCreated || !popup.Visible || !ShadowNative.IsWindowVisible(popup.Handle) || popup.Width<1 || popup.Height<1) { Hide(); return; }
        updating=true;
        try {
            if(bitmap==null || renderedSize!=popup.Size || renderedScale!=scale || renderedRadius!=radius) {
                if(bitmap!=null) bitmap.Dispose();
                bitmap=RenderForPreview(popup.Size,scale,radius);
                renderedSize=popup.Size; renderedScale=scale; renderedRadius=radius;
                uploadDirty=true;
            }
            int pad=PaddingFor(scale);
            Rectangle contentScreen=popup.RectangleToScreen(popup.ClientRectangle);
            Rectangle frame=new Rectangle(contentScreen.Left-pad,contentScreen.Top-pad,bitmap.Width,bitmap.Height);
            Rectangle work=Screen.FromControl(popup).WorkingArea;
            Rectangle visible=Rectangle.Intersect(frame,work);
            if(visible.Width<1 || visible.Height<1) { Hide(); return; }
            Point source=new Point(visible.Left-frame.Left,visible.Top-frame.Top);
            var form=popup as Form;
            Form owner=form==null?null:form.Owner;
            // An owned shadow shares the popup's owner; owning it by the popup would force it above the content.
            if(owner!=null && (owner.IsDisposed || !owner.IsHandleCreated)) owner=null;
            if(layer.Owner!=owner) layer.Owner=owner;
            if(uploadDirty || !layer.IsHandleCreated || uploadedBounds!=visible || uploadedSource!=source) {
                layer.Upload(bitmap,visible,source); uploadedBounds=visible;uploadedSource=source;uploadDirty=false;
            }
            if(!layer.Visible) layer.Show();
            // Explicitly stay out of the topmost band, then place directly behind the popup.
            if(!ShadowNative.SetWindowPos(layer.Handle,new IntPtr(-2),visible.Left,visible.Top,visible.Width,visible.Height,0x10|0x40))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            long style=ShadowNative.GetWindowLongPtr(popup.Handle,-20).ToInt64();
            // Native dropdowns may be topmost. Their shadow remains at the top of the ordinary band.
            IntPtr after=(style&8)!=0?IntPtr.Zero:popup.Handle;
            if(!ShadowNative.SetWindowPos(layer.Handle,after,visible.Left,visible.Top,visible.Width,visible.Height,0x10|0x40))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        catch(Win32Exception) { Hide(); }
        catch(InvalidOperationException) { Hide(); }
        finally { updating=false; }
    }
    internal void Hide() { if(!disposed && layer.Visible) layer.Hide(); }
    void Changed(object sender,EventArgs e) { UpdatePlacement(); }
    void HandleCreated(object sender,EventArgs e) { tracker.Attach(popup.Handle); UpdatePlacement(); }
    void HandleDestroyed(object sender,EventArgs e) { tracker.Detach(); Hide(); }
    void ContentDisposed(object sender,EventArgs e) { Dispose(); }
    internal static int PaddingFor(double dpi) { return Math.Max(1,(int)Math.Ceiling(12*ValidScale(dpi))); }
    internal static Bitmap RenderForPreview(Size content,double dpi,float corner)
    {
        double factor=ValidScale(dpi);
        int pad=PaddingFor(factor),width=checked(content.Width+2*pad),height=checked(content.Height+2*pad);
        var image=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
        var bytes=new byte[checked(width*height*4)];
        double r=Math.Min(Math.Max(0,corner*factor),Math.Min(content.Width,content.Height)/2.0);
        double centerX=pad+content.Width/2.0,centerY=pad+content.Height/2.0+2*factor;
        double halfX=content.Width/2.0-r,halfY=content.Height/2.0-r,sigma=3.5*factor;
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) {
            double dx=Math.Abs(x+.5-centerX)-halfX,dy=Math.Abs(y+.5-centerY)-halfY;
            double outside=Math.Sqrt(Math.Max(dx,0)*Math.Max(dx,0)+Math.Max(dy,0)*Math.Max(dy,0));
            double distance=outside+Math.Min(Math.Max(dx,dy),0)-r;
            // Smooth Gaussian coverage, not nested opaque outlines.
            double z=distance/(sigma*Math.Sqrt(2)),t=1/(1+.3275911*Math.Abs(z));
            double polynomial=((((1.061405429*t-1.453152027)*t)+1.421413741)*t-.284496736)*t+.254829592;
            double erf=1-polynomial*t*Math.Exp(-z*z); if(z<0) erf=-erf;
            int alpha=(int)Math.Round(38*.5*(1-erf));
            bytes[(y*width+x)*4+3]=(byte)Math.Max(0,Math.Min(38,alpha));
        }
        var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try {
            if(data.Stride==width*4) Marshal.Copy(bytes,0,data.Scan0,bytes.Length);
            else for(int y=0;y<height;y++) Marshal.Copy(bytes,y*width*4,IntPtr.Add(data.Scan0,y*data.Stride),width*4);
        } finally { image.UnlockBits(data); }
        return image;
    }
    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        popup.VisibleChanged-=Changed; popup.LocationChanged-=Changed; popup.SizeChanged-=Changed;
        popup.HandleCreated-=HandleCreated; popup.HandleDestroyed-=HandleDestroyed; popup.Disposed-=ContentDisposed;
        tracker.Detach(); layer.Dispose(); if(bitmap!=null) { bitmap.Dispose(); bitmap=null; }
    }
    sealed class PopupTracker : NativeWindow
    {
        readonly PopupShadow owner;
        internal PopupTracker(PopupShadow value) { owner=value; }
        internal void Attach(IntPtr handle) { if(Handle==handle) return; Detach(); AssignHandle(handle); }
        internal void Detach() { if(Handle!=IntPtr.Zero) ReleaseHandle(); }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if(m.Msg==0x18 && m.WParam==IntPtr.Zero) owner.Hide();
            else if(m.Msg==0x47 || m.Msg==0x18) owner.UpdatePlacement();
        }
    }
    sealed class ShadowWindow : Form
    {
        internal ShadowWindow()
        {
            FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; AutoScaleMode=AutoScaleMode.None;
            StartPosition=FormStartPosition.Manual; TopMost=false;
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var p=base.CreateParams; p.ExStyle|=0x80|0x20|0x80000|0x08000000; return p; }
        }
        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x84) { m.Result=new IntPtr(-1); return; }
            if(m.Msg==0x21) { m.Result=new IntPtr(3); return; }
            base.WndProc(ref m);
        }
        internal void Upload(Bitmap image,Rectangle bounds,Point offset)
        {
            IntPtr screen=ShadowNative.GetDC(IntPtr.Zero),memory=IntPtr.Zero,pixels=IntPtr.Zero,previous=IntPtr.Zero;
            try {
                if(screen==IntPtr.Zero) throw new Win32Exception();
                memory=ShadowNative.CreateCompatibleDC(screen);
                if(memory==IntPtr.Zero) throw new Win32Exception();
                pixels=image.GetHbitmap(Color.FromArgb(0));
                previous=ShadowNative.SelectObject(memory,pixels);
                if(previous==IntPtr.Zero || previous==new IntPtr(-1)) { previous=IntPtr.Zero; throw new Win32Exception(Marshal.GetLastWin32Error()); }
                var target=new ShadowNative.POINT(bounds.Left,bounds.Top);
                var size=new ShadowNative.SIZE(bounds.Width,bounds.Height);
                var source=new ShadowNative.POINT(offset.X,offset.Y);
                var blend=new ShadowNative.BLENDFUNCTION { BlendOp=0,BlendFlags=0,SourceConstantAlpha=255,AlphaFormat=1 };
                if(!ShadowNative.UpdateLayeredWindow(Handle,screen,ref target,ref size,memory,ref source,0,ref blend,2))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally {
                if(previous!=IntPtr.Zero && memory!=IntPtr.Zero) ShadowNative.SelectObject(memory,previous);
                if(pixels!=IntPtr.Zero) ShadowNative.DeleteObject(pixels);
                if(memory!=IntPtr.Zero) ShadowNative.DeleteDC(memory);
                if(screen!=IntPtr.Zero) ShadowNative.ReleaseDC(IntPtr.Zero,screen);
            }
        }
    }
    static class ShadowNative
    {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { internal int X,Y; internal POINT(int x,int y) { X=x;Y=y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE { internal int X,Y; internal SIZE(int x,int y) { X=x;Y=y; } }
        [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct BLENDFUNCTION { internal byte BlendOp,BlendFlags,SourceConstantAlpha,AlphaFormat; }
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd,IntPtr hdc,ref POINT target,ref SIZE size,IntPtr source,ref POINT origin,int key,ref BLENDFUNCTION blend,int flags);
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd,IntPtr hdc);
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,int flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd,int index);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("gdi32.dll",SetLastError=true)] internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr hdc,IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr hdc);
    }
}
