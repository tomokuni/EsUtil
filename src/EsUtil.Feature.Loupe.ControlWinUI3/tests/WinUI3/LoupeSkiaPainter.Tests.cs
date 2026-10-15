using EsUtil.Feature.Loupe.WinUI3.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using SkiaSharp;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Feature.Loupe.WinUI3.Tests;

/// <summary><see cref="LoupeSkiaPainter"/> の描画テスト（画素を検証する）。</summary>
/// <remarks>
/// 描画計画（<see cref="LoupeDrawPlan"/>）は実際の提示層（<see cref="LoupePresenter"/>）が解決するため、<br/>
/// 本テストは「層の順序に従って描画 API へ写す」ことと「描画計画の値が画素へ現れる」ことを同時に検証する。<br/>
/// 画素の検証は Skia（<c>SKCanvas</c>）で行うため、UI 初期化とウィンドウなしで実行できる<br/>
/// （WinUI 3 の XAML 型はヘッドレス環境で生成できないため、描画の検証は本テストが担う）。<br/>
/// 色・表示可否は外観（<see cref="LoupeAppearance"/>）が単一所有する。
/// </remarks>
public class LoupeSkiaPainterTests
{
    /// <summary>ペーストボード（画像外）の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>拡大画像に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_image = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>枠線の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>中心座標ラベルの背景色（黒・不透明）。</summary>
    private static readonly DrawingColor s_labelBackground = DrawingColor.FromArgb(255, 0, 0, 0);

    /// <summary>中心座標ラベルの文字色（白・不透明）。</summary>
    private static readonly DrawingColor s_labelText = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>ペーストボードの赤（画素比較用）。</summary>
    private static readonly SKColor s_backgroundPixel = new(255, 0, 0);

    /// <summary>外観が与えるラベルの実寸（幅・高さ）。 文字の実測に依存しない検証にする。</summary>
    private static readonly EsSize s_labelMeasure = new(20, 12);

    /// <summary>ルーペの一辺（物理ピクセル）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // ペーストボード（画像外の色）
    // ====================================================================

    /// <summary>ペーストボードが描画面を置き換えることを検証する。</summary>
    [Fact]
    internal void Paint_ペーストボードで描画面を置き換える()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.Clear(s_image.ToSKColor());

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(0, 0));
        Assert.Equal(s_backgroundPixel, target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    /// <summary>拡大中心が未設定でもペーストボードを描くことを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心が未設定でもペーストボードを描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null);

        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    /// <summary>ペーストボードが非表示なら前フレームを消して透明にすることを検証する。</summary>
    /// <remarks>
    /// 描画面を保持する UI では層を省くと前フレームが残るため、<br/>
    /// 非表示は「描画面を透明で塗る」として実装する（<see cref="LoupeAppearance.ResolvePasteboardColor"/>）。
    /// </remarks>
    [Fact]
    internal void Paint_ペーストボードが非表示なら透明で塗る()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        // 前フレームの残像を模して別の色で塗っておく
        target.Canvas.Clear(s_image.ToSKColor());

        Paint(target, CreateAppearance(pasteboard: s_background, showPasteboard: false), s_center);

        // 透明（アルファ 0）になり、前フレームの色も残らない
        Assert.Equal(0, target.GetPixel(16, 16).Alpha);
        Assert.Equal(0, target.GetPixel(0, 0).Alpha);
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    /// <summary>画像の範囲外（画像端の外）はペーストボードの色（無地）になることを検証する。</summary>
    /// <remarks>
    /// 画像の外は描かないため、描画面全体を塗るペーストボードの色がそのまま見える<br/>
    /// （無地）。 中心を画像の左外へ寄せ、切り出し領域の左端が画像の外へ出る状態を作る。
    /// </remarks>
    [Fact]
    internal void Paint_画像の外はペーストボードの色になる()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);
        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        // 中心 x=1・一辺 32・拡大率 4 → 切り出し 8 px の左上 x=-3（画像の外）→ 描画先の左側が画像外になる
        Paint(target, CreateAppearance(pasteboard: s_background), new EsPoint(1, LoupeSize / 2), read);

        // 画像の外（左端）はペーストボードの色、画像の中（右端）は拡大画像の色
        Assert.Equal(s_backgroundPixel, target.GetPixel(0, LoupeSize / 2));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize / 2));
    }

    /// <summary>拡大中心の周囲を切り出して拡大描画することを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心の周囲を切り出して拡大描画する()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);
        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center, read);

        // 切り出した領域（一様な色）がルーペ全体へ拡大される
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(16, 16));
        Assert.Equal(new SKColor(0, 0, 255), target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    /// <summary>切り出しが画像の内側に収まる場合、元画像の同じ位置の画素が描かれることを検証する。</summary>
    /// <remarks>
    /// 内側に収まる切り出しは 0 コピーのビュー（<c>EsPixmap</c>）で描くため、<b>行幅（元画像のストライド）が
    /// 正しく渡っていないと 2 行目以降がずれる</b>（幅×4 を渡すと、切り出し幅が元画像の幅と異なる場合に行がずれる）。<br/>
    /// そのため列と行で値が変わる画像を使い、1 行目と 2 行目の両方を検証する。
    /// </remarks>
    [Fact]
    internal void Paint_画像の内側の切り出しは元の位置の画素を描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        // 列と行で値が変わる画像（位置と行のずれを検出する）
        using var source = EsBitmap.Create(LoupeSize * 2, LoupeSize * 2);
        for (var y = 0; y < LoupeSize * 2; y++)
        {
            for (var x = 0; x < LoupeSize * 2; x++)
            {
                source.SetPixel(x, y, b: (byte)x, g: (byte)y, r: 0, a: 255);
            }
        }

        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        // 中心 (32, 32)・一辺 32・拡大率 4 → 切り出し 8 px の左上 (28, 28)（画像の内側）
        Paint(target, CreateAppearance(pasteboard: s_background), new EsPoint(32, 32), read);

        // 描画先の (0, 0) は切り出し領域の左上（元画像の (28, 28)）
        Assert.Equal(new SKColor(0, 28, 28, 255), target.GetPixel(0, 0));
        // 描画先の (0, 4) は切り出し領域の 2 行目（元画像の (28, 29)）。 行幅が誤っているとここがずれる
        Assert.Equal(new SKColor(0, 29, 28, 255), target.GetPixel(0, 4));
    }

    /// <summary>ソースが無ければ拡大画像を描かないことを検証する。</summary>
    [Fact]
    internal void Paint_ソースが無ければ拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background), s_center);

        // ペーストボードのみ（拡大画像は描かれない）
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    /// <summary>拡大中心が未設定なら拡大画像を描かないことを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心が未設定なら拡大画像を描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        using var source = EsBitmap.Create(LoupeSize, LoupeSize, s_image);
        source.SetReadOnly();
        using var read = EsReadOnlyBitmap.FromBitmap(source);

        Paint(target, CreateAppearance(pasteboard: s_background), center: null, read);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    // ====================================================================
    // 十字線・枠線
    // ====================================================================

    /// <summary>十字線を切り出し領域の中央へ描くことを検証する。</summary>
    [Fact]
    internal void Paint_十字線を切り出し領域の中央へ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross), s_center);

        // 拡大率 4・一辺 32 → 切り出し一辺 8、交点は 8 / 2 × 4 = 16
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(17, 1));
        Assert.Equal(new SKColor(255, 255, 255), target.GetPixel(1, 17));
        // 十字の外側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(1, 1));
    }

    /// <summary>十字線が非表示なら描かないことを検証する。</summary>
    [Fact]
    internal void Paint_十字線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, crosshair: s_cross, showCrosshair: false), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(17, 1));
    }

    /// <summary>枠線を四辺へ太さ分だけ描くことを検証する。</summary>
    [Fact]
    internal void Paint_枠線を四辺へ太さ分だけ描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border), s_center);

        // 上辺（太さ 2）
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(16, 1));
        // 左辺
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(1, 16));
        // 内側はペーストボードのまま
        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 16));
    }

    /// <summary>枠線が非表示なら描かないことを検証する。</summary>
    [Fact]
    internal void Paint_枠線が非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateAppearance(pasteboard: s_background, border: s_border, showBorder: false), s_center);

        Assert.Equal(s_backgroundPixel, target.GetPixel(16, 1));
    }

    // ====================================================================
    // テキスト（中心座標ラベル）
    // ====================================================================

    /// <summary>中心座標ラベルの背景と文字を描画面へ描くことを検証する。</summary>
    /// <remarks>
    /// 配置指示は DIP であるため、本テストはスケーリング係数 1.0（等倍）で背景の位置と文字の描画を確かめる<br/>
    /// （文字の有無は描画面の画素を数えて確かめる。 位置や書体に依存しない検証にする）。
    /// </remarks>
    [Fact]
    internal void Paint_中心座標ラベルの背景と文字を描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateLabelAppearance(), s_center);

        // 背景は基準点（左上のオフセット 2 DIP）から始まる（角丸 2 の内側を確かめる）
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(3, 3));
        // 文字（白）が背景の内側へ描かれている
        Assert.True(CountPixels(target, new SKColor(255, 255, 255)) > 0);
    }

    /// <summary>中心座標ラベルが非表示なら描かないことを検証する。</summary>
    [Fact]
    internal void Paint_中心座標ラベルが非表示なら描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateLabelAppearance() with { ShowCoordLabel = false }, s_center);

        // 背景の色も文字の色も現れない（ペーストボードのみ）
        Assert.Equal(s_backgroundPixel, target.GetPixel(3, 3));
        Assert.Equal(0, CountPixels(target, new SKColor(0, 0, 0)));
    }

    /// <summary>拡大中心が未設定なら中心座標ラベルを描かないことを検証する。</summary>
    [Fact]
    internal void Paint_拡大中心が未設定なら中心座標ラベルを描かない()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(target, CreateLabelAppearance(), center: null);

        Assert.Equal(s_backgroundPixel, target.GetPixel(3, 3));
    }

    /// <summary>中心座標ラベルが枠線より後に描かれる（最上位になる）ことを検証する。</summary>
    /// <remarks>
    /// 描く順序は描画層（<see cref="LoupeDrawPlan.Layers"/>）が単一所有する。 本テストは計画の順序どおりに<br/>
    /// 写していることを、ラベルが枠線へ重なる画素で確かめる（ラベルの基準点を 0 にして枠線と重ねる）。
    /// </remarks>
    [Fact]
    internal void Paint_中心座標ラベルは枠線より後に描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);

        Paint(
            target,
            CreateLabelAppearance() with { BorderColor = s_border, ShowBorder = true, CoordLabelOffset = 0 },
            s_center);

        // ラベルと重なる左上の枠線（太さ 2）はラベルの背景（黒）で覆われる
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(1, 1));
        // ラベルの外側の枠線は残る（ラベルの幅は 20 + 左右の余白 8 = 28）
        Assert.Equal(new SKColor(255, 255, 0), target.GetPixel(31, 1));
    }

    /// <summary>スケーリング係数に応じてラベルを物理ピクセルへ換算して描くことを検証する。</summary>
    /// <remarks>
    /// 配置指示（DIP）から物理ピクセルへの換算は描画側が行う（矩形・内側余白・フォントサイズ・角丸へ同じ係数を掛ける）。
    /// </remarks>
    [Fact]
    internal void Paint_スケーリング係数に応じてラベルを拡大する()
    {
        using var target = new RenderTarget(LoupeSize * 2, LoupeSize * 2);

        // 一辺 32 DIP・スケーリング 2 → 描画面は 64 物理ピクセル
        Paint(target, CreateLabelAppearance(), s_center, scaling: 2);

        // 基準点（2 DIP）と角丸はどちらも 2 倍になり、背景は (4, 4) から始まる
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(6, 6));
        // 等倍なら背景の外である位置にも背景が広がる（(30, 6) は 28 DIP に相当する）
        Assert.Equal(new SKColor(0, 0, 0), target.GetPixel(30, 6));
    }

    // ====================================================================
    // 既定描画の入口
    // ====================================================================

    /// <summary>公開している既定描画（<see cref="LoupeControl.DrawPlan"/>）が同じ画素を描くことを検証する。</summary>
    [Fact]
    internal void DrawPlan_計画どおりに描く()
    {
        using var target = new RenderTarget(LoupeSize, LoupeSize);
        var plan = CreatePlan(CreateAppearance(pasteboard: s_background), s_center);

        LoupeControl.DrawPlan(target.Canvas, null, in plan);

        Assert.Equal(s_backgroundPixel, target.GetPixel(0, 0));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    /// <summary>描画先が null なら例外になることを検証する。</summary>
    [Fact]
    internal void Paint_描画先がnullなら例外()
    {
        using var target = new RenderTarget(4, 4);
        var plan = CreatePlan(CreateAppearance(), s_center);

        Assert.Throws<ArgumentNullException>(
            () => LoupeSkiaPainter.Paint(null!, null, in plan));
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>指定の外観・中心で描画計画を解決し、描画先へ描く。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="requestedSize">ルーペの一辺（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数（環境値）。</param>
    /// <remarks>ラベルの実寸は固定値（<see cref="s_labelMeasure"/>）を返す関数で測る（文字の実測に依存しない）。</remarks>
    private static void Paint(
        RenderTarget target,
        LoupeAppearance appearance,
        EsPoint? center,
        EsReadOnlyBitmap? source = null,
        int requestedSize = LoupeSize,
        double scaling = 1.0)
        => LoupeSkiaPainter.Paint(target.Canvas, source, CreatePlan(appearance, center, requestedSize, scaling));

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <param name="requestedSize">ルーペの一辺（DIP）。</param>
    /// <param name="scaling">表示 DIP → 物理ピクセルのスケーリング係数（環境値）。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定は固定値を返す関数を渡す（文字の実測に依存しない画素検証にする）。</remarks>
    private static LoupeDrawPlan CreatePlan(
        LoupeAppearance appearance, EsPoint? center, int requestedSize = LoupeSize, double scaling = 1.0)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = requestedSize, ZoomLevel = 4 },
            Appearance = appearance,
            CenterPoint = center,
            ClientToPixelScaling = scaling,
        };

        return presenter.ResolveDrawPlan(requestedSize, static (_, _) => s_labelMeasure);
    }

    /// <summary>テスト用の外観を生成する（指定しない色は透明＝描かない）。</summary>
    /// <param name="pasteboard">ペーストボード（画像外）の色。</param>
    /// <param name="border">枠線の色。</param>
    /// <param name="crosshair">十字線の色。</param>
    /// <param name="showCrosshair">十字線を表示するかどうか。</param>
    /// <param name="showBorder">枠線を表示するかどうか。</param>
    /// <param name="showPasteboard">ペーストボードを表示するかどうか。</param>
    /// <returns>描画に使う外観。</returns>
    private static LoupeAppearance CreateAppearance(
        DrawingColor? pasteboard = null,
        DrawingColor? border = null,
        DrawingColor? crosshair = null,
        bool showCrosshair = true,
        bool showBorder = true,
        bool showPasteboard = true)
        => LoupeAppearance.Default with
        {
            PasteboardColor = pasteboard ?? DrawingColor.Transparent,
            BorderColor = border ?? DrawingColor.Transparent,
            CrossColor = crosshair ?? DrawingColor.Transparent,
            BorderThickness = 2,
            ShowCrosshair = showCrosshair,
            ShowBorder = showBorder,
            ShowPasteboard = showPasteboard,
            ShowCoordLabel = false,
        };

    /// <summary>中心座標ラベルの色と体裁を指定した外観を生成する。</summary>
    /// <returns>ラベルを描く外観。</returns>
    private static LoupeAppearance CreateLabelAppearance()
        => CreateAppearance(pasteboard: s_background) with
        {
            ShowCoordLabel = true,
            CoordLabelColor = s_labelText,
            CoordLabelBackgroundColor = s_labelBackground,
            CoordLabelFontSize = 12,
            CoordLabelCornerRadius = 2,
        };

    /// <summary>指定の色の画素の数を数える。</summary>
    /// <param name="target">描画先。</param>
    /// <param name="color">数える色。</param>
    /// <returns>一致した画素の数。</returns>
    private static int CountPixels(RenderTarget target, SKColor color)
    {
        var count = 0;
        for (var y = 0; y < target.Height; y++)
        {
            for (var x = 0; x < target.Width; x++)
            {
                if (target.GetPixel(x, y) == color)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>画素を検証できる描画先（SKBitmap とそのキャンバス）。</summary>
    private sealed class RenderTarget : IDisposable
    {
        /// <summary>描画結果を保持するビットマップ。</summary>
        private readonly SKBitmap _bitmap;

        /// <summary>指定サイズの描画先を生成する。</summary>
        /// <param name="width">幅（ピクセル）。</param>
        /// <param name="height">高さ（ピクセル）。</param>
        public RenderTarget(int width, int height)
        {
            _bitmap = new SKBitmap(width, height);
            Canvas = new SKCanvas(_bitmap);
        }

        /// <summary>描画先の幅（ピクセル）を取得する。</summary>
        public int Width => _bitmap.Width;

        /// <summary>描画先の高さ（ピクセル）を取得する。</summary>
        public int Height => _bitmap.Height;

        /// <summary>描画先のキャンバスを取得する。</summary>
        public SKCanvas Canvas { get; }

        /// <summary>指定位置の画素を取得する。</summary>
        /// <param name="x">X 座標。</param>
        /// <param name="y">Y 座標。</param>
        /// <returns>画素の色。</returns>
        public SKColor GetPixel(int x, int y) => _bitmap.GetPixel(x, y);

        /// <summary>キャンバスとビットマップを破棄する。</summary>
        public void Dispose()
        {
            Canvas.Dispose();
            _bitmap.Dispose();
        }
    }
}
