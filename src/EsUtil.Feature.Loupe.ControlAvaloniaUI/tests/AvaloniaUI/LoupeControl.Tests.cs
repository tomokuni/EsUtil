using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;
using Xunit;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using DrawingColor = System.Drawing.Color;
using Point = Avalonia.Point;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Tests;

/// <summary>
/// <see cref="LoupeControl"/> の単体テスト（Source / CenterPoint の Binding 方式・バッチ化・表示分離・範囲補正）。
/// </summary>
/// <remarks>
/// Avalonia ヘッドレステストは並列実行非対応（Dispatcher.UIThread が共有）のため、
/// 同じコレクション（<see cref="AvaloniaHeadlessGroup"/>）内で直列実行する。
/// </remarks>
[Collection("AvaloniaHeadless")]
public class LoupeControlTests
{
    /// <summary>テスト用の LoupeControl を生成する（ヘッドレスで初期化）。</summary>
    private static LoupeControl CreateControl()
        => new() { Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 }, IsVisible = true };

    /// <summary>コントロールを親パネルへ載せてレイアウトを確定する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="size">親パネルの一辺のサイズ（DIP）。</param>
    /// <returns>配置に使った親パネル。</returns>
    /// <remarks>ラベル要素（子要素）は配置フェーズ（<c>ArrangeOverride</c>）で更新されるため、テストは配置を明示的に実行する。</remarks>
    private static Panel Layout(LoupeControl control, double size = 300)
    {
        var panel = new Panel { Width = size, Height = size };
        panel.Children.Add(control);
        panel.Measure(new Size(size, size));
        panel.Arrange(new Rect(0, 0, size, size));
        return panel;
    }

    /// <summary>表示用の画像要素（子要素のうち <see cref="Image"/>）を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示用の画像要素。</returns>
    private static Image ImageElement(LoupeControl control) => control.Children.OfType<Image>().Single();

    /// <summary>中心座標ラベルの要素（子要素のうち <see cref="Border"/>）を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>ラベルの背景要素。</returns>
    private static Border LabelElement(LoupeControl control) => control.Children.OfType<Border>().Single();

    /// <summary>中心座標ラベルの文言を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示されている文言。</returns>
    private static string LabelText(LoupeControl control) => ((TextBlock)LabelElement(control).Child!).Text!;

    // ====================================================================
    // 更新経路（バッチ化）
    // ====================================================================

    [AvaloniaFact]
    internal async Task SourceとCenterPointを設定するとLoupeBitmapが生成される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);

        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task EsBitmapAdapterをSourceに設定するとLoupeBitmapが生成される()
    {
        // EsBitmapAdapter（コアの画像を IImage として公開するアダプタ）を Source に設定した場合、
        // ルーペ画像が生成される（WriteableBitmap を生成しない経路の検証）。
        using var bmp = EsWritableBitmap.Create(200, 200);
        using var image = new EsBitmapAdapter(bmp);
        var control = CreateControl();
        control.Source = image;
        control.CenterPoint = new Point(100, 100);

        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task WriteableBitmapをSourceに設定するとLoupeBitmapが生成される()
    {
        // WriteableBitmap は Bitmap の派生であるため、Bitmap と同じ経路（CopyPixels による複製）で対応する。
        using var source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        var control = CreateControl();
        control.Source = source;
        control.CenterPoint = new Point(100, 100);

        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal void 対応可能なソース型の判定はWriteableBitmapとRenderTargetBitmapを含む()
    {
        // 判定は「Bitmap の派生かどうか」で決まる（実測: WriteableBitmap / RenderTargetBitmap は Bitmap の派生）。
        using var writeable = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(10, 10, Colors.Blue);
        using var target = new RenderTargetBitmap(new PixelSize(10, 10), new Vector(96, 96));

        Assert.True(LoupeSourceResolver.IsSourceSupported(writeable));
        Assert.True(LoupeSourceResolver.IsSourceSupported(target));

        // Bitmap の派生ではない IImage（CroppedBitmap は AvaloniaObject の派生）は非対応
        Assert.False(LoupeSourceResolver.IsSourceSupported(new CroppedBitmap(writeable, new PixelRect(0, 0, 5, 5))));
    }

    [AvaloniaFact]
    internal async Task 複数プロパティの連続変更はポスト後に1回の再描画に結合される()
    {
        var control = CreateControl();

        // ポスト前は同期生成されない（バッチ化: 要求は1つの Dispatcher ジョブに結合される）
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        Assert.Null(control.LoupeBitmap);

        await control.WaitForRenderedAsync();
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task Source未設定でも市松模様のLoupeBitmapが生成される()
    {
        var control = CreateControl();

        control.CenterPoint = new Point(50, 50);
        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.Equal(100, control.LoupeBitmap!.Size.Width);
        Assert.Equal(100, control.LoupeBitmap.Size.Height);
    }

    [AvaloniaFact]
    internal async Task Source設定_CenterPoint未設定でも市松模様のLoupeBitmapが生成される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);

        await control.WaitForRenderedAsync();

        // center が未設定のため、画像抽出は行わず市松模様のみ描画する
        Assert.NotNull(control.LoupeBitmap);
        Assert.Equal(100, control.LoupeBitmap!.Size.Width);
        Assert.Equal(100, control.LoupeBitmap.Size.Height);
    }

    [AvaloniaFact]
    internal async Task CenterPoint変更でLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();
        var first = control.LoupeBitmap;

        control.CenterPoint = new Point(120, 120);
        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task RequestRenderでLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();
        var first = control.LoupeBitmap;

        // プロパティ変更を伴わない再生成要求（生成完了は Rendered で通知される）
        control.RequestRender();
        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task 設定の変更でLoupeBitmapが再生成される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();
        var first = control.LoupeBitmap;

        control.Options = control.Options with { ZoomLevel = 4 };
        await control.WaitForRenderedAsync();

        Assert.NotNull(control.LoupeBitmap);
        Assert.NotSame(first, control.LoupeBitmap);
    }

    // ====================================================================
    // 表示制御（表示状態と再描画の分離）
    // ====================================================================

    [AvaloniaFact]
    internal async Task ClearでLoupeBitmapが解放される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        control.Clear();

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    internal async Task IsVisible_falseでLoupeBitmapが解放される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        // 表示状態（true）からの遷移でのみ Changed が発火して解放される
        // （AXAML 初期値が false のため、false→false では Changed は発火しない）
        control.IsVisible = true;
        control.IsVisible = false;

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    internal async Task 非表示中の再描画は画像を生成しない()
    {
        var control = CreateControl();
        control.IsVisible = false;

        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);

        // 非表示のため描画は要求されない（Rendered は発火しない）
        await TestHelper.PumpAsync();
        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);

        control.IsVisible = true;
        Assert.True(control.IsVisible);
    }

    [AvaloniaFact]
    internal async Task IsVisible_trueで非同期生成して表示する()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        control.IsVisible = false;
        control.IsVisible = true;
        await control.WaitForRenderedAsync();

        Assert.True(control.IsVisible);
        Assert.NotNull(control.LoupeBitmap);
    }

    // ====================================================================
    // 範囲検証（coercion）
    // ====================================================================

    [AvaloniaFact]
    internal void RequestedSize_範囲外は補正される()
    {
        var control = CreateControl();

        control.Options = control.Options with { RequestedSize = 0 };
        Assert.Equal(1, control.Options.RequestedSize);

        control.Options = control.Options with { RequestedSize = 5000 };
        Assert.Equal(4096, control.Options.RequestedSize);
    }

    [AvaloniaFact]
    internal void ZoomLevel_範囲外は補正される()
    {
        var control = CreateControl();

        control.Options = control.Options with { ZoomLevel = 0 };
        Assert.Equal(1, control.Options.ZoomLevel);

        control.Options = control.Options with { ZoomLevel = 100 };
        Assert.Equal(64, control.Options.ZoomLevel);
    }

    [AvaloniaFact]
    internal void BorderThickness_範囲外は補正される()
    {
        var control = CreateControl();

        control.Appearance = control.Appearance with { BorderThickness = 0 };

        Assert.Equal(1, control.Appearance.BorderThickness);
    }

    // ====================================================================
    // エラーハンドリング
    // ====================================================================

    [AvaloniaFact]
    internal async Task 未対応の画像型はログへ記録され描画を継続する()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = new CroppedBitmap(
            EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(100, 100, Colors.Blue), new PixelRect(0, 0, 10, 10));
        control.CenterPoint = new Point(5, 5);

        // 描画の例外（未対応型）はアプリを落とさずログへ記録される（開始側が fire-and-forget で例外を観測する）
        await TestHelper.PumpUntilAsync(() => sink.HasError);

        Assert.True(sink.HasError);
    }

    [AvaloniaFact]
    internal async Task RequestRender_生成失敗時は例外が観測されログへ記録される()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = new CroppedBitmap(
            EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(100, 100, Colors.Blue), new PixelRect(0, 0, 10, 10));
        control.CenterPoint = new Point(5, 5);

        // 明示的な再生成要求（RequestRender）経由の生成失敗 → 開始側の例外観測でログへ記録される
        control.RequestRender();
        await TestHelper.PumpUntilAsync(() => sink.HasError);

        Assert.True(sink.HasError);
    }

    // ====================================================================
    // 表示制御（生成中の非表示・非表示時の明示的操作）
    // ====================================================================

    [AvaloniaFact]
    internal async Task 生成中に非表示になると生成画像は破棄される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);

        // 生成（Dispatcher への反映待ち）中に非表示へ切り替えると、
        // 反映時に IsVisible=false のため生成ビットマップは破棄される
        control.IsVisible = false;
        await TestHelper.PumpAsync();

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    [AvaloniaFact]
    internal void RequestRender_非表示時はビットマップを解放する()
    {
        var control = CreateControl();
        control.IsVisible = false;
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);

        control.RequestRender();

        Assert.Null(control.LoupeBitmap);
        Assert.False(control.IsVisible);
    }

    // ====================================================================
    // 範囲検証（coercion）の追加分
    // ====================================================================

    [AvaloniaFact]
    internal void ClientToPixelScaling_範囲外は補正される()
    {
        var control = CreateControl();

        control.ClientToPixelScaling = 0.0;
        Assert.Equal(0.1, control.ClientToPixelScaling);

        control.ClientToPixelScaling = 10.0;
        Assert.Equal(8.0, control.ClientToPixelScaling);

        control.ClientToPixelScaling = double.NaN;
        Assert.Equal(1.0, control.ClientToPixelScaling);

        control.ClientToPixelScaling = double.PositiveInfinity;
        Assert.Equal(1.0, control.ClientToPixelScaling);
    }

    [AvaloniaFact]
    internal void 設定の補正は値の生成時に適用される()
    {
        var control = CreateControl();

        // プロパティのスタイル値を直接設定する（バインディングと同じ経路。 CLR プロパティの setter を通らない）
        // 数値項目の補正は値の型（LoupeOptions）が生成時に適用するため、
        // どの設定経路でも範囲内の値だけがコントロールへ渡る（UI 側に補正の登録を要求しない）。
        control.SetValue(LoupeControl.OptionsProperty, LoupeOptions.Default with
        {
            RequestedSize = 100_000,
            ZoomLevel = 1000,
        });
        control.SetValue(LoupeControl.AppearanceProperty, LoupeAppearance.Default with { BorderThickness = 1000 });
        control.SetValue(LoupeControl.ClientToPixelScalingProperty, 100.0);

        // 環境値（DPI）の補正はプロパティのメタデータ（coerce）へ登録する
        Assert.Equal(RequestedSizeMax, control.Options.RequestedSize);
        Assert.Equal(ZoomLevelMax, control.Options.ZoomLevel);
        Assert.Equal(ClientToPixelScalingMax, control.ClientToPixelScaling);
        Assert.Equal(BorderThicknessMax, control.Appearance.BorderThickness);
    }

    /// <summary>寸法の設定値が保持されることを検証する（色は描画の設定が単一所有する）。</summary>
    [AvaloniaFact]
    internal void 外観の設定値が保持される()
    {
        var control = CreateControl();

        control.Appearance = new LoupeAppearance
        {
            CrossColor = DrawingColor.Green,
            PasteboardColor = DrawingColor.Green,
            BorderColor = DrawingColor.Green,
            BorderThickness = 3,
        };

        Assert.Equal(3, control.Appearance.BorderThickness);
        Assert.Equal(DrawingColor.Green, control.Appearance.CrossColor);
        Assert.Equal(DrawingColor.Green, control.Appearance.PasteboardColor);
        Assert.Equal(DrawingColor.Green, control.Appearance.BorderColor);
    }

    [AvaloniaFact]
    internal void 依存プロパティのデフォルト値が正しい()
    {
        var control = new LoupeControl();

        Assert.Equal(160, control.Options.RequestedSize);
        Assert.Equal(4, control.Options.ZoomLevel);
        Assert.Equal(1.0, control.ClientToPixelScaling);

        // 寸法の既定値はコア（LoupeOptions）が単一所有する（両 UI で同一）
        Assert.Equal(2, control.Appearance.BorderThickness);
        // 色の既定値は描画の設定が単一所有する
        Assert.Equal(LoupeAppearance.Default, control.Appearance);
        Assert.Equal(DrawingColor.FromArgb(102, 255, 0, 0), control.Appearance.CrossColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0x53, 0x53, 0x53), control.Appearance.PasteboardColor);
        Assert.Equal(DrawingColor.FromArgb(255, 0xD3, 0xD3, 0xD3), control.Appearance.BorderColor);
    }

    // ====================================================================
    // 中心座標ラベル（文言・配置はコアが決め、本コントロールがテキスト要素として表示する）
    // ====================================================================

    [AvaloniaFact]
    internal void 座標ラベルのデフォルト値が正しい()
    {
        var control = new LoupeControl();

        // ルーペ座標ラベルは既定で表示（設定画面の現在値）。 色は描画の設定（白 / 黒 60% = #99000000）
        Assert.True(control.Appearance.ShowCoordLabel);
        Assert.Equal(DrawingColor.White, control.Appearance.CoordLabelColor);
        Assert.Equal(DrawingColor.FromArgb(153, 0, 0, 0), control.Appearance.CoordLabelBackgroundColor);
        Assert.Equal(12.0, control.Appearance.CoordLabelFontSize);
    }

    [AvaloniaFact]
    internal void 座標ラベルを有効化してCenterPointを設定するとラベル要素が表示される()
    {
        var control = CreateControl();
        control.Appearance = control.Appearance with { ShowCoordLabel = true };
        control.CenterPoint = new Point(123, 456);

        Layout(control);

        // 文言はコアの EsPoint.Format が単一所有する（本コントロールは要 素へ写すだけ）
        Assert.Equal("(123, 456)", LabelText(control));
    }

    [AvaloniaFact]
    internal void 座標ラベル無効時はCenterPointがあっても要素を表示しない()
    {
        var control = CreateControl();
        control.Appearance = control.Appearance with { ShowCoordLabel = false };
        control.CenterPoint = new Point(123, 456);

        Layout(control);

        // 計画は表示可否でも指示を消さないため、要素は置かれるが非表示になる（文言は保たれる）
        var border = Assert.Single(control.Children.OfType<Border>());
        Assert.False(border.IsVisible);
        Assert.Equal("(123, 456)", LabelText(control));
    }

    [AvaloniaFact]
    internal void 座標ラベルはルーペ左上の内側に実測幅で配置される()
    {
        var control = CreateControl();
        control.CenterPoint = new Point(12, 34);

        Layout(control);

        var label = LabelElement(control);

        // 背景の大きさはコアが「実測幅 + 内側余白」で決める（本コントロールは自 UI の API で実測して渡す）
        var text = new FormattedText(
            "(12, 34)", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface.Default, control.Appearance.CoordLabelFontSize, Brushes.White);
        Assert.True(label.Width > text.Width);

        // 左上（基準点はルーペの左上から 2 DIP。 描画層が配置を単一所有する）
        Assert.Equal(2.0, label.Margin.Left, 1);
        Assert.Equal(2.0, label.Margin.Top, 1);
    }

    [AvaloniaFact]
    internal void 座標ラベルの外観が要素へ反映される()
    {
        var control = CreateControl();
        var background = DrawingColor.FromArgb(179, 0, 0, 139); // 暗青 70%
        control.Appearance = control.Appearance with
        {
            ShowCoordLabel = true,
            CoordLabelColor = DrawingColor.Yellow,
            CoordLabelBackgroundColor = background,
            CoordLabelFontSize = 16,
            CoordLabelCornerRadius = 8,
        };
        control.CenterPoint = new Point(10, 20);

        Layout(control);

        var border = LabelElement(control);
        var text = (TextBlock)border.Child!;

        Assert.Equal(16.0, text.FontSize);
        Assert.Equal(8, border.CornerRadius.TopLeft);

        // 色は UI のブラシへ変換して反映する
        Assert.Equal(DrawingColor.Yellow.ToAvaloniaColor(), ((SolidColorBrush)text.Foreground!).Color);
        Assert.Equal(background.ToAvaloniaColor(), ((SolidColorBrush)border.Background!).Color);
    }

    [AvaloniaFact]
    internal async Task CenterPointを変更するとラベル要素の文言が追従する()
    {
        // 配置の無効化（InvalidateArrange）が実際のレイアウト パスで反映されることを検証する
        // （コントロールを載せた状態でレイアウト パスを走らせる）。
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();

        control.CenterPoint = new Point(1, 2);
        await TestHelper.PumpAsync();
        Assert.Equal("(1, 2)", LabelText(control));

        control.CenterPoint = new Point(3, 4);
        await TestHelper.PumpAsync();

        Assert.Equal("(3, 4)", LabelText(control));
        window.Close();
    }

    // ====================================================================
    // 表示位置・サイズ（UpdatePosition）
    // ====================================================================

    [AvaloniaFact]
    internal void PositionOffsetを設定すると表示位置が更新される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // オフセット 0 → カーソル位置（50,50）に表示
        var before = control.RenderTransform;

        control.PositionOffset = 20;

        // オフセット 20 → カーソル右下 +20 に表示（RenderTransform は同一インスタンスで値が更新される）
        Assert.Equal(20, control.PositionOffset);
        Assert.NotNull(control.RenderTransform);
        Assert.Same(before, control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    internal void SetCoordTransformを設定すると表示位置が更新される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(100, 100);

        // 対象 = DIP × 2（拡大）→ 中心 (100,100) は DIP (50,50) に変換される
        control.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X * 2, p.Y * 2),
            p => new EsPoint(p.X / 2, p.Y / 2),
            () => new EsSize(300, 300)));

        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    internal void SetCoordTransform_nullで等価変換にフォールバックする()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // null 設定 → 等価変換にフォールバック（例外なく表示位置が計算される）
        control.SetCoordTransform(null);

        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    internal void 親パネルサイズ0でも表示位置計算が動作する()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 0, Height = 0 }; // サイズ 0 → クランプをスキップ
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // サイズ 0 ではクランプせず基本サイズのまま表示位置を計算する（例外なし）
        control.PositionOffset = 10;
        Assert.NotNull(control.RenderTransform);
        window.Close();
    }

    [AvaloniaFact]
    internal void 親パネルより大きいRequestedSizeは縮小しない()
    {
        var control = CreateControl();
        control.Options = control.Options with { RequestedSize = 400 }; // パネル (300,300) より大きい
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);

        // 要求サイズは領域に合わせて縮小しない（要素サイズは要求サイズのまま）
        Assert.Equal(400, control.Width);
        Assert.Equal(400, control.Height);

        // 親領域が収められないため配置もしない（コアが位置を返さない）
        Assert.Null(control.RenderTransform);
        window.Close();
    }

    /// <summary>親パネルへ接続した後に RequestedSize を変更しても表示サイズと描画サイズが一致することを検証する。</summary>
    /// <remarks>
    /// 表示サイズの決定（要求サイズの適用と、親未接続時の扱い）は 1 箇所で行う。<br/>
    /// 複数箇所で書き込むと、要求サイズの変更直後に古い値で描画される（表示サイズと描画ビットマップが食い違う）。
    /// </remarks>
    [AvaloniaFact]
    internal async Task 親パネル接続後のRequestedSize変更でも表示サイズと描画サイズが一致する()
    {
        var control = new LoupeControl { Options = LoupeOptions.Default with { RequestedSize = 400, ZoomLevel = 2 }, IsVisible = true }; // パネル (300,300) より大きい
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(50, 50);
        await control.WaitForRenderedAsync();
        Assert.Equal(400, control.LoupeBitmap!.Size.Width);

        // 接続後にさらに大きい要求へ変更し、レイアウト（Arrange）を待たずに描画する
        control.Options = control.Options with { RequestedSize = 500 };
        await control.WaitForRenderedAsync();

        // 表示サイズ（要求サイズ）と描画ビットマップの一辺（物理ピクセル）が一致する
        Assert.Equal(500, control.LoupeBitmap!.Size.Width);
        window.Close();
    }

    [AvaloniaFact]
    internal void RefreshPosition_最新のポートで表示位置が再計算される()
    {
        var control = CreateControl();
        var panel = new Panel { Width = 300, Height = 300 };
        panel.Children.Add(control);
        var window = new Window { Content = panel };
        window.Show();
        control.CenterPoint = new Point(100, 100);

        // 変換係数を後から変更できるよう、可変な係数を使う（パン・ズーム等の動的変化の模擬）
        var factor = 2.0;
        control.SetCoordTransform(new CoordTransform(
            p => new EsPoint(p.X * factor, p.Y * factor),
            p => new EsPoint(p.X / factor, p.Y / factor),
            () => new EsSize(300, 300)));
        var before = control.RenderTransform;

        // 係数を変更して RefreshPosition → 表示位置（RenderTransform）が再計算される
        factor = 4.0;
        control.RefreshPosition();

        Assert.NotNull(control.RenderTransform);
        Assert.Same(before, control.RenderTransform);
        window.Close();
    }

    // ====================================================================
    // 破棄済み Bitmap（ObjectDisposedException 分岐）
    // ====================================================================

    [AvaloniaFact]
    internal async Task 破棄済みSourceは市松模様のみで描画される()
    {
        var control = CreateControl();
        var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.Source = bitmap;
        control.CenterPoint = new Point(100, 100);
        bitmap.Dispose(); // 破棄済み Bitmap（生存検証で ObjectDisposedException）

        await control.WaitForRenderedAsync();

        // 例外なく完了し、市松模様のみの Bitmap が生成される
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task 抽出中にSourceが破棄されると市松模様のみで描画される()
    {
        var control = CreateControl();
        var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.Source = bitmap;
        control.CenterPoint = new Point(100, 100);

        // 描画要求の反映前にソースを破棄すると、ソース解決が破棄済みを検知して切り出しを行わない
        // （市松模様のみで描画され、例外は描画層へ伝播しない）
        bitmap.Dispose();
        await control.WaitForRenderedAsync();

        // 例外なく完了し、市松模様のみの Bitmap が生成される
        Assert.NotNull(control.LoupeBitmap);
    }

    [AvaloniaFact]
    internal async Task 生成後に表示用画像のソースが破棄されず有効に保持される()
    {
        // 回帰テスト: SetLoupeBitmap が using で即座に破棄した WriteableBitmap を Image.Source に
        // 設定すると、レイアウトパス（Image.MeasureOverride → Bitmap.get_Size）で ObjectDisposedException が
        // 発生し、領域選択開始時にアプリがハングする。 表示用画像（EsBitmapAdapter）はコントロールが所有し、
        // 内包する EsWritableBitmap が有効なまま保持されることを検証する。
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        // Source が破棄済みでないこと（Size 参照で ObjectDisposedException が発生しない）
        var image = ImageElement(control);
        Assert.NotNull(image.Source);
        Assert.True(image.Source is EsBitmapAdapter img
            && img.GetEsBitmap() is { } bmp && bmp.Size.Width > 0);
    }

    [AvaloniaFact]
    internal async Task Clear後に表示用画像のソースが解放される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        // Clear で表示用 Bitmap と Image.Source の両方が解放される（破棄済み Bitmap の残留防止）
        control.Clear();

        Assert.Null(control.LoupeBitmap);
        Assert.Null(ImageElement(control).Source);
    }

    [AvaloniaFact]
    internal void Sourceを再設定すると変更通知される()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);

        // 再設定（OldValue が null でない）→ 変更ログの両分岐が実行される
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(50, 50);
        control.Options = control.Options with { RequestedSize = 120 };
        control.ClientToPixelScaling = 1.5;
        control.Options = control.Options with { ZoomLevel = 8 };

        Assert.NotNull(control.Source);
    }

    [AvaloniaFact]
    internal async Task 実ロガー設定時に全ログメソッドが実行される()
    {
        var control = CreateControl();
        control.Log = new TestHelper.ListLogger();

        // 初回設定（OldValue = null）と再設定（NewValue あり）の両経路で LogSettingChanged を実行する
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);

        // null 設定（NewValue = null）で LogSettingChanged の null 分岐を実行する
        control.Source = null;
        control.CenterPoint = null;

        // 描画を実行して描画パイプラインのログ（LoupeLogMessage）を出力する
        await control.WaitForRenderedAsync();
        Dispatcher.UIThread.RunJobs();
        control.Clear();
    }

    [AvaloniaFact]
    internal void CoordLabelFontSize_NaNは12に補正される()
    {
        var control = new LoupeControl();

        control.Appearance = control.Appearance with { CoordLabelFontSize = double.NaN };

        Assert.Equal(12.0, control.Appearance.CoordLabelFontSize);
    }

    [AvaloniaFact]
    internal async Task 描画完了でRenderedイベントが発火し描画済みBitmapと表示情報を通知する()
    {
        var control = CreateControl();
        LoupeRenderedEventArgs? args = null;
        control.Rendered += (_, e) => args = e;

        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        // RequestedSize=100 / ZoomLevel=2 / ClientToPixelScaling=1.0 の場合:
        // pixelSize = ceil(100×1.0) = 100, captureSize = ceil(100/2) = 50
        // CaptureTopLeft = (100−25, 100−25) = (75, 75), ZoomLevel = 2
        Assert.NotNull(args);
        Assert.NotNull(args!.Bitmap);
        Assert.Equal(100, args.Bitmap.Size.Width);
        Assert.Equal(100, args.Bitmap.Size.Height);
        Assert.Equal(new EsPoint(75, 75), args.Content.CaptureTopLeft);
        Assert.Equal(2, args.Content.ZoomLevel);
    }

    [AvaloniaFact]
    internal async Task 描画完了通知のスケールはDisplayScalingに依存しない()
    {
        // 破線オーバーレイの位置が DPI でずれないことの回帰テスト。
        // 描画は切り出し領域を ZoomLevel 倍で描くため、拡大率は常に ZoomLevel と同値になる
        // （DIP 表示サイズを物理ピクセルで割った値ではない）。
        var control = CreateControl();
        control.ClientToPixelScaling = 1.5;
        LoupeRenderedEventArgs? args = null;
        control.Rendered += (_, e) => args = e;

        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        Assert.NotNull(args);
        // RequestedSize=100 / ZoomLevel=2 / ClientToPixelScaling=1.5 → pixelSize=150, captureSize=75
        Assert.Equal(150, args!.Bitmap!.Size.Width);
        Assert.Equal(2, args.Content.ZoomLevel);
    }

    [AvaloniaFact]
    internal async Task 中心未設定でもRenderedイベントが発火し市松模様のBitmapとnullのCaptureTopLeftを通知する()
    {
        var control = CreateControl();
        LoupeRenderedEventArgs? args = null;
        control.Rendered += (_, e) => args = e;

        // CenterPoint 未設定（市松模様のみの描画）でもイベントは発火する
        await control.WaitForRenderedAsync();

        Assert.NotNull(args!);
        Assert.NotNull(args.Bitmap);
        Assert.Null(args.Content.CaptureTopLeft);
    }

    [AvaloniaFact]
    internal async Task 再描画のたびにRenderedイベントが発火する()
    {
        var control = CreateControl();
        control.Source = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(200, 200, Colors.Blue);
        control.CenterPoint = new Point(100, 100);
        await control.WaitForRenderedAsync();

        var count = 0;
        control.Rendered += (_, _) => count++;

        control.CenterPoint = new Point(120, 120);
        await control.WaitForRenderedAsync();

        Assert.Equal(1, count);
    }

    // ====================================================================
    // LoupeSourceResolver.IsSourceSupported（対応可能型の判定・ヘッドレス要）
    // ====================================================================

    [AvaloniaFact]
    internal void IsSourceSupported_Bitmapは対応可能である()
    {
        // WriteableBitmap（Avalonia.Media.Imaging.Bitmap のサブクラス）は対応可能。
        // WriteableBitmap 生成はヘッドレス（IPlatformRenderInterface）を必要とするため、[AvaloniaFact] で検証する。
        using var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(10, 10, Colors.White);

        Assert.True(LoupeSourceResolver.IsSourceSupported(bitmap));
    }

    [AvaloniaFact]
    internal void IsSourceSupported_未対応型は非対応である()
    {
        // CroppedBitmap は Bitmap を継承しないため、未対応型（非対応）として判定される。
        using var bitmap = EsBitmapAvaloniaExtensions.CreateAvaloniaWriteableBitmap(10, 10, Colors.White);
        using var cropped = new CroppedBitmap(bitmap, new PixelRect(0, 0, 5, 5));

        Assert.False(LoupeSourceResolver.IsSourceSupported(cropped));
    }
}
