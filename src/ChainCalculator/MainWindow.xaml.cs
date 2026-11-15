using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChainCalculator;

public partial class MainWindow : Window
{
    private const int CellCount = 5;

    private sealed record Row(Button OperationButton, Button BracketButton, TextBox Input, TextBlock Running)
    {
        public Operation Operation { get; set; } = Operation.Add;
        public Bracket Bracket { get; set; } = Bracket.None;
    }

    private readonly List<Row> _rows = [];

    public MainWindow()
    {
        InitializeComponent();

        for (int i = 0; i < CellCount; i++)
        {
            _rows.Add(CreateRow(i));
        }

        Recalculate();
        Loaded += (_, _) => _rows[0].Input.Focus();
    }

    private Row CreateRow(int index)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) }); // 8 gap + 56 button
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var button = new Button { Style = (Style)FindResource("OperationButton"), Content = Operation.Add.Symbol(), Tag = "Operation" };
        button.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Operation, cell {index + 1}");
        var bracketButton = new Button
        {
            Style = (Style)FindResource("OperationButton"),
            Margin = new Thickness(8, 0, 0, 0),
            Content = Bracket.None.Symbol(),
            Tag = "Bracket",
            ToolTip = "Bracket: none, ( or )",
        };
        bracketButton.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Bracket, cell {index + 1}");
        var input = new TextBox { Style = (Style)FindResource("ValueBox") };
        input.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Cell {index + 1}");
        var running = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        Grid.SetColumn(button, 0);
        Grid.SetColumn(bracketButton, 1);
        Grid.SetColumn(input, 2);
        Grid.SetColumn(running, 3);
        grid.Children.Add(button);
        grid.Children.Add(bracketButton);
        grid.Children.Add(input);
        grid.Children.Add(running);

        RowsPanel.Children.Add(new Border { Style = (Style)FindResource("Row"), Child = grid });

        var row = new Row(button, bracketButton, input, running);
        button.Click += (_, _) =>
        {
            row.Operation = row.Operation.Next();
            button.Content = row.Operation.Symbol();
            Recalculate();
        };
        bracketButton.Click += (_, _) =>
        {
            row.Bracket = ChainEngine.NextBracket(CurrentCells(), index);
            bracketButton.Content = row.Bracket.Symbol();
            Recalculate();
        };
        input.TextChanged += (_, _) => Recalculate();
        return row;
    }

    private List<Cell> CurrentCells() => _rows.Select(r => new Cell(r.Operation, r.Input.Text, r.Bracket)).ToList();

    private void Recalculate()
    {
        var cells = CurrentCells();
        var running = ChainEngine.Evaluate(cells);
        var green = (Brush)FindResource("GreenBrush");
        var greenDim = (Brush)FindResource("GreenDimBrush");
        var error = (Brush)FindResource("ErrorBrush");

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            row.Input.Foreground = double.IsNaN(ChainEngine.Parse(row.Input.Text)) ? error : green;
            row.Running.Text = ChainEngine.Format(running[i]);
            row.Running.Foreground = double.IsNaN(running[i]) ? error : greenDim;
        }

        double result = running[^1];
        ResultText.Text = ChainEngine.Format(result);
        ResultText.Foreground = double.IsNaN(result) ? error : green;

        // Copy results pastes as one line of text, e.g. "240 ÷ (4 + 2) − 15 = 25";
        // on screen the same calculation shows divisions as fractions.
        string expression = ChainEngine.Expression(cells);
        _copyText = expression.Length == 0 ? ResultText.Text : $"{expression} = {ChainEngine.Format(result)}";
        ShowExpression(ChainEngine.Layout(cells), result, double.IsNaN(result) ? error : greenDim);
        System.Windows.Automation.AutomationProperties.SetName(ExpressionPanel, expression.Length == 0 ? "Calculation" : _copyText);

        // "Copied" only describes what's on the clipboard until something changes.
        CopyButton.Content = "Copy results";
    }

    private string _copyText = "0";

    // Shows "<layout> = <result>". The top-level terms go straight into the
    // wrap panel so a long calculation wraps between them.
    private void ShowExpression(MathNode? layout, double result, Brush brush)
    {
        ExpressionPanel.Children.Clear();
        if (layout is null) return;
        var items = layout is MathRow row ? row.Items : [layout];
        foreach (var item in items) ExpressionPanel.Children.Add(Draw(item, brush));
        ExpressionPanel.Children.Add(Draw(new MathText("=", IsOperator: true), brush));
        ExpressionPanel.Children.Add(Draw(new MathText(ChainEngine.Format(result)), brush));
    }

    private static FrameworkElement Draw(MathNode node, Brush brush)
    {
        switch (node)
        {
            case MathText text:
                return new TextBlock
                {
                    Text = text.Text,
                    Foreground = brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = text.IsOperator ? new Thickness(10, 0, 10, 0) : new Thickness(0),
                };
            case MathRow row:
                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (var item in row.Items) panel.Children.Add(Draw(item, brush));
                return panel;
            case MathBrackets brackets:
                var group = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                group.Children.Add(Draw(new MathText("("), brush));
                group.Children.Add(Draw(brackets.Inner, brush));
                group.Children.Add(Draw(new MathText(")"), brush));
                return group;
            case MathFraction fraction:
                var numerator = Draw(fraction.Numerator, brush);
                var denominator = Draw(fraction.Denominator, brush);
                numerator.HorizontalAlignment = denominator.HorizontalAlignment = HorizontalAlignment.Center;
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 2, 2, 2) };
                stack.Children.Add(numerator);
                // The bar spans the wider of numerator and denominator, with a little overhang.
                stack.Children.Add(new Border { Height = 2, Background = brush, Margin = new Thickness(-2, 3, -2, 3) });
                stack.Children.Add(denominator);
                return stack;
            default:
                throw new ArgumentOutOfRangeException(nameof(node));
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        string text = _copyText;
        try
        {
            Clipboard.SetDataObject(text, copy: true);
            CopyButton.Content = "Copied";
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Another app has the clipboard open; leave the label as it is so
            // it doesn't claim a copy that didn't happen.
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.Operation = Operation.Add;
            row.OperationButton.Content = Operation.Add.Symbol();
            row.Bracket = Bracket.None;
            row.BracketButton.Content = Bracket.None.Symbol();
            row.Input.Text = string.Empty;
        }
        Recalculate();
        _rows[0].Input.Focus();
    }
}
