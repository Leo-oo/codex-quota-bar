using System;
using System.Threading;
using System.Windows.Automation;
using System.Drawing;
using System.Collections.Generic;

internal sealed class NavigationSnapshot
{
    internal int State = -1;
    internal Rectangle Host, Avatar, Update;
    internal double Scale = 1;
    internal Rectangle[] Obstacles = new Rectangle[0];
    internal DateTime Fetched = DateTime.UtcNow;
    internal bool NativePopupVisible;
    internal Rectangle[] NativePopupBounds = new Rectangle[0];
    internal IntPtr[][] NativePopupAncestors = new IntPtr[0][];
    internal DateTime NativePopupFetched = DateTime.MinValue;
}

// Reads only navigation controls' accessible names and bounds. No chat text is retrieved.
// Labels verified against the installed client's app.sidebar.show / app.sidebar.hide strings.
internal sealed class SidebarState : IDisposable
{
    int busy;
    int fixedRailKnown;
    long target;
    DateTime nextCheck;
    readonly object lifetime=new object();
    readonly AutoResetEvent workReady=new AutoResetEvent(false);
    Thread worker;
    IntPtr requestedWindow;
    volatile bool disposed;
    volatile IntPtr[] popupWindowExclusions=new IntPtr[0];
    internal bool WorkerStopped { get { return worker==null || !worker.IsAlive; } }
    // Immutable after publication: unknown=-1, legacy expanded=0/collapsed=1, fixed rail=2.
    internal volatile NavigationSnapshot Snapshot = new NavigationSnapshot();
    internal void SetPopupWindowExclusions(IntPtr[] windows)
    {
        var copy=new List<IntPtr>();
        if(windows!=null) foreach(IntPtr window in windows)
            if(window!=IntPtr.Zero && !copy.Contains(window)) copy.Add(window);
        lock(lifetime)
        {
            if(disposed) return;
            popupWindowExclusions=copy.ToArray();
            // A UIA result may have arrived just before the UI thread created
            // or registered a tool popup. Refilter by its stored HWND identity,
            // never by a rectangle that a real host menu could share.
            var previous=Snapshot;
            var updated=CopyNavigation(previous);
            updated.NativePopupVisible=previous.NativePopupVisible;
            updated.NativePopupBounds=previous.NativePopupBounds;
            updated.NativePopupAncestors=previous.NativePopupAncestors;
            updated.NativePopupFetched=previous.NativePopupFetched;
            ApplyPopupExclusions(updated);
            Snapshot=updated;
        }
    }
    internal void Poll(IntPtr window)
    {
        lock(lifetime)
        {
        if(disposed || window==IntPtr.Zero) return;
        if (Interlocked.Read(ref target) != window.ToInt64())
        {
            Interlocked.Exchange(ref target, window.ToInt64());
            Interlocked.Exchange(ref fixedRailKnown,0);
            Snapshot = new NavigationSnapshot(); nextCheck = DateTime.MinValue;
        }
        if (DateTime.UtcNow < nextCheck || Interlocked.CompareExchange(ref busy, 1, 0) != 0) return;
        nextCheck = DateTime.UtcNow.AddMilliseconds(Snapshot.State==2?250:800);
        requestedWindow=window;
        if(worker==null)
        {
            // Keep UI Automation's COM calls on one explicitly owned MTA.
            // A ThreadPool callback has no application-level shutdown boundary.
            worker=new Thread(ReadNavigation) { IsBackground=true,Name="CodexUsageMini navigation" };
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        workReady.Set();
        }
    }
    void ReadNavigation()
    {
        try
        {
            while(true)
            {
                workReady.WaitOne();
                if(disposed) return;
                IntPtr window=requestedWindow;
                ReadNavigation(window);
            }
        }
        finally { workReady.Dispose(); }
    }
    void ReadNavigation(IntPtr window)
    {
            var result = new NavigationSnapshot();
            bool popupReadSucceeded=false;
            bool popupReadComplete=false;
            try
            {
                if(disposed || !Native.IsWindow(window)) return;
                AutomationElement root = AutomationElement.FromHandle(window);
                Native.Rect host;
                if(!Native.BoundsOf(window,out host)) return;
                result.Host=Rectangle.FromLTRB(host.Left,host.Top,host.Right,host.Bottom);
                result.Scale=Native.Scale(window);
                // An embedded web menu can be painted inside the host HWND.
                // Native z-order alone cannot place a separate overlay beneath it.
                // Read only menu semantics and geometry, never menu/account text.
                result.NativePopupBounds=ReadPopupBounds(root,result.Host,out popupReadComplete,out result.NativePopupAncestors);
                result.NativePopupVisible=result.NativePopupBounds.Length!=0;
                result.NativePopupFetched=DateTime.UtcNow;
                Native.Rect popupHost;
                bool popupHostStable=Native.BoundsOf(window,out popupHost) && popupHost.Left==host.Left &&
                    popupHost.Top==host.Top && popupHost.Right==host.Right && popupHost.Bottom==host.Bottom;
                if(popupHostStable) popupReadSucceeded=PublishPopup(window,result,popupReadComplete);
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new OrCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "显示侧边栏"),
                        new PropertyCondition(AutomationElement.NameProperty, "隐藏侧边栏"),
                        new PropertyCondition(AutomationElement.NameProperty, "Show sidebar"),
                        new PropertyCondition(AutomationElement.NameProperty, "Hide sidebar")));
                var candidates = root.FindAll(TreeScope.Descendants, condition);
                var bounds = root.Current.BoundingRectangle;
                double scale = result.Scale;
                double bestX = Double.MaxValue;
                foreach (AutomationElement button in candidates)
                {
                    var state = button.Current;
                    var r = state.BoundingRectangle;
                    if (state.IsOffscreen || r.IsEmpty || r.Top > bounds.Top + 140*scale ||
                        r.Left < bounds.Left || r.Left > bounds.Left + 400*scale || r.Left >= bestX) continue;
                    bestX = r.Left;
                    result.State = state.Name == "显示侧边栏" || state.Name == "Show sidebar" ? 1 : 0;
                }
                // Current desktop rail uses this accessible label for its avatar button.
                // Verified in 26.928.2636.0 resources; do not guess from user initials.
                var profileCondition=new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
                    new OrCondition(new PropertyCondition(AutomationElement.NameProperty,"打开个人资料菜单"),
                        new PropertyCondition(AutomationElement.NameProperty,"Open profile menu")));
                foreach(AutomationElement button in root.FindAll(TreeScope.Descendants,profileCondition))
                {
                    var state=button.Current; var r=state.BoundingRectangle;
                    if(state.IsOffscreen || r.IsEmpty || r.Width<20*scale || r.Width>56*scale ||
                        r.Height<20*scale || r.Height>64*scale || r.Left<host.Left ||
                        r.Right>host.Left+72*scale || r.Top<host.Bottom-120*scale) continue;
                    result.Avatar=Rectangle.Round(new RectangleF((float)r.Left,(float)r.Top,(float)r.Width,(float)r.Height));
                    break;
                }
                if(!result.Avatar.IsEmpty)
                {
                    // Only collect bounds in the narrow rail, never chat text or account names.
                    var clickable=new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Hyperlink),
                        new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem));
                    var obstacles=new List<Rectangle>();
                    foreach(AutomationElement control in root.FindAll(TreeScope.Descendants,clickable))
                    {
                        var state=control.Current; var r=state.BoundingRectangle;
                        if(state.IsOffscreen || r.IsEmpty || r.Left<host.Left || r.Right>host.Left+72*scale) continue;
                        var rect=Rectangle.Round(new RectangleF((float)r.Left,(float)r.Top,(float)r.Width,(float)r.Height));
                        if(rect.IntersectsWith(result.Avatar)) continue;
                        string name=state.Name??"";
                        bool update=name=="有可用更新" || name=="Update available" || name=="更新" || name=="Update" ||
                            name.StartsWith("正在下载",StringComparison.Ordinal) || name.StartsWith("Downloading",StringComparison.OrdinalIgnoreCase) ||
                            name=="正在安装" || name=="Installing";
                        if(update && rect.Top>result.Avatar.Top-160*scale && rect.Bottom<=result.Avatar.Top)
                            result.Update=result.Update.IsEmpty?rect:Rectangle.Union(result.Update,rect);
                        else obstacles.Add(rect);
                    }
                    result.Obstacles=obstacles.ToArray();
                    // The fixed rail remains present across chats and settings.
                    result.State=2;
                }
                Native.Rect after;
                if(Native.Scale(window)!=result.Scale || !Native.BoundsOf(window,out after) || after.Left!=host.Left || after.Top!=host.Top ||
                    after.Right!=host.Right || after.Bottom!=host.Bottom) result.State=-1;
            }
            catch (ElementNotAvailableException) { result.State=-1; }
            catch (System.Runtime.InteropServices.COMException) { result.State=-1; }
            catch (InvalidOperationException) { result.State=-1; }
            catch (ArgumentException) { result.State=-1; }
            catch (NotSupportedException) { result.State=-1; }
            catch (System.UnauthorizedAccessException) { result.State=-1; }
            finally
            {
                result.Fetched=DateTime.UtcNow;
                lock(lifetime)
                if (!disposed && Interlocked.Read(ref target) == window.ToInt64())
                {
                    if(result.State==2) Interlocked.Exchange(ref fixedRailKnown,1);
                    else if((result.State==0 || result.State==1) && Interlocked.CompareExchange(ref fixedRailKnown,0,0)==1)
                        result.State=-1; // A transient missing avatar must never restore the legacy bar.
                    // Navigation can disappear from UI Automation during a route transition.
                    // Preserve the last confirmed geometry for this HWND; Poll resets it for a new HWND.
                    // Keep its original timestamp and host bounds so resizing is still validated.
                    var previous=Snapshot;
                    if(popupReadSucceeded)
                    {
                        ApplyPopupExclusions(result);
                        if(!popupReadComplete) MergePreviousPopups(result,previous);
                        popupReadSucceeded=popupReadComplete || result.NativePopupVisible;
                    }
                    if(!popupReadSucceeded)
                    {
                        // Failure is not evidence that a detected popup closed.
                        // Clear only after a successful empty query or HWND reset.
                        result.NativePopupVisible=previous.NativePopupVisible;
                        result.NativePopupBounds=previous.NativePopupBounds;
                        result.NativePopupAncestors=previous.NativePopupAncestors;
                        result.NativePopupFetched=previous.NativePopupFetched;
                    }
                    if(result.State>=0 || previous.State<0) Snapshot = result;
                    else
                    {
                        // Geometry retention and popup presence have independent
                        // lifetimes: a successful empty menu query clears the flag
                        // even when the avatar disappears during a route transition.
                        Snapshot=new NavigationSnapshot {
                            State=previous.State,Host=previous.Host,Scale=previous.Scale,Avatar=previous.Avatar,Update=previous.Update,
                            Obstacles=previous.Obstacles,Fetched=previous.Fetched,
                            NativePopupVisible=result.NativePopupVisible,NativePopupBounds=result.NativePopupBounds,
                            NativePopupAncestors=result.NativePopupAncestors,
                            NativePopupFetched=result.NativePopupFetched
                        };
                    }
                }
                Interlocked.Exchange(ref busy, 0);
            }
    }
    bool PublishPopup(IntPtr window,NavigationSnapshot result,bool complete)
    {
        // Publish the valid popup read before slower navigation queries. Keep
        // the last navigation geometry until that separate read is complete.
        lock(lifetime)
        {
            if(disposed || Interlocked.Read(ref target)!=window.ToInt64()) return false;
            ApplyPopupExclusions(result);
            if(!complete) MergePreviousPopups(result,Snapshot);
            if(!complete && !result.NativePopupVisible) return false;
            var previous=Snapshot;
            Snapshot=new NavigationSnapshot {
                State=previous.State,Host=previous.Host,Scale=previous.Scale,Avatar=previous.Avatar,Update=previous.Update,
                Obstacles=previous.Obstacles,Fetched=previous.Fetched,
                NativePopupVisible=result.NativePopupBounds.Length!=0,NativePopupBounds=result.NativePopupBounds,
                NativePopupAncestors=result.NativePopupAncestors,
                NativePopupFetched=result.NativePopupFetched
            };
            return true;
        }
    }
    static NavigationSnapshot CopyNavigation(NavigationSnapshot previous)
    {
        return new NavigationSnapshot {
            State=previous.State,Host=previous.Host,Scale=previous.Scale,Avatar=previous.Avatar,Update=previous.Update,
            Obstacles=previous.Obstacles,Fetched=previous.Fetched
        };
    }
    void ApplyPopupExclusions(NavigationSnapshot result)
    {
        if(result.NativePopupAncestors.Length!=result.NativePopupBounds.Length) return;
        var bounds=new List<Rectangle>(); var paths=new List<IntPtr[]>();
        for(int i=0;i<result.NativePopupBounds.Length;i++)
        {
            if(IsExcluded(result.NativePopupAncestors[i])) continue;
            bounds.Add(result.NativePopupBounds[i]); paths.Add(result.NativePopupAncestors[i]);
        }
        result.NativePopupBounds=bounds.ToArray(); result.NativePopupAncestors=paths.ToArray();
        result.NativePopupVisible=bounds.Count!=0;
    }
    bool IsExcluded(IntPtr[] path)
    {
        var excluded=popupWindowExclusions;
        foreach(IntPtr window in path) if(Array.IndexOf(excluded,window)>=0) return true;
        return false;
    }
    void MergePreviousPopups(NavigationSnapshot result,NavigationSnapshot previous)
    {
        // A partial read may add evidence, never prove older host evidence gone.
        // In particular, a newly found self popup must not replace a host popup
        // that was unreadable, then erase that host evidence on late exclusion.
        var bounds=new List<Rectangle>(result.NativePopupBounds);
        var paths=new List<IntPtr[]>(result.NativePopupAncestors);
        for(int i=0;i<previous.NativePopupBounds.Length;i++)
        {
            var path=i<previous.NativePopupAncestors.Length?previous.NativePopupAncestors[i]:new IntPtr[0];
            if(IsExcluded(path)) continue;
            bool exists=false;
            for(int j=0;j<bounds.Count;j++)
            {
                if(bounds[j]!=previous.NativePopupBounds[i] || paths[j].Length!=path.Length) continue;
                bool same=true;
                for(int k=0;k<path.Length;k++) if(paths[j][k]!=path[k]) { same=false; break; }
                if(same) { exists=true; break; }
            }
            if(!exists) { bounds.Add(previous.NativePopupBounds[i]); paths.Add(path); }
        }
        result.NativePopupBounds=bounds.ToArray(); result.NativePopupAncestors=paths.ToArray();
        result.NativePopupVisible=bounds.Count!=0;
    }
    static IntPtr[] ReadNativeAncestors(AutomationElement element,out bool complete)
    {
        complete=true; var handles=new List<IntPtr>(); var parent=element;
        int level=0;
        for(;level<32 && parent!=null;level++)
        {
            try
            {
                var state=parent.Current;
                int nativeWindow=state.NativeWindowHandle;
                if(nativeWindow!=0) handles.Add(new IntPtr(nativeWindow));
                // MSAA can insert virtual Window nodes with HWND=0 above a
                // dropdown item. They are not a native boundary: continue to
                // the actual popup HWND so our own menu can be excluded.
                if(state.ControlType==ControlType.Window && nativeWindow!=0) break;
                parent=TreeWalker.ControlViewWalker.GetParent(parent);
            }
            catch(ElementNotAvailableException) { complete=false; break; }
            catch(System.Runtime.InteropServices.COMException) { complete=false; break; }
            catch(InvalidOperationException) { complete=false; break; }
            catch(ArgumentException) { complete=false; break; }
            catch(NotSupportedException) { complete=false; break; }
        }
        if(level==32 && parent!=null) complete=false;
        return handles.ToArray();
    }
    Rectangle[] ReadPopupBounds(AutomationElement root,Rectangle host,out bool complete,out IntPtr[][] ancestors)
    {
        complete=true;
        var condition=new OrCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Menu),
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.MenuItem));
        var result=new List<Rectangle>();
        var paths=new List<IntPtr[]>();
        foreach(AutomationElement element in root.FindAll(TreeScope.Descendants,condition))
        {
            try
            {
                var state=element.Current; var r=state.BoundingRectangle;
                if(state.IsOffscreen) continue;
                if(r.IsEmpty || r.Width<=0 || r.Height<=0 ||
                    Double.IsNaN(r.Left) || Double.IsNaN(r.Top) || Double.IsNaN(r.Right) || Double.IsNaN(r.Bottom) ||
                    Double.IsInfinity(r.Left) || Double.IsInfinity(r.Top) || Double.IsInfinity(r.Right) || Double.IsInfinity(r.Bottom) ||
                    r.Left<Int32.MinValue || r.Top<Int32.MinValue || r.Right>Int32.MaxValue || r.Bottom>Int32.MaxValue)
                { complete=false; continue; }
                bool ancestryComplete;
                var path=ReadNativeAncestors(element,out ancestryComplete);
                if(IsExcluded(path)) continue;
                if(!ancestryComplete) complete=false;
                if(state.ControlType==ControlType.MenuItem)
                {
                    bool menuBarAncestryComplete;
                    bool menuBar=IsMenuBarEntry(element,out menuBarAncestryComplete);
                    if(!menuBarAncestryComplete) complete=false;
                    if(menuBar) continue;
                }
                var bounds=Rectangle.Round(new RectangleF((float)r.Left,(float)r.Top,(float)r.Width,(float)r.Height));
                if(bounds.Width<=0 || bounds.Height<=0 || !bounds.IntersectsWith(host)) continue;
                // Equal geometry can belong to a tool popup and a real host
                // popup. Keep each identity so excluding one never drops both.
                result.Add(bounds); paths.Add(path);
            }
            catch(ElementNotAvailableException) { complete=false; }
            catch(System.Runtime.InteropServices.COMException) { complete=false; }
            catch(InvalidOperationException) { complete=false; }
            catch(ArgumentException) { complete=false; }
            catch(NotSupportedException) { complete=false; }
        }
        ancestors=paths.ToArray(); return result.ToArray();
    }
    static bool IsMenuBarEntry(AutomationElement element,out bool complete)
    {
        complete=true;
        // A visible top-level menu-bar item is not an open popup. A Menu ancestor
        // encountered first identifies an expanded popup beneath that menu bar.
        AutomationElement parent=element;
        for(int level=0;level<12;level++)
        {
            parent=TreeWalker.ControlViewWalker.GetParent(parent);
            if(parent==null) { complete=false; return true; }
            var type=parent.Current.ControlType;
            if(type==ControlType.Menu) return false;
            if(type==ControlType.MenuBar) return true;
            if(type==ControlType.Window) return false;
        }
        complete=false; return true; // Unknown ancestry is not proof of an open popup.
    }
    public void Dispose()
    {
        Thread running;
        lock(lifetime)
        {
            if(disposed) return;
            disposed=true;
            Interlocked.Exchange(ref target,0);
            running=worker;
            if(running==null) workReady.Dispose();
            else workReady.Set();
        }
        // UI Automation providers can be temporarily unresponsive. Never abort
        // a native COM call or block the UI indefinitely while shutting down.
        if(running!=null && running!=Thread.CurrentThread) running.Join(1000);
    }
}
