using System.Globalization;

namespace ChainCalculator;

public enum Operation
{
    Add,
    Subtract,
    Multiply,
    Divide,
}

public static class OperationExtensions
{
    public static string Symbol(this Operation op) => op switch
    {
        Operation.Add => "+",
        Operation.Subtract => "−",
        Operation.Multiply => "×",
        Operation.Divide => "÷",
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };

    /// <summary>The operation the button moves to on the next click.</summary>
    public static Operation Next(this Operation op) =>
        (Operation)(((int)op + 1) % Enum.GetValues<Operation>().Length);

    public static double Apply(this Operation op, double left, double right) => op switch
    {
        Operation.Add => left + right,
        Operation.Subtract => left - right,
        Operation.Multiply => left * right,
        Operation.Divide => right == 0 ? double.NaN : left / right,
        _ => throw new ArgumentOutOfRangeException(nameof(op)),
    };
}

/// <summary>A cell's bracket button: nothing, an opening "(" or a closing ")".</summary>
public enum Bracket
{
    None,
    Open,
    Close,
}

public static class BracketExtensions
{
    public static string Symbol(this Bracket bracket) => bracket switch
    {
        Bracket.None => "",
        Bracket.Open => "(",
        Bracket.Close => ")",
        _ => throw new ArgumentOutOfRangeException(nameof(bracket)),
    };

}

public readonly record struct Cell(Operation Operation, string Text, Bracket Bracket = Bracket.None);

/// <summary>
/// The calculation laid out for display, with divisions as stacked fractions.
/// The window turns this into text and fraction bars.
/// </summary>
public abstract record MathNode;

/// <summary>A number (or text the user typed) or an operator symbol.</summary>
public sealed record MathText(string Text, bool IsOperator = false) : MathNode;

/// <summary>Items shown side by side.</summary>
public sealed record MathRow(IReadOnlyList<MathNode> Items) : MathNode;

/// <summary>Numerator over a bar over denominator.</summary>
public sealed record MathFraction(MathNode Numerator, MathNode Denominator) : MathNode;

/// <summary>The user's own brackets round a part of the calculation.</summary>
public sealed record MathBrackets(MathNode Inner) : MathNode;

/// <summary>
/// Evaluates the cells as one sum, read top to bottom, using normal maths order:
/// × and ÷ before + and −, so 1 + 1 + 1 ÷ 2 is 2.5. The first number starts
/// the sum: its − makes it negative, and its + × or ÷ have nothing to act on,
/// so they're ignored (no "0 ÷ …"). A cell with "(" starts a bracket
/// at that cell's number (its operation applies to the whole bracket), which
/// runs until a cell with ")" or, if there is none, the last cell. Brackets can
/// nest. A ")" with no open bracket, and brackets on empty cells, are ignored.
/// NaN marks an error (bad input or division by zero) and carries through.
/// </summary>
public static class ChainEngine
{
    // The chain as a tree: each group is a list of steps, and a step's term is
    // either a number (the cell's text) or a bracketed group.
    private abstract record Term;
    private sealed record NumberTerm(string Text) : Term;
    private sealed record GroupTerm(List<Step> Steps) : Term;
    private sealed record Step(Operation Operation, Term Term);

    /// <summary>
    /// Returns the running result after each cell: the sum of that cell and the
    /// ones above it. The last one is the final result. Inside an open bracket,
    /// a cell's running result is what the sum gives if the bracket closed there.
    /// </summary>
    public static IReadOnlyList<double> Evaluate(IReadOnlyList<Cell> cells)
    {
        var running = new double[cells.Count];
        for (int i = 0; i < cells.Count; i++)
        {
            running[i] = Evaluate(Build(cells, i + 1));
        }
        return running;
    }

    /// <summary>
    /// Writes the cells as one expression, e.g. "240 ÷ (4 + 2) − 15", with only
    /// the user's own brackets (and brackets round negative numbers after an
    /// operation). Empty cells are left out; text that isn't a number is shown
    /// as typed. Returns "" when every cell is empty.
    /// </summary>
    public static string Expression(IReadOnlyList<Cell> cells) => Render(Build(cells, cells.Count));

    /// <summary>
    /// The same calculation as <see cref="Expression"/>, laid out with each
    /// division as a fraction: the numerator is everything multiplied or
    /// divided since the last + or −, e.g. 1 + 1 + 1 ÷ 2 shows ½ for the last
    /// part. Brackets that would wrap a whole numerator or denominator are left
    /// out, since the bar already groups it. Returns null when every cell is empty.
    /// </summary>
    public static MathNode? Layout(IReadOnlyList<Cell> cells)
    {
        var steps = Build(cells, cells.Count);
        return steps.Count == 0 ? null : Layout(steps);
    }

    /// <summary>
    /// What the bracket button on cell <paramref name="index"/> moves to on the
    /// next click. It cycles none → ( → ), except that while a bracket above
    /// this cell is still open it offers the closing one first: none → ) → (.
    /// </summary>
    public static Bracket NextBracket(IReadOnlyList<Cell> cells, int index)
    {
        int open = 0;
        for (int i = 0; i < index; i++)
        {
            if (cells[i].Bracket == Bracket.Open) open++;
            else if (cells[i].Bracket == Bracket.Close && open > 0) open--;
        }
        Bracket[] order = open > 0
            ? [Bracket.None, Bracket.Close, Bracket.Open]
            : [Bracket.None, Bracket.Open, Bracket.Close];
        return order[(Array.IndexOf(order, cells[index].Bracket) + 1) % order.Length];
    }

    // Builds the tree from the first `count` cells. Brackets still open at the
    // end close there.
    private static List<Step> Build(IReadOnlyList<Cell> cells, int count)
    {
        var top = new List<Step>();
        var open = new Stack<List<Step>>();
        open.Push(top);
        for (int i = 0; i < count; i++)
        {
            var cell = cells[i];
            if (string.IsNullOrWhiteSpace(cell.Text)) continue;
            var number = new NumberTerm(cell.Text);

            if (cell.Bracket == Bracket.Open)
            {
                // The bracket starts from this cell's number; this cell's
                // operation applies to the bracket as a whole.
                var inner = new List<Step> { new(Operation.Add, number) };
                open.Peek().Add(new Step(cell.Operation, new GroupTerm(inner)));
                open.Push(inner);
            }
            else
            {
                open.Peek().Add(new Step(cell.Operation, number));
                if (cell.Bracket == Bracket.Close && open.Count > 1) open.Pop();
            }
        }
        return top;
    }

    // Evaluates "t1 op2 t2 op3 t3 …" with × and ÷ before + and −: + and − start
    // a new term, × and ÷ work on the current term, and the terms are summed.
    private static double Evaluate(List<Step> steps)
    {
        double sum = 0;
        double term = 0;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            double value = step.Term switch
            {
                NumberTerm n => Parse(n.Text),
                GroupTerm g => Evaluate(g.Steps),
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            };
            if (i == 0)
            {
                // The first number starts the sum; only − changes it.
                term = step.Operation == Operation.Subtract ? -value : value;
                continue;
            }
            switch (step.Operation)
            {
                case Operation.Add: sum += term; term = value; break;
                case Operation.Subtract: sum += term; term = -value; break;
                default: term = step.Operation.Apply(term, value); break;
            }
        }
        return sum + term;
    }

    private static string Render(List<Step> steps)
    {
        string expression = "";
        foreach (var step in steps)
        {
            string operand = step.Term switch
            {
                NumberTerm n => FormatOperand(n.Text),
                GroupTerm g => $"({Render(g.Steps)})",
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            };

            if (expression.Length == 0)
            {
                // The first number starts the sum; only a − is shown.
                expression = step.Operation == Operation.Subtract ? "−" + Bracketed(operand) : operand;
                continue;
            }
            expression = $"{expression} {step.Operation.Symbol()} {Bracketed(operand)}";
        }
        return expression;
    }

    // Builds the display layout for one sum (the whole calculation or one
    // bracket): + and − separate terms. Within a term, everything multiplied
    // goes on top and everything divided by goes underneath, as one fraction:
    // a × b ÷ c ÷ d shows as a × b over c × d, never a tower of fractions.
    private static MathNode Layout(List<Step> steps)
    {
        var row = new List<MathNode>();
        var top = new List<MathNode>();
        var bottom = new List<MathNode>();
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (i == 0)
            {
                // The first number starts the sum; only a − is shown.
                if (step.Operation == Operation.Subtract) row.Add(new MathText("−", IsOperator: true));
                top.Add(LayoutOperand(step.Term, afterOperator: step.Operation == Operation.Subtract));
                continue;
            }

            switch (step.Operation)
            {
                case Operation.Add or Operation.Subtract:
                    row.Add(TermLayout(top, bottom));
                    row.Add(new MathText(step.Operation.Symbol(), IsOperator: true));
                    top = [LayoutOperand(step.Term, afterOperator: true)];
                    bottom = [];
                    break;
                case Operation.Multiply:
                    top.Add(LayoutOperand(step.Term, afterOperator: true));
                    break;
                case Operation.Divide:
                    bottom.Add(LayoutOperand(step.Term, afterOperator: true));
                    break;
            }
        }
        row.Add(TermLayout(top, bottom));
        return row.Count == 1 ? row[0] : new MathRow(row);
    }

    // One term: its factors joined by ×, as a fraction if anything divides it.
    // A lone bracket on top or underneath loses its brackets, since the bar
    // already groups it.
    private static MathNode TermLayout(List<MathNode> top, List<MathNode> bottom)
    {
        if (bottom.Count == 0) return Product(top);
        return new MathFraction(Unbracketed(Product(top)), Unbracketed(Product(bottom)));
    }

    // A lone negative number under the bar needs no brackets: "−3", not "(−3)".
    private static MathNode Unbracketed(MathNode node) => node switch
    {
        MathBrackets b => b.Inner,
        MathText { IsOperator: false } t when t.Text.StartsWith("(−") || t.Text.StartsWith("(-") => t with { Text = t.Text[1..^1] },
        _ => node,
    };

    private static MathNode Product(List<MathNode> factors)
    {
        if (factors.Count == 1) return factors[0];
        var items = new List<MathNode>();
        foreach (var factor in factors)
        {
            if (items.Count > 0) items.Add(new MathText("×", IsOperator: true));
            items.Add(factor);
        }
        return new MathRow(items);
    }

    private static MathNode LayoutOperand(Term term, bool afterOperator) => term switch
    {
        NumberTerm n => new MathText(afterOperator ? Bracketed(FormatOperand(n.Text)) : FormatOperand(n.Text)),
        GroupTerm g => new MathBrackets(Layout(g.Steps)),
        _ => throw new ArgumentOutOfRangeException(nameof(term)),
    };

    // Numbers are tidied the same way as results ("5.0" → "5") with a proper
    // minus sign; anything that doesn't parse is shown as the user typed it.
    private static string FormatOperand(string text)
    {
        double value = Parse(text);
        return double.IsNaN(value) ? text.Trim() : Format(value).Replace('-', '−');
    }

    // Negative numbers after an operation read better in brackets: "10 − (−3)".
    private static string Bracketed(string operand) =>
        operand.StartsWith('−') || operand.StartsWith('-') ? $"({operand})" : operand;

    /// <summary>Parses a cell's text; empty is 0, anything that isn't a finite number is NaN.</summary>
    public static double Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && double.IsFinite(value)
            ? value
            : double.NaN;
    }

    /// <summary>Formats to 12 significant digits, which hides floating-point noise like 0.1 + 0.2.</summary>
    public static string Format(double value)
    {
        if (!double.IsFinite(value)) return "Error";
        if (value == 0) return "0"; // avoids showing "-0"
        return value.ToString("G12", CultureInfo.InvariantCulture);
    }
}
