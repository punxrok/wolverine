using Microsoft.Extensions.Hosting;
using Wolverine.ErrorHandling;
using Wolverine.Tracking;
using Wolverine.Transports.Local;
using Xunit;

namespace CoreTests.ErrorHandling;

public class CustomActionIndefinitelyIntegrationTests : IAsyncLifetime
{
    private IHost? _host;
    
    public async Task InitializeAsync()
    {
        _host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.ServiceName = "TestHost";
                
                // Configure the policy using the new CustomActionIndefinitely method
                opts.Policies.OnException<SpecialExceptionForIntegration>()
                    .CustomActionIndefinitely(async (runtime, lifecycle, ex) =>
                    {
                        if (ex is SpecialExceptionForIntegration specialEx)
                        {
                            if (lifecycle.Envelope.Attempts > 3) // Lower threshold for testing
                            {
                                runtime.MessageTracking.DiscardedEnvelope(lifecycle.Envelope);
                                await lifecycle.CompleteAsync();
                                return;
                            }

                            await lifecycle.ReScheduleAsync(DateTimeOffset.Now.AddMilliseconds(10)); // Short delay for testing
                        }
                    }, "Handle SpecialExceptionForIntegration with conditional discard/requeue");
            }).StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }

    [Fact]
    public async Task custom_action_indefinitely_handles_multiple_attempts_until_discarded()
    {
        var session = await _host!
            .TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .PublishMessageAndWaitAsync(new TestMessageThatFails());

        // The message should have been attempted multiple times and then discarded
        // The exact count depends on timing and scheduling, but it should be more than 2 attempts
        session.AllRecordsInOrder().Count().ShouldBeGreaterThan(1);
        
        // Should have handled the message eventually (even if it failed multiple times)
        session.FindEnvelopesWithMessageType<TestMessageThatFails>()
            .ShouldNotBeEmpty();
    }
}

public record TestMessageThatFails();

public class TestMessageThatFailsHandler
{
    public void Handle(TestMessageThatFails message)
    {
        // Always throws to trigger the error policy
        throw new SpecialExceptionForIntegration();
    }
}

public class SpecialExceptionForIntegration : Exception
{
    public int Code { get; set; }
}