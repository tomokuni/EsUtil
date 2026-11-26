using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Ui.MewUI.SampleKit;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.MewUI.StyledText.Sample.Views;

/// <summary>サンプルのメイン ウィンドウ（<see cref="StyledTextControl"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: 本文を入力欄で覆わず、その場で表示・編集できること。 すべての操作
/// （キャレット・選択・入力・削除・書式・クリップボード）を API から行えること。<br/>
/// <b>書式バー</b>: バーは本文コントロールが所有し、サンプルは「自前で埋め込む」と「ポップアップで出す」を
/// <see cref="StyledTextControl.FormatBarHosting"/> で切り替える。 出す項目（太字・色など）は
/// <see cref="StyledTextControl.FormatBarItems"/> で切り替える（すべて出さないとバーは出ない）。<br/>
/// <b>配置</b>: サンプル共通の <see cref="SampleShell"/>（左: 設定 / 右上: 本文 / 右下: ログ）を使う。
/// </remarks>
public static class MainWindow
{
    /// <summary>サンプル ウィンドウを作る。</summary>
    /// <returns>作ったウィンドウ。</returns>
    public static Window Create()
    {
        // ── ログ（Serilog。 非同期に表示する） ──
        var logging = new SampleLogging();
        var log = new SampleLogPanel(logging.Buffer);
        var logger = logging.Logger;
        logger.LogInformation("サンプルを起動しました。");

        // 本文は run 単位の書式で持つ（色・太字を混ぜられる）。 本文はコントロールが単一所有する
        var editor = new StyledTextControl
        {
            RichTextValue = RichText.Create("ここが重要です。\n文字単位の書式は API でも変えられます。", TextDefaults.DefaultStyle)
                .ApplyStyle(6, 4, TextDefaults.DefaultStyle with
                {
                    IsBold = true,
                    Color = System.Drawing.Color.FromArgb(255, 200, 0, 0),
                }),
            LineHeightFactor = 1.2,
        };

        // 書式バーを載せる場所（表示すべき間だけ見せる）
        var barHost = new Border { IsVisible = false, Margin = new Thickness(0, 0, 0, 8) };

        // 状態の表示
        var status = new TextBlock { FontSize = SampleSettings.FontSize };

        editor.RichTextChanged += (_, _) =>
        {
            logger.LogInformation("本文が変わりました。");
            UpdateStatus();
        };
        editor.SelectionChanged += (_, _) =>
        {
            logger.LogDebug("選択が変わりました。");
            UpdateFormatBar();
            UpdateStatus();
        };
        editor.EditingChanged += (_, _) =>
        {
            logger.LogInformation("編集の状態が変わりました。");
            UpdateFormatBar();
            UpdateStatus();
        };

        UpdateFormatBar();

        var editorHost = new Border().BorderThickness(1).BorderBrush(new Color(0xC0, 0xC0, 0xC0)).Child(editor);
        var content = new Grid().Columns("*").Rows("Auto,*").Children(barHost, editorHost);

        // ── プロパティ設定（対象コントロールの公開プロパティ。 並びは他 UI と共通） ──
        var properties = new StackPanel().Spacing(6);
        properties.Add(SampleSettings.Slider("行間", 1.2, 0.8, 2.0, 0.1, true, value => editor.LineHeightFactor = value));
        properties.Add(SampleSettings.Slider("表示倍率", 1.0, 0.5, 2.0, 0.1, true, value => editor.RenderScale = value));
        properties.Add(SampleSettings.Slider("文字サイズ", 20, 8, 48, 1, true, value =>
            editor.ApplyTextStyle(0, 0, TextDefaults.DefaultStyle with { FontSize = value })));
        properties.Add(SampleSettings.Check("入力を受け付ける", true, value => editor.IsInputEnabled = value));
        properties.Add(SampleSettings.Choice("書式バーの出し方", ["自前で埋め込む（カスタム）", "ポップアップで出す"], 0, index => UpdateHosting(index == 1)));
        properties.Add(SampleSettings.Check("編集中に書式バーを自動表示", true, value =>
        {
            editor.ShowFormatBarWhileEditing = value;
            UpdateFormatBar();
        }));
        properties.Add(SampleSettings.Check("書式バー: 太字", true, value => UpdateItems(items => items with { ShowBold = value })));
        properties.Add(SampleSettings.Check("書式バー: 斜体", true, value => UpdateItems(items => items with { ShowItalic = value })));
        properties.Add(SampleSettings.Check("書式バー: 下線", true, value => UpdateItems(items => items with { ShowUnderline = value })));
        properties.Add(SampleSettings.Check("書式バー: 取り消し線", true, value => UpdateItems(items => items with { ShowStrike = value })));
        properties.Add(SampleSettings.Check("書式バー: 色", true, value => UpdateItems(items => items with { ShowColor = value })));
        properties.Add(SampleSettings.Check("書式バー: 文字サイズ", true, value => UpdateItems(items => items with { ShowFontSize = value })));
        properties.Add(SampleSettings.Check("書式バー: フォント名", true, value => UpdateItems(items => items with { ShowFont = value })));

        // ── API操作（対象コントロールのメソッド実行） ──
        var methods = new StackPanel().Spacing(6);
        methods.Add(SampleSettings.Buttons(
            ("編集を開始", () => editor.BeginEditing()),
            ("編集を終了", () => editor.EndEditing()),
            ("すべて選択", () => editor.SelectAll())));
        methods.Add(SampleSettings.Buttons(
            ("先頭 5 文字を選択", () => editor.Select(0, 5)),
            ("キャレットを先頭へ", () => editor.MoveCaret(0, extend: false)),
            ("キャレットを末尾へ", () => editor.MoveCaret(int.MaxValue, extend: false)),
            ("位置 (0,0) からキャレット", () => editor.SetCaretFromPoint(new EsPoint(0, 0), extend: false))));
        methods.Add(SampleSettings.Buttons(
            ("前の 1 文字へ", () => editor.MoveCaretByCluster(-1, extend: false)),
            ("次の 1 文字へ", () => editor.MoveCaretByCluster(1, extend: false)),
            ("行頭へ", () => editor.MoveCaretToLineEdge(first: true, extend: false)),
            ("行末へ", () => editor.MoveCaretToLineEdge(first: false, extend: false)),
            ("上の行へ", () => editor.MoveCaretByLine(-1, extend: false)),
            ("下の行へ", () => editor.MoveCaretByLine(1, extend: false))));
        methods.Add(SampleSettings.Buttons(
            ("選択を「置換」で置き換え", () => editor.ReplaceSelection("置換")),
            ("範囲 1〜3 を「範囲」で置き換え", () => editor.ReplaceRange(1, 3, "範囲")),
            ("前を削除", () => editor.DeleteAtCaret(forward: false)),
            ("後を削除", () => editor.DeleteAtCaret(forward: true))));
        methods.Add(SampleSettings.Buttons(
            ("太字の切替", () => editor.TransformTextStyle(static style => style with { IsBold = !style.IsBold })),
            ("斜体の切替", () => editor.TransformTextStyle(static style => style with { IsItalic = !style.IsItalic })),
            ("下線の切替", () => editor.TransformTextStyle(static style => style with { IsUnderline = !style.IsUnderline }))));
        methods.Add(SampleSettings.Buttons(
            ("取り消し線の切替", () => editor.TransformTextStyle(static style => style with { IsStrike = !style.IsStrike })),
            ("文字色を変える", () => editor.ApplyTextStyle(
                TextDefaults.DefaultStyle with { Color = System.Drawing.Color.FromArgb(255, 0, 90, 200) })),
            ("書式を戻す", () => editor.ApplyTextStyle(TextDefaults.DefaultStyle))));
        methods.Add(SampleSettings.Buttons(
            ("選択をコピー", () => _ = editor.CopyAsync()),
            ("選択を切り取り", () => _ = editor.CutAsync()),
            ("貼り付け", () => _ = editor.PasteAsync())));
        methods.Add(SampleSettings.Buttons(
            ("選択文字を状態へ出す", ShowSelectedText),
            ("選択が太字か調べる", ShowSelectedBold),
            ("キャレットの点滅を戻す", () => editor.RefreshCaretBlink())));

        // ── サンプル設定（サンプルアプリの外観と状態） ──
        var samples = new StackPanel().Spacing(6);
        samples.Add(SampleSettings.LogLevel(logging));
        samples.Add(status);
        samples.Add(SampleSettings.Buttons(
            ("テスト ログを出力（非同期）", () => logger.LogInformation("テスト ログ（非同期）")),
            ("本文を初期化", ResetText)));

        UpdateStatus();
        return SampleShell.Create(
            "StyledText サンプル (MewUI)", properties, methods, samples, content, log);

        // 書式バーの出し方を変える（自前で埋め込む / コントロールがポップアップで出す）
        void UpdateHosting(bool popup)
        {
            editor.FormatBarHosting = popup ? TextFormatBarHosting.Popup : TextFormatBarHosting.Custom;
            UpdateFormatBar();
        }

        // 書式バーを出すか隠すかを決める（条件は本文コントロールが単一所有する）
        // ポップアップを選んだときは、コントロールが自分で出すため埋め込みの場所は使わない
        void UpdateFormatBar()
        {
            if (editor.FormatBarHosting == TextFormatBarHosting.Popup)
            {
                barHost.IsVisible = false;
                return;
            }

            // バーはコントロールが所有する（作る・項目の反映・内容の更新もコントロールの仕事）
            barHost.Child = editor.FormatBar.View;
            barHost.IsVisible = editor.ShouldShowFormatBar;
        }

        // 書式バーに出す項目を変える（バーの作り直しはコントロールが行う）
        void UpdateItems(Func<TextFormatBarItemVisibility, TextFormatBarItemVisibility> update)
        {
            editor.FormatBarItems = update(editor.FormatBarItems);
            UpdateFormatBar();
        }

        // キャレット・選択・書式の状態を 1 行で出す
        void UpdateStatus() => status.Text =
            $"編集中: {editor.IsEditing} / 選択あり: {editor.HasSelection} / " +
            $"キャレット: {editor.CaretIndex} / 選択: ({editor.SelectionStart}, {editor.SelectionLength})";

        // 選択中の文字列を状態へ出す（長い場合は先頭 20 文字に切る）
        void ShowSelectedText()
        {
            var text = editor.GetSelectedText();
            status.Text = $"選択文字: {text[..Math.Min(20, text.Length)]}";
        }

        // 選択範囲のすべてが太字かを状態へ出す（書式ボタンの押下状態と同じ判定）
        void ShowSelectedBold() =>
            status.Text = $"選択はすべて太字: {editor.SelectedTextMatches(static style => style.IsBold)}";

        // 本文を先頭の 1 行へ戻す（編集の状態も捨てる）
        void ResetText()
        {
            editor.EndEditing();
            editor.RichTextValue = RichText.Create("ここが重要です。", TextDefaults.DefaultStyle);
            UpdateStatus();
        }
    }
}