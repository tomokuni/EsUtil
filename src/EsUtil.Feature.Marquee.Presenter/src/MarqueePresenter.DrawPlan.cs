using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Feature.Marquee;

/// <summary>選択矩形の描画計画のうち、装飾（暗転マスク・枠線）の矩形の導出を担う部分。</summary>
/// <remarks>
/// コアが返す幾何（描画面・注目矩形）と外観（表示可否・枠線の太さ）から、描く矩形を導出する。<br/>
/// 注目矩形（DIP・閉区間解決済み）はコアが単一所有するため、本クラスは「与えられた矩形から何を描くか」だけを計算する。<br/>
/// 導出は本クラスが単一所有し、描画実装は返された矩形を自身の描画 API へ渡すだけにする（二重所有を防ぐ）。<br/>
/// <b>可視性</b>: 内部手順であるため private static とし、公開面（描画計画）だけを UI へ渡す。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public partial class MarqueePresenter
{
    // ====================================================================
    // 装飾（枠線・暗転領域）の矩形
    // ====================================================================

    /// <summary>矩形の四辺に描く枠線の矩形を計算する。</summary>
    /// <param name="rect">対象の矩形（DIP 座標）。</param>
    /// <param name="thickness">枠線の太さ（DIP）。 補正済みの値を渡す（安全域は <c>MarqueeCore.Limits</c> が単一所有する）。</param>
    /// <returns>上・下・左・右の順の枠線矩形（4 個）。</returns>
    /// <remarks>
    /// 辺の矩形の計算は <see cref="EsRect.ComputeInnerEdges(double)"/> が単一所有する（ルーペの枠線と同じ式）。<br/>
    /// 太さは矩形の短辺で上限を取る（短辺より太い枠線は矩形全体を覆い、矩形からはみ出さない）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeBorderDipRects(EsRect rect, double thickness)
        => rect.ComputeInnerEdges(Math.Min(thickness, Math.Min(rect.Width, rect.Height)));

    /// <summary>描画面から注目矩形を除いた暗転領域（4 帯）を計算する。</summary>
    /// <param name="surface">描画面の全体（DIP。 左上を原点とする矩形）。</param>
    /// <param name="focus">注目矩形（DIP）。 null の場合は全面（1 領域）。</param>
    /// <returns>暗転して塗る矩形の一覧（上・下・左・右の順）。 幅・高さが 0 以下の領域は含めない。</returns>
    /// <remarks>
    /// 暗転領域は描画面（<paramref name="surface"/>）を基準に計算する（描画 API のコンテキスト原点は常にコントロールの左上であるため、<br/>
    /// 親座標を混ぜるとマスクが原点分ずれる）。<br/>
    /// 注目矩形が完全に描画面の外の場合は全面を返す（描画漏れを防ぐ）。
    /// </remarks>
    internal static IReadOnlyList<EsRect> ComputeOverlayDipRects(EsRect surface, EsRect? focus)
    {
        if (focus is not { } h)
        {
            return [surface];
        }

        // 注目矩形を描画面内へクランプする（領域外の部分は暗転しない）
        var left = Math.Clamp(h.X, surface.X, surface.Right);
        var top = Math.Clamp(h.Y, surface.Y, surface.Bottom);
        var right = Math.Clamp(h.Right, surface.X, surface.Right);
        var bottom = Math.Clamp(h.Bottom, surface.Y, surface.Bottom);

        if (right <= left || bottom <= top)
        {
            // 注目矩形が描画面と交差しない: 全面を暗転する
            return [surface];
        }

        var regions = new List<EsRect>(4);
        AddRegion(regions, new EsRect(surface.X, surface.Y, surface.Width, top - surface.Y));       // 上
        AddRegion(regions, new EsRect(surface.X, bottom, surface.Width, surface.Bottom - bottom));  // 下
        AddRegion(regions, new EsRect(surface.X, top, left - surface.X, bottom - top));             // 左
        AddRegion(regions, new EsRect(right, top, surface.Right - right, bottom - top));            // 右
        return regions;
    }

    /// <summary>幅・高さが正の矩形のみを追加する。</summary>
    /// <param name="regions">追加先のリスト。</param>
    /// <param name="rect">追加する矩形。</param>
    private static void AddRegion(List<EsRect> regions, EsRect rect)
    {
        if (rect.Width > 0 && rect.Height > 0)
        {
            regions.Add(rect);
        }
    }
}
