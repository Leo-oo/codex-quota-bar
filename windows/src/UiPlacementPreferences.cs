using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Only the caller's own ui-placement.json is read/written. Positive DIP offset
// means downward movement; native-control safety is enforced by RailLayout.
internal sealed class UiPlacementPreferences
{
    internal const int MaximumUpwardOffsetDip = 258;
    internal const int MaximumDownwardOffsetDip = 32;
    internal const int MaximumOffsetDip = MaximumUpwardOffsetDip;
    const int MaximumFileBytes = 4096;
    readonly object sync=new object();
    readonly string path;
    int offsetDip;
    internal UiPlacementPreferences(string path)
    {
        if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("placement path");
        this.path=Path.GetFullPath(path);
    }
    internal string FilePath { get { return path; } }
    internal string LastLoadStatus { get; private set; }
    internal string LastLoadError { get; private set; }
    internal string LastSaveStatus { get; private set; }
    internal string LastSaveError { get; private set; }
    internal int OffsetDip { get { lock(sync) return offsetDip; } }
    internal static int Clamp(long offset)
    { return (int)Math.Max(-MaximumUpwardOffsetDip,Math.Min(MaximumDownwardOffsetDip,offset)); }

    internal int Load()
    {
        lock(sync)
        {
            offsetDip=0;
            LastLoadStatus="not-read"; LastLoadError="";
            try
            {
                if(!File.Exists(path)) { LastLoadStatus="missing"; return offsetDip; }
                string text;
                using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,
                    FileShare.ReadWrite|FileShare.Delete))
                {
                    if(stream.Length<=0 || stream.Length>MaximumFileBytes) { LastLoadStatus="invalid-length"; return offsetDip; }
                    using(var reader=new StreamReader(stream,Encoding.UTF8,true)) text=reader.ReadToEnd();
                }
                var serializer=new JavaScriptSerializer { MaxJsonLength=MaximumFileBytes };
                var values=serializer.DeserializeObject(text) as Dictionary<string,object>;
                object version,value;
                if(values==null || !values.TryGetValue("schemaVersion",out version) ||
                    !(version is int) || (int)version!=1 ||
                    !values.TryGetValue("verticalOffsetDip",out value)) { LastLoadStatus="invalid-schema"; return offsetDip; }
                bool valid=false;
                if(value is int) { offsetDip=Clamp((int)value); valid=true; }
                else if(value is long) { offsetDip=Clamp((long)value); valid=true; }
                else if(value is decimal)
                {
                    decimal number=(decimal)value;
                    if(Decimal.Truncate(number)==number)
                    {
                        offsetDip=number>MaximumDownwardOffsetDip?MaximumDownwardOffsetDip:
                            number < -MaximumUpwardOffsetDip?-MaximumUpwardOffsetDip:(int)number;
                        valid=true;
                    }
                }
                else if(value is double)
                {
                    double number=(double)value;
                    if(!Double.IsNaN(number) && !Double.IsInfinity(number) && Math.Truncate(number)==number)
                    {
                        offsetDip=number>MaximumDownwardOffsetDip?MaximumDownwardOffsetDip:
                            number < -MaximumUpwardOffsetDip?-MaximumUpwardOffsetDip:(int)number;
                        valid=true;
                    }
                }
                LastLoadStatus=valid?"loaded":"invalid-offset";
                return offsetDip;
            }
            catch(IOException e) { LoadFailed(e); return offsetDip; }
            catch(UnauthorizedAccessException e) { LoadFailed(e); return offsetDip; }
            catch(System.Security.SecurityException e) { LoadFailed(e); return offsetDip; }
            catch(ArgumentException e) { LoadFailed(e); return offsetDip; }
            catch(InvalidOperationException e) { LoadFailed(e); return offsetDip; }
        }
    }
    void LoadFailed(Exception error) { LastLoadStatus="error"; LastLoadError=error.GetType().Name+":0x"+error.HResult.ToString("X8"); }
    void SaveFailed(Exception error) { LastSaveStatus="error"; LastSaveError=error.GetType().Name+":0x"+error.HResult.ToString("X8"); }

    internal bool Save(int offset)
    {
        lock(sync)
        {
            int clamped=Clamp(offset);
            string temporary=null;
            LastSaveStatus="attempting"; LastSaveError="";
            try
            {
                string directory=Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                temporary=Path.Combine(directory,Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
                var values=new Dictionary<string,object> {
                    { "schemaVersion",1 }, { "verticalOffsetDip",clamped }
                };
                string text=new JavaScriptSerializer().Serialize(values)+Environment.NewLine;
                // Same-directory replacement leaves either complete old or new JSON.
                using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {
                    byte[] bytes=new UTF8Encoding(false).GetBytes(text);
                    stream.Write(bytes,0,bytes.Length); stream.Flush(true);
                }
                if(File.Exists(path)) File.Replace(temporary,path,null);
                else File.Move(temporary,path);
                temporary=null; offsetDip=clamped;
                LastSaveStatus="saved";
                return true;
            }
            catch(IOException e) { SaveFailed(e); return false; }
            catch(UnauthorizedAccessException e) { SaveFailed(e); return false; }
            catch(System.Security.SecurityException e) { SaveFailed(e); return false; }
            catch(ArgumentException e) { SaveFailed(e); return false; }
            finally
            {
                if(temporary!=null)
                {
                    try { File.Delete(temporary); }
                    catch(IOException) { }
                    catch(UnauthorizedAccessException) { }
                    catch(System.Security.SecurityException) { }
                }
            }
        }
    }
    internal bool RestoreInitial() { return Save(0); }
}
