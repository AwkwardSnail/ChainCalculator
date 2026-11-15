// Renders the Microsoft Store screenshots for Chain Calc from the real app window.
//
//   dotnet run --project tools/StoreScreenshots [output folder]
//
// Each screenshot fills in the cells, picks the operations, then draws the
// window's content at 2x onto a 2560x1440 canvas with a caption. The app is
// drawn as vectors, so it stays sharp at Store resolution.

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ChainCalculator;

internal static class Program
{
    private const int CanvasWidth = 2560;
    private const int CanvasHeight = 1440;
    private const double AppScale = 2.0;

    private sealed record Shot(string FileName, string Caption, (int Clicks, string Text)[] Cells);

    // Clicks is how many times to press a cell's operation button: 0 +, 1 −, 2 ×, 3 ÷.
    // Text starting with "(" or ending with ")" sets that cell's bracket button instead.
    private static readonly Shot[] Shots =
    [
        new("01-chain.png", "Every cell picks its own operation",
            [(0, "(10"), (0, "5)"), (2, "3"), (3, "9"), (0, "")]),
        new("02-running.png", "See the running result at every step",
            [(0, "1200"), (3, "12"), (2, "1.1"), (0, "25"), (1, "35")]),
        new("03-decimals.png", "Five cells, one result",
            [(0, "(19.99"), (2, "3"), (1, "4.97)"), (3, "2"), (0, "0.5")]),
        new("04-brackets.png", "Brackets when you need them",
            [(0, "240"), (3, "(4"), (0, "2)"), (1, "15"), (0, "")]),
    ];

    [STAThread]
    private static void Main(string[] args)
    {
        string outDir = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine("packaging", "store-screenshots"));
        Directory.CreateDirectory(outDir);

        // Loads App.xaml's brushes and styles without starting the app.
        var app = new App();
        app.InitializeComponent();

        foreach (var shot in Shots)
        {
            var window = new MainWindow { ShowActivated = false, Left = -10000, Top = -10000 };
            window.Show();
            Fill(window, shot.Cells);
            window.UpdateLayout();

            string path = Path.Combine(outDir, shot.FileName);
            Save(Compose((FrameworkElement)window.Content, shot.Caption), path);
            window.Close();
            Console.WriteLine(path);
        }
    }

    private static void Fill(MainWindow window, (int Clicks, string Text)[] cells)
    {
        var boxes = Descendants<TextBox>(window).ToList();
        var opButtons = Descendants<Button>(window).Where(b => "Operation".Equals(b.Tag)).ToList();
        var bracketButtons = Descendants<Button>(window).Where(b => "Bracket".Equals(b.Tag)).ToList();
        var click = new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent);
        for (int i = 0; i < cells.Length; i++)
        {
            for (int c = 0; c < cells[i].Clicks; c++) opButtons[i].RaiseEvent(click);

            // The bracket button's order depends on the cells above, so click until it shows the wanted one.
            string text = cells[i].Text;
            string bracket = text.StartsWith('(') ? "(" : text.EndsWith(')') ? ")" : "";
            for (int c = 0; c < 3 && (string)bracketButtons[i].Content != bracket; c++) bracketButtons[i].RaiseEvent(click);
            boxes[i].Text = text.Trim('(', ')');
        }
        // Let layout and rendering catch up before the window is captured.
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        // The window focuses the first cell on load; drop it so no cell shows a caret or highlight.
        FocusManager.SetFocusedElement(window, null);
        Keyboard.ClearFocus();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static DrawingVisual Compose(FrameworkElement content, string caption)
    {
        var resources = Application.Current.Resources;
        var background = (Brush)resources["BackgroundBrush"];
        var green = (Brush)resources["GreenBrush"];

        var typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var text = new FormattedText(caption, System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, 64, green, 1.0);

        double appWidth = content.ActualWidth * AppScale;
        double appHeight = content.ActualHeight * AppScale;
        double gap = 72;
        double top = (CanvasHeight - (text.Height + gap + appHeight)) / 2;

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(background, null, new Rect(0, 0, CanvasWidth, CanvasHeight));
            dc.DrawText(text, new Point((CanvasWidth - text.Width) / 2, top));
            var brush = new VisualBrush(content) { Stretch = Stretch.Fill };
            dc.DrawRectangle(brush, null, new Rect((CanvasWidth - appWidth) / 2, top + text.Height + gap, appWidth, appHeight));
        }
        return visual;
    }

    private static void Save(Visual visual, string path)
    {
        var bitmap = new RenderTargetBitmap(CanvasWidth, CanvasHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}
