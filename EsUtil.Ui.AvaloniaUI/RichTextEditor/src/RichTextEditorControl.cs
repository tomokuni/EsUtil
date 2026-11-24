using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace EsUtil.Ui.AvaloniaUI.RichTextEditor;

/// <summary>本文を表示し、その場で編集するコントロール（表示と編集を 1 つの部品にまとめる）。</summary>
/// <remarks>
/// <b>表示と編集を分けない</b>: 入力欄を重ねる方法では、書式（run 単位の色・太字）が反映されず、
/// 書式バーを操作するたびにフォーカスと選択が外れる。 本コントロールは本文を自分で描き、キー入力のたびに
/// 本文を書き換える（書式はその場で反映される）。<br/>
/// <b>内容は <see cref="Text"/> が単一所有する</b>: 編集中も <see cref="Text"/> が真の値であり、変更のたびに
/// <see cref="TextChanged"/> を発生させる（ホストは文書へ写すだけでよい）。<br/>
/// <b>規則は提示層が単一所有する</b>: キャレット・選択・書式の適用は <see cref="TextEditState"/>、
/// キーの割り当ては <see cref="TextEditKeyMap"/>、折り返しと行の配置は <see cref="TextLayout"/> が決める。
/// 本コントロールはフレームワーク固有の描画・入力・IME・クリップボードの配線だけを行う。<br/>
/// <b>座標は元画像ピクセル</b>である: <see cref="RenderScale"/> で画面の DIP へ写すため、拡大しても
/// 保存画像と同じ比率で文字が並ぶ。
/// </remarks>
public sealed partial class RichTextEditorControl : Control
{
    /// <summary>表示する本文。</summary>
    public static readonly StyledProperty<RichText> TextProperty =
        AvaloniaProperty.Register<RichTextEditorControl, RichText>(
            nameof(Text), RichText.Empty, inherits: false);

    /// <summary>画面の DIP への倍率（1 以上で拡大）。</summary>
    public static readonly StyledProperty<double> RenderScaleProperty =
        AvaloniaProperty.Register<RichTextEditorControl, double>(nameof(RenderScale), 1.0);

    /// <summary>行間の倍率。</summary>
    public static readonly StyledProperty<double> LineHeightFactorProperty =
        AvaloniaProperty.Register<RichTextEditorControl, double>(nameof(LineHeightFactor), 1.0);

    /// <summary>本文を編集中かどうか。</summary>
    public static readonly DirectProperty<RichTextEditorControl, bool> IsEditingProperty =
        AvaloniaProperty.RegisterDirect<RichTextEditorControl, bool>(
            nameof(IsEditing), control => control.IsEditing);

    /// <summary>表示する本文を取得または設定する。</summary>
    public RichText Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>画面の DIP への倍率を取得または設定する。</summary>
    /// <value>1 以上で拡大。 1 未満・非有限は 1 として扱う。</value>
    public double RenderScale
    {
        get => GetValue(RenderScaleProperty);
        set => SetValue(RenderScaleProperty, value);
    }

    /// <summary>行間の倍率を取得または設定する。</summary>
    /// <value>1 未満・非有限は 1 として扱う。</value>
    public double LineHeightFactor
    {
        get => GetValue(LineHeightFactorProperty);
        set => SetValue(LineHeightFactorProperty, value);
    }

    /// <summary>本文を編集中かどうかを取得する。</summary>
    public bool IsEditing => _edit.IsActive;

    /// <summary>文字列と書式から実寸を測る関数を取得または設定する。</summary>
    /// <value>既定は本コントロールの描画 API で測る関数。 描画と同じエンジンで測るため通常は変更しない。</value>
    /// <remarks>
    /// <b>差し替えられるようにする</b>: 折り返しとキャレットの位置は実寸に依存するため、ホストが
    /// 「描画に使うのと同じ計測」を渡せる必要がある（テストでは固定の計測を渡して位置だけを検証する）。
    /// </remarks>
    public Func<string, TextStyle, EsUtil.Platform.Core.Geometry.EsSize> MeasureText { get; set; }
        = RichTextPainter.Measure;

    /// <summary>本文を書き換えたときに発生する。</summary>
    /// <remarks>ホストは文書（または設定）へ写す。 写した結果を <see cref="Text"/> へ戻さないこと（往復する）。</remarks>
    public event EventHandler? TextChanged;

    /// <summary>キャレットまたは選択範囲が変わったときに発生する。</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>編集の開始・終了で発生する。</summary>
    public event EventHandler? EditingChanged;

    /// <summary>本文を折り返して行へ配置する（表示と編集で同じ結果を使う）。</summary>
    /// <returns>行の並び（本文が空・大きさが 0 の場合は空）。</returns>
    private IReadOnlyList<TextLinePlan> ResolveLines()
    {
        var scale = ResolveScale();
        var width = Bounds.Width / scale;
        var height = Bounds.Height / scale;
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        return TextLayout.ResolveLines(
            Text,
            new EsUtil.Platform.Core.Geometry.EsRect(0, 0, width, height),
            MeasureText,
            LineHeightFactor);
    }

    /// <summary>行と文字位置の対応を作る。</summary>
    /// <returns>対応。 行が無い場合は <see langword="null"/>。</returns>
    private TextLineIndex? ResolveLineIndex() => TextLineIndex.Create(ResolveLines(), Text.PlainText);

    /// <summary>表示倍率を取得する（1 未満・非有限は 1）。</summary>
    /// <returns>表示倍率。</returns>
    private double ResolveScale()
    {
        var scale = RenderScale;
        return double.IsFinite(scale) && scale > 0 ? scale : 1.0;
    }

    /// <summary>本文・選択範囲・キャレットを描く。</summary>
    /// <param name="context">描画先。</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var scale = ResolveScale();
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale));

        var lines = ResolveLines();
        foreach (var line in lines)
        {
            RichTextPainter.DrawLine(context, line);
        }

        if (!IsEditing)
        {
            return;
        }

        var index = TextLineIndex.Create(lines, Text.PlainText);
        if (index is null)
        {
            return;
        }

        foreach (var rect in index.ResolveSelectionRects(SelectionStart, SelectionEnd, MeasureText))
        {
            context.FillRectangle(
                RichTextPainter.ToBrush(TextEditorAppearance.SelectionColor),
                new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        // **変換中はキャレットを変換中の文字列の末尾へ置く**: 末尾へ置かないと、変換中の文字が
        // キャレットの位置へ重なって見える。
        var caretIndex = TextCompositionPreview.ResolveCaret(_edit.CaretIndex, _preeditText);
        if (index.ResolveCaret(caretIndex, MeasureText) is not { } caret || !IsCaretVisible)
        {
            return;
        }

        var thickness = TextEditorAppearance.CaretThicknessDip / scale;
        context.FillRectangle(
            RichTextPainter.ToBrush(TextEditorAppearance.CaretColor),
            new Rect(caret.X, caret.Y, thickness, caret.Height));
    }

    /// <summary>本文が変わったときに、描画と編集の状態を更新する。</summary>
    /// <param name="change">プロパティの変更内容。</param>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty || change.Property == RenderScaleProperty
            || change.Property == LineHeightFactorProperty)
        {
            // **本文が外から変わったら編集位置を本文の範囲へ収める**（差し替えで範囲外に残さない）
            if (change.Property == TextProperty && _edit.IsActive)
            {
                _edit.Clamp(Text.PlainText.Length);
            }

            InvalidateVisual();
        }
    }
}
