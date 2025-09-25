using Wolverine.ErrorHandling;
using Wolverine.Runtime;

namespace Wolverine.Pulsar.ErrorHandling;

public class PulsarNativeContinuationSource : IContinuationSource
{
    public string Description { get; } = "Pulsar native retry and dead letter queue handling";

    public IContinuation Build(Exception ex, Envelope envelope)
    {
        // Only handle Pulsar envelopes/listeners
        if (envelope.Listener is PulsarListener)
        {
            return new PulsarNativeResiliencyContinuation(ex);
        }

        // Fall back to standard error handling if not a Pulsar listener
        return new MoveToErrorQueue(ex);
    }
}