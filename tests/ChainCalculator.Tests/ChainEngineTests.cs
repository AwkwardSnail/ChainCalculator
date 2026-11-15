using static ChainCalculator.Operation;

namespace ChainCalculator.Tests;

public class ChainEngineTests
{
    private static IReadOnlyList<double> Run(params (Operation Op, string Text)[] cells) =>
        ChainEngine.Evaluate(cells.Select(c => new Cell(c.Op, c.Text)).ToList());

    [Fact]
    public void MultiplyAndDivideComeBeforeAddAndSubtract()
    {
        // 1 + 1 + 1 ÷ 2 = 2.5, not (1 + 1 + 1) ÷ 2.
        Assert.Equal([1, 2, 3, 2.5], Run((Add, "1"), (Add, "1"), (Add, "1"), (Divide, "2")));
        // 10 + 5 × 3 ÷ 6: the running result is the sum so far.
        Assert.Equal([10, 15, 25, 12.5, 12.5], Run((Add, "10"), (Add, "5"), (Multiply, "3"), (Divide, "6"), (Add, "")));
        Assert.Equal(-10, Run((Add, "2"), (Subtract, "3"), (Multiply, "4"))[^1]);
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
    public void ExpressionAddsNoBracketsOfItsOwn()
    {
        Assert.Equal("1 + 1 + 1 ÷ 2", Expr((Add, "1"), (Add, "1"), (Add, "1"), (Divide, "2")));
        Assert.Equal("10 + 5 × 3 ÷ 9", Expr((Add, "10"), (Add, "5"), (Multiply, "3"), (Divide, "9"), (Add, "")));
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

    private static List<Cell> Bracketed(params (Operation Op, Bracket Bracket, string Text)[] cells) =>
        cells.Select(c => new Cell(c.Op, c.Text, c.Bracket)).ToList();

    [Fact]
    public void BracketGroupsTheCellsBelowIt()
    {
        // 10 ÷ (2 + 3), not 10 ÷ 2 + 3.
        var cells = Bracketed((Add, Bracket.None, "10"), (Divide, Bracket.Open, "2"), (Add, Bracket.Close, "3"), (Add, Bracket.None, "1"));
        Assert.Equal([10, 5, 2, 3], ChainEngine.Evaluate(cells));
        Assert.Equal("10 ÷ (2 + 3) + 1", ChainEngine.Expression(cells));
    }

    [Fact]
    public void OpenBracketClosesAtTheEndUnlessClosedEarlier()
    {
        var cells = Bracketed((Add, Bracket.None, "100"), (Subtract, Bracket.Open, "10"), (Add, Bracket.None, "5"), (Multiply, Bracket.None, "2"));
        // 100 − (10 + 5 × 2) = 80: normal order applies inside the bracket too.
        Assert.Equal([100, 90, 85, 80], ChainEngine.Evaluate(cells));
        Assert.Equal("100 − (10 + 5 × 2)", ChainEngine.Expression(cells));
    }

    [Fact]
    public void BracketsGiveTheOldTopToBottomOrderWhenWanted()
    {
        // (1 + 1 + 1) ÷ 2
        var cells = Bracketed((Add, Bracket.Open, "1"), (Add, Bracket.None, "1"), (Add, Bracket.Close, "1"), (Divide, Bracket.None, "2"));
        Assert.Equal(1.5, ChainEngine.Evaluate(cells)[^1]);
        Assert.Equal("(1 + 1 + 1) ÷ 2", ChainEngine.Expression(cells));
    }

    [Fact]
    public void BracketsNest()
    {
        var cells = Bracketed((Add, Bracket.None, "1"), (Divide, Bracket.Open, "2"), (Add, Bracket.None, "3"),
            (Divide, Bracket.Open, "4"), (Add, Bracket.Close, "4"));
        // 1 ÷ (2 + 3 ÷ (4 + 4)): the inner bracket closes at the last cell, the outer at the end.
        Assert.Equal(1 / (2 + 3 / 8.0), ChainEngine.Evaluate(cells)[^1], 12);
        Assert.Equal("1 ÷ (2 + 3 ÷ (4 + 4))", ChainEngine.Expression(cells));
    }

    [Fact]
    public void StrayBracketsAreIgnored()
    {
        // A ")" with nothing open, and a "(" on an empty cell, change nothing.
        var cells = Bracketed((Add, Bracket.Close, "6"), (Multiply, Bracket.Open, ""), (Add, Bracket.None, "2"));
        Assert.Equal([6, 6, 8], ChainEngine.Evaluate(cells));
        Assert.Equal("6 + 2", ChainEngine.Expression(cells));
    }

    [Fact]
    public void DivisionByZeroInsideABracketIsAnError()
    {
        var cells = Bracketed((Add, Bracket.None, "9"), (Divide, Bracket.Open, "2"), (Subtract, Bracket.Close, "2"));
        Assert.True(double.IsNaN(ChainEngine.Evaluate(cells)[^1]));
        Assert.Equal("9 ÷ (2 − 2)", ChainEngine.Expression(cells));
    }

    [Fact]
    public void BracketButtonOffersOpenFirstWhenNothingIsOpen()
    {
        var cells = Bracketed((Add, Bracket.None, "1"), (Add, Bracket.None, "2"));
        Assert.Equal(Bracket.Open, ChainEngine.NextBracket(cells, 1));
        cells[1] = cells[1] with { Bracket = Bracket.Open };
        Assert.Equal(Bracket.Close, ChainEngine.NextBracket(cells, 1));
        cells[1] = cells[1] with { Bracket = Bracket.Close };
        Assert.Equal(Bracket.None, ChainEngine.NextBracket(cells, 1));
    }

    [Fact]
    public void BracketButtonOffersCloseFirstBelowAnOpenBracket()
    {
        var cells = Bracketed((Add, Bracket.Open, "1"), (Add, Bracket.None, "2"), (Add, Bracket.None, "3"));
        Assert.Equal(Bracket.Close, ChainEngine.NextBracket(cells, 2));
        cells[2] = cells[2] with { Bracket = Bracket.Close };
        Assert.Equal(Bracket.Open, ChainEngine.NextBracket(cells, 2));
        cells[2] = cells[2] with { Bracket = Bracket.Open };
        Assert.Equal(Bracket.None, ChainEngine.NextBracket(cells, 2));

        // Once the bracket above is closed, ( comes first again.
        cells = Bracketed((Add, Bracket.Open, "1"), (Add, Bracket.Close, "2"), (Add, Bracket.None, "3"));
        Assert.Equal(Bracket.Open, ChainEngine.NextBracket(cells, 2));
    }

    // Writes a layout as text with fractions as [numerator / denominator].
    private static string Describe(MathNode? node) => node switch
    {
        null => "",
        MathText t => t.Text,
        MathRow r => string.Join(" ", r.Items.Select(Describe)),
        MathBrackets b => $"({Describe(b.Inner)})",
        MathFraction f => $"[{Describe(f.Numerator)} / {Describe(f.Denominator)}]",
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

    private static string Laid(params (Operation Op, Bracket Bracket, string Text)[] cells) =>
        Describe(ChainEngine.Layout(Bracketed(cells)));

    [Fact]
    public void LayoutShowsDivisionsAsFractions()
    {
        Assert.Equal("1 + 1 + [1 / 2]", Laid((Add, Bracket.None, "1"), (Add, Bracket.None, "1"), (Add, Bracket.None, "1"), (Divide, Bracket.None, "2")));
        // The numerator is the whole product since the last + or −.
        Assert.Equal("10 + [5 × 3 / 9]", Laid((Add, Bracket.None, "10"), (Add, Bracket.None, "5"), (Multiply, Bracket.None, "3"), (Divide, Bracket.None, "9")));
        // Dividing again divides the fraction; multiplying after sits beside it.
        Assert.Equal("[[8 / 2] / 2] × 3", Laid((Add, Bracket.None, "8"), (Divide, Bracket.None, "2"), (Divide, Bracket.None, "2"), (Multiply, Bracket.None, "3")));
        Assert.Equal("", Laid((Add, Bracket.None, "")));
    }

    [Fact]
    public void LayoutDropsBracketsTheFractionBarAlreadyShows()
    {
        // 240 ÷ (4 + 2): the bar groups 4 + 2, so no brackets.
        Assert.Equal("[240 / 4 + 2] − 15", Laid((Add, Bracket.None, "240"), (Divide, Bracket.Open, "4"), (Add, Bracket.Close, "2"), (Subtract, Bracket.None, "15")));
        Assert.Equal("[1 + 1 + 1 / 2]", Laid((Add, Bracket.Open, "1"), (Add, Bracket.None, "1"), (Add, Bracket.Close, "1"), (Divide, Bracket.None, "2")));
        // Brackets that don't wrap a whole numerator or denominator stay.
        Assert.Equal("[(2 + 3) × 4 / 5]", Laid((Add, Bracket.Open, "2"), (Add, Bracket.Close, "3"), (Multiply, Bracket.None, "4"), (Divide, Bracket.None, "5")));
    }

    [Fact]
    public void LayoutKeepsTheStartingZeroAndNegativeBrackets()
    {
        Assert.Equal("[0 / 7]", Laid((Divide, Bracket.None, "7")));
        Assert.Equal("− 7 × 2", Laid((Subtract, Bracket.None, "7"), (Multiply, Bracket.None, "2")));
        Assert.Equal("[10 / −3] − (−1)", Laid((Add, Bracket.None, "10"), (Divide, Bracket.None, "-3"), (Subtract, Bracket.None, "-1")));
    }
}
