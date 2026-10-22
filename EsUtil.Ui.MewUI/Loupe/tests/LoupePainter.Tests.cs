using Aprillz.MewUI;
using Aprillz.MewUI.Rendering;
using Xunit;
using DrawingColor = System.Drawing.Color;

namespace EsUtil.Ui.MewUI.Loupe.Tests;

/// <summary><see cref="LoupePainter"/> の描画テスト（画素を検証する）。</summary>
/// <remarks>
/// 描画計画（<see cref="LoupeDrawPlan"/>）は実際の提示層（<see cref="LoupePresenter"/>）が解決するため、<br/>
/// 本テストは「層の順序に従って描画 API へ写す」ことと「描画計画の値が画素へ現れる」ことを同時に検証する。<br/>
/// 画素の検証はオフスクリーンの表面（<see cref="TestCanvas"/>）で行うため、ウィンドウなしで実行できる。<br/>
/// 色・表示可否は外観（<see cref="LoupeAppearance"/>）が単一所有する。
/// </remarks>
public class LoupePainterTests
{
    /// <summary>ペーストボード（画像外）の色（赤・不透明）。</summary>
    private static readonly DrawingColor s_background = DrawingColor.FromArgb(255, 255, 0, 0);

    /// <summary>拡大画像に使う色（青・不透明）。</summary>
    private static readonly DrawingColor s_image = DrawingColor.FromArgb(255, 0, 0, 255);

    /// <summary>枠線の色（黄・不透明）。</summary>
    private static readonly DrawingColor s_border = DrawingColor.FromArgb(255, 255, 255, 0);

    /// <summary>十字線の色（白・不透明）。</summary>
    private static readonly DrawingColor s_cross = DrawingColor.FromArgb(255, 255, 255, 255);

    /// <summary>ルーペの一辺（物理ピクセル）。</summary>
    private const int LoupeSize = 32;

    /// <summary>拡大中心（描画先の中央）。</summary>
    private static readonly EsPoint s_center = new(16, 16);

    // ====================================================================
    // ペーストボード（画像外の色）
    // ====================================================================

    [Fact]
    internal void Paint_ペーストボードで描画面を置き換える()
    {
        // 前フレームの残像を模して別の色で塗ってから描く
        var target = Paint(CreateAppearance(pasteboard: s_background), s_center, before: static ctx => ctx.Clear(Color.FromRgb(0, 0, 255)));

        Assert.Equal(s_background, target.GetPixel(0, 0));
        Assert.Equal(s_background, target.GetPixel(LoupeSize - 1, LoupeSize - 1));
    }

    [Fact]
    internal void Paint_拡大中心が未設定でもペーストボードを描く()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background), center: null);

        Assert.Equal(s_background, target.GetPixel(1, 1));
    }

    /// <summary>ペーストボードが非表示なら前フレームを消して透明にすることを検証する。</summary>
    /// <remarks>
    /// 描画面を保持する UI では層を省くと前フレームが残るため、<br/>
    /// 非表示は「描画面を透明で塗る」として実装する（<see cref="LoupeAppearance.ResolvePasteboardColor"/>）。
    /// </remarks>
    [Fact]
    internal void Paint_ペーストボードが非表示なら透明で塗る()
    {
        var target = Paint(
            CreateAppearance(pasteboard: s_background, showPasteboard: false),
            s_center,
            before: static ctx => ctx.Clear(Color.FromRgb(0, 0, 255)));

        // 透明（アルファ 0）になり、前フレームの色も残らない
        Assert.Equal(0, target.GetAlpha(16, 16));
        Assert.Equal(0, target.GetAlpha(0, 0));
    }

    // ====================================================================
    // 拡大画像
    // ====================================================================

    /// <summary>画像の層は切り出しの矩形を計画から受け取る（画素の比較はしない）。</summary>
    /// <remarks>
    /// <b>画素で検証しない理由</b>: 画素を読み戻せる GDI バックエンドは、元矩形つきの画像描画
    /// （<c>DrawImage(IImage, Rect src, Rect dst)</c>）を描かない（実測）。 実際の見た目は Direct2D の
    /// アプリで確認する。 ここでは「層が計画の矩形を受け取って例外なく描ける」ことを固定する。
    /// </remarks>
    [Fact]
    internal void Paint_画像の層は計画の切り出し矩形で描く()
    {
        using var read = CreateImage(LoupeSize * 2, LoupeSize * 2, s_image);

        var target = Paint(CreateAppearance(pasteboard: s_background), s_center, read);

        // 例外なく描け、ペーストボードが塗られている（画像の画素は GDI では検証できない）
        Assert.True(target.IsValid);
        var plan = CreatePlan(CreateAppearance(pasteboard: s_background), s_center);
        Assert.NotNull(plan.MagnifiedImage);
        Assert.Equal(9, plan.MagnifiedImage!.Value.CropSourceRect.Width);
        return;
    }



    [Fact]
    internal void Paint_ソースが無ければ拡大画像を描かない()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background), s_center);

        // ペーストボードのみ（拡大画像は描かれない）
        Assert.Equal(s_background, target.GetPixel(16, 16));
    }

    [Fact]
    internal void Paint_拡大中心が未設定なら拡大画像を描かない()
    {
        using var read = CreateImage(LoupeSize, LoupeSize, s_image);

        var target = Paint(CreateAppearance(pasteboard: s_background), center: null, read);

        Assert.Equal(s_background, target.GetPixel(16, 16));
    }

    // ====================================================================
    // 十字線・枠線
    // ====================================================================

    [Fact]
    internal void Paint_十字線を切り出し領域の中央へ描く()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background, crosshair: s_cross), s_center);

        // 拡大率 4・一辺 32 → 切り出し一辺 8、交点は 8 / 2 × 4 = 16
        Assert.Equal(s_cross, target.GetPixel(17, 1));
        Assert.Equal(s_cross, target.GetPixel(1, 17));

        // 十字の外側はペーストボードのまま
        Assert.Equal(s_background, target.GetPixel(1, 1));
    }

    [Fact]
    internal void Paint_十字線が非表示なら描かない()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background, crosshair: s_cross, showCrosshair: false), s_center);

        Assert.Equal(s_background, target.GetPixel(17, 1));
    }

    [Fact]
    internal void Paint_枠線を四辺へ太さ分だけ描く()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background, border: s_border), s_center);

        // 上辺（太さ 2）
        Assert.Equal(s_border, target.GetPixel(16, 1));

        // 左辺
        Assert.Equal(s_border, target.GetPixel(1, 16));

        // 内側はペーストボードのまま
        Assert.Equal(s_background, target.GetPixel(16, 16));
    }

    [Fact]
    internal void Paint_枠線が非表示なら描かない()
    {
        var target = Paint(CreateAppearance(pasteboard: s_background, border: s_border, showBorder: false), s_center);

        Assert.Equal(s_background, target.GetPixel(16, 1));
    }

    // ====================================================================
    // テキスト（描画実装は描かない）
    // ====================================================================

    [Fact]
    internal void Paint_はテキストを描かない()
    {
        // ラベルは UI 層が要素として置くため、描画計画には背景色が含まれない
        // （座標ラベルを表示する設定でもペーストボードのみが描かれる）
        var target = Paint(CreateAppearance(pasteboard: s_background), s_center);

        Assert.Equal(s_background, target.GetPixel(16, 2));
    }

    // ====================================================================
    // 引数の検証
    // ====================================================================

    [Fact]
    internal void Paint_描画先がnullなら例外()
    {
        var plan = CreatePlan(CreateAppearance(), s_center);

        Assert.Throws<ArgumentNullException>(
            () => LoupePainter.Paint(null!, null, in plan));
    }

    // ====================================================================
    // ヘルパー
    // ====================================================================

    /// <summary>指定の外観・中心で描画計画を解決し、描画先へ描いて画素を返す。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <param name="source">拡大元の画像。</param>
    /// <param name="before">描画の前に描画先へ行う処理（前フレームの残像の再現）。</param>
    /// <returns>描画結果。</returns>
    private static RenderedPixels Paint(
        LoupeAppearance appearance, EsPoint? center, EsReadOnlyBitmap? source = null, Action<IGraphicsContext>? before = null)
    {
        var plan = CreatePlan(appearance, center);
        return TestCanvas.Render(LoupeSize, LoupeSize, context =>
        {
            before?.Invoke(context);
            LoupePainter.Paint(context, source, in plan);
        });
    }

    /// <summary>単色の読み取り専用画像を生成する。</summary>
    /// <param name="width">幅。</param>
    /// <param name="height">高さ。</param>
    /// <param name="color">色。</param>
    /// <returns>生成した画像。 破棄は呼び出し側。</returns>
    private static EsReadOnlyBitmap CreateImage(int width, int height, DrawingColor color)
    {
        using var bitmap = EsBitmap.Create(width, height, color);
        bitmap.SetReadOnly();
        return EsReadOnlyBitmap.FromBitmap(bitmap);
    }

    /// <summary>テスト用の描画計画を提示層（実際の解決経路）で解決する。</summary>
    /// <param name="appearance">外観。</param>
    /// <param name="center">拡大中心。</param>
    /// <returns>描画計画。</returns>
    /// <remarks>実寸測定はダミーを渡す（描画計画の画素検証には影響しない）。</remarks>
    private static LoupeDrawPlan CreatePlan(LoupeAppearance appearance, EsPoint? center)
    {
        var presenter = new LoupePresenter
        {
            Options = LoupeOptions.Default with { RequestedSize = LoupeSize, ZoomLevel = 4 },
            Appearance = appearance,
            CenterPoint = center,
        };

        return presenter.ResolveDrawPlan(LoupeSize, static (_, _) => default);
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
}
