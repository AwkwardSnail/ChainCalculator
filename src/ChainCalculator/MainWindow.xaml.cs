using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ChainCalculator;

public partial class MainWindow : Window
{
    private const int CellCount = 5;

    private sealed record Row(Button OperationButton, TextBox Input, TextBlock Running)
    {
        public Operation Operation { get; set; } = Operation.Add;
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
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var button = new Button { Style = (Style)FindResource("OperationButton"), Content = Operation.Add.Symbol() };
        var input = new TextBox { Style = (Style)FindResource("ValueBox") };
        input.SetValue(System.Windows.Automation.AutomationProperties.NameProperty, $"Cell {index + 1}");
        var running = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        Grid.SetColumn(button, 0);
        Grid.SetColumn(input, 1);
        Grid.SetColumn(running, 2);
        grid.Children.Add(button);
        grid.Children.Add(input);
        grid.Children.Add(running);

        RowsPanel.Children.Add(new Border { Style = (Style)FindResource("Row"), Child = grid });

        var row = new Row(button, input, running);
        button.Click += (_, _) =>
        {
            row.Operation = row.Operation.Next();
            button.Content = row.Operation.Symbol();
            Recalculate();
        };
        input.TextChanged += (_, _) => Recalculate();
        return row;
    }

    private void Recalculate()
    {
        var running = ChainEngine.Evaluate(_rows.Select(r => new Cell(r.Operation, r.Input.Text)).ToList());
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
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.Operation = Operation.Add;
            row.OperationButton.Content = Operation.Add.Symbol();
            row.Input.Text = string.Empty;
        }
        Recalculate();
        _rows[0].Input.Focus();
    }
}
