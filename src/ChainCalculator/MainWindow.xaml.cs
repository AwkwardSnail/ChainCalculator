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
            row.Bracket = row.Bracket.Next();
            bracketButton.Content = row.Bracket.Symbol();
            Recalculate();
        };
        input.TextChanged += (_, _) => Recalculate();
        return row;
    }

    private void Recalculate()
    {
        var cells = _rows.Select(r => new Cell(r.Operation, r.Input.Text, r.Bracket)).ToList();
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

        string expression = ChainEngine.Expression(cells);
        ExpressionText.Text = expression.Length == 0 ? "" : $"{expression} = {ChainEngine.Format(result)}";
        ExpressionText.Foreground = double.IsNaN(result) ? error : greenDim;

        // "Copied" only describes what's on the clipboard until something changes.
        CopyButton.Content = "Copy results";
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        // The whole calculation, e.g. "240 ÷ (4 + 2) − 15 = 25", or just the
        // result when every cell is empty.
        string text = ExpressionText.Text.Length > 0 ? ExpressionText.Text : ResultText.Text;
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
