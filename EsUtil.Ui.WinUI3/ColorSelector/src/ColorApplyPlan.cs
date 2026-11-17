namespace EsUtil.Ui.WinUI3.ColorSelector;

/// <summary>一覧・カラーピッカーで色を確定したときの適用の計画。</summary>
/// <param name="Value">適用する色（色名または <c>#AARRGGBB</c>）。</param>
/// <param name="ThemeKey">テーマの色のときの識別子（例: <c>背景</c>）。 色名・カスタムは null。</param>
/// <param name="RaiseApplied"><see cref="ColorSelectorControl.ColorApplied"/> を発生させるか。</param>
/// <remarks>
/// <b>確定の扱いを 1 箇所で決める</b>: 一覧とカラーピッカーの 2 経路が同じ計画を使うため、
/// 「同じ色を選び直したときも通知する」規則が片方だけ欠けることがない。<br/>
/// UI を持たないため、XAML ランタイム無しのテストからも規則を検証できる。
/// </remarks>
internal readonly record struct ColorApplyPlan(string? Value, string? ThemeKey, bool RaiseApplied);
