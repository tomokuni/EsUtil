using System.ComponentModel;
using EsUtil.Platform.Core.Window;

namespace EsUtil.Platform.Core.Native;

/// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）の計測を抽象化するインターフェイス。</summary>
/// <remarks>
/// <b>なぜ抽象にするか</b>: 枠の太さは環境（テーマ・DPI・OS のバージョン）で変わるため定数にできず、実測するしかない。
/// 抽象にすれば、テーマや DPI が異なる環境の値でも「外寸へ足す計算」を検証できる。<br/>
/// <b>失敗の表し方</b>: 測れない場合は <see cref="Win32Exception"/> を投げる（枠を無視した寸法でウィンドウを作ると
/// 最小サイズが破られるため、呼び出し側が気付けるようにする）。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時の差し替えが可能になる（DIP）。
/// </remarks>
public interface IWindowFrameMetrics
{
    /// <summary>ウィンドウの外寸とクライアント領域の差（枠の太さ）を取得する。</summary>
    /// <param name="handle">対象ウィンドウのネイティブ ハンドル。</param>
    /// <returns>枠の幅と高さ（画面物理ピクセル）。</returns>
    /// <exception cref="Win32Exception">外寸またはクライアント領域を取得できなかった場合。</exception>
    (int Width, int Height) GetSize(EsPlatformHandle handle);
}
