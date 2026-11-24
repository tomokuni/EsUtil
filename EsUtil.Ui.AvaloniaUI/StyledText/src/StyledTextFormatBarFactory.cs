namespace EsUtil.Ui.AvaloniaUI.StyledText;

/// <summary>組み込みの書式バーを作る処理。</summary>
public sealed class StyledTextFormatBarFactory : IStyledTextFormatBarFactory
{
    /// <inheritdoc/>
    public IStyledTextFormatBar Create(ITextFormatBarContext context, StyledTextFormatBarOptions options)
        => new StyledTextFormatBar(context, options);
}