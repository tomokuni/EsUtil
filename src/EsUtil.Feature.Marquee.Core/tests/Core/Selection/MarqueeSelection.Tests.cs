using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Feature.Marquee.Core.Selection;
using EsUtil.Platform.Core.Geometry;
using Xunit;

namespace EsUtil.Feature.Marquee.Tests.Core.Selection;

/// <summary><see cref="MarqueeSelection"/> の単体テスト（選択矩形の値を扱う純粋な幾何計算）。</summary>
/// <remarks>
/// 矩形の値だけから決まる計算（正規化・ハンドル位置・対角・境界クリップ・ドラッグ変形）を検証する。<br/>
/// UI フレームワークに依存せず、ヘッドレス実行を必要としないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
public class MarqueeSelectionTests
{
    // ====================================================================
    // FromPoints（正規化）
    // ====================================================================

    [Fact]
    public void FromPoints_始点と終点から正規化された矩形を生成する()
    {
        // ドラッグ由来の選択は閉区間（始点・終点のピクセルを含む）
        var expected = new EsRect(10, 20, 30, 40, EsRectInterval.Closed);

        // 右下方向（通常）
        Assert.Equal(expected, MarqueeSelection.FromPoints(new EsPoint(10, 20), new EsPoint(40, 60)).Rect);
        // 左上方向（反転）
        Assert.Equal(expected, MarqueeSelection.FromPoints(new EsPoint(40, 60), new EsPoint(10, 20)).Rect);
        // X のみ反転
        Assert.Equal(expected, MarqueeSelection.FromPoints(new EsPoint(40, 20), new EsPoint(10, 60)).Rect);
        // Y のみ反転
        Assert.Equal(expected, MarqueeSelection.FromPoints(new EsPoint(10, 60), new EsPoint(40, 20)).Rect);
    }

    // ====================================================================
    // Handle（ハンドル位置）
    // ====================================================================

    [Fact]
    public void Handle_8方向ハンドルとMoveの位置を返す()
    {
        var selection = new MarqueeSelection(new EsRect(10, 20, 100, 80)); // Right=110 Bottom=100 Center=(60,60)

        Assert.Equal(new EsPoint(10, 20), selection.Handle(MarqueeHitTarget.TopLeft));
        Assert.Equal(new EsPoint(60, 20), selection.Handle(MarqueeHitTarget.TopCenter));
        Assert.Equal(new EsPoint(110, 20), selection.Handle(MarqueeHitTarget.TopRight));
        Assert.Equal(new EsPoint(10, 60), selection.Handle(MarqueeHitTarget.MiddleLeft));
        Assert.Equal(new EsPoint(110, 60), selection.Handle(MarqueeHitTarget.MiddleRight));
        Assert.Equal(new EsPoint(10, 100), selection.Handle(MarqueeHitTarget.BottomLeft));
        Assert.Equal(new EsPoint(60, 100), selection.Handle(MarqueeHitTarget.BottomCenter));
        Assert.Equal(new EsPoint(110, 100), selection.Handle(MarqueeHitTarget.BottomRight));
        Assert.Equal(new EsPoint(60, 60), selection.Handle(MarqueeHitTarget.Move));
    }

    [Fact]
    public void Handle_未知のハンドルは右下隅を返す()
    {
        var selection = new MarqueeSelection(new EsRect(10, 20, 100, 80));

        Assert.Equal(selection.Rect.BottomRight, selection.Handle((MarqueeHitTarget)999));
    }

    // ====================================================================
    // OppositeCorner（始点と反対側の隅）
    // ====================================================================

    [Fact]
    public void OppositeCorner_始点と反対側の隅を返す()
    {
        var selection = new MarqueeSelection(new EsRect(10, 20, 100, 80));

        // 始点が左上（rect.X, rect.Y に一致）→ 右下（Right, Bottom）
        Assert.Equal(new EsPoint(110, 100), selection.OppositeCorner(new EsPoint(10, 20)));
        // 始点が右上（rect.Right, rect.Y に一致）→ 左下（X, Bottom）
        Assert.Equal(new EsPoint(10, 100), selection.OppositeCorner(new EsPoint(110, 20)));
        // 始点が左下（rect.X, rect.Bottom に一致）→ 右上（Right, Y）
        Assert.Equal(new EsPoint(110, 20), selection.OppositeCorner(new EsPoint(10, 100)));
        // 始点が右下（rect.Right, rect.Bottom に一致）→ 左上（X, Y）
        Assert.Equal(new EsPoint(10, 20), selection.OppositeCorner(new EsPoint(110, 100)));
    }

    // ====================================================================
    // ClampToBounds（ハンドル固定のクリップ）
    // ====================================================================

    [Fact]
    public void ClampToBounds_Moveは矩形全体を境界内へシフトする()
    {
        var bounds = new EsRect(0, 0, 200, 100);
        // 左下方向へはみ出した矩形（Move 中）→ 境界内へシフト
        var selection = new MarqueeSelection(new EsRect(180, 80, 50, 40)); // Right=230 Bottom=120

        var result = selection.ClampToBounds(bounds, MarqueeHitTarget.Move);

        Assert.Equal(new EsRect(150, 60, 50, 40), result.Rect); // サイズは維持・シフトのみ
    }

    [Fact]
    public void ClampToBounds_ハンドル変形は固定角を維持して辺のみクランプする()
    {
        var bounds = new EsRect(0, 0, 200, 100);
        // TopLeft ハンドルが左端・上端を超える → 固定角（右下）を維持し、左・上のみクランプ
        var selection = new MarqueeSelection(new EsRect(-20, -10, 100, 80)); // Right=80 Bottom=70

        var result = selection.ClampToBounds(bounds, MarqueeHitTarget.TopLeft);

        Assert.Equal(new EsRect(0, 0, 80, 70), result.Rect);
    }

    // ====================================================================
    // Drag（ドラッグ変形）
    // ====================================================================

    [Fact]
    public void Drag_TopLeftハンドルで変形する()
    {
        // 元矩形 (10,20,100,80)、TopLeft を (dx=30, dy=10) 移動 → l=40, t=30, r=110, b=100 → (40,30,70,70)
        var result = new MarqueeSelection(new EsRect(10, 20, 100, 80)).Drag(30, 10, MarqueeHitTarget.TopLeft, 10);
        Assert.Equal(new EsRect(40, 30, 70, 70), result.Rect);
    }

    [Fact]
    public void Drag_各辺ハンドルは1軸のみ変形する()
    {
        var selection = new MarqueeSelection(new EsRect(10, 20, 100, 80));

        // TopCenter は上辺のみ、MiddleLeft は左辺のみを動かす
        Assert.Equal(new EsRect(10, 30, 100, 70), selection.Drag(0, 10, MarqueeHitTarget.TopCenter, 10).Rect);
        Assert.Equal(new EsRect(20, 20, 90, 80), selection.Drag(10, 0, MarqueeHitTarget.MiddleLeft, 10).Rect);
        Assert.Equal(new EsRect(10, 20, 110, 80), selection.Drag(10, 0, MarqueeHitTarget.MiddleRight, 10).Rect);
        Assert.Equal(new EsRect(10, 20, 100, 90), selection.Drag(0, 10, MarqueeHitTarget.BottomCenter, 10).Rect);
    }

    [Fact]
    public void Drag_Moveで全体を平行移動する()
    {
        var result = new MarqueeSelection(new EsRect(10, 20, 100, 80)).Drag(5, -10, MarqueeHitTarget.Move, 10);
        Assert.Equal(new EsRect(15, 10, 100, 80), result.Rect);
    }

    [Fact]
    public void Drag_反対側までドラッグすると矩形が反転される()
    {
        // TopLeft を右へ移動し l > r になる → 反転（正規化）
        var result = new MarqueeSelection(new EsRect(10, 20, 100, 80)).Drag(200, 0, MarqueeHitTarget.TopLeft, 10);
        // l=210, r=110 → x1=110, x2=210
        Assert.Equal(new EsRect(110, 20, 100, 80), result.Rect);
    }

    [Fact]
    public void Drag_最小サイズ未満は拡張される()
    {
        // BottomRight を (dx=-95, dy=-95): r=15, b=5, l=10, t=20 → x1=10,x2=15(幅5)→最小10へ、y1=5,y2=20(高さ15)維持
        var result = new MarqueeSelection(new EsRect(10, 20, 100, 80)).Drag(-95, -95, MarqueeHitTarget.BottomRight, 10);
        Assert.Equal(new EsRect(10, 5, 10, 15), result.Rect);
    }

    [Fact]
    public void Drag_渡された最小サイズをそのまま使う()
    {
        // 最小サイズの補正（下限 1.0）は設定値の生成時に済ませる（規則は MarqueeCore.Limits が単一所有する）
        var result = new MarqueeSelection(new EsRect(10, 20, 0.2, 0.2)).Drag(0, 0, MarqueeHitTarget.Move, 10);

        Assert.Equal(10.0, result.Rect.Width);
        Assert.Equal(10.0, result.Rect.Height);
    }
}
