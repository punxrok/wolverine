using JasperFx.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace CoreTests.Runtime;

public class ReschedulingWithinHandlerTests
{
    [Fact]
    public async Task reschedule_message_within_handler_should_work()
    {
        // This test demonstrates that when rescheduling a message from within its own handler,
        // it should work without causing duplicate key errors
        
        using var host = await Host.CreateDefaultBuilder()
            .UseWolverine(opts =>
            {
                opts.Services.AddSingleton<ReschedulingTracker>();
                
                opts.Publish(x => x.Message<TestMessageForRescheduling>()
                    .ToLocalQueue("reschedule_test").UseDurableInbox());
                    
                opts.Discovery.DisableConventionalDiscovery().IncludeType<ReschedulingMessageHandler>();
            })
            .StartAsync();
        
        var tracker = host.Services.GetRequiredService<ReschedulingTracker>();
        
        var message = new TestMessageForRescheduling { Id = Guid.NewGuid(), ShouldReschedule = true };
        
        // Send the message and let it be processed
        await host.SendAsync(message);
        
        // Give it a moment to be processed
        await Task.Delay(1000);

        // The message should have been handled once (and then rescheduled)
        tracker.HandledMessages.Count.ShouldBe(1);
        tracker.RescheduledMessages.Count.ShouldBe(1);
        
        // The first message should be the same one we sent
        tracker.HandledMessages.First().Id.ShouldBe(message.Id);
        tracker.RescheduledMessages.First().Id.ShouldBe(message.Id);
        
        await host.StopAsync();
    }
}

public class TestMessageForRescheduling
{
    public Guid Id { get; set; }
    public bool ShouldReschedule { get; set; }
}

public class ReschedulingTracker
{
    public List<TestMessageForRescheduling> HandledMessages { get; } = new();
    public List<TestMessageForRescheduling> RescheduledMessages { get; } = new();
}

public class ReschedulingMessageHandler
{
    private readonly ReschedulingTracker _tracker;

    public ReschedulingMessageHandler(ReschedulingTracker tracker)
    {
        _tracker = tracker;
    }

    public async Task Handle(TestMessageForRescheduling message, IMessageContext context)
    {
        _tracker.HandledMessages.Add(message);
        
        if (message.ShouldReschedule)
        {
            // This should use ScheduleExecutionAsync internally (our fix)
            // rather than ScheduleJobAsync (which would cause duplicates)
            await ((IEnvelopeLifecycle)context).ReScheduleAsync(DateTimeOffset.UtcNow.AddHours(1));
            _tracker.RescheduledMessages.Add(message);
        }
    }
}