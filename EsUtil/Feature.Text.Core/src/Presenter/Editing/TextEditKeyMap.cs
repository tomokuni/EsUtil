namespace EsUtil.Feature.Text.Presenter.Editing;

/// <summary>キーの割り当て（どのキーがどの編集の操作になるか）の単一所有。</summary>
/// <remarks>
/// <b>割り当てを 1 か所で決める</b>: 修飾キーとキーの組み合わせは利用者の慣習で決まるため、
/// UI ごとに書くと 4 つの UI 版で操作が食い違う。 各 UI は自分のキーの型を <see cref="TextEditKey"/> と
/// <see cref="TextEditModifiers"/> へ写して本クラスを呼び、返された操作を実行するだけにする。<br/>
/// <b>割り当ての規則</b>: 装飾は Control + B / I / U、取り消し線は Alt + Shift + 5、終了は Escape と
/// Control + Enter、改行は Enter、削除は Back / Delete、移動は矢印と Home / End、選択は Control + A、
/// クリップボードは Control + C / X / V。 矢印と Home / End は Shift を押すと選択を広げる。
/// </remarks>
public static class TextEditKeyMap
{
    /// <summary>押されたキーと修飾キーから編集の操作を解決する。</summary>
    /// <param name="key">押されたキー。</param>
    /// <param name="modifiers">押されていた修飾キー。</param>
    /// <returns>解決した要求。 割り当ての無いキーは <see cref="TextEditCommand.None"/>。</returns>
    public static TextEditRequest Resolve(TextEditKey key, TextEditModifiers modifiers)
    {
        var control = (modifiers & TextEditModifiers.Control) != 0;
        var shift = (modifiers & TextEditModifiers.Shift) != 0;
        var alt = (modifiers & TextEditModifiers.Alt) != 0;

        // 装飾（一般的な表記に合わせる。 取り消し線だけは Alt + Shift + 5）
        if (control && !shift && !alt)
        {
            switch (key)
            {
                case TextEditKey.B:
                    return new TextEditRequest(TextEditCommand.ToggleBold, false);
                case TextEditKey.I:
                    return new TextEditRequest(TextEditCommand.ToggleItalic, false);
                case TextEditKey.U:
                    return new TextEditRequest(TextEditCommand.ToggleUnderline, false);
            }
        }

        if (alt && shift && !control && key == TextEditKey.Digit5)
        {
            return new TextEditRequest(TextEditCommand.ToggleStrike, false);
        }

        // 選択を広げるのは移動のキーだけである（削除・改行・終了は広げない）
        switch (key)
        {
            case TextEditKey.Escape:
                return new TextEditRequest(TextEditCommand.EndEditing, false);
            case TextEditKey.Enter:
                return control
                    ? new TextEditRequest(TextEditCommand.EndEditing, false)
                    : new TextEditRequest(TextEditCommand.InsertLineBreak, false);
            case TextEditKey.Back:
                return new TextEditRequest(TextEditCommand.DeleteBackward, false);
            case TextEditKey.Delete:
                return new TextEditRequest(TextEditCommand.DeleteForward, false);
            case TextEditKey.Left:
                return new TextEditRequest(TextEditCommand.CaretLeft, shift);
            case TextEditKey.Right:
                return new TextEditRequest(TextEditCommand.CaretRight, shift);
            case TextEditKey.Up:
                return new TextEditRequest(TextEditCommand.CaretUp, shift);
            case TextEditKey.Down:
                return new TextEditRequest(TextEditCommand.CaretDown, shift);
            case TextEditKey.Home:
                return new TextEditRequest(TextEditCommand.CaretLineStart, shift);
            case TextEditKey.End:
                return new TextEditRequest(TextEditCommand.CaretLineEnd, shift);
            case TextEditKey.A when control:
                return new TextEditRequest(TextEditCommand.SelectAll, false);
            case TextEditKey.C when control:
                return new TextEditRequest(TextEditCommand.Copy, false);
            case TextEditKey.X when control:
                return new TextEditRequest(TextEditCommand.Cut, false);
            case TextEditKey.V when control:
                return new TextEditRequest(TextEditCommand.Paste, false);
            default:
                return default;
        }
    }
}
