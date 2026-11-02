using Aprillz.MewUI;
using Aprillz.MewUI.Controls;

namespace EsUtil.Ui.MewUI.SampleKit;

/// <summary>サンプル共通のウィンドウ構成（左: 設定 / 右上: 画像と操作 / 右下: ログ）。</summary>
/// <remarks>
/// <b>配置</b>: 左の設定パネルは固定幅、右は上下 2 段にする。 上段が画像と操作、下段がログであり、
/// <b>境界はドラッグで上下に動かせる</b>（<see cref="SplitPanel"/>）。 ログは下段の領域全体へ広がる。<br/>
/// <b>設定の分け方</b>: 左は「プロパティ設定」（対象コントロールの公開プロパティ）・「API操作」
/// （対象コントロールのメソッド実行）・「サンプル設定」（サンプルアプリの外観と状態の変更）の 3 つの領域に分ける。<br/>
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

    /// <summary>ウィンドウを組み立てる。</summary>
    /// <param name="title">ウィンドウのタイトル。</param>
    /// <param name="propertySettings">プロパティ設定の行。</param>
    /// <param name="methodSettings">API操作の行。 null の場合は領域を作らない。</param>
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
        UIElement? methodSettings,
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

        // 表示テーマはサンプル共通の設定（WinForms はテーマを持たないため本クラスを使わない）
        var themeField = SampleSettings.ChoiceRow(
            "表示テーマ",
            [.. SampleTheme.Choices],
            SampleTheme.CurrentIndex(),
            index =>
            {
                var isDark = SampleTheme.IsDarkAt(index);
                SampleTheme.Apply(isDark);
            });

        // 表示テーマはサンプル設定の先頭へ置く（サンプル固有の操作より前）
        var sampleBody = new StackPanel().Vertical().Spacing(6);
        sampleBody.Children(themeField.Element, sampleSettings);

        var settingsBody = new StackPanel().Vertical().Spacing(12);
        settingsBody.Children(SampleSettings.Area("プロパティ設定", propertySettings));

        // API操作は該当するメソッドを持つコントロールだけに出す（無いサンプルは領域ごと作らない）
        if (methodSettings is not null)
        {
            settingsBody.Children(
                SampleSettings.Separator(),
                SampleSettings.Area("API操作", methodSettings));
        }

        settingsBody.Children(
            SampleSettings.Separator(),
            SampleSettings.Area("サンプル設定", sampleBody));

        var settingsHost = new Border
        {
            Child = new ScrollViewer().Content(settingsBody),
            Padding = new Thickness(12),
        };

        var contentHost = new Border { Child = content, Padding = new Thickness(12) };
        var logHost = new Border { Child = log, Padding = new Thickness(12) };

        var split = new SplitPanel
        {
            Orientation = Orientation.Vertical,
            First = contentHost,
            Second = logHost,
            FirstLength = GridLength.Star,
            SecondLength = GridLength.Pixels(LogInitialHeight),
            MinFirst = ContentMinHeight,
            MinSecond = LogMinHeight,
        };

        var root = new Grid().Columns($"{SettingsWidth},*").Rows("*");
        root.Add(settingsHost.Column(0));
        root.Add(split.Column(1));

        var window = new Window()
            .Title(title)
            .Resizable(width, height, minWidth: 900, minHeight: 600);
        window.Content(root);

        // 地色もテーマへ追随させる。 MewUI の Background は色の値を持ち、テーマを切り替えても自動では変わらない。
        // 追随させないと、テーマを切り替えたあとも内容領域（子が背景を塗らない領域）がライトのまま残る（実測 2026-11-01）。
        // 併せて、MewUI は Application.Run までテーマを読めないため初期化前の選択がライトになる点も直す
        // （初期化後（Loaded）とテーマの切り替わり（ThemeChanged）に、表示を実際のテーマへ合わせる）。
        void ApplyTheme(Theme theme)
        {
            window.Background = theme.Palette.WindowBackground;
            settingsHost.Background = theme.Palette.ContainerBackground;
            contentHost.Background = theme.Palette.ContainerBackground;
            logHost.Background = theme.Palette.ContainerBackground;
            themeField.SetValue(SampleTheme.CurrentIndex());
        }

        window.Loaded += () => ApplyTheme(Application.Current.Theme);
        window.ThemeChanged += (_, newTheme) => ApplyTheme(newTheme);
        return window;
    }
}
