using System;
using System.Diagnostics;
using System.Windows.Forms;

// UI-thread-only hover policy. Entry/popup mouse events call Refresh immediately;
// the normal tracking tick is only a fallback for a stationary pointer/window move.
internal sealed class TooltipHover : IDisposable
{
    internal const int ShowDelayMilliseconds = 150;
    internal const int LeaveGraceMilliseconds = 120;
    enum Pending { None, Show, Hide }
    readonly Func<bool> canOpen,canKeepOpen,isVisible;
    readonly Action show,hide;
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly Timer timer=new Timer { Interval=15 };
    Pending pending;
    long due,generation,cancellationGeneration;
    bool disposed;

    internal TooltipHover(Func<bool> canOpen,Func<bool> canKeepOpen,
        Func<bool> isVisible,Action show,Action hide)
    {
        if(canOpen==null || canKeepOpen==null || isVisible==null || show==null || hide==null)
            throw new ArgumentNullException("hover callback");
        this.canOpen=canOpen; this.canKeepOpen=canKeepOpen;
        this.isVisible=isVisible; this.show=show; this.hide=hide;
        timer.Tick+=OnTick;
    }

    internal bool WaitingToShow { get { return pending==Pending.Show; } }
    internal bool WaitingToHide { get { return pending==Pending.Hide; } }
    internal bool IsDisposed { get { return disposed; } }
    internal void EnterBar() { Refresh(); }
    internal void LeaveBar() { Refresh(); }
    internal void EnterPopup() { Refresh(); }
    internal void LeavePopup() { Refresh(); }

    internal void Refresh()
    {
        if(disposed) return;
        long current=generation;
        bool visible=isVisible();
        if(disposed || current!=generation) return;
        bool eligible=visible?canKeepOpen():canOpen();
        if(disposed || current!=generation) return;
        if(visible)
        {
            if(eligible) ClearPending();
            else Schedule(Pending.Hide,LeaveGraceMilliseconds);
        }
        else
        {
            if(eligible) Schedule(Pending.Show,ShowDelayMilliseconds);
            else ClearPending();
        }
    }

    void Schedule(Pending next,int delay)
    {
        // Repeated mouse moves/tracking refreshes do not postpone a deadline.
        if(disposed || pending==next) return;
        pending=next; due=clock.ElapsedMilliseconds+delay;
        timer.Start();
    }

    void ClearPending()
    {
        generation++; pending=Pending.None; due=0; timer.Stop();
    }

    void OnTick(object sender,EventArgs args)
    {
        if(disposed || pending==Pending.None) return;
        // Validate at every timer tick, including the final delayed transition.
        long current=generation;
        bool visible=isVisible();
        if(disposed || current!=generation) return;
        bool eligible=pending==Pending.Show?canOpen():canKeepOpen();
        if(disposed || current!=generation) return;
        if(pending==Pending.Show && (visible || !eligible))
        { ClearPending(); return; }
        if(pending==Pending.Hide && (!visible || eligible))
        { ClearPending(); return; }
        if(clock.ElapsedMilliseconds<due) return;
        Pending action=pending;
        ClearPending(); // Callbacks may synchronously Cancel or dispose us.
        current=generation;
        visible=isVisible();
        if(disposed || current!=generation) return;
        eligible=action==Pending.Show?canOpen():canKeepOpen();
        if(disposed || current!=generation) return;
        if(action==Pending.Show)
        {
            if(!visible && eligible)
            {
                long cancellation=cancellationGeneration;
                show();
                // A synchronous Cancel during Show wins over the completed Show.
                if((disposed || cancellation!=cancellationGeneration) && isVisible()) hide();
            }
        }
        else if(visible && !eligible) hide();
    }

    // Menus/news/native popups, Conceal and shutdown use immediate cancellation;
    // they must never leave the short hover grace active behind another surface.
    internal void Cancel()
    {
        if(disposed) return;
        cancellationGeneration++;
        ClearPending();
        if(isVisible()) hide();
    }

    public void Dispose()
    {
        if(disposed) return;
        disposed=true;
        cancellationGeneration++;
        ClearPending();
        timer.Tick-=OnTick;
        timer.Dispose();
        try { if(isVisible()) hide(); }
        finally { clock.Stop(); }
    }
}
