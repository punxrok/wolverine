using System.Diagnostics;
using CoreTests.Runtime;
using JasperFx.Core.Reflection;
using NSubstitute;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;
using Wolverine.ComplianceTests;
using Xunit;

namespace CoreTests.ErrorHandling;

public class CustomActionIndefinitelyTests
{
    private readonly Envelope theEnvelope = ObjectMother.Envelope();
    private readonly HandlerGraph theHandlers = new();

    [Fact]
    public void custom_action_only_runs_twice_with_current_implementation()
    {
        var callCount = 0;
        var maxAttempts = 0;

        theHandlers.OnException<SpecialException>()
            .CustomAction(async (runtime, lifecycle, ex) =>
            {
                callCount++;
                maxAttempts = Math.Max(maxAttempts, lifecycle.Envelope.Attempts);
                
                if (lifecycle.Envelope.Attempts > 10)
                {
                    runtime.MessageTracking.DiscardedEnvelope(lifecycle.Envelope);
                    await lifecycle.CompleteAsync();
                    return;
                }

                await lifecycle.ReScheduleAsync(DateTimeOffset.Now.AddSeconds(11));
            }, "Handle SpecialException with conditional discard/requeue");

        var exception = new SpecialException();
        
        // First attempt - should trigger custom action
        theEnvelope.Attempts = 1;
        var continuation1 = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
        continuation1.ShouldBeOfType<LambdaContinuation>();
        
        // Second attempt - should default to MoveToErrorQueue because no InfiniteSource is set  
        theEnvelope.Attempts = 2;
        var continuation2 = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
        continuation2.ShouldBeOfType<MoveToErrorQueue>();
        
        // Third attempt - should also default to MoveToErrorQueue
        theEnvelope.Attempts = 3;
        var continuation3 = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
        continuation3.ShouldBeOfType<MoveToErrorQueue>();
    }

    [Fact] 
    public void custom_action_indefinitely_should_run_until_user_decides_to_stop()
    {
        var callCount = 0;

        theHandlers.OnException<SpecialException>()
            .CustomActionIndefinitely(async (runtime, lifecycle, ex) =>
            {
                callCount++;
                
                if (lifecycle.Envelope.Attempts > 10)
                {
                    runtime.MessageTracking.DiscardedEnvelope(lifecycle.Envelope);
                    await lifecycle.CompleteAsync();
                    return;
                }

                await lifecycle.ReScheduleAsync(DateTimeOffset.Now.AddSeconds(11));
            }, "Handle SpecialException with conditional discard/requeue indefinitely");

        var exception = new SpecialException();
        
        // Test multiple attempts - should all trigger custom action
        for (int attempt = 1; attempt <= 15; attempt++)
        {
            theEnvelope.Attempts = attempt;
            var continuation = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
            continuation.ShouldBeOfType<LambdaContinuation>();
        }
    }

    [Fact]
    public void user_original_problem_statement_scenario()
    {
        // This test reproduces the exact scenario from the user's problem statement
        var callCount = 0;
        var completedCount = 0;
        var rescheduledCount = 0;

        theHandlers.OnException<SpecialException>()
            .CustomActionIndefinitely(async (runtime, lifecycle, ex) =>
            {
                callCount++;
                
                if (ex is SpecialException specialEx)
                {
                    if (lifecycle.Envelope.Attempts > 10)
                    {
                        runtime.MessageTracking.DiscardedEnvelope(lifecycle.Envelope);
                        await lifecycle.CompleteAsync();
                        completedCount++;
                        return;
                    }

                    await lifecycle.ReScheduleAsync(DateTimeOffset.Now.AddSeconds(11));
                    rescheduledCount++;
                }
            }, "Handle SpecialException with conditional discard/requeue", null);

        var exception = new SpecialException { Code = 500 };
        
        // Test attempts 1-10: should reschedule
        for (int attempt = 1; attempt <= 10; attempt++)
        {
            theEnvelope.Attempts = attempt;
            var continuation = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
            continuation.ShouldBeOfType<LambdaContinuation>();
        }
        
        // Test attempts 11+: should still trigger custom action (not default to error queue)
        for (int attempt = 11; attempt <= 15; attempt++)
        {
            theEnvelope.Attempts = attempt;
            var continuation = theHandlers.Failures.DetermineExecutionContinuation(exception, theEnvelope);
            continuation.ShouldBeOfType<LambdaContinuation>();
        }
        
        // The custom action should be available to handle the decision whether to discard or reschedule
        // This verifies that the system doesn't automatically default to error queue after 2 attempts
        callCount.ShouldBe(0); // Not executed yet, just configured
    }
}

public class SpecialException : Exception
{
    public int Code { get; set; }
}