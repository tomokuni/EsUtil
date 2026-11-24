namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーを作る処理（差し替えの入口）。</summary>
public interface IStyledTextFormatBarFactory
{
    /// <summary>書式バーを作る。</summary>
    /// <param name="context">書式を読み書きする相手。</param>
    /// <param name="options">バーの構成。</param>
    /// <returns>書式バー。</returns>
    IStyledTextFormatBar Create(ITextFormatBarContext context, StyledTextFormatBarOptions options);
}