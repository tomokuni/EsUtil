
namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>書式バーを作る処理（差し替えの入口）。</summary>
/// <remarks>
/// <b>独自の GUI を使うときに差し替える</b>: ホストはパネルの <c>FormatBarFactory</c> へ本インターフェイスの
/// 実装を設定する。 既定は <see cref="StyledTextFormatBarFactory"/>（組み込みのバー）。
/// </remarks>
public interface IStyledTextFormatBarFactory
{
    /// <summary>書式バーを作る。</summary>
    /// <param name="context">書式を読み書きする相手。</param>
    /// <param name="options">バーの構成。</param>
    /// <returns>書式バー。</returns>
    IStyledTextFormatBar Create(ITextFormatBarContext context, StyledTextFormatBarOptions options);
}
