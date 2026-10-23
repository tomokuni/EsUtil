using EsUtil.Platform.Core.Capture;
using EsUtil.Platform.Core.Common;

namespace EsUtil.Platform.Core;

/// <summary>キャプチャ（種別）機能の窓口。</summary>
/// <remarks>
/// 機能の型は <see cref="EsUtil.Platform.Core.Capture"/>（<see cref="CaptureType"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけで種別の解釈を行えるようにする</b>ための窓口であり、<br/>
/// <b>実装を持たない</b>（判断は各型が単一所有し、本クラスは委譲する）。
/// </remarks>
public static class EsCapture
{
    /// <summary>文字列（設定ファイル・コマンド ラインの値）からキャプチャ種別を解釈する。</summary>
    /// <param name="name">種別の名前。</param>
    /// <param name="captureType">解釈した種別。 解釈できなかった場合は <c>ScreenSelect</c>。</param>
    /// <returns>解釈できた場合は <see langword="true"/>。</returns>
    /// <remarks>数値文字列は受理しない（列挙に無い値を弾く。 判断は <see cref="EnumCanonical"/> が単一所有する）。</remarks>
    public static bool TryParseType(string name, out CaptureType captureType) => EnumCanonical.TryParse(name, out captureType);
}
