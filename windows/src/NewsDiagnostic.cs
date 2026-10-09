using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Bounded message-state evidence only: no quota/account data, UIA text,
// conversation text, credentials, environment variables or response bodies.
internal sealed class NewsDiagnostic
{
    readonly string path;
    readonly int processId=Process.GetCurrentProcess().Id;
    readonly string startedUtc=DateTime.UtcNow.ToString("o");
    readonly List<object> events=new List<object>();
    int sequence;
    internal NewsDiagnostic(string file) { path=file; }
    internal void Record(string stage,object state)
    {
        try {
            events.Add(new { sequence=++sequence,utc=DateTime.UtcNow.ToString("o"),stage=stage,state=state });
            if(events.Count>80) events.RemoveAt(0);
            var json=new JavaScriptSerializer { MaxJsonLength=8388608 };
            string text;
            do {
                text=json.Serialize(new { schema=1,version="0.11.10.0",processId=processId,startedUtc=startedUtc,events=events });
                if(text.Length<=524288 || events.Count==1) break;
                events.RemoveAt(0);
            } while(true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary=path+".tmp";
            File.WriteAllText(temporary,text,Encoding.UTF8);
            if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
        } catch(IOException) { }
        catch(UnauthorizedAccessException) { }
        catch(ArgumentException) { }
        catch(InvalidOperationException) { }
    }
}
