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

    private static string Expr(params (Operation Op, string Text)[] cells) =>
        ChainEngine.Expression(cells.Select(c => new Cell(c.Op, c.Text)).ToList());

    [Fact]
    public void ExpressionBracketsSoNormalMathsGivesTheSameAnswer()
    {
        Assert.Equal("(10 + 5) × 3 ÷ 9", Expr((Add, "10"), (Add, "5"), (Multiply, "3"), (Divide, "9"), (Add, "")));
        Assert.Equal("((2 + 3) × 4 − 1) × 2", Expr((Add, "2"), (Add, "3"), (Multiply, "4"), (Subtract, "1"), (Multiply, "2")));
        Assert.Equal("2 × 3 + 4", Expr((Add, "2"), (Multiply, "3"), (Add, "4")));
        Assert.Equal("8 − 2 − 1", Expr((Add, "8"), (Subtract, "2"), (Subtract, "1")));
    }

    [Fact]
    public void ExpressionShowsTheStartingZeroOnlyWhenItMatters()
    {
        Assert.Equal("7", Expr((Add, "7")));
        Assert.Equal("−7 × 2", Expr((Subtract, "7"), (Multiply, "2")));
        Assert.Equal("0 × 7 + 1", Expr((Multiply, "7"), (Add, "1")));
    }

    [Fact]
    public void ExpressionSkipsEmptyCellsAndBracketsNegatives()
    {
        Assert.Equal("", Expr((Add, ""), (Multiply, " ")));
        Assert.Equal("10 − (−3)", Expr((Add, ""), (Add, "10"), (Divide, ""), (Subtract, "-3")));
        Assert.Equal("−3 + 2.5", Expr((Add, " -3 "), (Add, "2.50")));
    }

    [Fact]
    public void ExpressionShowsBadInputAsTyped()
    {
        Assert.Equal("12 ÷ 0", Expr((Add, "12"), (Divide, "0")));
        Assert.Equal("4 + abc", Expr((Add, "4"), (Add, " abc ")));
    }
}
