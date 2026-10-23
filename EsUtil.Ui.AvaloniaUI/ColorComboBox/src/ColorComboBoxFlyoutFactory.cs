using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using EsUtil.Ui.AvaloniaUI.ColorComboBox.Logging;
using Microsoft.Extensions.Logging;

namespace EsUtil.Ui.AvaloniaUI.ColorComboBox;

/// <summary>カラーピッカー（ColorView）を格納した Flyout を構築するファクトリ。</summary>
/// <remarks>
/// <see cref="ColorComboBoxControl"/> の「…」ボタン押下時に呼び出され、OK / キャンセル ボタン付きの
/// Flyout を構築する。 構築とイベント配線を本クラスに分離することで、<see cref="ColorComboBoxControl"/> の
/// 単一責任（依存関係プロパティとイベント処理）を維持する。<br/>
/// <br/>
/// **適用パターン**: Simple Factory（シンプルファクトリ）— 生成ロジックを静的なファクトリメソッドへ集約。<br/>
/// <br/>
/// **構成**:<br/>
/// - ColorView の初期色は <see cref="ColorString.TryParse(string, out Color, out double)"/> で解析
///   （失敗時は白）。 Hex 入力タブは <c>#AARRGGBB</c>（アルファ先頭）形式で表示する。<br/>
/// - OK ボタンは <see cref="ColorString.ToArgbHex(Color)"/> で保存値へ変換し、
///   <see cref="Action{T}"/>（applyColor）経由で <see cref="ColorComboBoxControl.SelectedColor"/> へ反映する。<br/>
/// - ログ出力は <c>ColorPickerLog</c> の LoggerMessage 定義を使用する。<br/>
/// <br/>
/// <b>ColorView テーマはホストが取り込む</b>: <c>ColorView</c> は独自のテーマ（Avalonia.Controls.ColorPicker の
/// <c>Fluent.xaml</c>）を必要とする。 Flyout はオーバーレイ レイヤーへ表示されるためコントロールのローカル Styles では
/// 伝播せず、アプリの <c>Application.Styles</c> へ <c>StyleInclude</c> で取り込む必要がある
/// （実行時の XAML 解決はトリミング / Native AOT で失敗するため、本クラスは自動適用しない）。
/// </remarks>
internal static class ColorComboBoxFlyoutFactory
{
    /// <summary>カラーピッカー Flyout を構築する。</summary>
    /// <param name="currentColor">現在の選択色（保存値。 例: "#80FF0000"）。 解析失敗時は初期色が白になる。</param>
    /// <param name="log">ログ出力先。 null は許可しない（呼び出し側が <see cref="ColorComboBoxControl.Log"/> を渡す）。</param>
    /// <param name="applyColor">OK 確定時に選択色（#AARRGGBB）を反映するコールバック。</param>
    /// <returns>OK / キャンセル ボタンを含む Flyout。</returns>
    /// <remarks>
    /// 処理フロー:<br/>
    /// 1. 初期色を解析して ColorView を構築する（固定サイズは設定しない。 タブを収めるためテーマの自然なサイズに任せる）。<br/>
    /// 2. OK / キャンセル ボタンを構築し、クリックイベントを配線する。<br/>
    /// 3. Flyout を構築して返す（表示は呼び出し側が行う）。
    /// </remarks>
    internal static Flyout Create(string currentColor, ILogger log, Action<string> applyColor)
    {
        // 初期色をパース（色 + 不透明度の統合形式に対応。 失敗時は白）
        var initColor = Colors.White;
        if (!string.IsNullOrWhiteSpace(currentColor) && ColorString.TryParse(currentColor, out var parsed, out _))
        {
            initColor = parsed;
        }

        // ColorView コントロール
        // 固定サイズ（Width/Height）は設定しない。 ColorSpectrum は常に正方形で配置され、
        // タブ（Spectrum / Palette / Components）とスライダーを収めるには高さが必要なため、
        // 固定サイズ（280x240）ではコンテンツが左右下で見切れる。 テーマの自然なサイズに任せる。
        // Hex 入力タブは #AARRGGBB（アルファ先頭）形式で表示する（Avalonia 標準の Color.Parse と整合）。
        var colorView = new ColorView
        {
            Color = initColor,
            MinWidth = ColorViewMinWidth,
            MinHeight = ColorViewMinHeight,
            HexInputAlphaPosition = AlphaComponentPosition.Leading,
        };

        // OK / キャンセル ボタン（共通の外観設定はヘルパーに集約。 重複を排除）
        // 固定 Width を設定した Button は Fluent テーマでコンテンツが左寄せになるため、
        // HorizontalContentAlignment を明示的に中央揃えにする（短い "OK" でも中央に表示される）。
        var okButton = CreateActionButton("OK");
        var cancelButton = CreateActionButton("キャンセル");

        // Flyout コンテンツ
        var flyout = new Flyout
        {
            Content = new StackPanel
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
            },
            Placement = PlacementMode.BottomEdgeAlignedRight,
        };

        // OK: 色を適用して閉じる（SelectedColor = 保存値は #AARRGGBB 形式。 不透明度はアルファへ合成済み）
        okButton.Click += (_, _) =>
        {
            // アルファを含む #AARRGGBB 形式へ整形して SelectedColor（保存値）へ反映する
            var formatted = ColorString.ToArgbHex(colorView.Color);
            log.LogFlyoutOk(formatted);
            applyColor(formatted);
            flyout.Hide();
        };

        // キャンセル: 何もせず閉じる
        cancelButton.Click += (_, _) =>
        {
            log.LogFlyoutCancel();
            flyout.Hide();
        };

        return flyout;
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
        Width = ActionButtonWidth,
        Margin = new Thickness(3),
        HorizontalContentAlignment = HorizontalAlignment.Center,
    };

    /// <summary>ColorView（カラーピッカー）の最小幅。</summary>
    private const double ColorViewMinWidth = 280;

    /// <summary>ColorView（カラーピッカー）の最小高さ。</summary>
    private const double ColorViewMinHeight = 320;

    /// <summary>操作ボタン（OK / キャンセル）の幅。</summary>
    private const double ActionButtonWidth = 70;
}
