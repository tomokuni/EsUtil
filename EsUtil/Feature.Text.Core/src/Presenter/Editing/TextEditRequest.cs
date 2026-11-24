namespace EsUtil.Feature.Text.Presenter.Editing;

/// <summary>押されたキーから解決した編集の要求（操作と、選択を広げるかどうか）。</summary>
/// <param name="Command">実行する操作。</param>
/// <param name="Extend">選択を広げる場合は <see langword="true"/>（Shift を押しながらの移動）。</param>
public readonly record struct TextEditRequest(TextEditCommand Command, bool Extend)
{
    /// <summary>実行する操作があるかどうかを取得する。</summary>
    /// <value>操作がある場合は <see langword="true"/>。</value>
    public bool IsHandled => Command != TextEditCommand.None;
}
