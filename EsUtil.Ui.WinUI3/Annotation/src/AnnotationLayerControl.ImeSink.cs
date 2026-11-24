using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace EsUtil.Ui.WinUI3.Annotation;

/// <summary>注釈レイヤーの IME 連携（OS の入力欄へ変換を任せる）。</summary>
/// <remarks>
/// <b><c>CoreTextEditContext</c> は使わない</b>: WinUI 3 のデスクトップ アプリでは窓口を作れても
/// 入力が届かず、変換の開始・更新・確定が一度も呼ばれません。<br/>
/// <b>実際の <see cref="TextBox"/> を置く</b>: 変換・候補の位置・未確定の下線は OS 側が行います。
/// 見た目は本レイヤーが書式付きで描くため、受け口は透明・1×1・入力は受け取らない
/// （フォーカスと IME のためだけに存在する）。<br/>
/// （<see cref="_imeSinkHost"/>）へ載せ、キャレットの位置だけを合わせます。
/// </remarks>
public sealed partial class AnnotationLayerControl
{
    /// <summary>入力の受け口（IME の変換を任せる入力欄）。 編集中だけ置く。</summary>
    private TextBox? _imeSink;


    /// <summary>受け口の文字列をこちらから書き換えている間は <see langword="true"/>（再入を防ぐ）。</summary>
    /// <summary>変換候補のフライアウトと入力中の文字の間の距離（DIP）。</summary>
    /// <remarks>
    /// <b>受け口を文字の下へずらす</b>: OS は受け口（入力欄）の中の変換中の範囲を基準に候補を出すため、
    /// 受け口の高さを変えても候補の位置は変わりません。 受け口そのものを文字の下へ置くと候補が下がります。
    /// </remarks>
    private const double PickerCandidateOffset = 12;

    /// <summary>受け口の文字列をこちらから書き換えている間は <see langword="true"/>（再入を防ぐ）。</summary>
    private bool _sinkSyncing;

    /// <summary>入力の受け口を用意する。</summary>
    private void EnsureImeSink()
    {
        if (_imeSink is not null)
        {
            return;
        }

        var sink = new TextBox
        {
            Opacity = 0.01,
            Width = 1,
            Height = 1,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            IsSpellCheckEnabled = false,
            // **タブ移動の対象にする**: `IsTabStop = false` だと `Focus` が失敗し、IME が使えません
            IsTabStop = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        sink.KeyDown += OnImeSinkKeyDown;
        sink.TextChanged += OnImeSinkTextChanged;
        sink.TextCompositionStarted += OnImeSinkCompositionStarted;
        sink.TextCompositionChanged += OnImeSinkCompositionChanged;
        sink.TextCompositionEnded += OnImeSinkCompositionEnded;

        _imeSinkHost.Children.Add(sink);
        _imeSink = sink;
    }

    /// <summary>受け口へフォーカスを移して IME を使えるようにする。</summary>
    private void FocusImeSink()
    {
        EnsureImeSink();
        if (_imeSink is not { } sink)
        {
            return;
        }

        PlaceImeSink();
        _ = sink.Focus(FocusState.Programmatic);
    }

    /// <summary>受け口を片付ける（文字列を空へ戻し、レイヤーへフォーカスを返す）。</summary>
    private void ReleaseImeSink()
    {
        _isComposing = false;
        SetPreedit(string.Empty);
        ClearImeSink();

        if (_imeSink is not { } sink)
        {
            return;
        }

        _imeSinkHost.Children.Remove(sink);
        sink.KeyDown -= OnImeSinkKeyDown;
        sink.TextChanged -= OnImeSinkTextChanged;
        sink.TextCompositionStarted -= OnImeSinkCompositionStarted;
        sink.TextCompositionChanged -= OnImeSinkCompositionChanged;
        sink.TextCompositionEnded -= OnImeSinkCompositionEnded;
        _imeSink = null;
        _ = Focus(FocusState.Programmatic);
    }

    /// <summary>受け口をキャレットの位置へ置く（変換候補がキャレットの近くに出るようにする）。</summary>
    private void PlaceImeSink()
    {
        if (_imeSink is not { } sink)
        {
            return;
        }

        var caret = ResolveCaretDip();
        var style = SelectedTextStyle;
        var (scale, _) = ResolveImageTransform();

        // **文字の下へ置く**: OS は受け口の中の変換中の範囲を基準に候補を出すため、受け口を文字の
        // すぐ下へ置くと候補がその下に出ます。
        sink.Height = Math.Max(1, caret.Height);
        sink.FontSize = Math.Max(1, style.FontSize * scale);
        Canvas.SetLeft(sink, Math.Max(0, caret.X));
        Canvas.SetTop(sink, Math.Max(0, TextTopDip(caret, style) + caret.Height + PickerCandidateOffset));
    }

    /// <summary>受け口の文字列を空へ戻す（こちらの書き換えとして扱う）。</summary>
    private void ClearImeSink()
    {
        if (_imeSink is not { } sink || sink.Text.Length == 0)
        {
            return;
        }

        _sinkSyncing = true;
        try
        {
            sink.Text = string.Empty;
        }
        finally
        {
            _sinkSyncing = false;
        }
    }

    /// <summary>受け口のキーを本文の編集へ渡す。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">キーの引数。</param>
    private void OnImeSinkKeyDown(object sender, KeyRoutedEventArgs e)
    {
        _ = sender;

        // 変換中は IME がキーを使うため、こちらの処理へ渡さない（渡すと変換が壊れる）
        if (_isComposing && e.Key != VirtualKey.Escape)
        {
            return;
        }

        if (HandleTextEditingKey(e))
        {
            e.Handled = true;
        }
    }

    /// <summary>受け口へ入った文字を本文へ入れる（変換中は確定まで待つ）。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    /// <remarks>
    /// <b>確定は次の描画へ回す</b>: <c>TextChanged</c> は変換の通知（<c>TextCompositionChanged</c>）より
    /// **先に**届くことがあります。 その場で確定すると変換中の文字まで本文へ入り、
    /// 変換中の表示と二重になります。
    /// 1 度待ってから「変換中か」を見直し、変換中なら確定しません。
    /// </remarks>
    private void OnImeSinkTextChanged(object sender, TextChangedEventArgs e)
    {
        _ = e;
        if (_sinkSyncing || _isComposing || sender is not TextBox sink || sink.Text.Length == 0)
        {
            return;
        }

        var typed = sink.Text;
        void Commit()
        {
            // **待つ間に変換が始まっていたら確定しない**（変換中の表示と二重になる）
            if (_sinkSyncing || _isComposing || _preeditText.Length > 0 || _imeSink is not { } current)
            {
                return;
            }

            if (!string.Equals(current.Text, typed, StringComparison.Ordinal))
            {
                return;
            }

            ClearImeSink();
            ReplaceSelection(typed);
            PlaceImeSink();
        }

        if (DispatcherQueue is { } queue)
        {
            queue.TryEnqueue(Commit);
            return;
        }

        Commit();
    }
    /// <summary>IME の変換が始まったときに変換中の表示へ切り替える。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnImeSinkCompositionStarted(TextBox sender, TextCompositionStartedEventArgs e)
    {
        _ = sender;
        _ = e;
        _isComposing = true;
        SetPreedit(string.Empty);
        Redraw();
    }

    /// <summary>変換中の文字列を控えて下線付きで描く。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnImeSinkCompositionChanged(TextBox sender, TextCompositionChangedEventArgs e)
    {
        _ = sender;

        // **変換中として扱う**: `TextCompositionStarted` が来ない IME があるため、
        // ここでも立てる（立てないと確定して文字が二重になる）
        _isComposing = true;

        var text = _imeSink?.Text ?? string.Empty;
        var start = Math.Clamp(e.StartIndex, 0, text.Length);
        var end = Math.Clamp(e.StartIndex + e.Length, start, text.Length);
        SetPreedit(text[start..end]);
        Redraw();
    }

    /// <summary>変換が終わったら確定した文字列を本文へ入れる。</summary>
    /// <param name="sender">送信元。</param>
    /// <param name="e">引数。</param>
    private void OnImeSinkCompositionEnded(TextBox sender, TextCompositionEndedEventArgs e)
    {
        _ = sender;
        _ = e;

        _isComposing = false;
        SetPreedit(string.Empty);

        // 受け口には変換の結果だけが残る（確定のたびに空へ戻しているため）
        var committed = _imeSink?.Text ?? string.Empty;
        ClearImeSink();
        if (committed.Length > 0)
        {
            ReplaceSelection(committed);
        }

        PlaceImeSink();
        Redraw();
    }
}
