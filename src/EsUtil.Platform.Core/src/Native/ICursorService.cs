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
/// 具象実装ではなく本インターフェイスへ依存させることで、以下を達成する（DIP）:<br/>
/// - 他プラットフォームでは実装を注入して差し替え可能<br/>
/// - テスト時のモックが可能<br/>
/// </remarks>
public interface ICursorService
{
    /// <summary>現在のカーソル位置を画面物理座標で取得する。</summary>
    /// <returns>現在のカーソル位置（画面物理座標）。</returns>
    /// <remarks>WM_SETCURSOR 等の OS イベント外でカーソルの絶対位置が必要な場合に使用する。</remarks>
    EsPointInt GetCursorScreenPoint();

    /// <summary>カーソルを画面物理座標に移動する。</summary>
    /// <param name="point">移動先の画面物理座標。</param>
    void SetCursorScreenPoint(EsPointInt point);

    /// <summary>マウスポインターの表示/非表示を切り替える。</summary>
    /// <param name="show">表示する場合は <see langword="true"/>、非表示にする場合は <see langword="false"/>。</param>
    void ShowCursor(bool show);
}
