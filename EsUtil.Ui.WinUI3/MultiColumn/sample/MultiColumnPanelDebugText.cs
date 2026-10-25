using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.MultiColumn.Sample;

/// <summary>レイアウト結果を画面へ重ねて表示する <see cref="MultiColumnPanel"/>（サンプル）。</summary>
/// <remarks>
/// <b>派生の例</b>: コントロールを継承し、レイアウト結果を読むだけで独自の表示を足せます。 結果は読み取り専用の
/// 依存関係プロパティ（<see cref="MultiColumnPanel.LayoutMinHeight"/> など）として公開されています。<br/>
/// <b>表示の置き場所</b>: デバッグ表示の要素は自分の子ではなく、祖先の <see cref="Panel"/> へ追加します
/// （自分の子にすると列の配置対象になり、レイアウト結果が変わってしまいます）。
/// </remarks>
public partial class MultiColumnPanelDebugText : MultiColumnPanel
{
    /// <summary>デバッグ表示の内容。</summary>
    private readonly TextBlock _debugText = new()
    {
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        Text = "待機中...",
    };

    /// <summary>デバッグ表示の枠。</summary>
    private readonly Border _debugArea;

    /// <summary>デバッグ表示を持つパネルを初期化する。</summary>
    public MultiColumnPanelDebugText()
    {
        _debugArea = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(70, 255, 0, 0)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(200, 255, 255, 255)),
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = _debugText,
        };

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }


    /// <summary><see cref="DebugTextVisibility"/> の依存関係プロパティ。</summary>
    public static readonly DependencyProperty DebugTextVisibilityProperty = DependencyProperty.Register(
        nameof(DebugTextVisibility),
        typeof(Visibility),
        typeof(MultiColumnPanelDebugText),
        new PropertyMetadata(Visibility.Collapsed, OnDebugTextVisibilityChanged));

    /// <summary>デバッグ表示の表示状態を取得または設定する。</summary>
    public Visibility DebugTextVisibility
    {
        get => (Visibility)GetValue(DebugTextVisibilityProperty);
        set => SetValue(DebugTextVisibilityProperty, value);
    }

    /// <summary>デバッグ表示を出すかどうかを取得または設定する。</summary>
    public bool ShowDebugText
    {
        get => DebugTextVisibility == Visibility.Visible;
        set => DebugTextVisibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>デバッグ表示の内容を最新のレイアウト結果で更新する。</summary>
    public void UpdateDebugInfo() => _debugText.Text = GetDebugText();

    /// <summary>レイアウト結果を含むデバッグ表示の文言を組み立てる。</summary>
    /// <returns>デバッグ表示の文言。</returns>
    public string GetDebugText()
    {
        var text = $"【このコントロール  {GetType().Name}】";
        text += $"    内側の大きさ（概算）: ({this.GetInnerSize().Format()})\n";
        text += $"    Margin: ({Margin.Format()})";
        text += $"   |   Size: ({new Size(Width, Height).Format()})";
        text += $"   |   ActualSize: ({new Size(ActualWidth, ActualHeight).Format()})";
        text += $"   |   Padding: ({this.GetPadding().Format()})";

        if (LayoutMinHeight is { } minHeight)
        {
            text += "\n\n【レイアウト結果】\n";
            text += $"    MinHeight: {minHeight:F1}";
            text += $"   |   UsedWidth: {LayoutUsedWidth:F1}";
            text += $"   |   列数: {LayoutColumnCount}";
            text += $"   |   ColumnSegments(EndIdx): {LayoutColumnSegments}";
        }

        return text;
    }

    /// <summary><see cref="DebugTextVisibility"/> の変更を表示へ反映する。</summary>
    /// <param name="d">変更された要素。</param>
    /// <param name="e">変更の内容。</param>
    private static void OnDebugTextVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((MultiColumnPanelDebugText)d)._debugArea.Visibility = (Visibility)e.NewValue;

    /// <summary>読み込み時にデバッグ表示を祖先へ取り付ける。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの引数。</param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDebugInfo();
        Helper.AttachElementToAncestor(this, _debugArea);
    }

    /// <summary>大きさが変わったときにデバッグ表示を更新する。</summary>
    /// <param name="sender">イベントの送信元。</param>
    /// <param name="e">イベントの引数。</param>
    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => UpdateDebugInfo();
}