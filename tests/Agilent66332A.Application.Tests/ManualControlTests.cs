using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Studio.Tests;

namespace Agilent66332A.Application.Tests;

public class ManualControlTests
{
    [Fact]
    public async Task OutputOffDuringApplyPreventsTheFollowUpEnable()
    {
        var instrument = new FakeInstrument(new FakeClock());
        await using var session = new SessionCoordinator(new SystemClock(), new FakeRecordings());
        await session.ConnectAsync(instrument, new());
        Task<ShutdownResult>? shutdown = null;
        instrument.AfterCall = command =>
        {
            if (command.StartsWith("apply ", StringComparison.Ordinal))
                shutdown = session.OutputOffAsync("Test operator stop");
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ApplyAndEnableAsync(new(4, .1)));
        Assert.NotNull(shutdown);
        Assert.Equal(ShutdownOutcome.VerifiedOff, (await shutdown).Outcome);
        Assert.Equal(OutputState.Off, session.Snapshot?.Output);
        await session.DisconnectAsync();
        Assert.DoesNotContain(instrument.Calls, call => call.Command == "on");
    }
}
