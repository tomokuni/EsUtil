using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.AvaloniaUI.Interop;
using EsUtil.Ui.AvaloniaUI.Interop.Imaging;
using Xunit;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using DrawingColor = System.Drawing.Color;
using Point = Avalonia.Point;

namespace EsUtil.Ui.AvaloniaUI.Loupe.Tests;

/// <summary><see cref="LoupeControl"/> の単体テスト（Source / CenterPoint の Binding 方式・バッチ化・表示分離・範囲補正）。</summary>
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

    /// <summary>装飾を止めたテスト用の LoupeControl を生成する（画素の検証で装飾と混ざらないようにする）。</summary>
    /// <param name="source">拡大元の画像（コア画像）。 null の場合は画像なし（下地のみ）。</param>
    /// <param name="centerPoint">拡大中心。 null の場合は未設定。</param>
    /// <param name="display">表示用の射影（省略可）。 渡すと切り出しの複製を作らない経路になる。</param>
    /// <returns>生成したコントロール。</returns>
    private static LoupeControl CreatePlainControl(
        EsReadOnlyBitmap? source = null, Point? centerPoint = null, IImage? display = null)
        => new()
        {
            Options = LoupeOptions.Default with { RequestedSize = 100, ZoomLevel = 2 },
            Appearance = LoupeAppearance.Default with { ShowCrosshair = false, ShowBorder = false, ShowCoordLabel = false },
            IsVisible = true,
            Source = source,
            DisplaySource = display,
            CenterPoint = centerPoint,
        };

    /// <summary>コントロールを親パネルへ載せてレイアウトを確定する（ウィンドウは作らない）。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="size">親パネルの一辺のサイズ（DIP）。</param>
    /// <returns>配置に使った親パネル。</returns>
    /// <remarks>
    /// ラベル要素（子要素）は配置フェーズ（<c>ArrangeOverride</c>）で更新されるため、テストは配置を明示的に実行する。<br/>
    /// 描画（<c>Rendered</c> の発火）を待つテストは <c>TestHelper.ShowInWindow</c> を使う（描画はウィンドウのフレームで行われる）。
    /// </remarks>
    private static Panel Layout(LoupeControl control, double size = 300)
    {
        var panel = new Panel { Width = size, Height = size };
        panel.Children.Add(control);
        panel.Measure(new Size(size, size));
        panel.Arrange(new Rect(0, 0, size, size));
        return panel;
    }


    /// <summary>既定の外観で塗られる下地の色（<c>0xRRGGBB</c>）。 非表示のときに残っていないことの判定に使う。</summary>
    private const int DefaultPasteboardPixel = 0x535353;

    /// <summary>描画の要求と非同期の画像生成が静まるまで、実行キューとフレームを進める。</summary>
    /// <param name="control">対象のコントロール（ウィンドウへ接続されていること）。</param>
    /// <remarks>
    /// 「描画されないこと」を確かめるテストは、**判定の前**にこれを呼んで保留中の要求を流し切る。<br/>
    /// 流し切らずに判定すると、判定の後に処理された要求の分だけ結果がぶれる（実行環境の速さに依存する）。<br/>
    /// 数えるのは静まったことの判定だけであり、合否は描画結果（画素）で判定する（数え直しで誤りを隠さない）。
    /// </remarks>
    private static async Task AdvanceUntilQuietAsync(LoupeControl control)
    {
        var rendered = 0;
        void OnRendered(object? sender, LoupeRenderedEventArgs e) => rendered++;
        control.Rendered += OnRendered;
        try
        {
            await control.AdvanceUntilQuietAsync(() => rendered);
        }
        finally
        {
            control.Rendered -= OnRendered;
        }
    }


    /// <param name="pixel">ルーペの位置の画素（<c>0xRRGGBB</c>）。</param>
    /// <remarks>
    /// 非表示のときは拡大した内容も下地も描かれない（ウィンドウの地がそのまま残る）。<br/>
    /// 「描画回数が 0」で判定しないのは、ヘッドレスの描画は無効化された要素の <c>Render</c> を<br/>
    /// 1 回呼ぶことがあり、状態の誤りと描画回数の差を区別できないため（実測で確認）。
    /// </remarks>
    private static void AssertHiddenPixel(int pixel)
    {
        Assert.NotEqual(0x0000FF, pixel);
        Assert.NotEqual(DefaultPasteboardPixel, pixel);
    }

    /// <summary>ルーペのローカル座標の画素を、ウィンドウの描画結果から読む（描画内容の実測）。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="control">対象のコントロール。</param>
    /// <param name="x">ルーペ内の X 座標（DIP）。</param>
    /// <param name="y">ルーペ内の Y 座標（DIP）。</param>
    /// <returns>画素の色（<c>0xRRGGBB</c>）。</returns>
    /// <remarks>
    /// ルーペの表示位置は中心とオフセットから決まるため、ローカル座標をウィンドウ座標へ写してから読む。<br/>
    /// 読み取りは物理ピクセル座標（ヘッドレスのスケーリングは 1.0 のため DIP と一致する）。
    /// </remarks>
    private static int LoupePixelAt(Window window, LoupeControl control, int x, int y)
    {
        var point = control.TranslatePoint(new Point(x, y), window);
        Assert.NotNull(point);
        return PixelAt(window, (int)point.Value.X, (int)point.Value.Y);
    }

    /// <summary>ウィンドウの描画結果から画素を読む（描画内容の実測）。</summary>
    /// <param name="window">対象のウィンドウ。</param>
    /// <param name="x">X 座標（物理ピクセル）。</param>
    /// <param name="y">Y 座標（物理ピクセル）。</param>
    /// <returns>画素の色（<c>0xRRGGBB</c>）。</returns>
    /// <remarks>
    /// ヘッドレスでも実際に描かれた画素を読めるため、描画内容（切り出し・下地・付加情報）を実測で検証できる。<br/>
    /// 読み取りはフレームバッファの並び（BGRA / RGBA）を吸収して <c>0xRRGGBB</c> へ揃える。
    /// </remarks>
    private static int PixelAt(Window window, int x, int y)
    {
        var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var raw = System.Runtime.InteropServices.Marshal.ReadInt32(buffer.Address + (y * buffer.RowBytes) + (x * 4));
        var b = (byte)(raw & 0xFF);
        var g = (byte)((raw >> 8) & 0xFF);
        var r = (byte)((raw >> 16) & 0xFF);
        return buffer.Format == PixelFormat.Bgra8888
            ? (r << 16) | (g << 8) | b
            : (b << 16) | (g << 8) | r;
    }

    /// <summary>エラーログが記録されるまで描画フレームを進める。</summary>
    /// <param name="control">対象のコントロール（ウィンドウへ接続されていること）。</param>
    /// <param name="sink">エラーの有無を確認するロガー。</param>
    private static async Task WaitForErrorAsync(LoupeControl control, TestHelper.ListLogger sink)
    {
        for (var i = 0; i < 50 && !sink.HasError; i++)
        {
            Dispatcher.UIThread.RunJobs();
            TopLevel.GetTopLevel(control)?.CaptureRenderedFrame();
            await Task.Yield();
        }
    }

    /// <summary>中心座標ラベルの要素（子要素のうち <see cref="Border"/>）を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>ラベルの背景要素。</returns>
    private static Border LabelElement(LoupeControl control) => control.Children.OfType<Border>().Single();

    /// <summary>中心座標ラベルの文言を取得する。</summary>
    /// <param name="control">対象のコントロール。</param>
    /// <returns>表示されている文言。</returns>
    private static string LabelText(LoupeControl control) => ((TextBlock)LabelElement(control).Child!).Text!;

    // ====================================================================
    // 描画（窓の描画面へ直接描く）
    // ====================================================================

    [AvaloniaFact]
    internal async Task SourceとCenterPointを設定すると描画される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        // ルーペの領域（中心の右下 = 左上 100,100）が拡大元の色で描かれる
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 表示用の射影を渡すと表示用の画像から描画される()
    {
        // EsBitmapAdapter（コアの画像を IImage として公開するアダプタ）を DisplaySource へ渡した場合、
        // 表示用の射影をそのまま使って描画される（切り出しの複製を作らない経路の検証）。
        using var read = TestHelper.CreateSource(200, 200, DrawingColor.FromArgb(255, 200, 30, 30));
        using var image = new EsBitmapAdapter(read);
        var control = CreatePlainControl(read, new Point(100, 100), display: image);
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0xC81E1E, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 表示用の射影が無くてもコア画像から描画される()
    {
        // 表示用の射影（DisplaySource）は省略可能である。 省略した場合は拡大元の画素（0 コピーのビュー）から描く。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var control = CreatePlainControl(source, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 表示用の射影の有無で描画結果が同じになる()
    {
        // 描画の幾何は拡大元の寸法だけを基準にするため、射影の有無で結果が変わらない
        // （射影は「切り出しの複製を作らない」ヒントであり、描く内容を変えない）。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var projection = new EsBitmapAdapter(source);

        var withDisplay = CreatePlainControl(source, new Point(100, 100), display: projection);
        var withoutDisplay = CreatePlainControl(source, new Point(100, 100));
        var firstWindow = TestHelper.ShowInWindow(withDisplay);
        var secondWindow = TestHelper.ShowInWindow(withoutDisplay);

        await withDisplay.WaitForRenderedAsync();
        await withoutDisplay.WaitForRenderedAsync();

        foreach (var (x, y) in new[] { (0, 0), (30, 30), (99, 99) })
        {
            Assert.Equal(
                LoupePixelAt(secondWindow, withoutDisplay, x, y),
                LoupePixelAt(firstWindow, withDisplay, x, y));
        }

        firstWindow.Close();
        secondWindow.Close();
    }

    [AvaloniaFact]
    internal async Task 寸法が一致しない表示用の射影は無視されコア画像から描画される()
    {
        // 描画 API は元矩形を画像の座標系で解釈するため、寸法が拡大元と違う射影は使わない（誤った切り出しを描かない）。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var smaller = TestHelper.CreateSource(100, 100, DrawingColor.Red);
        using var projection = new EsBitmapAdapter(smaller);
        var control = CreatePlainControl(source, new Point(100, 100), display: projection);
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 破棄済みの表示用の射影は無視されコア画像から描画される()
    {
        // 破棄済みの射影は寸法を取得できないため「使えないヒント」として扱い、拡大元の画素から描く
        // （表示用の射影はホストが所有するため、ホストが先に破棄した状態と競合しうる）。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var dead = TestHelper.CreateSource(200, 200, DrawingColor.Red);
        var projection = new EsBitmapAdapter(dead);
        var control = CreatePlainControl(source, new Point(100, 100), display: projection);
        dead.Dispose();
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 表示用の射影の変更で再描画される()
    {
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var projection = new EsBitmapAdapter(source);
        var control = CreatePlainControl(source, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        var rendered = 0;
        control.Rendered += (_, _) => rendered++;
        control.DisplaySource = projection;
        await control.WaitForRenderedAsync();

        Assert.Equal(1, rendered);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 寸法が一致する表示用の射影が優先して使われる()
    {
        // 寸法が一致する射影は切り出しの複製を作らない経路で使われる（射影の色がそのまま出る）。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var red = TestHelper.CreateSource(200, 200, DrawingColor.Red);
        using var projection = new EsBitmapAdapter(red);
        var control = CreatePlainControl(source, new Point(100, 100), display: projection);
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0xFF0000, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 表示用の射影だけでは描画されない()
    {
        // 拡大元（コア画像）が描画の真実である。 射影だけを渡しても切り出す元が無いため下地のみを描く。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var projection = new EsBitmapAdapter(source);
        var control = CreatePlainControl(centerPoint: new Point(100, 100), display: projection);
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 拡大元の範囲外は下地のみが描画される()
    {
        // 拡大元の外側は描かない（交差する部分だけを描く）ため、画像外の色（下地）が見える。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var control = CreatePlainControl(source, new Point(2, 2));
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        // 可視域は中心 ±25（要求サイズ 100 / 拡大率 2）であるため、左上側は画像の外になる
        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        // 右下側は画像の内側である
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 80, 80));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 拡大元を差し替えると新しい画像が描画される()
    {
        using var blue = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var red = TestHelper.CreateSource(200, 200, DrawingColor.Red);
        var control = CreatePlainControl(blue, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));

        control.Source = red;
        await control.WaitForRenderedAsync();

        Assert.Equal(0xFF0000, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 破棄済みの拡大元を差し替えると描画が復帰する()
    {
        // 破棄済みの画像は下地のみになるが、新しい画像へ差し替えると描画が戻る（破棄済みを保持し続けない）。
        var dead = TestHelper.CreateSource(200, 200, DrawingColor.Red);
        var control = CreatePlainControl(dead, new Point(100, 100));
        dead.Dispose();
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();
        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));

        using var blue = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.Source = blue;
        await control.WaitForRenderedAsync();

        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal void 複数プロパティの連続変更は1フレーム1回の描画に結合される()
    {
        var control = CreatePlainControl();
        var window = TestHelper.ShowInWindow(control);

        var rendered = 0;
        control.Rendered += (_, _) => rendered++;

        // フレームの描画前に複数のプロパティを変更しても、描画は 1 回に結合される
        for (var i = 0; i < 5; i++)
        {
            control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
            control.CenterPoint = new Point(100 + i, 100);
        }

        control.RenderOnce();

        Assert.Equal(1, rendered);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task Source未設定でも下地のみ描画される()
    {
        var control = CreatePlainControl(centerPoint: new Point(50, 50));
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        // 画像が無い場合は下地（ペーストボードの単色）で塗られる
        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task Source設定_CenterPoint未設定でも下地のみ描画される()
    {
        var control = CreatePlainControl(
            TestHelper.CreateSource(200, 200, DrawingColor.Blue));
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        // center が未設定のため、画像の切り出しは行わず下地のみ描画する
        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task CenterPoint変更で再描画される()
    {
        var control = CreatePlainControl(
            TestHelper.CreateSource(200, 200, DrawingColor.Blue), new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        EsPoint? firstTopLeft = null;
        control.Rendered += (_, e) => firstTopLeft = e.Content.CaptureTopLeft;
        control.CenterPoint = new Point(120, 120);
        await control.WaitForRenderedAsync();

        // 中心の変更で切り出し領域が動く（同じ中心で描かれる）
        Assert.NotNull(firstTopLeft);
        Assert.Equal(new EsPoint(95, 95), firstTopLeft);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task RequestRenderで再描画される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        // プロパティ変更を伴わない再描画要求（完了は Rendered で通知される）
        var rendered = 0;
        control.Rendered += (_, _) => rendered++;
        control.RequestRender();
        await control.WaitForRenderedAsync();

        Assert.Equal(1, rendered);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 設定の変更で再描画される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        var zoom = 0;
        control.Rendered += (_, e) => zoom = e.Content.ZoomLevel;
        control.Options = control.Options with { ZoomLevel = 8 };
        await control.WaitForRenderedAsync();

        // 設定の変更で描画計画が変わる（拡大率が反映される）
        Assert.Equal(8, zoom);
        window.Close();
    }

    // ====================================================================
    // 表示制御（表示状態と描画の分離）
    // ====================================================================

    [AvaloniaFact]
    internal async Task Clearで非表示になる()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));

        control.Clear();
        // 非表示にした直後に残っている描画要求と画像の生成を流し切ってから判定する（静まるまで待つ）
        await AdvanceUntilQuietAsync(control);

        // 非表示になると描画されない（ルーペの位置に下地も残らない）
        Assert.False(control.IsVisible);
        AssertHiddenPixel(LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task IsVisible_falseで描画されない()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));

        control.IsVisible = false;
        control.CenterPoint = new Point(120, 120);
        // 非表示にした直後に残っている描画要求と画像の生成を流し切ってから判定する（静まるまで待つ）
        await AdvanceUntilQuietAsync(control);

        Assert.False(control.IsVisible);
        AssertHiddenPixel(LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 非表示中の再描画要求は描画しない()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        control.IsVisible = false;
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        await AdvanceUntilQuietAsync(control);

        // 非表示のため描画は行われない（状態は保持され、表示時に反映される）
        Assert.False(control.IsVisible);
        AssertHiddenPixel(LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task IsVisible_trueで表示して描画する()
    {
        var control = CreatePlainControl(
            TestHelper.CreateSource(200, 200, DrawingColor.Blue), new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        control.IsVisible = false;
        await TestHelper.PumpAsync();
        control.IsVisible = true;
        await control.WaitForRenderedAsync();

        Assert.True(control.IsVisible);
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));
        window.Close();
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
    // 既定の描画の提供（DrawPlan。 static。 ホストが任意の順序で描く入口）
    // ====================================================================

    [AvaloniaFact]
    internal async Task DrawPlan_コア画像を渡すと計画どおりに描画される()
    {
        // ホストは Rendered の Content（描画計画）をそのまま渡して既定の描画を行える。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var control = CreatePlainControl(source, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);

        var calls = 0;
        control.Rendered += (_, e) =>
        {
            // 同じ描画コンテキストへ既定の描画を重ねる（例外なく完了し、描画は継続する）
            var plan = e.Content;
            LoupeControl.DrawPlan(e.Context, source, in plan);
            calls++;
        };

        await control.WaitForRenderedAsync();

        Assert.Equal(1, calls);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task DrawPlan_表示用の射影を渡しても描画される()
    {
        // 省略可能な引数（表示用の射影）を渡す経路も描画が成立する（射影は切り出しの複製を省くヒントである）。
        using var source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        using var projection = new EsBitmapAdapter(source);
        var control = CreatePlainControl(source, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);

        var calls = 0;
        control.Rendered += (_, e) =>
        {
            var plan = e.Content;
            LoupeControl.DrawPlan(e.Context, source, in plan, projection);
            calls++;
        };

        await control.WaitForRenderedAsync();

        Assert.Equal(1, calls);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task DrawPlan_拡大元が無くても描画できる()
    {
        // 拡大元が null（画像なし）でも描画は成立する（下地のみを描く）。
        var control = CreatePlainControl(centerPoint: new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);

        var calls = 0;
        control.Rendered += (_, e) =>
        {
            var plan = e.Content;
            LoupeControl.DrawPlan(e.Context, source: null, in plan);
            calls++;
        };

        await control.WaitForRenderedAsync();

        Assert.Equal(1, calls);
        window.Close();
    }

    [AvaloniaFact]
    internal void DrawPlan_コンテキストがnullの場合は例外になる()
    {
        // 描画先は必須である（描画できない呼び出しを早期に失敗させる）。
        var plan = default(LoupeDrawPlan);

        Assert.Throws<ArgumentNullException>(() => LoupeControl.DrawPlan(null!, source: null, in plan));
    }

    // ====================================================================
    // 拡大中心の取り直し（描画の直前）
    // ====================================================================

    [AvaloniaFact]
    internal async Task CenterPointProvider_描画の直前に中心を取り直す()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);

        // ポインター移動イベントの座標より新しい位置（実カーソル）を返す
        control.CenterPointProvider = () => new Point(120, 130);

        LoupeRenderedEventArgs? rendered = null;
        control.Rendered += (_, e) => rendered = e;

        await control.WaitForRenderedAsync();

        // 取り直した中心がプロパティと拡大画像（切り出し領域）の両方へ反映される
        // （要求サイズ 100・拡大率 2 の可視域は 50x50 のため、切り出し領域の左上は中心 - 25）
        Assert.Equal(new Point(120, 130), control.CenterPoint);
        Assert.NotNull(rendered);
        Assert.Equal(new EsPoint(95, 105), rendered.Content.CaptureTopLeft);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task CenterPointProvider_nullを返す場合は中心を変えない()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        control.CenterPointProvider = () => null;
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        Assert.Equal(new Point(100, 100), control.CenterPoint);
        window.Close();
    }

    // ====================================================================
    // エラーハンドリング
    // ====================================================================

    [AvaloniaFact]
    internal async Task 描画経路の例外はログへ記録され描画を継続する()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);

        // 付加情報を描くホストのハンドラーが例外を投げても、アプリを落とさずログへ記録される
        // （描画の経路が例外を吸収する。 拡大元の型は型で保証されるため、未対応型の例外は起こらない）
        control.Rendered += static (_, _) => throw new InvalidOperationException("付加情報の描画に失敗");
        var window = TestHelper.ShowInWindow(control);

        await WaitForErrorAsync(control, sink);

        Assert.True(sink.HasError);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task RequestRender_描画経路の例外もログへ記録される()
    {
        var control = CreateControl();
        var sink = new TestHelper.ListLogger();
        control.Log = sink;
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        control.Rendered += static (_, _) => throw new InvalidOperationException("付加情報の描画に失敗");
        var window = TestHelper.ShowInWindow(control);

        // 明示的な再描画要求（RequestRender）経由の描画失敗もログへ記録される
        control.RequestRender();
        await WaitForErrorAsync(control, sink);

        Assert.True(sink.HasError);
        window.Close();
    }

    // ====================================================================
    // 表示制御（非表示時の明示的操作）
    // ====================================================================

    [AvaloniaFact]
    internal async Task 非表示にすると描画されない()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        // 描画の要求（実行キューへのポスト）と描画の間に非表示へ切り替えても描画されない
        Assert.Equal(0x0000FF, LoupePixelAt(window, control, 30, 30));

        control.IsVisible = false;
        control.RequestRender();
        // 非表示にした直後に残っている描画要求と画像の生成を流し切ってから判定する（静まるまで待つ）
        await AdvanceUntilQuietAsync(control);

        Assert.False(control.IsVisible);
        AssertHiddenPixel(LoupePixelAt(window, control, 30, 30));
        window.Close();
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
    /// 複数箇所で書き込むと、要求サイズの変更直後に古い値で描画される（表示サイズと切り出しの座標系が食い違う）。
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

        var pixelSize = 0;
        control.Rendered += (_, e) => pixelSize = e.Content.ClientPixelSize;
        await control.WaitForRenderedAsync();
        Assert.Equal(400, pixelSize);

        // 接続後にさらに大きい要求へ変更し、レイアウト（Arrange）を待たずに描画する
        control.Options = control.Options with { RequestedSize = 500 };
        await control.WaitForRenderedAsync();

        // 表示サイズ（要求サイズ）と描画の一辺（物理ピクセル）が一致する
        Assert.Equal(500, pixelSize);
        Assert.Equal(500, control.Width);
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
    internal async Task 破棄済みSourceは下地のみで描画される()
    {
        var bitmap = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var control = CreatePlainControl(bitmap, new Point(100, 100));
        bitmap.Dispose(); // 破棄済み Bitmap（生存検証で ObjectDisposedException）
        var window = TestHelper.ShowInWindow(control);

        await control.WaitForRenderedAsync();

        // 例外なく完了し、下地のみが描かれる
        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 描画中にSourceが破棄されると下地のみで描画される()
    {
        var bitmap = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        var control = CreatePlainControl(bitmap, new Point(100, 100));
        var window = TestHelper.ShowInWindow(control);

        // 描画の前にソースを破棄すると、ソース解決が破棄済みを検知して切り出しを行わない
        // （下地のみで描画され、例外は描画層へ伝播しない）
        bitmap.Dispose();
        await control.WaitForRenderedAsync();

        Assert.Equal(0x535353, LoupePixelAt(window, control, 30, 30));
        window.Close();
    }

    [AvaloniaFact]
    internal void Sourceを再設定すると変更通知される()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);

        // 再設定（OldValue が null でない）→ 変更ログの両分岐が実行される
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
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
        var window = TestHelper.ShowInWindow(control);

        // 初回設定（OldValue = null）と再設定（NewValue あり）の両経路で LogSettingChanged を実行する
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);

        // null 設定（NewValue = null）で LogSettingChanged の null 分岐を実行する
        control.Source = null;
        control.CenterPoint = null;

        // 描画を実行して描画のログ経路（LoupeLogMessage）を通す
        await control.WaitForRenderedAsync();
        control.Clear();
        window.Close();
    }

    [AvaloniaFact]
    internal void CoordLabelFontSize_NaNは12に補正される()
    {
        var control = new LoupeControl();

        control.Appearance = control.Appearance with { CoordLabelFontSize = double.NaN };

        Assert.Equal(12.0, control.Appearance.CoordLabelFontSize);
    }

    [AvaloniaFact]
    internal async Task 描画完了でRenderedイベントが発火し描画先と描画計画を通知する()
    {
        var control = CreateControl();
        LoupeRenderedEventArgs? args = null;
        control.Rendered += (_, e) => args = e;

        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        // RequestedSize=100 / ZoomLevel=2 / ClientToPixelScaling=1.0 の場合:
        // pixelSize = ceil(100×1.0) = 100, captureSize = ceil(100/2) = 50
        // CaptureTopLeft = (100−25, 100−25) = (75, 75), ZoomLevel = 2
        Assert.NotNull(args);
        Assert.NotNull(args!.Context);
        Assert.Equal(100, args.Content.ClientPixelSize);
        Assert.Equal(new EsPoint(75, 75), args.Content.CaptureTopLeft);
        Assert.Equal(2, args.Content.ZoomLevel);
        window.Close();
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

        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        Assert.NotNull(args);
        // RequestedSize=100 / ZoomLevel=2 / ClientToPixelScaling=1.5 → pixelSize=150, captureSize=75
        Assert.Equal(150, args!.Content.ClientPixelSize);
        Assert.Equal(2, args.Content.ZoomLevel);

        // 表示（DIP）は 100 のままである（描画は物理ピクセル、要素は DIP）
        Assert.Equal(100, control.Width);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 中心未設定でもRenderedイベントが発火しnullのCaptureTopLeftを通知する()
    {
        var control = CreateControl();
        LoupeRenderedEventArgs? args = null;
        control.Rendered += (_, e) => args = e;
        var window = TestHelper.ShowInWindow(control);

        // CenterPoint 未設定（下地のみの描画）でもイベントは発火する
        await control.WaitForRenderedAsync();

        Assert.NotNull(args!);
        Assert.NotNull(args.Context);
        Assert.Null(args.Content.CaptureTopLeft);
        window.Close();
    }

    [AvaloniaFact]
    internal async Task 再描画のたびにRenderedイベントが発火する()
    {
        var control = CreateControl();
        control.Source = TestHelper.CreateSource(200, 200, DrawingColor.Blue);
        control.CenterPoint = new Point(100, 100);
        var window = TestHelper.ShowInWindow(control);
        await control.WaitForRenderedAsync();

        var count = 0;
        control.Rendered += (_, _) => count++;

        control.CenterPoint = new Point(120, 120);
        await control.WaitForRenderedAsync();

        Assert.Equal(1, count);
        window.Close();
    }

    // ====================================================================
    // 拡大元の型（コアの画像。 型で保証されるため実行時の判定は無い）
    // ====================================================================

    [AvaloniaFact]
    internal void 拡大元のプロパティの型はコアの画像である()
    {
        // 拡大元（Source）はコアの具象型であり、表示用の射影（DisplaySource）は UI の画像型である。
        // 型で保証されるため、未対応型を実行時に判定する仕組み（型チェック・反射）を持たない（`EsUtil/README.md` §5）。
        Assert.Equal(typeof(EsReadOnlyBitmap), LoupeControl.SourceProperty.PropertyType);
        Assert.Equal(typeof(IImage), LoupeControl.DisplaySourceProperty.PropertyType);
    }
}
