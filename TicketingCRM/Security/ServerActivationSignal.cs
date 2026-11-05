namespace TicketingCRM.Security;
// Host-local lifecycle signal. Configuration is reloaded only after authorized setup has committed.
public sealed class ServerActivationSignal
{
    private int requested;
    public bool Requested => Volatile.Read(ref requested)==1;
    public void Request()=>Interlocked.Exchange(ref requested,1);
}
