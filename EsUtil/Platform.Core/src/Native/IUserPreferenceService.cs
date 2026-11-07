namespace EsUtil.Platform.Core.Native;

/// <summary>OS のユーザー設定（操作の感覚に影響する値）を抽象化するインターフェイス。</summary>
/// <remarks>
/// UI フレームワークが公開しない OS の設定値を提供する（Windows: <c>GetDoubleClickTime</c>）。<br/>
/// コントロールが固定値を決めると、OS の設定を変えた利用者と操作の感覚が食い違うため、値を照会して使う。<br/>
/// 実装は各プラットフォーム（Windows: Win32 API）が提供する。<br/>
/// 具象実装ではなく本インターフェイスへ依存させることで、テスト時のモックが可能になる（DIP）。<br/>
/// <b>本層はログを出力しない</b>: 失敗の記録は呼び出し側が行う。
/// </remarks>
public interface IUserPreferenceService
{
    /// <summary>ダブルクリックと見なす時間の上限（ミリ秒）を取得する。</summary>
    /// <value>上限（ミリ秒）。 OS の設定値（既定は 500）。</value>
    long DoubleClickIntervalMilliseconds { get; }
}
