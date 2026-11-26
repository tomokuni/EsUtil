using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.WinUI3.SampleKit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace EsUtil.Ui.WinUI3.StyledText.Sample;

/// <summary>サンプルのメイン ウィンドウ（<see cref="StyledTextControl"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: 本文を入力欄で覆わず、その場で表示・編集できること。 すべての操作
/// （キャレット・選択・入力・削除・書式・クリップボード）を API から行えること。<br/>
/// <b>書式バー</b>: バーは本文コントロールが所有し、サンプルは「自前で埋め込む」と「ポップアップで出す」を
/// <see cref="StyledTextControl.FormatBarHosting"/> で切り替える。 出す項目（太字・色など）は
/// <see cref="StyledTextControl.FormatBarItems"/> で切り替える（すべて出さないとバーは出ない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 本文 / 右下: ログ）を使う。
/// </remarks>
public sealed class MainWindow : Window
{
    /// <summary>本文を表示・編集するコントロール。</summary>
    private readonly StyledTextControl _editor = new();

    /// <summary>ログ（Serilog + 非同期表示）。</summary>
    private readonly SampleLogging _logging = new();

    /// <summary>書式バーを載せる場所（表示すべき間だけ見せる）。</summary>
    private readonly Border _barHost = new() { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 8) };

    /// <summary>キャレット・選択・書式の状態を出す文言。</summary>
    private readonly TextBlock _status = new() { FontSize = SampleSettings.FontSize, TextWrapping = TextWrapping.Wrap };


    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        var logger = _logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 本文は run 単位の書式で持つ（色・太字を混ぜられる）。 本文はコントロールが単一所有する
        _editor.RichTextValue = RichText.Create("ここが重要です。\n文字単位の書式は API でも変えられます。", TextDefaults.DefaultStyle)
            .ApplyStyle(6, 4, TextDefaults.DefaultStyle with
            {
                IsBold = true,
                Color = System.Drawing.Color.FromArgb(255, 200, 0, 0),
            });
        _editor.LineHeightFactor = 1.2;
        _editor.RichTextChanged += (_, _) =>
        {
            logger.LogInformation("本文が変わりました。");
            UpdateStatus();
        };
        _editor.SelectionChanged += (_, _) =>
        {
            logger.LogDebug("選択が変わりました。");
            UpdateFormatBar();
            UpdateStatus();
        };
        _editor.EditingChanged += (_, _) =>
        {
            logger.LogInformation("編集の状態が変わりました。");
            UpdateFormatBar();
            UpdateStatus();
        };

        UpdateFormatBar();

        var editorHost = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xC0, 0xC0, 0xC0)),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF)),
            Child = _editor,
        };
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.Children.Add(_barHost);
        Grid.SetRow(editorHost, 1);
        content.Children.Add(editorHost);

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel { Spacing = 6 };
        properties.Children.Add(SampleSettings.Slider("行間", 1.2, 0.8, 2.0, 0.1, true, value => _editor.LineHeightFactor = value));
        properties.Children.Add(SampleSettings.Slider("表示倍率", 1.0, 0.5, 2.0, 0.1, true, value => _editor.RenderScale = value));
        properties.Children.Add(SampleSettings.Slider("文字サイズ", 20, 8, 48, 1, true, value =>
            _editor.ApplyTextStyle(0, 0, TextDefaults.DefaultStyle with { FontSize = value })));
        properties.Children.Add(SampleSettings.Check("入力を受け付ける", true, value => _editor.IsInputEnabled = value));
        properties.Children.Add(SampleSettings.Choice("書式バーの出し方", ["自前で埋め込む（カスタム）", "ポップアップで出す"], 0, index => UpdateHosting(index == 1)));
        properties.Children.Add(SampleSettings.Check("編集中に書式バーを自動表示", true, value =>
        {
            _editor.ShowFormatBarWhileEditing = value;
            UpdateFormatBar();
        }));
        properties.Children.Add(SampleSettings.Check("書式バー: 太字", true, value => UpdateItems(items => items with { ShowBold = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: 斜体", true, value => UpdateItems(items => items with { ShowItalic = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: 下線", true, value => UpdateItems(items => items with { ShowUnderline = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: 取り消し線", true, value => UpdateItems(items => items with { ShowStrike = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: 色", true, value => UpdateItems(items => items with { ShowColor = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: 文字サイズ", true, value => UpdateItems(items => items with { ShowFontSize = value })));
        properties.Children.Add(SampleSettings.Check("書式バー: フォント名", true, value => UpdateItems(items => items with { ShowFont = value })));

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel { Spacing = 6 };
        methods.Children.Add(SampleSettings.Buttons(
            ("編集を開始", () => _editor.BeginEditing()),
            ("編集を終了", () => _editor.EndEditing()),
            ("すべて選択", () => _editor.SelectAll())));
        methods.Children.Add(SampleSettings.Buttons(
            ("先頭 5 文字を選択", () => _editor.Select(0, 5)),
            ("キャレットを先頭へ", () => _editor.MoveCaret(0, extend: false)),
            ("キャレットを末尾へ", () => _editor.MoveCaret(int.MaxValue, extend: false)),
            ("位置 (0,0) からキャレット", () => _editor.SetCaretFromPoint(new EsPoint(0, 0), extend: false))));
        methods.Children.Add(SampleSettings.Buttons(
            ("前の 1 文字へ", () => _editor.MoveCaretByCluster(-1, extend: false)),
            ("次の 1 文字へ", () => _editor.MoveCaretByCluster(1, extend: false)),
            ("行頭へ", () => _editor.MoveCaretToLineEdge(first: true, extend: false)),
            ("行末へ", () => _editor.MoveCaretToLineEdge(first: false, extend: false)),
            ("上の行へ", () => _editor.MoveCaretByLine(-1, extend: false)),
            ("下の行へ", () => _editor.MoveCaretByLine(1, extend: false))));
        methods.Children.Add(SampleSettings.Buttons(
            ("選択を「置換」で置き換え", () => _editor.ReplaceSelection("置換")),
            ("範囲 1〜3 を「範囲」で置き換え", () => _editor.ReplaceRange(1, 3, "範囲")),
            ("前を削除", () => _editor.DeleteAtCaret(forward: false)),
            ("後を削除", () => _editor.DeleteAtCaret(forward: true))));
        methods.Children.Add(SampleSettings.Buttons(
            ("太字の切替", () => _editor.TransformTextStyle(static style => style with { IsBold = !style.IsBold })),
            ("斜体の切替", () => _editor.TransformTextStyle(static style => style with { IsItalic = !style.IsItalic })),
            ("下線の切替", () => _editor.TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline }))));
        methods.Children.Add(SampleSettings.Buttons(
            ("取り消し線の切替", () => _editor.TransformTextStyle(static style => style with { IsStrike = !style.IsStrike })),
            ("文字色を変える", () => _editor.ApplyTextStyle(
                TextDefaults.DefaultStyle with { Color = System.Drawing.Color.FromArgb(255, 0, 90, 200) })),
            ("書式を戻す", () => _editor.ApplyTextStyle(TextDefaults.DefaultStyle))));
        methods.Children.Add(SampleSettings.Buttons(
            ("選択をコピー", () => _ = _editor.CopyAsync()),
            ("選択を切り取り", () => _ = _editor.CutAsync()),
            ("貼り付け", () => _ = _editor.PasteAsync())));
        methods.Children.Add(SampleSettings.Buttons(
            ("選択文字を状態へ出す", ShowSelectedText),
            ("選択が太字か調べる", ShowSelectedBold),
            ("キャレットの点滅を戻す", () => _editor.RefreshCaretBlink())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel { Spacing = 6 };
        samples.Children.Add(SampleSettings.LogLevel(_logging));
        samples.Children.Add(_status);
        samples.Children.Add(SampleSettings.Buttons(
            ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）")),
            ("本文を初期化", ResetText)));

        UpdateStatus();
        var log = new SampleLogPanel(_logging.Buffer);
        Content = SampleShell.CreateContent(properties, methods, samples, content, log);

        // ウィンドウの大きさは DIP で指定する（他の UI と同じ 1400 x 900）
        SampleShell.ApplyWindowSize(this);
        Title = "StyledText サンプル (WinUI3)";
        Closed += (_, _) => _logging.Dispose();
        return;

        // 書式バーの出し方を変える（自前で埋め込む / コントロールがポップアップで出す）
        void UpdateHosting(bool popup)
        {
            _editor.FormatBarHosting = popup ? TextFormatBarHosting.Popup : TextFormatBarHosting.Custom;
            UpdateFormatBar();
        }

        // 書式バーを出すか隠すかを決める（条件は本文コントロールが単一所有する）
        // ポップアップを選んだときは、コントロールが自分で出すため埋め込みの場所は使わない
        void UpdateFormatBar()
        {
            if (_editor.FormatBarHosting == TextFormatBarHosting.Popup)
            {
                _barHost.Visibility = Visibility.Collapsed;
                return;
            }

            // バーはコントロールが所有する（作る・項目の反映・内容の更新もコントロールの仕事）
            _barHost.Child = _editor.FormatBar.View;
            _barHost.Visibility = _editor.ShouldShowFormatBar ? Visibility.Visible : Visibility.Collapsed;
        }

        // 書式バーに出す項目を変える（バーの作り直しはコントロールが行う）
        void UpdateItems(Func<TextFormatBarItemVisibility, TextFormatBarItemVisibility> update)
        {
            _editor.FormatBarItems = update(_editor.FormatBarItems);
            UpdateFormatBar();
        }

        // キャレット・選択・書式の状態を 1 行で出す
        void UpdateStatus() => _status.Text =
            $"編集中: {_editor.IsEditing} / 選択あり: {_editor.HasSelection}\n" +
            $"キャレット: {_editor.CaretIndex} / 選択: ({_editor.SelectionStart}, {_editor.SelectionLength})\n" +
            $"選択の書式: 太字={_editor.SelectedTextStyle.IsBold} 斜体={_editor.SelectedTextStyle.IsItalic} " +
            $"大きさ={_editor.SelectedTextStyle.FontSize:0.#}";

        // 選択中の文字列を状態へ出す（長い場合は先頭 20 文字に切る）
        void ShowSelectedText()
        {
            var text = _editor.GetSelectedText();
            _status.Text = $"選択文字: {text[..Math.Min(20, text.Length)]}";
        }

        // 選択範囲のすべてが太字かを状態へ出す（書式ボタンの押下状態と同じ判定）
        void ShowSelectedBold() =>
            _status.Text = $"選択はすべて太字: {_editor.SelectedTextMatches(static style => style.IsBold)}";

        // 本文を先頭の 1 行へ戻す（編集の状態も捨てる）
        void ResetText()
        {
            _editor.EndEditing();
            _editor.RichTextValue = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle);
            UpdateStatus();
        }
    }
}