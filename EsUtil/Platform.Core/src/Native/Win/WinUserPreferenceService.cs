using Windows.Win32;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary>Win32 API を用いた OS のユーザー設定の照会を提供する。</summary>
/// <remarks>
/// <b>Win32 の宣言は CsWin32 が生成する</b>（<c>src/NativeMethods.txt</c>）。 本クラスは <c>PInvoke</c> を直接呼ぶ。<br/>
/// <see cref="IUserPreferenceService"/> の実装であり、テスト時のモックに備える。<br/>
/// 継承不可。
/// </remarks>
public sealed class WinUserPreferenceService : IUserPreferenceService
{
    /// <summary>ダブルクリックと見なす時間の上限（ミリ秒）を取得する。</summary>
    /// <inheritdoc/>
    public long DoubleClickIntervalMilliseconds => PInvoke.GetDoubleClickTime();
}
