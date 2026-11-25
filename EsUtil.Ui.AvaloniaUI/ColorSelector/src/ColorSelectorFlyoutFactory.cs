using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Platform.Core.Palette;
using EsUtil.Ui.AvaloniaUI.ColorSelector.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.ColorSelector;

/// <summary>カラーピッカー（ColorView）を格納した Flyout を構築するファクトリ。</summary>
/// <remarks>
/// <see cref="ColorSelectorControl"/> の「…」ボタン押下時に呼び出され、OK / キャンセル ボタン付きの
/// Flyout を構築する。 構築とイベント配線を本クラスに分離することで、<see cref="ColorSelectorControl"/> の
/// 単一責任（依存関係プロパティとイベント処理）を維持する。<br/>
/// <br/>
/// **適用パターン**: Simple Factory（シンプルファクトリ）— 生成ロジックを静的なファクトリメソッドへ集約。<br/>
/// <br/>
/// **構成**:<br/>
/// - ColorView の初期色は <see cref="ColorString.TryParse(string, out System.Drawing.Color, out double)"/>（コア）で解析
///   （失敗時は白）。 Hex 入力タブは <c>#AARRGGBB</c>（アルファ先頭）形式で表示する。<br/>
/// - OK ボタンは <see cref="ColorString.ToArgbHex(System.Drawing.Color)"/>（コア）で保存値へ変換し、
///   <see cref="Action{T}"/>（applyColor）経由で <see cref="ColorSelectorControl.SelectedColor"/> へ反映する。<br/>
/// - ログ出力は <c>ColorPickerLog</c> の LoggerMessage 定義を使用する。<br/>
/// <br/>
/// <b>ColorView テーマはホストが取り込む</b>: <c>ColorView</c> は独自のテーマ（Avalonia.Controls.ColorPicker の
/// <c>Fluent.xaml</c>）を必要とする。 Flyout はオーバーレイ レイヤーへ表示されるためコントロールのローカル Styles では
/// 伝播せず、アプリの <c>Application.Styles</c> へ <c>StyleInclude</c> で取り込む必要がある
/// （実行時の XAML 解決はトリミング / Native AOT で失敗するため、本クラスは自動適用しない）。
/// </remarks>
internal static class ColorSelectorFlyoutFactory
{
    /// <summary>カラーピッカーの中身（OK / キャンセル付き）を構築する。</summary>
    /// <param name="currentColor">現在の選択色（保存値。 例: "#80FF0000"）。 解析失敗時は初期色が白になる。</param>
    /// <param name="log">ログ出力先。 null は許可しない（呼び出し側が <see cref="ColorSelectorControl.Log"/> を渡す）。</param>
    /// <param name="applyColor">OK 確定時に選択色（#AARRGGBB）を反映するコールバック。</param>
    /// <param name="close">OK / キャンセルの後に一覧を閉じる処理。</param>
    /// <param name="initialTabIndex">最初に選ぶカラーピッカーのタブ（前回選んだタブ。 範囲外は先頭）。</param>
    /// <param name="onTabChanged">カラーピッカーのタブが変わったときに、その索引を知らせる処理。</param>
    /// <param name="availableWidth">一覧の中身に使える幅（DIP。 0 以下は既定の最小幅）。</param>
    /// <returns>カラーピッカーと OK / キャンセル ボタンを含む要素。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 初期色を解析して ColorView を構築する（固定サイズは設定しない。 タブを収めるためテーマの自然なサイズに任せる）。<br/>
    /// 2. OK / キャンセル ボタンを構築し、クリックイベントを配線する。<br/>
    /// 3. 要素を返す（一覧のタブの中へ直接置く。 別のフライアウトを重ねない）。<br/>
    /// <b>タブの選択を覚える</b>: カラーピッカー自身のタブ（スペクトル / パレット / 成分）も前回の位置から開く
    /// （開くたびに先頭へ戻ると、続けて同じタブを使うときに切り替えの手間がかかる）。
    /// </remarks>
    internal static Control CreateContent(
        string currentColor,
        ILogger log,
        Action<string> applyColor,
        Action close,
        int initialTabIndex = 0,
        Action<int>? onTabChanged = null,
        double availableWidth = 0)
    {
        // 初期色をパース（色 + 不透明度の統合形式に対応。 失敗時は白）
        var initColor = Colors.White;
        if (ColorString.TryParse(currentColor, out var parsed, out _))
        {
            initColor = Color.FromArgb(parsed.A, parsed.R, parsed.G, parsed.B);
        }

        // ColorView コントロール
        // 固定サイズ（Width/Height）は設定しない。 ColorSpectrum は常に正方形で配置され、
        // タブ（Spectrum / Palette / Components）とスライダーを収めるには高さが必要なため、
        // 固定サイズ（280x240）ではコンテンツが左右下で見切れる。 テーマの自然なサイズに任せる。
        // Hex 入力タブは #AARRGGBB（アルファ先頭）形式で表示する（Avalonia 標準の Color.Parse と整合）。
        // **一覧の幅へ収める**: 幅を指定しないと ColorView の希望幅が一覧の幅を超え、右側が切れる。
        // 渡された幅を上限にする。
        var width = availableWidth > 0 ? availableWidth : ColorViewMinWidth;

        var colorView = new ColorView
        {
            Color = initColor,
            Width = width,
            MinWidth = Math.Min(ColorViewMinWidth, width),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            MinHeight = ColorViewMinHeight,
            HexInputAlphaPosition = AlphaComponentPosition.Leading,
        };

        // 前回選んだタブから開く（範囲外は先頭）
        if (initialTabIndex >= 0 && initialTabIndex < ColorViewTabCount)
        {
            colorView.SelectedIndex = initialTabIndex;
        }

        // タブの変更を覚える（一覧を閉じた後も次の表示で同じタブから開く）
        if (onTabChanged is not null)
        {
            colorView.PropertyChanged += (_, e) =>
            {
                if (e.Property == ColorView.SelectedIndexProperty)
                {
                    onTabChanged(colorView.SelectedIndex);
                }
            };
        }

        // OK / キャンセル ボタン（共通の外観設定はヘルパーに集約。 重複を排除）
        // 固定 Width を設定した Button は Fluent テーマでコンテンツが左寄せになるため、
        // HorizontalContentAlignment を明示的に中央揃えにする（短い "OK" でも中央に表示される）。
        var okButton = CreateActionButton("OK");
        var cancelButton = CreateActionButton("キャンセル");

        var content = new StackPanel
        {
            Spacing = 8,
            Children =
            {
                colorView,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 5,
                    Children = { okButton, cancelButton },
                },
            },
        };

        // OK: 色を適用して閉じる（SelectedColor = 保存値は #AARRGGBB 形式。 不透明度はアルファへ合成済み）
        okButton.Click += (_, _) =>
        {
            // アルファを含む #AARRGGBB 形式へ整形して SelectedColor（保存値）へ反映する
            var formatted = ColorString.ToArgbHex(System.Drawing.Color.FromArgb(colorView.Color.A, colorView.Color.R, colorView.Color.G, colorView.Color.B));
            log.LogFlyoutOk(formatted);
            applyColor(formatted);
            close();
        };

        // キャンセル: 何もせず閉じる（キャンセルした時点の色を記録する）
        cancelButton.Click += (_, _) =>
        {
            log.LogFlyoutCancel(
                ColorString.ToArgbHex(System.Drawing.Color.FromArgb(
                    colorView.Color.A, colorView.Color.R, colorView.Color.G, colorView.Color.B)));
            close();
        };

        return content;
    }

    /// <summary>Flyout の操作ボタン（OK / キャンセル）を生成する。</summary>
    /// <param name="content">ボタンの表示テキスト。</param>
    /// <returns>共通の外観設定（幅・マージン・中央揃え）が適用された Button。</returns>
    /// <remarks>
    /// OK / キャンセル ボタンで同一の外観設定を共有するための生成ヘルパー（DRY）。
    /// </remarks>
    private static Button CreateActionButton(string content) => new()
    {
        Content = content,
        MinWidth = ActionButtonWidth,
        Margin = new Thickness(3),
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    /// <summary>ColorView（カラーピッカー）のタブの数（スペクトル / パレット / 成分）。</summary>
    private const int ColorViewTabCount = 3;

    /// <summary>ColorView（カラーピッカー）の最小幅。</summary>
    /// <remarks>
    /// <b>実際に必要な幅に合わせる</b>: 280 DIP では成分の入力欄とスライダーが収まらず、
    /// 一覧の幅（フライアウトの上限）を超えて**右側が切れます**。
    /// </remarks>
    internal const double ColorViewMinWidth = 380;
    /// <summary>ColorView（カラーピッカー）の最小高さ。</summary>
    private const double ColorViewMinHeight = 320;

    /// <summary>操作ボタン（OK / キャンセル）の最小幅（DIP）。</summary>
    /// <remarks>
    /// <b>幅を固定しない</b>: 固定すると「キャンセル」の文字が切れる。
    /// 最小幅だけを決め、文言が長ければボタンが広がるようにします。
    /// </remarks>
    private const double ActionButtonWidth = 70;
}
