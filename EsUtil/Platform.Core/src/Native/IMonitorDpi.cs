using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native;

/// <summary>指定位置のモニターの実効 DPI の照会を抽象化するインターフェイス。</summary>
/// <remarks>
/// <b>なぜ抽象にするか</b>: DPI はモニターの構成（台数・倍率）に依存し、テストでは 100% と 150% が混在する環境を
/// 作れない。 抽象にすれば、倍率の丸め・最小サイズの計算を任意の倍率で検証できる。<br/>
/// <b>失敗を例外にしない</b>: 照会できない場合（モニターが無い・API が失敗した）は <see langword="null"/> を返す
/// （呼び出し側が代替値で解決する）。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時の差し替えが可能になる（DIP）。
/// </remarks>
public interface IMonitorDpi
{
    /// <summary>指定位置を含むモニターの実効 DPI を取得する。</summary>
    /// <param name="screenPoint">照会する位置（画面物理座標）。</param>
    /// <returns>実効 DPI（96 = 100%）。 照会できない場合は <see langword="null"/>。</returns>
    uint? GetEffectiveDpi(EsPointInt screenPoint);
}
