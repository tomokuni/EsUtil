using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Window;

namespace EsUtil.Platform.Core;

/// <summary>ウィンドウ（ネイティブ ハンドルと表示状態）機能の窓口。</summary>
/// <remarks>
/// 機能の型は <see cref="EsUtil.Platform.Core.Window"/>（<see cref="EsPlatformHandle"/> / <see cref="EsWindowState"/>）に置く。<br/>
/// 本クラスは<b>利用側が <c>using EsUtil.Platform.Core;</c> だけでハンドルの生成と状態の解釈を行えるようにする</b>ための窓口であり、<br/>
/// <b>実装を持たない</b>（判断は各型が単一所有し、本クラスは委譲する）。
/// </remarks>
public static class EsWindow
{
    /// <summary>ネイティブ ハンドル値からハンドルを生成する。</summary>
    /// <param name="handle">ネイティブ ハンドル値（Win32: HWND）。 未取得時は 0。</param>
    /// <param name="descriptor">ハンドルの種別を表す説明（例: "HWND"）。 不明な場合は null。</param>
    /// <returns>生成したハンドル。</returns>
    public static EsPlatformHandle FromHandle(nint handle, string? descriptor = null) => new(handle, descriptor);

    /// <summary>文字列（設定ファイルの値）から表示状態を解釈する。</summary>
    /// <param name="name">表示状態の名前。</param>
    /// <param name="state">解釈した表示状態。 解釈できなかった場合は <c>Normal</c>。</param>
    /// <returns>解釈できた場合は <see langword="true"/>。</returns>
    /// <remarks>数値文字列は受理しない（列挙に無い値を弾く。 判断は <see cref="EnumCanonical"/> が単一所有する）。</remarks>
    public static bool TryParseState(string name, out EsWindowState state) => EnumCanonical.TryParse(name, out state);
}
