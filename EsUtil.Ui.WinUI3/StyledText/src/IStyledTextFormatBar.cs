using EsUtil.Platform.Core.Palette;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace EsUtil.Ui.WinUI3.StyledText;

/// <summary>書式バーの画面要素（差し替え可能な入口）。</summary>
/// <remarks>
/// <b>ホストは独自のバーを差し替えられる</b>: 既定のバーを使わず、自分の GUI を載せたい場合は
/// <see cref="IStyledTextFormatBarFactory"/> を実装してホストへ渡す。
/// </remarks>
public interface IStyledTextFormatBar
{
    /// <summary>バーの画面要素を取得する。</summary>
    FrameworkElement View { get; }

    /// <summary>バーの幅（DIP）を取得する。</summary>
    double PreferredWidth { get; }

    /// <summary>色の入力欄へ出す表記を取得または設定する。</summary>
    ColorTextFormat TextFormat { get; set; }

    /// <summary>現在の選択範囲の書式を表示へ反映する。</summary>
    void Refresh();

    /// <summary>本文のキー操作を転送する処理を設定する。</summary>
    /// <param name="forward">転送する処理（キーの引数を受け取る。<see langword="null"/> は転送しない）。</param>
    void SetKeyHandler(Func<KeyRoutedEventArgs, bool>? forward);

    /// <summary>本文へフォーカスを戻す処理を設定する。</summary>
    /// <param name="request">フォーカスを戻す処理（<see langword="null"/> は何もしない）。</param>
    void SetFocusRequest(Action? request);
}