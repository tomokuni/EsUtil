namespace EsUtil.Platform.Core.Native;

/// <summary>ホットキー押下イベントの引数（押下されたホットキーに対応するアクション）。</summary>
/// <typeparam name="T">ホットキーに対応づけたアクションの型（本アプリでは <c>CaptureType</c>）。</typeparam>
/// <remarks>
/// イベントの型は <see cref="EventHandler{T}"/>（引数は <see cref="EventArgs"/> 派生）に統一する（CA1003）。<br/>
/// アクションの型を総称にすることで、ホットキーに何を対応づけるかを利用側が決められる。
/// </remarks>
/// <param name="action">押下されたホットキーに対応するアクション。</param>
public sealed class HotKeyPressedEventArgs<T>(T action) : EventArgs
{
    /// <summary>押下されたホットキーに対応するアクションを取得する。</summary>
    /// <value>登録時に対応づけたアクション。</value>
    public T Action { get; } = action;
}
