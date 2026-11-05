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

public readonly record struct Cell(Operation Operation, string Text);

/// <summary>
/// Evaluates a chain of cells. The chain starts at 0 and each cell applies its
/// operation to the running result of the cells above it. NaN marks an error
/// (bad input or division by zero) and carries through to the end of the chain.
/// </summary>
public static class ChainEngine
{
    /// <summary>Returns the running result after each cell; the last one is the final result.</summary>
    public static IReadOnlyList<double> Evaluate(IReadOnlyList<Cell> cells)
    {
        var running = new double[cells.Count];
        double result = 0;
        for (int i = 0; i < cells.Count; i++)
        {
            // Empty cells pass the running result through unchanged.
            if (!string.IsNullOrWhiteSpace(cells[i].Text))
            {
                result = cells[i].Operation.Apply(result, Parse(cells[i].Text));
            }
            running[i] = result;
        }
        return running;
    }

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
