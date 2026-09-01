using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace SentinelKiosk.Runtime.Services;

internal static class PipeAcl
{
    /// <summary>
    /// Creates an inbound pipe the agent (often SYSTEM) can write to from a user-session runtime.
    /// </summary>
    public static NamedPipeServerStream CreateInbound(string name)
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.WorldSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance,
            AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            name,
            PipeDirection.In,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity: security);
    }
}
