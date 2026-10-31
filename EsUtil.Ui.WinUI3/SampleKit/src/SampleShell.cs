using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.SampleKit;

/// <summary>サンプル共通のウィンドウ構成（左: 設定 / 右上: 画像と操作 / 右下: ログ）。</summary>
/// <remarks>
/// <b>配置</b>: 左の設定パネルは固定幅、右は上下 2 段にする。 上段が画像と操作、下段がログであり、
/// <b>境界はドラッグで上下に動かせる</b>（WinUI 3 は <c>GridSplitter</c> を持たないため、境界の要素が
/// 行の高さを書き換える）。 ログは下段の領域全体へ広がる。<br/>
/// <b>設定の分け方</b>: 左は「プロパティ設定」（対象コントロールの公開プロパティ）と「サンプル設定」
/// （サンプル固有の操作）の 2 つの領域に分ける。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogPanel"/>）。
/// </remarks>
public static class SampleShell
{
    /// <summary>設定パネルの幅（DIP）。</summary>
    public const double SettingsWidth = 380;

    /// <summary>ログの初期の高さ（DIP）。</summary>
    private const double LogInitialHeight = 220;

    /// <summary>ログの最小の高さ（DIP）。</summary>
    private const double LogMinHeight = 80;

    /// <summary>画像の最小の高さ（DIP）。</summary>
    private const double ContentMinHeight = 120;

    /// <summary>境界の太さ（DIP）。</summary>
    private const double SplitterThickness = 6;

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <param name="title">ウィンドウのタイトル。</param>
    /// <param name="propertySettings">プロパティ設定の行。</param>
    /// <param name="sampleSettings">サンプル設定の行。</param>
    /// <param name="content">画像と操作（右上の領域）。</param>
    /// <param name="log">ログ（右下の領域）。</param>
    /// <param name="width">ウィンドウの初期幅（DIP）。</param>
    /// <param name="height">ウィンドウの初期高さ（DIP）。</param>
    /// <returns>組み立てたウィンドウ。</returns>
    /// <exception cref="ArgumentNullException">必須の引数が null の場合。</exception>
    public static Window Create(
        string title,
        UIElement propertySettings,
        UIElement sampleSettings,
        UIElement content,
        SampleLogPanel log,
        double width = 1400,
        double height = 900)
    {
        ArgumentNullException.ThrowIfNull(propertySettings);
        ArgumentNullException.ThrowIfNull(sampleSettings);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(log);

        var settingsBody = new StackPanel { Spacing = 12 };
        settingsBody.Children.Add(SampleSettings.Area("プロパティ設定", propertySettings));
        settingsBody.Children.Add(SampleSettings.Separator());
        settingsBody.Children.Add(SampleSettings.Area("サンプル設定", sampleSettings));

        var settingsHost = new Border
        {
            Padding = new Thickness(12),
            Child = new ScrollViewer { Content = settingsBody, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
        };

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = ContentMinHeight });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SplitterThickness) });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(LogInitialHeight), MinHeight = LogMinHeight });

        var contentHost = new Border { Padding = new Thickness(12), Child = content };
        right.Children.Add(contentHost);

        var splitter = CreateSplitter(right);
        Grid.SetRow(splitter, 1);
        right.Children.Add(splitter);

        var logHost = new Border { Padding = new Thickness(12), Child = log };
        Grid.SetRow(logHost, 2);
        right.Children.Add(logHost);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SettingsWidth) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(settingsHost);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);

        var window = new Window
        {
            Title = title,
            Content = root,
        };
        window.AppWindow.Resize(new Windows.Graphics.SizeInt32((int)width, (int)height));
        return window;
    }

    /// <summary>境界（ドラッグで上下の行の高さを変える要素）を作る。</summary>
    /// <param name="grid">対象のグリッド（3 行: 画像 / 境界 / ログ）。</param>
    /// <returns>境界の要素。</returns>
    private static Border CreateSplitter(Grid grid)
    {
        var splitter = new Border
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            Opacity = 0.4,
        };

        var dragging = false;
        var startY = 0.0;
        var startContent = 0.0;
        var startLog = 0.0;

        splitter.PointerPressed += (_, e) =>
        {
            dragging = splitter.CapturePointer(e.Pointer);
            startY = e.GetCurrentPoint(grid).Position.Y;
            startContent = grid.RowDefinitions[0].ActualHeight;
            startLog = grid.RowDefinitions[2].ActualHeight;
        };

        splitter.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            var delta = e.GetCurrentPoint(grid).Position.Y - startY;
            var content = startContent + delta;
            var log = startLog - delta;
            var total = startContent + startLog;

            // 最小の高さを割らない範囲へ収める（合計は変えない）
            content = Math.Clamp(content, ContentMinHeight, total - LogMinHeight);
            log = total - content;

            grid.RowDefinitions[0].Height = new GridLength(content);
            grid.RowDefinitions[2].Height = new GridLength(log);
        };

        void Release(PointerRoutedEventArgs e)
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            splitter.ReleasePointerCapture(e.Pointer);
        }

        splitter.PointerReleased += (_, e) => Release(e);
        splitter.PointerCaptureLost += (_, _) => dragging = false;
        return splitter;
    }
}
