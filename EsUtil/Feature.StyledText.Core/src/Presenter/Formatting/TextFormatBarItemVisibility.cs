using System.Collections.Immutable;

namespace EsUtil.Feature.StyledText.Presenter.Formatting;

/// <summary>書式バーに出す項目の組み合わせ（どこを出すかを単一所有する）。</summary>
/// <remarks>
/// <b>出さない項目は場所ごと詰める</b>: 空の枠を残すと、押せない余白が挟まって「間延びした」バーになる。
/// 4 つの UI 版は <see cref="VisibleItems"/> の順にだけ要素を並べる（並び順は <see cref="TextFormatBarLayout.Order"/>
/// が単一所有する）。<br/>
/// <b>すべて出さない場合はバー自体を出さない</b>: 空のバーを出すと、操作できない枠だけが本文に重なる。
/// 判定は <see cref="TextFormatBarVisibility.ShouldShow"/> が本値を見て行う。<br/>
/// <b>値は不変</b>である（部分更新は <c>with</c> 式で行う）。
/// </remarks>
public sealed record TextFormatBarItemVisibility
{
    /// <summary>すべての項目を出す既定の組み合わせを取得する。</summary>
    public static TextFormatBarItemVisibility Default { get; } = new();

    /// <summary>太字を出すかどうかを取得する。</summary>
    public bool ShowBold { get; init; } = true;

    /// <summary>斜体を出すかどうかを取得する。</summary>
    public bool ShowItalic { get; init; } = true;

    /// <summary>下線を出すかどうかを取得する。</summary>
    public bool ShowUnderline { get; init; } = true;

    /// <summary>取り消し線を出すかどうかを取得する。</summary>
    public bool ShowStrike { get; init; } = true;

    /// <summary>色を出すかどうかを取得する。</summary>
    public bool ShowColor { get; init; } = true;

    /// <summary>フォントの大きさを出すかどうかを取得する。</summary>
    public bool ShowFontSize { get; init; } = true;

    /// <summary>フォント名を出すかどうかを取得する。</summary>
    public bool ShowFont { get; init; } = true;

    /// <summary>1 つ以上の項目を出すかどうかを取得する。</summary>
    /// <value>すべて出さない場合は <see langword="false"/>。</value>
    public bool HasAny => ShowBold || ShowItalic || ShowUnderline || ShowStrike || ShowColor || ShowFontSize || ShowFont;

    /// <summary>出す項目を並び順に並べたものを取得する。</summary>
    /// <value>出さない項目を含まない並び（すべて出さない場合は空）。</value>
    /// <remarks>並びは <see cref="TextFormatBarLayout.Order"/> に従う（UI が独自の順で並べない）。</remarks>
    public ImmutableArray<TextFormatBarItem> VisibleItems
    {
        get
        {
            var builder = ImmutableArray.CreateBuilder<TextFormatBarItem>(TextFormatBarLayout.Order.Count);
            foreach (var item in TextFormatBarLayout.Order)
            {
                if (ShouldShow(item))
                {
                    builder.Add(item);
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>指定した項目を出すかどうかを取得する。</summary>
    /// <param name="item">項目。</param>
    /// <returns>出す場合は <see langword="true"/>。</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="item"/> が定義された値でない場合。</exception>
    public bool ShouldShow(TextFormatBarItem item) => item switch
    {
        TextFormatBarItem.Bold => ShowBold,
        TextFormatBarItem.Italic => ShowItalic,
        TextFormatBarItem.Underline => ShowUnderline,
        TextFormatBarItem.Strike => ShowStrike,
        TextFormatBarItem.Color => ShowColor,
        TextFormatBarItem.Size => ShowFontSize,
        TextFormatBarItem.Font => ShowFont,
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, "書式バーの項目として定義されていません。"),
    };
}
