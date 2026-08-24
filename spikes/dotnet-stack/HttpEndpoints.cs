using Wolverine.Http;

namespace BabelSpike;

/// <summary>Minimal WolverineFx.Http endpoint, present to prove the package works.</summary>
public static class SpikeEndpoints
{
    [WolverineGet("/spike/health")]
    public static object Health() => new
    {
        status = "ok",
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        proofs = new[] { "a-decimal", "b-balanced", "c-eventstore", "d-outbox", "e-tenancy" }
    };

    [WolverineGet("/spike/messages")]
    public static object Messages() => new { delivered = MessageLog.Received.Count };
}
