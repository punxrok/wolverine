using CoreTests.Runtime;
using NSubstitute;
using Wolverine;
using Wolverine.ComplianceTests;
using Wolverine.ErrorHandling;
using Wolverine.Runtime;
using Xunit;

namespace CoreTests.ErrorHandling;

public class ScheduledRetryContinuationTests
{
    [Fact]
    public async Task should_mark_envelope_for_reschedule_existing_when_executing()
    {
        var envelope = ObjectMother.Envelope();
        var context = Substitute.For<IEnvelopeLifecycle, IMessageContext>();
        var messageContext = context.As<IMessageContext>();
        messageContext.Envelope.Returns(envelope);
        
        var continuation = new ScheduledRetryContinuation(TimeSpan.FromMinutes(5));
        
        await continuation.ExecuteAsync(context, new MockWolverineRuntime(), DateTimeOffset.Now, null);
        
        // Verify the header is set
        envelope.Headers.ShouldContainKey(EnvelopeConstants.RescheduleExistingKey);
        envelope.Headers[EnvelopeConstants.RescheduleExistingKey].ShouldBe("true");
        
        // Verify ReScheduleAsync was called
        await context.Received(1).ReScheduleAsync(Arg.Any<DateTimeOffset>());
    }
    
    [Fact]
    public async Task should_not_fail_if_context_is_not_message_context()
    {
        var context = Substitute.For<IEnvelopeLifecycle>();
        var continuation = new ScheduledRetryContinuation(TimeSpan.FromMinutes(5));
        
        // Should not throw when context is not IMessageContext
        await continuation.ExecuteAsync(context, new MockWolverineRuntime(), DateTimeOffset.Now, null);
        
        await context.Received(1).ReScheduleAsync(Arg.Any<DateTimeOffset>());
    }
}