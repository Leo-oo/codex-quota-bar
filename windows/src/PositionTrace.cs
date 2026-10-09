using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

// Diagnostic events contain only caller-selected tool state and geometry.
// Record never writes a file, queries UIA, or samples the global cursor.
internal sealed class PositionTrace : IMessageFilter, IDisposable
{
    const int MaximumEvents=160;
    const int MaximumJsonBytes=128*1024;
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref Point point);
    sealed class TraceEvent
    {
        public long sequence,elapsedMilliseconds,commandId;
        public string kind,utc;
        public object data;
    }
    sealed class DropdownObservation
    {
        internal ToolStripDropDown Dropdown;
        internal string Role;
        internal System.ComponentModel.CancelEventHandler Opening;
        internal EventHandler Opened,Disposed;
        internal ToolStripDropDownClosingEventHandler Closing;
        internal ToolStripDropDownClosedEventHandler Closed;
        internal ToolStripItemEventHandler ItemAdded;
    }
    sealed class ItemObservation
    {
        internal ToolStripItem Item;
        internal MouseEventHandler Down,Up;
        internal EventHandler Enter,Click;
    }
    readonly object gate=new object(),writeGate=new object();
    readonly Queue<TraceEvent> events=new Queue<TraceEvent>();
    readonly List<DropdownObservation> dropdowns=new List<DropdownObservation>();
    readonly List<ItemObservation> items=new List<ItemObservation>();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly System.Windows.Forms.Timer flushTimer;
    readonly string path,fallbackPath,session=Guid.NewGuid().ToString("N"),startUtc=DateTime.UtcNow.ToString("o");
    readonly string version=typeof(PositionTrace).Assembly.GetName().Version.ToString();
    readonly int processId=Process.GetCurrentProcess().Id;
    long sequence,commandId,captureDeadline,flushedSequence,dropped;
    int flushQueued;
    volatile bool disposed;
    bool filterRegistered;
    string lastError="";
    string actualFilePath="";
    object startup;
    bool flushSucceeded;

    internal PositionTrace(string filePath)
    {
        if(String.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("position trace path");
        path=Path.GetFullPath(filePath);
        fallbackPath=Path.Combine(Path.GetTempPath(),"CodexUsageMini-position-"+processId+".json");
        flushTimer=new System.Windows.Forms.Timer {Interval=150};
        flushTimer.Tick+=FlushTimerTick;
        flushTimer.Start();
    }
    internal string FilePath {get {return path;}}
    internal string FallbackFilePath {get {return fallbackPath;}}
    internal string ActualFilePath {get {lock(gate)return actualFilePath;}}
    internal long CurrentCommandId {get {return Interlocked.Read(ref commandId);}}
    internal bool CaptureActive {get {return !disposed && DateTime.UtcNow.Ticks<=Interlocked.Read(ref captureDeadline);}}
    internal long LastFlushedSequence {get {return Interlocked.Read(ref flushedSequence);}}
    internal bool FlushSucceeded {get {lock(gate)return flushSucceeded;}}
    internal string LastError {get {lock(gate)return lastError;}}

    internal void StartupRecord(object data)
    { lock(gate) startup=data; ExtendCapture(3000); Record("startup",data); }
    internal long BeginCommand()
    {
        long id=Interlocked.Increment(ref commandId);
        ExtendCapture(2000);
        return id;
    }
    void ExtendCapture(int milliseconds)
    {
        long next=DateTime.UtcNow.AddMilliseconds(milliseconds).Ticks,previous;
        do {
            previous=Interlocked.Read(ref captureDeadline);
            if(previous>=next) return;
        } while(Interlocked.CompareExchange(ref captureDeadline,next,previous)!=previous);
    }
    internal void Record(string kind,object data)
    {
        if(disposed) return;
        // Kind is a code label, never a control's accessible name or text.
        if(String.IsNullOrEmpty(kind) || kind.Length>64) kind="other";
        lock(gate)
        {
            if(disposed) return;
            while(events.Count>=MaximumEvents) {events.Dequeue();dropped++;}
            events.Enqueue(new TraceEvent {sequence=++sequence,elapsedMilliseconds=clock.ElapsedMilliseconds,
                utc=DateTime.UtcNow.ToString("o"),commandId=CurrentCommandId,kind=kind,data=data});
        }
    }
    static string FixedItem(ToolStripItem item)
    {
        if(item==null) return "other";
        string name=item.Name;
        return name=="up" || name=="down" || name=="reset" || name=="move" || name=="entry"?name:"other";
    }
    static string FixedRole(string role)
    {return role=="main" || role=="position" || role=="entry"?role:"other";}
    void ItemEvent(string kind,ToolStripItem item,string role)
    {
        ExtendCapture(2000);
        Record(kind,new {role=role,item=FixedItem(item),enabled=item.Enabled,available=item.Available,
            ownerVisible=item.Owner!=null && item.Owner.Visible});
    }
    void AttachItem(ToolStripItem item,string role)
    {
        foreach(var existing in items) if(existing.Item==item) return;
        if(FixedItem(item)!="other")
        {
            var observation=new ItemObservation {Item=item};
            observation.Enter=delegate {ItemEvent("item-enter",item,role);};
            observation.Down=delegate {ItemEvent("item-mouse-down",item,role);};
            observation.Up=delegate {ItemEvent("item-mouse-up",item,role);};
            // An observer attached after the production Click delegate records
            // its place in the actual multicast chain, not handler entry.
            observation.Click=delegate {ItemEvent("item-click-observer",item,role);};
            item.MouseEnter+=observation.Enter; item.MouseDown+=observation.Down;
            item.MouseUp+=observation.Up; item.Click+=observation.Click;
            items.Add(observation);
        }
        var group=item as ToolStripDropDownItem;
        if(group!=null && group.HasDropDownItems)
            AttachDropDown(group.DropDown,FixedItem(item)=="move"?"position":"other");
    }
    internal void AttachDropDown(ToolStripDropDown dropdown,string role)
    {
        if(disposed || dropdown==null || dropdown.IsDisposed) return;
        foreach(var existing in dropdowns) if(existing.Dropdown==dropdown) return;
        role=FixedRole(role);
        var observation=new DropdownObservation {Dropdown=dropdown,Role=role};
        observation.Opening=delegate(object sender,System.ComponentModel.CancelEventArgs e) {
            ExtendCapture(2000);Record("menu-opening",new {role=role,cancel=e.Cancel});
        };
        observation.Opened=delegate {ExtendCapture(2000);Record("menu-opened",new {role=role,visible=dropdown.Visible,
            hwnd=dropdown.IsHandleCreated?dropdown.Handle.ToInt64():0});};
        observation.Closing=delegate(object sender,ToolStripDropDownClosingEventArgs e) {
            Record("menu-closing",new {role=role,reason=e.CloseReason.ToString(),cancel=e.Cancel,visible=dropdown.Visible});
        };
        observation.Closed=delegate(object sender,ToolStripDropDownClosedEventArgs e) {
            Record("menu-closed",new {role=role,reason=e.CloseReason.ToString(),visible=dropdown.Visible});
        };
        observation.Disposed=delegate {Record("menu-disposed",new {role=role});};
        observation.ItemAdded=delegate(object sender,ToolStripItemEventArgs e) {AttachItem(e.Item,role);};
        dropdown.Opening+=observation.Opening;dropdown.Opened+=observation.Opened;
        dropdown.Closing+=observation.Closing;dropdown.Closed+=observation.Closed;
        dropdown.Disposed+=observation.Disposed;dropdown.ItemAdded+=observation.ItemAdded;
        dropdowns.Add(observation);
        foreach(ToolStripItem item in dropdown.Items) AttachItem(item,role);
    }
    internal void RegisterMessageFilter()
    {
        if(disposed || filterRegistered) return;
        Application.AddMessageFilter(this);filterRegistered=true;
    }
    public bool PreFilterMessage(ref Message message)
    {
        if(disposed || (message.Msg!=0x201 && message.Msg!=0x202)) return false;
        bool capture=CaptureActive;
        foreach(var observation in dropdowns)
        {
            var dropdown=observation.Dropdown;
            // A modal filter can close the menu before its queued button
            // message reaches this observer. During the short capture window,
            // retain that evidence only for a registered exact tool HWND.
            if(dropdown.IsDisposed || !dropdown.IsHandleCreated ||
                message.HWnd!=dropdown.Handle || (!dropdown.Visible && !capture)) continue;
            long packed=message.LParam.ToInt64();
            Point point=new Point(unchecked((short)(packed&65535)),unchecked((short)((packed>>16)&65535)));
            if(!ClientToScreen(message.HWnd,ref point)) return false;
            point=dropdown.PointToClient(point);
            var item=dropdown.GetItemAt(point);
            ExtendCapture(2000);
            Record("own-menu-message",new {role=observation.Role,kind=message.Msg,item=FixedItem(item),
                enabled=item!=null && item.Enabled,available=item!=null && item.Available,
                visible=dropdown.Visible,ownerVisible=item!=null && item.Owner!=null && item.Owner.Visible,
                captureActive=capture,inside=dropdown.ClientRectangle.Contains(point),hwnd=message.HWnd.ToInt64()});
            break;
        }
        return false;
    }
    internal void RecordPhysicalMenu(long kind,string role,string item,bool enabled,bool available)
    {
        ExtendCapture(2000);
        Record("physical-own-menu",new {source="WH_MOUSE_LL",kind=kind,role=FixedRole(role),
            item=item=="up" || item=="down" || item=="reset" || item=="move" || item=="entry"?item:"other",
            enabled=enabled,available=available});
    }
    void FlushTimerTick(object sender,EventArgs e)
    {
        if(disposed || Interlocked.CompareExchange(ref flushQueued,1,0)!=0) return;
        lock(gate) if(sequence<=flushedSequence) {Interlocked.Exchange(ref flushQueued,0);return;}
        ThreadPool.QueueUserWorkItem(delegate {
            try {FlushCore();} finally {Interlocked.Exchange(ref flushQueued,0);}
        });
    }
    // Explicit startup/shutdown flush is allowed to perform IO; hook callbacks
    // call only RecordPhysicalMenu/Record and never this method.
    internal bool FlushNow() {return FlushCore();}
    byte[] SerializeSnapshot(TraceEvent[] snapshot,long latest,long removed,object initial,string primaryError,string destination)
    {
        var serializer=new JavaScriptSerializer {MaxJsonLength=1024*1024};
        int skip=0;byte[] bytes;
        do {
            var kept=new TraceEvent[snapshot.Length-skip];Array.Copy(snapshot,skip,kept,0,kept.Length);
            string json=serializer.Serialize(new {version=version,process=processId,session=session,
                startUtc=startUtc,startup=initial,primaryPath=path,fallbackPath=fallbackPath,
                actualFilePath=destination,primaryWriteError=primaryError,
                lastSequence=latest,dropped=removed+skip,maximumEvents=MaximumEvents,
                maximumJsonBytes=MaximumJsonBytes,events=kept});
            bytes=new UTF8Encoding(false).GetBytes(json+Environment.NewLine);
            if(bytes.Length<=MaximumJsonBytes) return bytes;
            skip++;
        } while(skip<=snapshot.Length);
        throw new InvalidOperationException("trace size");
    }
    static void WriteAtomic(string destination,byte[] bytes)
    {
        string temporary=null;
        try {
            string directory=Path.GetDirectoryName(destination);Directory.CreateDirectory(directory);
            temporary=Path.Combine(directory,Path.GetFileName(destination)+"."+Guid.NewGuid().ToString("N")+".tmp");
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(destination)) File.Replace(temporary,destination,null);else File.Move(temporary,destination);
            temporary=null;
        } finally {
            if(temporary!=null) try {File.Delete(temporary);} catch(IOException) { }
                catch(UnauthorizedAccessException) { } catch(System.Security.SecurityException) { }
        }
    }
    bool FlushCore()
    {
        lock(writeGate)
        {
            TraceEvent[] snapshot;long latest,removed;object initial;
            lock(gate) {snapshot=events.ToArray();latest=sequence;removed=dropped;initial=startup;}
            string primaryError="";
            try
            {
                WriteAtomic(path,SerializeSnapshot(snapshot,latest,removed,initial,"",path));
                lock(gate) {flushSucceeded=true;lastError="";actualFilePath=path;Interlocked.Exchange(ref flushedSequence,latest);}
                return true;
            }
            catch(Exception ex)
            {
                primaryError=ex.GetType().Name+":"+ex.HResult;
            }
            try
            {
                WriteAtomic(fallbackPath,SerializeSnapshot(snapshot,latest,removed,initial,primaryError,fallbackPath));
                lock(gate) {flushSucceeded=true;lastError=primaryError;actualFilePath=fallbackPath;Interlocked.Exchange(ref flushedSequence,latest);}
                return true;
            }
            catch(Exception ex)
            {
                lock(gate) {flushSucceeded=false;lastError=primaryError+";fallback="+ex.GetType().Name+":"+ex.HResult;}
                return false;
            }
        }
    }
    public void Dispose()
    {
        if(disposed) return;
        flushTimer.Stop();flushTimer.Tick-=FlushTimerTick;flushTimer.Dispose();
        if(filterRegistered) {Application.RemoveMessageFilter(this);filterRegistered=false;}
        foreach(var observation in dropdowns) {
            var dropdown=observation.Dropdown;
            dropdown.Opening-=observation.Opening;dropdown.Opened-=observation.Opened;
            dropdown.Closing-=observation.Closing;dropdown.Closed-=observation.Closed;
            dropdown.Disposed-=observation.Disposed;dropdown.ItemAdded-=observation.ItemAdded;
        }
        foreach(var observation in items) {
            observation.Item.MouseEnter-=observation.Enter;observation.Item.MouseDown-=observation.Down;
            observation.Item.MouseUp-=observation.Up;observation.Item.Click-=observation.Click;
        }
        Record("trace-dispose",null);disposed=true;FlushCore();clock.Stop();
        dropdowns.Clear();items.Clear();
    }
}
