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
/// Evaluates a chain of cells. The chain starts at 0 and each cell applies its
/// operation to the running result of the cells above it. A cell with "(" starts
/// a bracket: its operation is held back and applied to the result of the
/// bracket, which starts from that cell's number and runs until a cell with ")"
/// or, if there is none, the end of the chain. Brackets can nest. A ")" with no
/// open bracket, and brackets on empty cells, are ignored. NaN marks an error
/// (bad input or division by zero) and carries through to the end of the chain.
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
    /// Returns the running result after each cell; the last one is the final
    /// result. Inside an open bracket, a cell's running result is what the chain
    /// would give if the bracket closed at that cell.
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
    /// Writes the chain as one expression, e.g. "(10 + 5) × 3 ÷ 9". The chain
    /// works top to bottom, but ordinary maths does × and ÷ before + and −, so
    /// the part before a × or ÷ is bracketed whenever it ends in + or −; that way
    /// the expression gives the same answer read either way. The user's own
    /// brackets are always shown. Empty cells are left out; text that isn't a
    /// number is shown as typed. Returns "" when every cell is empty.
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

    private static double Evaluate(List<Step> steps)
    {
        double result = 0;
        foreach (var step in steps)
        {
            double value = step.Term switch
            {
                NumberTerm n => Parse(n.Text),
                GroupTerm g => Evaluate(g.Steps),
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            };
            result = step.Operation.Apply(result, value);
        }
        return result;
    }

    private static string Render(List<Step> steps)
    {
        string expression = "";
        bool endsInAddOrSubtract = false;
        foreach (var step in steps)
        {
            string operand = step.Term switch
            {
                NumberTerm n => FormatOperand(n.Text),
                GroupTerm g => $"({Render(g.Steps)})",
                _ => throw new ArgumentOutOfRangeException(nameof(steps)),
            };
            bool addOrSubtract = step.Operation is Operation.Add or Operation.Subtract;

            if (expression.Length == 0)
            {
                // Each chain (and each bracket) starts at 0: "+ 7" is just 7 and
                // "− 7" is −7, but × and ÷ act on that 0, so it has to be shown.
                expression = step.Operation switch
                {
                    Operation.Add => operand,
                    Operation.Subtract => "−" + Bracketed(operand),
                    _ => $"0 {step.Operation.Symbol()} {Bracketed(operand)}",
                };
                endsInAddOrSubtract = false;
                continue;
            }

            if (!addOrSubtract && endsInAddOrSubtract) expression = $"({expression})";
            expression = $"{expression} {step.Operation.Symbol()} {Bracketed(operand)}";
            endsInAddOrSubtract = addOrSubtract;
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
