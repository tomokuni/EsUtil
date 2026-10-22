using EsUtil.Platform.Core;
using Windows.System;

namespace EsUtil.Ui.WinUI3.Marquee.Internal;

/// <summary>WinUI 3 のキー型（<see cref="VirtualKey"/>）を UI フレームワーク非依存の <see cref="EsKey"/> へ変換する。</summary>
/// <remarks>
/// <b>対応付けは値ではなく名前で行う</b>: <see cref="VirtualKey"/> の値は Win32 の仮想キーコードであるのに対し、<br/>
/// <see cref="EsKey"/> の値は列挙の序数であり、体系が異なる（例: <c>D0</c> ⇔ <c>Number0</c> / <c>NumLock</c> ⇔ <c>NumberKeyLock</c>）。<br/>
/// また <see cref="VirtualKey"/> はキー以外の入力（マウス ボタン・ゲームパッド）と記号キー（<c>Oem*</c>）・メディア キーを持たないため、<br/>
/// <b>全メンバーの対応表は作らない</b>（本コントロールが入力として解釈するキーだけを写す）。<br/>
/// <br/>
/// <b>本コントロールが解釈するキー</b>: キャンセル（Escape）/ 確定（Enter）/ 微調整（矢印 4 方向）である<br/>
/// （それ以外のキーは <see cref="EsKey.None"/> を返し、コアは入力を処理しない）。<br/>
/// コアは Enter と Return を同一のキーとして扱うため、<see cref="VirtualKey.Enter"/> は <see cref="EsKey.Enter"/> へ写す。<br/>
/// UI スレッドに依存しないため <c>[Fact]</c> で並列検証できる。
/// </remarks>
internal static class EsKeyWinUI3Interop
{
    /// <summary>WinUI 3 のキーを <see cref="EsKey"/> へ変換する。</summary>
    /// <param name="key">変換対象のキー。</param>
    /// <returns>
    /// 本コントロールが解釈するキー（矢印 4 方向・Escape・Enter）に対応する <see cref="EsKey"/>。<br/>
    /// それ以外のキーとキー以外の入力（マウス ボタン・ゲームパッド）は <see cref="EsKey.None"/>。
    /// </returns>
    public static EsKey ToEsKey(this VirtualKey key) => key switch
    {
        VirtualKey.Left => EsKey.Left,
        VirtualKey.Up => EsKey.Up,
        VirtualKey.Right => EsKey.Right,
        VirtualKey.Down => EsKey.Down,
        VirtualKey.Escape => EsKey.Escape,
        VirtualKey.Enter => EsKey.Enter,
        _ => EsKey.None,
    };

    /// <summary>WinUI 3 のキーが本コントロールの入力として解釈できるかどうかを判定する。</summary>
    /// <param name="key">判定対象のキー。</param>
    /// <returns>矢印 4 方向・Escape・Enter のいずれかの場合は true。</returns>
    public static bool IsRepresentable(this VirtualKey key) => key.ToEsKey() != EsKey.None;
}
