using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.SampleKit;

/// <summary>サンプル共通のウィンドウ構成（左: 設定 / 右上: 画像と操作 / 右下: ログ）。</summary>
/// <remarks>
/// <b>配置</b>: 左の設定パネルは固定幅、右は上下 2 段にする。 上段が画像と操作、下段がログであり、
/// <b>境界はドラッグで上下に動かせる</b>（<see cref="GridSplitter"/>）。 ログは下段の領域全体へ広がる。<br/>
/// <b>設定の分け方</b>: 左は「プロパティ設定」（対象コントロールの公開プロパティ）と「サンプル設定」
/// （サンプル固有の操作）の 2 つの領域に分ける。<br/>
/// <b>ログ</b>: Serilog の出力を非同期に表示する（<see cref="SampleLogPanel"/>）。
/// </remarks>
public static class SampleShell
{
    /// <summary>設定パネルの幅（DIP）。</summary>
    public const double SettingsWidth = 420;

    /// <summary>ログの初期の高さ（DIP）。</summary>
    private const double LogInitialHeight = 220;

    /// <summary>ログの最小の高さ（DIP）。</summary>
    private const double LogMinHeight = 80;

    /// <summary>画像の最小の高さ（DIP）。</summary>
    private const double ContentMinHeight = 120;

    /// <summary>ウィンドウの最小サイズ（DIP）。</summary>
    private const double WindowMinWidth = 900;

    /// <summary>ウィンドウの最小の高さ（DIP）。</summary>
    private const double WindowMinHeight = 600;

    /// <summary>ウィンドウの中身（3 領域のレイアウト）を組み立てる。</summary>
    /// <param name="propertySettings">プロパティ設定の行。</param>
    /// <param name="sampleSettings">サンプル設定の行。</param>
    /// <param name="content">画像と操作（右上の領域）。</param>
    /// <param name="log">ログ（右下の領域）。</param>
    /// <returns>組み立てた要素（ウィンドウの <c>Content</c> へ設定する）。</returns>
    /// <exception cref="ArgumentNullException">必須の引数が null の場合。</exception>
    public static Control CreateContent(
        Control propertySettings, Control sampleSettings, Control content, SampleLogPanel log)
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
            Child = new ScrollViewer { Content = settingsBody },
            Padding = new Thickness(12),
        };

        var contentHost = new Border { Child = content, Padding = new Thickness(12) };
        var logHost = new Border { Child = log, Padding = new Thickness(12) };

        var right = new Grid();
        right.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star) { MinHeight = ContentMinHeight });
        right.RowDefinitions.Add(new RowDefinition(4, GridUnitType.Pixel));
        right.RowDefinitions.Add(new RowDefinition(LogInitialHeight, GridUnitType.Pixel) { MinHeight = LogMinHeight });

        Grid.SetRow(contentHost, 0);
        right.Children.Add(contentHost);

        var splitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Rows,
            Background = Brushes.Transparent,
        };
        Grid.SetRow(splitter, 1);
        right.Children.Add(splitter);

        Grid.SetRow(logHost, 2);
        right.Children.Add(logHost);

        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition(SettingsWidth, GridUnitType.Pixel));
        root.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        root.Children.Add(settingsHost);
        Grid.SetColumn(right, 1);
        root.Children.Add(right);
        return root;
    }

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
        Control propertySettings,
        Control sampleSettings,
        Control content,
        SampleLogPanel log,
        double width = 1400,
        double height = 900)
        => new()
        {
            Title = title,
            Width = width,
            Height = height,
            MinWidth = WindowMinWidth,
            MinHeight = WindowMinHeight,
            Content = CreateContent(propertySettings, sampleSettings, content, log),
        };
}
