using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

// Explicit fixture Main only. It reads checked-in icon inputs and writes own
// evidence. It creates no app window, UsageBar, clients, hooks or global input.
internal static class Icon119Probe
{
    public sealed class ThemeInput {
        public bool Dark {get;set;}
        public string Svg {get;set;}
        public string Ico {get;set;}
        public Dictionary<string,string> Pngs {get;set;}
    }
    public sealed class Inputs {public ThemeInput[] Themes {get;set;} public string ProgramIco {get;set;}}
    sealed class CheckResult {public string id;public bool passed;public object details;}
    sealed class Frame {internal int Size;internal byte[] Pixels;}
    static readonly int[] Frames={16,18,20,22,24,32,40,48,64,256};
    static readonly int[] NativeSizes={16,18,20,22,24,32,40,48,64};
    static readonly List<CheckResult> checks=new List<CheckResult>();
    static int fail;
    static string repo,output;
    static void Check(string id,bool passed,object details=null) {checks.Add(new CheckResult{id=id,passed=passed,details=details});if(!passed)fail++;}
    static void Require(bool value,string message) {if(!value)throw new InvalidDataException(message);}
    static string FileInRepo(string relative) {
        Require(!Path.IsPathRooted(relative),"Relative icon path required");
        string full=Path.GetFullPath(Path.Combine(repo,relative.Replace('/',Path.DirectorySeparatorChar)));
        Require(full.StartsWith(repo+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Icon must remain in repository");
        return full;
    }
    static byte[] Hash(byte[] bytes) {using(var sha=SHA256.Create())return sha.ComputeHash(bytes);}
    static bool Same(byte[] a,byte[] b) {if(a.Length!=b.Length)return false;for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
    static byte[] BitmapPixels(Bitmap bitmap) {
        var bytes=new byte[bitmap.Width*bitmap.Height*4];
        var locked=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        try {for(int y=0;y<bitmap.Height;y++)Marshal.Copy(IntPtr.Add(locked.Scan0,y*locked.Stride),bytes,y*bitmap.Width*4,bitmap.Width*4);}
        finally {bitmap.UnlockBits(locked);}return bytes;
    }
    static Bitmap BitmapFrom(int size,byte[] bytes) {
        var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        var locked=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);
        try {for(int y=0;y<size;y++)Marshal.Copy(bytes,y*size*4,IntPtr.Add(locked.Scan0,y*locked.Stride),size*4);}
        finally {bitmap.UnlockBits(locked);}return bitmap;
    }
    static Dictionary<int,Frame> DecodeIco(byte[] data) {
        Require(data.Length>=6&&BitConverter.ToUInt16(data,0)==0&&BitConverter.ToUInt16(data,2)==1,"Classic ICO header");
        int count=BitConverter.ToUInt16(data,4);Require(count==Frames.Length&&data.Length>=6+count*16,"Ten ICO entries");
        var found=new Dictionary<int,Frame>();var intervals=new List<int[]>();
        for(int i=0;i<count;i++) {
            int entry=6+i*16,size=data[entry]==0?256:data[entry],height=data[entry+1]==0?256:data[entry+1];
            Require(size==height&&Array.IndexOf(Frames,size)>=0&&!found.ContainsKey(size),"Unique expected square frame");
            int length=BitConverter.ToInt32(data,entry+8),offset=BitConverter.ToInt32(data,entry+12),stride=((size+31)/32)*4,pixels=size*size*4;
            Require(data[entry+2]==0&&data[entry+3]==0&&BitConverter.ToUInt16(data,entry+4)==1&&BitConverter.ToUInt16(data,entry+6)==32,"32bpp ICO entry");
            Require(offset>=6+count*16&&length==40+pixels+stride*size&&offset<=data.Length-length,"Exact DIB bounds");
            foreach(var interval in intervals)Require(offset>=interval[1]||offset+length<=interval[0],"Nonoverlapping frames");
            intervals.Add(new[]{offset,offset+length});
            Require(BitConverter.ToInt32(data,offset)==40&&BitConverter.ToInt32(data,offset+4)==size&&BitConverter.ToInt32(data,offset+8)==2*size&&
                BitConverter.ToUInt16(data,offset+12)==1&&BitConverter.ToUInt16(data,offset+14)==32&&BitConverter.ToInt32(data,offset+16)==0,"Classic uncompressed 32bpp DIB");
            byte[] decoded=new byte[pixels];
            for(int y=0;y<size;y++) {
                int row=offset+40+(size-1-y)*size*4;Buffer.BlockCopy(data,row,decoded,y*size*4,size*4);
                int mask=offset+40+pixels+(size-1-y)*stride;
                for(int x=0;x<size;x++)Require(((data[mask+x/8]>>(7-x%8))&1)==(decoded[(y*size+x)*4+3]==0?1:0),"AND mask exactly follows alpha zero");
                for(int x=size;x<stride*8;x++)Require(((data[mask+x/8]>>(7-x%8))&1)==0,"AND padding stays zero");
            }
            found.Add(size,new Frame{Size=size,Pixels=decoded});
        }
        foreach(int size in Frames)Require(found.ContainsKey(size),"Required ICO frame");return found;
    }
    static uint U32(byte[] bytes,int start) {return (uint)(bytes[start]<<24|bytes[start+1]<<16|bytes[start+2]<<8|bytes[start+3]);}
    static uint Crc(byte[] bytes,int start,int length) {
        uint crc=0xffffffff;for(int i=start;i<start+length;i++){crc^=bytes[i];for(int bit=0;bit<8;bit++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}return crc^0xffffffff;
    }
    static int Paeth(int left,int up,int corner) {int p=left+up-corner,a=Math.Abs(p-left),b=Math.Abs(p-up),c=Math.Abs(p-corner);return a<=b&&a<=c?left:b<=c?up:corner;}
    static byte[] DecodePng(string path,int expected) {
        byte[] png=File.ReadAllBytes(path),signature={137,80,78,71,13,10,26,10};Require(png.Length>=8,"PNG header");
        for(int i=0;i<8;i++)Require(png[i]==signature[i],"PNG signature");
        int width=0,height=0;bool end=false;
        using(var idat=new MemoryStream()) {
            for(int cursor=8;cursor<png.Length;) {
                Require(cursor<=png.Length-12,"PNG chunk");int length=checked((int)U32(png,cursor));
                Require(length>=0&&cursor<=png.Length-12-length,"PNG chunk bounds");
                string type=System.Text.Encoding.ASCII.GetString(png,cursor+4,4);
                Require(Crc(png,cursor+4,length+4)==U32(png,cursor+8+length),"PNG CRC");
                if(type=="IHDR") {
                    Require(length==13,"PNG IHDR");width=checked((int)U32(png,cursor+8));height=checked((int)U32(png,cursor+12));
                    Require(width==expected&&height==expected&&png[cursor+16]==8&&png[cursor+17]==6&&png[cursor+18]==0&&png[cursor+19]==0&&png[cursor+20]==0,"Square RGBA8 noninterlaced PNG");
                } else if(type=="IDAT")idat.Write(png,cursor+8,length);
                else if(type=="IEND"){Require(length==0,"PNG IEND");end=true;Require(cursor+12==png.Length,"No PNG trailing bytes");}
                cursor+=length+12;
            }
            Require(end&&width==expected&&height==expected,"Complete PNG");byte[] zlib=idat.ToArray();
            Require(zlib.Length>=6&&(zlib[0]&15)==8&&(zlib[0]*256+zlib[1])%31==0&&(zlib[1]&32)==0,"PNG zlib header");
            int stride=width*4;byte[] filtered=new byte[(stride+1)*height];int count=0;
            using(var compressed=new MemoryStream(zlib,2,zlib.Length-6))
            using(var deflate=new DeflateStream(compressed,CompressionMode.Decompress)) {
                while(count<filtered.Length){int got=deflate.Read(filtered,count,filtered.Length-count);if(got==0)break;count+=got;}
                Require(count==filtered.Length&&deflate.ReadByte()==-1,"Exact PNG decompressed length");
            }
            uint a=1,b=0;foreach(byte value in filtered){a=(a+value)%65521;b=(b+a)%65521;}Require((b<<16|a)==U32(zlib,zlib.Length-4),"PNG Adler32");
            var raw=new byte[width*height*4];
            for(int y=0;y<height;y++) {
                int kind=filtered[y*(stride+1)];Require(kind<=4,"PNG filter type");
                for(int x=0;x<stride;x++) {
                    int left=x>=4?raw[y*stride+x-4]:0,up=y>0?raw[(y-1)*stride+x]:0,corner=y>0&&x>=4?raw[(y-1)*stride+x-4]:0;
                    int delta=kind==0?0:kind==1?left:kind==2?up:kind==3?(left+up)/2:Paeth(left,up,corner);
                    raw[y*stride+x]=(byte)(filtered[y*(stride+1)+x+1]+delta);
                }
            }
            for(int i=0;i<raw.Length;i+=4){byte red=raw[i];raw[i]=raw[i+2];raw[i+2]=red;}return raw;
        }
    }
    static bool PaintedAndTransparent(byte[] bytes) {int painted=0,empty=0,partial=0;for(int i=3;i<bytes.Length;i+=4){if(bytes[i]==0)empty++;else painted++;if(bytes[i]>0&&bytes[i]<255)partial++;}return painted>0&&empty>0&&partial>0;}
    static bool Ink(byte[] bytes,int value) {for(int i=0;i<bytes.Length;i+=4)if(bytes[i+3]>0&&(bytes[i]!=value||bytes[i+1]!=value||bytes[i+2]!=value))return false;return true;}
    static string Attr(XmlNode node,string name) {for(var at=node;at!=null&&at.NodeType==XmlNodeType.Element;at=at.ParentNode){var attr=at.Attributes[name];if(attr!=null)return attr.Value;}return "";}
    static bool SvgColor(string value,bool dark) {value=value.Replace(" ","").ToLowerInvariant();return value=="none"||value==""||(dark?value=="white"||value=="#fff"||value=="#ffffff"||value=="rgb(255,255,255)":value=="black"||value=="#000"||value=="#000000"||value=="rgb(0,0,0)");}
    static void Svg(ThemeInput theme,string name) {
        var doc=new XmlDocument{XmlResolver=null};
        using(var reader=XmlReader.Create(FileInRepo(theme.Svg),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(reader);
        var root=doc.DocumentElement;var paths=doc.SelectNodes("//*[local-name()='path']");var circles=doc.SelectNodes("//*[local-name()='circle']");
        string[] box=(root.GetAttribute("viewBox")??"").Split(new[]{' ',',','\t'},StringSplitOptions.RemoveEmptyEntries);
        Check(name+" SVG square viewbox",root.LocalName=="svg"&&box.Length==4&&box[2]==box[3]);
        Check(name+" SVG exactly one arc needle and filled hub",paths.Count==2&&circles.Count==1&&doc.SelectNodes("//*[local-name()='ellipse' or local-name()='rect' or local-name()='polygon' or local-name()='polyline' or local-name()='line']").Count==0);
        XmlNode arc=null,needle=null;foreach(XmlNode path in paths){string d=Attr(path,"d");if(Regex.Matches(d,"[Aa]").Count==1)arc=path;if(Regex.Matches(d,"[Ll]").Count==1)needle=path;}
        var numbers=needle==null?new Match[0]:Matches(Regex.Matches(Attr(needle,"d"),@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?"));
        bool direction=numbers.Length==4&&Number(numbers[2].Value)>Number(numbers[0].Value)&&Number(numbers[3].Value)<Number(numbers[1].Value)&&
            Math.Abs((Number(numbers[2].Value)-Number(numbers[0].Value))+(Number(numbers[3].Value)-Number(numbers[1].Value)))<.05;
        Check(name+" SVG single open arc and upper-right 45 degree needle",arc!=null&&needle!=null&&arc!=needle&&direction);
        Check(name+" SVG round caps and eight-unit strokes",arc!=null&&needle!=null&&Attr(arc,"stroke-linecap")=="round"&&Attr(needle,"stroke-linecap")=="round"&&Attr(arc,"stroke-width")=="8"&&Attr(needle,"stroke-width")=="8");
        var hub=circles.Count==1?circles[0]:null;
        Check(name+" SVG solid eight-unit hub",hub!=null&&Attr(hub,"r")=="8"&&Attr(hub,"fill")!=""&&Attr(hub,"fill")!="none"&&SvgColor(Attr(hub,"fill"),theme.Dark));
        bool colors=true;foreach(XmlNode node in doc.SelectNodes("//*"))foreach(XmlAttribute attr in node.Attributes)if(attr.LocalName=="stroke"||attr.LocalName=="fill")colors&=SvgColor(attr.Value,theme.Dark);
        Check(name+" SVG only intended ink and no external active content",colors&&doc.SelectNodes("//*[local-name()='script' or local-name()='image' or local-name()='foreignObject' or local-name()='filter']|//@*[local-name()='href']").Count==0);
    }
    static Match[] Matches(MatchCollection values) {var result=new Match[values.Count];values.CopyTo(result,0);return result;}
    static double Number(string value) {return Double.Parse(value,CultureInfo.InvariantCulture);}
    [StructLayout(LayoutKind.Sequential)]struct IconInfo{internal bool icon;internal uint x,y;internal IntPtr mask,color;}
    [StructLayout(LayoutKind.Sequential)]struct GdiBitmap{internal int type,width,height,widthBytes;internal ushort planes,bits;internal IntPtr data;}
    [DllImport("user32.dll",SetLastError=true)]static extern bool GetIconInfo(IntPtr icon,out IconInfo info);
    [DllImport("gdi32.dll")]static extern int GetObject(IntPtr obj,int length,out GdiBitmap bitmap);
    [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
    static bool FreeInfo(IconInfo info) {bool ok=true;if(info.color!=IntPtr.Zero)ok&=DeleteObject(info.color);if(info.mask!=IntPtr.Zero)ok&=DeleteObject(info.mask);return ok;}
    static void NativeIcon(int size,bool dark,Frame expected,string name) {
        IntPtr owned=IntPtr.Zero;bool free=false;
        using(var icon=ToolIcon.Create(dark,size)) {
            owned=icon.Handle;Check(name+" HICON selected requested size",icon.Width==size&&icon.Height==size);
            IconInfo info;bool got=GetIconInfo(owned,out info);GdiBitmap bitmap=default(GdiBitmap);
            try {Check(name+" HICON native color bitmap dimensions",got&&info.color!=IntPtr.Zero&&GetObject(info.color,Marshal.SizeOf(typeof(GdiBitmap)),out bitmap)>0&&bitmap.width==size&&bitmap.height==size&&bitmap.bits==32);}
            finally {if(got)free=FreeInfo(info);}
            Check(name+" GetIconInfo temporary bitmaps released",got&&free);
            foreach(bool white in new[]{false,true})using(var actual=new Bitmap(size,size,PixelFormat.Format32bppArgb))
            using(var reference=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(var raw=BitmapFrom(size,expected.Pixels)) {
                Color background=white?Color.White:Color.Black;
                using(var g=Graphics.FromImage(actual)){g.Clear(background);g.DrawIconUnstretched(icon,new Rectangle(0,0,size,size));}
                using(var g=Graphics.FromImage(reference)){g.Clear(background);g.DrawImageUnscaled(raw,0,0);}
                int differences=0;for(int y=0;y<size;y++)for(int x=0;x<size;x++){Color a=actual.GetPixel(x,y),b=reference.GetPixel(x,y);if(Math.Max(Math.Abs(a.R-b.R),Math.Max(Math.Abs(a.G-b.G),Math.Abs(a.B-b.B)))>2)differences++;}
                Check(name+" HICON independent DIB composition "+(white?"white":"black"),differences==0,new{mismatchedPixels=differences});
                actual.Save(Path.Combine(output,name+"-"+(white?"white":"black")+".png"),ImageFormat.Png);
            }
        }
        IconInfo stale;bool alive=GetIconInfo(owned,out stale);if(alive)FreeInfo(stale);
        Check(name+" managed Icon Dispose releases native handle",!alive);
    }
    static Dictionary<int,Frame> Theme(ThemeInput theme) {
        string name=theme.Dark?"dark-white":"light-black";int color=theme.Dark?255:0;
        Svg(theme,name);byte[] encoded=File.ReadAllBytes(FileInRepo(theme.Ico));var frames=DecodeIco(encoded);
        Check(name+" ICO ten classic DIB frames and exact AND masks",frames.Count==10);
        string alias=theme.Dark?"QuotaBar.Tray.Dark.ico":"QuotaBar.Tray.Light.ico";
        byte[] embedded;
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(alias))using(var memory=new MemoryStream()){Require(stream!=null,"Missing tray resource");stream.CopyTo(memory);embedded=memory.ToArray();}
        Check(name+" embedded alias equals checked-in ICO",Same(Hash(encoded),Hash(embedded)));
        foreach(int size in Frames) {
            var wanted=frames[size].Pixels;byte[] png=DecodePng(FileInRepo(theme.Pngs[size.ToString(CultureInfo.InvariantCulture)]),size);
            Check(name+" "+size+"px PNG equals independent ICO RGBA",Same(wanted,png));
            Check(name+" "+size+"px has painted transparent and antialiased pixels",PaintedAndTransparent(wanted));
            Check(name+" "+size+"px exact intended ink RGB",Ink(wanted,color));
            using(var original=NativeTrayAssets.Original(size,theme.Dark))using(var actual=ToolIcon.Draw(size,theme.Dark)) {
                Check(name+" "+size+"px Original equals independent DIB",original.Width==size&&original.Height==size&&Same(BitmapPixels(original),wanted));
                Check(name+" "+size+"px Draw equals Original with no extra gauge",actual.Width==size&&actual.Height==size&&Same(BitmapPixels(actual),BitmapPixels(original))&&Same(BitmapPixels(actual),wanted));
            }
        }
        foreach(int size in new[]{128,512,1024}) {
            byte[] png=DecodePng(FileInRepo(theme.Pngs[size.ToString(CultureInfo.InvariantCulture)]),size);
            Check(name+" "+size+"px shared PNG transparency and antialiasing",PaintedAndTransparent(png));
            Check(name+" "+size+"px shared PNG intended ink RGB",Ink(png,color));
        }
        foreach(int size in NativeSizes)NativeIcon(size,theme.Dark,frames[size],name+"-"+size);
        return frames;
    }
    [STAThread]static int Main(string[] args) {
        if(args.Length!=3)return 2;
        repo=Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);output=Path.GetFullPath(args[2]);Directory.CreateDirectory(output);
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try {
            var inputs=new JavaScriptSerializer().Deserialize<Inputs>(File.ReadAllText(args[1]));
            Require(inputs.Themes.Length==2&&inputs.Themes[0].Dark!=inputs.Themes[1].Dark,"Exactly dark and light input");
            Dictionary<int,Frame> light=null;
            foreach(var theme in inputs.Themes){var result=Theme(theme);if(!theme.Dark)light=result;}
            var app=DecodeIco(File.ReadAllBytes(FileInRepo(inputs.ProgramIco)));
            Check("program ICO ten classic DIB frames and exact AND masks",app.Count==10);
            foreach(int size in Frames)Check("program "+size+"px equals approved black tray frame",Same(app[size].Pixels,light[size].Pixels));
        }catch(Exception error){Check("fixture no exception",false,new{type=error.GetType().FullName,message=error.Message});}
        bool completed=checks.Count==247&&fail==0;
        var report=new{expectedAssertions=247,pass=checks.Count-fail,fail=fail,completed=completed,checks=checks,
            normalProgramMainStarted=false,usageBarCreated=false,networkAccessed=false,accountFilesRead=false,actualRuntimeCacheRead=false,
            userProcessTouched=false,physicalGlobalInputSent=false,ownWindowsShown=false,liveExplorerTrayVerified=false,
            scope="independent SVG/PNG/classic ICO and memory HICON fixture; no live application or tray interaction"};
        File.WriteAllText(Path.Combine(output,"icon119-result.json"),new JavaScriptSerializer{MaxJsonLength=2097152}.Serialize(report));
        return completed?0:1;
    }
}

