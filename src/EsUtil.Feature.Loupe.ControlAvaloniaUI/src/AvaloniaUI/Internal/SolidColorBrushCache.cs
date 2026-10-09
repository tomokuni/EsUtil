using Avalonia.Media;
using DrawingColor = System.Drawing.Color;


// 複製元: EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging（独立パッケージ用の意図的な複製。 .github/依存関係.md §4.1）
namespace EsUtil.Feature.Loupe.AvaloniaUI.Internal;

/// <summary>コア色型に対応するブラシを容量制限付きで共有するキャッシュ（Flyweight）。</summary>
/// <remarks>
/// <b>位置付け</b>: 描画とラベル要素の配置はどちらも「色 → ブラシ」の変換を必要とするため、<br/>
/// 変換と容量管理（無制限のメモリ増加の防止）の所有者を本クラスへ集約する<br/>
/// （描画アダプタとラベル要素の配置が別々にキャッシュを持つと、同じ色が二重に保持され容量も二重になる）。<br/>
/// <br/>
/// <b>変換</b>: 色の型変換は <see cref="ColorAvaloniaInterop"/> が単一所有する（本クラスは呼ぶだけ）。<br/>
/// ブラシは不変であり、同じ色で使い回しても描画結果は変わらない。<br/>
/// <br/>
/// <b>容量</b>: 容量に達すると全体をクリアする（<see cref="CapacityLimitedCache{TKey, TValue}"/> が単一所有する）。
/// </remarks>
internal static class SolidColorBrushCache
{
    /// <summary>色に対応するブラシのキャッシュ（容量制限付き）。</summary>
    private static readonly CapacityLimitedCache<Color, SolidColorBrush> s_brushes =
        new(static color => new SolidColorBrush(color));

    /// <summary>色に対応するブラシを取得する（同じ色は共有する）。</summary>
    /// <param name="color">ブラシへ変換する色。</param>
    /// <returns>色に対応するブラシ。</returns>
    public static SolidColorBrush GetOrCreate(DrawingColor color)
        => s_brushes.GetOrCreate(color.ToAvaloniaColor());
}
