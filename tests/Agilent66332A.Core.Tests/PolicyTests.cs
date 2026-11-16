using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Core.Sequences;
using Studio.Tests;
namespace Agilent66332A.Core.Tests;
public class PolicyTests
{
    [Theory]
    [InlineData(4, .2, "VOLT,CURR")]
    [InlineData(6, .05, "CURR,VOLT")]
    [InlineData(6, .2, "CURR,VOLT")]
    [InlineData(4, .05, "VOLT,CURR")]
    public void ChangesDecreaseFirstAndEstablishCurrentBeforeVoltage(double v, double i, string expected) =>
        Assert.Equal(expected, string.Join(',', SettingsPolicy.Order(new(v, i), TestModels.State.Settings).Select(x => x.Field)));
    [Theory]
    [InlineData(double.NaN, 1)] [InlineData(double.PositiveInfinity, 1)] [InlineData(-1, 1)] [InlineData(1, -1)] [InlineData(21, 1)]
    public void InvalidTargetsAreRejected(double v, double i) => Assert.Throws<ArgumentException>(() => SettingsPolicy.Validate(new(v, i), TestModels.State));
    [Fact] public void OvpMustExceedSetpoint() => Assert.Throws<ArgumentException>(() => SettingsPolicy.Validate(new(5, .1), TestModels.State with { Protections = new(5, true, .08) }));
    [Fact] public void QuantizationUsesPerFieldResolution()
    {
        Assert.True(TestModels.State.Capabilities.Voltage.Accepts(1.001, 1));
        Assert.False(TestModels.State.Capabilities.Current.Accepts(.101, .1));
    }
    [Fact] public void SignedPowerAndInvalidPairRemainDistinct()
    {
        var pair = TestModels.Pair(); Assert.Equal(-.1, pair.EstimatedPowerW);
        Assert.Null((pair with { Quality = SampleQuality.Interrupted }).EstimatedPowerW);
        Assert.Null((pair with { Current = pair.Current with { Value = null } }).EstimatedPowerW);
        Assert.True(SettingsPolicy.IsStale(4, pair, 1)); Assert.False(SettingsPolicy.IsStale(4, pair, 2));
    }
    [Fact] public void SequenceRejectsDuplicatesHoldsRepeatsAndFutureSchema()
    {
        var d = SequenceDocument.Example;
        var invalid = d with { SchemaVersion = 9, RepeatCount = 0, Steps = [d.Steps[0] with { HoldSeconds = .1 }, d.Steps[0]] };
        var errors = SequenceValidator.Validate(invalid, TestModels.State);
        Assert.Contains(errors, e => e.Contains("schema")); Assert.Contains(errors, e => e.Contains("unique"));
        Assert.Contains(errors, e => e.Contains("hold")); Assert.Contains(errors, e => e.Contains("Repeat"));
    }
    [Fact] public void SequenceDurationOverflowAndNonfiniteSetpointsAreRejected()
    {
        var d = SequenceDocument.Example;
        Assert.NotEmpty(SequenceValidator.Validate(d with { RepeatCount = 100000, Steps = [d.Steps[0] with { HoldSeconds = TimeSpan.MaxValue.TotalSeconds }] }));
        Assert.NotEmpty(SequenceValidator.Validate(d with { Steps = [d.Steps[0] with { VoltageV = double.NaN }] }));
        var copy = d.Freeze(); d.Steps[0] = d.Steps[0] with { VoltageV = 9 }; Assert.Equal(1, copy.Steps[0].VoltageV);
    }
}
