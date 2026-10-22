using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Native;

/// <summary>OS 依存のカーソル操作を抽象化するインターフェイス。</summary>
/// <remarks>
/// UI フレームワーク標準で代替可能な機能（カーソル形状変更・イベント内のポインター位置取得）は<br/>
/// 本インターフェイスに含めない。 本インターフェイスには、UI フレームワーク標準に存在しない<br/>
/// <b>OS カーソルの位置取得</b>・<b>位置移動</b>・<b>表示/非表示</b>のみを残す。<br/>
/// 実装は各プラットフォーム（Windows: Win32 API）が提供する。<br/>
/// <br/>
/// 座標は UI フレームワーク非依存の <see cref="EsPointInt"/>（画面物理ピクセル）で扱う。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。<br/>
/// <br/>
/// <b>失敗の表し方</b>: 位置の取得と移動は <c>Try</c> 系の <see langword="bool"/> 戻り値で表す<br/>
/// （「該当なし」「回復できる状況」は値で返す。 [`.github/例外処理規約.md`](../../../../.github/例外処理規約.md) §3）。<br/>
/// <b>本層はログを出力しない</b>: 失敗の記録は呼び出し側が行う（失敗の文脈を知っているのは呼び出し側であるため）。
/// </remarks>
public interface ICursorService
{
    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <param name="point">取得したカーソル位置（画面物理座標）の出力先。 取得できなかった場合は既定値。</param>
    /// <returns>取得できた場合は <see langword="true"/>。 取得できなかった場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// WM_SETCURSOR 等の OS イベント外でカーソルの絶対位置が必要な場合に使用する。<br/>
    /// <b>失敗を原点として返さない理由</b>: 原点は画面の左上という<b>有効な座標</b>であるため、失敗を原点で表すと
    /// 呼び出し側が「左上へ移動する」という誤った操作を行う（失敗が座標として意味を持ってしまう）。
    /// </remarks>
    bool TryGetCursorScreenPoint(out EsPointInt point);

    /// <summary>カーソルを画面物理座標へ移動する。</summary>
    /// <param name="point">移動先の画面物理座標。</param>
    /// <returns>移動できた場合は <see langword="true"/>。 移動できなかった場合は <see langword="false"/>。</returns>
    /// <remarks>
    /// nudge（カーソルキーによる選択の移動）で使用する。<br/>
    /// <b>失敗を値で返す理由</b>: 移動は表示上の追従であり、失敗しても選択操作そのものは続行できる。
    /// </remarks>
    bool TrySetCursorScreenPoint(EsPointInt point);

    /// <summary>マウスポインターの表示/非表示を切り替える。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>、非表示にする場合は <see langword="false"/>。</param>
    /// <remarks>
    /// Win32 の <c>ShowCursor</c> は表示カウンタを増減するため、<br/>
    /// 非表示（false）と表示（true）を対で呼び出すことでカウンタのバランスを保つ。<br/>
    /// <b>失敗しない</b>: Win32 の <c>ShowCursor</c> はカウンタを返すだけで成否を表さないため、戻り値を持たない。
    /// </remarks>
    void ShowCursor(bool show);
}
