using static ChainCalculator.Operation;

namespace ChainCalculator.Tests;

public class ChainEngineTests
{
    private static IReadOnlyList<double> Run(params (Operation Op, string Text)[] cells) =>
        ChainEngine.Evaluate(cells.Select(c => new Cell(c.Op, c.Text)).ToList());

    [Fact]
    public void AppliesEachOperationToTheRunningResult()
    {
        var running = Run((Add, "10"), (Add, "5"), (Multiply, "3"), (Divide, "9"), (Add, ""));
        Assert.Equal([10, 15, 45, 5, 5], running);
    }

    [Fact]
    public void ChainStartsAtZero()
    {
        Assert.Equal(-4, Run((Subtract, "4"))[^1]);
        Assert.Equal(0, Run((Multiply, "7"))[^1]);
    }

    [Fact]
    public void EmptyCellsPassTheResultThrough()
    {
        var running = Run((Add, "8"), (Multiply, ""), (Divide, "  "), (Subtract, "3"));
        Assert.Equal([8, 8, 8, 5], running);
    }

    [Fact]
    public void DivisionByZeroIsAnErrorForTheRestOfTheChain()
    {
        var running = Run((Add, "12"), (Divide, "0"), (Add, "1"));
        Assert.True(double.IsNaN(running[1]));
        Assert.True(double.IsNaN(running[2]));
        Assert.Equal("Error", ChainEngine.Format(running[2]));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1,5")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void InvalidInputIsAnError(string text)
    {
        Assert.True(double.IsNaN(ChainEngine.Parse(text)));
    }

    [Theory]
    [InlineData("2.5", 2.5)]
    [InlineData(" -3 ", -3)]
    [InlineData("1e3", 1000)]
    [InlineData("", 0)]
    public void ParsesNumbers(string text, double expected)
    {
        Assert.Equal(expected, ChainEngine.Parse(text));
    }

    [Theory]
    [InlineData(0.1 + 0.2, "0.3")]
    [InlineData(-0.0, "0")]
    [InlineData(1.0 / 3, "0.333333333333")]
    [InlineData(40, "40")]
    public void FormatsLikeTheWebVersion(double value, string expected)
    {
        Assert.Equal(expected, ChainEngine.Format(value));
    }

    [Fact]
    public void OperationButtonCyclesThroughAllFour()
    {
        Assert.Equal(Subtract, Add.Next());
        Assert.Equal(Multiply, Subtract.Next());
        Assert.Equal(Divide, Multiply.Next());
        Assert.Equal(Add, Divide.Next());
    }
}
