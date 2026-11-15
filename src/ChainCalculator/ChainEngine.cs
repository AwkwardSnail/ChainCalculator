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

    /// <summary>The bracket the button moves to on the next click.</summary>
    public static Bracket Next(this Bracket bracket) =>
        (Bracket)(((int)bracket + 1) % Enum.GetValues<Bracket>().Length);
}

public readonly record struct Cell(Operation Operation, string Text, Bracket Bracket = Bracket.None);

/// <summary>
/// Evaluates the cells as one sum, read top to bottom, using normal maths order:
/// × and ÷ before + and −, so 1 + 1 + 1 ÷ 2 is 2.5. The sum starts at 0, so
/// the first cell's operation applies to 0. A cell with "(" starts a bracket
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

    // Evaluates "0 op1 t1 op2 t2 …" with × and ÷ before + and −: + and − start
    // a new term, × and ÷ work on the current term, and the terms are summed.
    private static double Evaluate(List<Step> steps)
    {
        double sum = 0;
        double term = 0;   // the leading 0 the sum starts from
        foreach (var step in steps)
        {
            double value = step.Term switch
            {
                NumberTerm n => Parse(n.Text),
                GroupTerm g => Evaluate(g.Steps),
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            };
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
                // Each sum (and each bracket) starts at 0: "+ 7" is just 7 and
                // "− 7" is −7, but × and ÷ act on that 0, so it has to be shown.
                expression = step.Operation switch
                {
                    Operation.Add => operand,
                    Operation.Subtract => "−" + Bracketed(operand),
                    _ => $"0 {step.Operation.Symbol()} {Bracketed(operand)}",
                };
                continue;
            }
            expression = $"{expression} {step.Operation.Symbol()} {Bracketed(operand)}";
        }
        return expression;
    }

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
