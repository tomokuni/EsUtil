using System.ComponentModel;
using Microsoft.Extensions.Logging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Presenter.Panel;
using EsUtil.Feature.Annotation.Logging;
using EsUtil.Feature.StyledText;
using EsUtil.Feature.StyledText.Presenter.Formatting;
using EsUtil.Ui.WinForms.StyledText;

namespace EsUtil.Ui.WinForms.Annotation;

/// <summary>注釈の本文（表示とその場編集）と書式バーを 1 つにまとめたコントロール（WinForms 版）。</summary>
/// <remarks>
/// <b>セットで使う</b>: レイヤー（画像・注釈・入力）と、書式バー・右クリックの外観パネルをまとめて持つ。
/// 2 つを別々に置くと、位置合わせと表示の切り替えをホストが書くことになる。<br/>
/// <b>レイヤーは <see cref="Layer"/> から触る</b>: 画像・文書・選択・元に戻すなどは
/// <see cref="AnnotationLayerControl"/> の API を使う（本コントロールは配置だけを担う）。<br/>
/// <b>位置と開閉はコアが決める</b>: <see cref="AnnotationPanelController"/> が「どこへ出すか・ドラッグで
/// どう動くか・いつ閉じるか」を単一所有する。 本クラスは <see cref="IAnnotationPanelHost"/> を実装し、
/// パネルの作成・移動・破棄だけを行う（4 つの UI 版で動作を揃えるため）。<br/>
/// <b>パネルは枠なしのウィンドウへ載せる</b>: アプリの子にするとウィンドウの外へ出せない。 枠なしの
/// ウィンドウなら画面の内側まで置ける（はみ出さないことは位置の適用が作業領域で止める）。
/// </remarks>
public sealed class AnnotationEditorPanel : UserControl, IAnnotationPanelHost
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>書式バー（編集中だけ表示する）。 独自の GUI へ差し替えられる。</summary>
    private IStyledTextFormatBar _formatBar;

    /// <summary>書式バーを作る処理（差し替えの入口）。</summary>
    private IStyledTextFormatBarFactory _formatBarFactory = new StyledTextFormatBarFactory();

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観パネルを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>本文の表示を担うコントロール（選択中の吹き出しの本文の上へ重ねる）。</summary>
    private readonly StyledText.StyledTextControl _textEditor;

    /// <summary>パネルを載せる枠なしウィンドウ（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Form> _windows = [];

    /// <summary>パネルを置いた位置（種別ごと。 このコントロールの左上を原点とする DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationEditorPanel()
    {
        _layer = new AnnotationLayerControl { Dock = DockStyle.Fill };
        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar, () => _layer.Log);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor, () => _layer.Log);

        // **本文の表示は専用のコントロールが担う**: 選択中の吹き出しの本文の上へ重ね、
        // 本文の文字・選択範囲・キャレット・変換中の文字列を描く（レイヤーは本文を描かない）。
        // 入力とフォーカスはレイヤーが持つため、本コントロールは表示専用にする（キーと IME の宛先を変えない）。
        _textEditor = new StyledText.StyledTextControl
        {
            IsInputEnabled = false,
            Visible = false,
            MeasureText = StyledText.RichTextPainter.Measure,
            Editor = _layer.Session.Editor,
        };

        Controls.Add(_layer);
        Controls.Add(_textEditor);

        // **書式バーは本文コントロールの後に作る**: バーは本文へのフォーカスとキー転送を受け取る
        _formatBar = CreateFormatBar();
        HookDrag(AnnotationPanelKind.FormatBar, _formatBar.View);

        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                // **バーは選択ができてから出す**: 編集中でも 1 文字以上を選ぶまでは出さない
                UpdateFormatBarVisibility();

                // **入力とフォーカスは本文コントロールが持つ**: 編集を始めたらコントロールへ移す
                BeginInvoke(() =>
                {
                    if (_layer.IsEditingText)
                    {
                        _textEditor.Focus();
                    }
                });
                return;
            }

            _formatBarPanel.Close();
        };

        _layer.TextSelectionChanged += (_, _) => UpdateFormatBarVisibility();

        _layer.SelectionChanged += (_, _) =>
        {
            UpdateFormatBarVisibility();

            // 外観パネルは「選んでいる注釈」を編集するため、選択が変わったら閉じる
            if (!_layer.IsEditingText)
            {
                _stylePanel.Close();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);

        // **入力・編集・IME は本文コントロールが単一所有する**: 確定した本文をセッションへ書き戻し、
        // コントロールが編集を終えたらセッションも終える（書き戻しの経路を 1 本にする）。
        _textEditor.RichTextChanged += (_, _) => OnTextEditorChanged();
        _textEditor.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText && !_textEditor.IsEditing)
            {
                _layer.Session.EndTextEditing();
            }
        };

        // **選択の変更は 2 か所へ伝える**: 書式バーの表示と、レイヤーの購読者（ホスト）への通知。
        _textEditor.SelectionChanged += (_, _) =>
        {
            _layer.NotifyTextSelectionChanged();
            UpdateFormatBarVisibility();
        };

        // 文書・選択・編集の状態が変わったら、重ねた本文コントロールの内容と位置を追随させる
        _layer.DocumentChanged += (_, _) => SyncTextEditor();
        _layer.SelectionChanged += (_, _) => SyncTextEditor();
        _layer.EditingChanged += (_, _) => SyncTextEditor();

        // 選択範囲の変更は本文コントロールが描くため、変わったら渡し直す
        _layer.TextSelectionChanged += (_, _) => SyncTextEditor();

    }

    /// <summary>本文の表示を担うコントロールを取得する（テストと差し替えの入口）。</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal StyledText.StyledTextControl TextEditor => _textEditor;

    /// <summary>本文コントロールの内容（本文・変換中の文字列・倍率）と位置を追随させる。</summary>
    /// <remarks>
    /// <b>本文はレイヤーが単一所有する</b>: コントロールは描画の入口であり、本文の値は編集セッションから写す。
    /// </remarks>
    internal void SyncTextEditor()
    {
        _textEditor.Editor = _layer.Session.Editor;
        _textEditor.LineHeightFactor = _layer.LineHeightFactor;

        if (_layer.Session.Selected is not BubbleAnnotation bubble)
        {
            _textEditor.Visible = false;
            _textEditor.IsInputEnabled = false;
            _layer.ExternalTextId = null;
            return;
        }

        // **変換中の文字列は本文コントロールが描く**: 本文へ混ぜて渡すと二重になるため、
        // 本文は文書の値のまま渡し、変換中の文字列は PreeditText で別に渡す。
        _textEditor.RichTextValue = bubble.Text;
        _textEditor.RenderScale = _layer.ViewScale;

        // **編集中だけ入力を受ける**: 編集していない間はレイヤーが本文を描き、押下もレイヤーへ渡す
        _textEditor.IsInputEnabled = _layer.IsEditingText;
        _textEditor.TabStop = _layer.IsEditingText;
        var body = _layer.GetSelectedBodyRect();
        _textEditor.Bounds = Rectangle.Round(new RectangleF((float)body.X, (float)body.Y, (float)body.Width, (float)body.Height));
        _textEditor.Visible = true;

        // レイヤーは同じ本文を描かない（二重に描くと縁が太り、位置も二重に見える）
        _layer.ExternalTextId = bubble.Id;
    }

    /// <summary>本文コントロールで本文が変わったら、編集セッション（文書）へ書き戻す。</summary>
    /// <remarks>
    /// <b>文書は編集セッションが単一所有する</b>: コントロールは入力の入口であり、確定した本文は
    /// セッションへ渡す（元に戻す・保存・他の表示は文書だけを見る）。 同じ本文なら何もしない
    /// （<see cref="SyncTextEditor"/> が文書から写した値を書き戻して往復しない）。
    /// </remarks>
    private void OnTextEditorChanged()
    {
        if (!_layer.IsEditingText || _layer.Session.Selected is not BubbleAnnotation bubble)
        {
            return;
        }

        if (bubble.Text.Equals(_textEditor.RichTextValue))
        {
            return;
        }

        _layer.Session.CommitTextEdit();
    }

    /// <summary>選択中の本文をクリップボードへコピーする。</summary>
    /// <remarks>
    /// <b>クリップボードの処理は本文コントロールが単一所有する</b>: 注釈のレイヤーは持ちません
    /// （キーと API の両方から同じ処理を呼べるよう、入口だけをここに置きます）。
    /// </remarks>
    public void CopyTextSelection()
    {
        _layer.Log.LogTextOperation(
            "コピー", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, _textEditor.GetSelectedText());
        _ = _textEditor.CopyAsync();
    }

    /// <summary>選択中の本文をクリップボードへ切り取る。</summary>
    public void CutTextSelection()
    {
        _layer.Log.LogTextOperation(
            "切り取り", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, _textEditor.GetSelectedText());
        _ = _textEditor.CutAsync();
    }

    /// <summary>クリップボードの文字列をキャレットの位置へ貼り付ける。</summary>
    public void PasteText() => _ = PasteAndLogAsync();

    /// <summary>クリップボードの文字列を貼り付け、操作の内容と値を記録する。</summary>
    /// <returns>貼り付けの非同期処理。</returns>
    private async Task PasteAndLogAsync()
    {
        var inserted = await _textEditor.PasteAsync();
        if (inserted is null)
        {
            return;
        }

        _layer.Log.LogTextOperation(
            "貼り付け", _textEditor.CaretIndex, _textEditor.SelectionStart, _textEditor.SelectionLength, inserted);
    }
    /// <summary>書式バーを作る処理を取得または設定する（独自の GUI へ差し替える入口）。</summary>
    /// <value>既定は組み込みの書式バー（<see cref="StyledTextFormatBarFactory"/>）。</value>
    /// <remarks>差し替えはバーを出す前に行う（表示中のバーを入れ替えると位置と操作の対象が食い違う）。</remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IStyledTextFormatBarFactory FormatBarFactory
    {
        get => _formatBarFactory;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_formatBarPanel.IsOpen)
            {
                throw new InvalidOperationException("書式バーを表示中は差し替えできません。");
            }

            _formatBarFactory = value;
            _formatBar = CreateFormatBar();
        }
    }

    /// <summary>書式バーの構成（フォントの一覧・色のボタン・色の表記）を取得する。</summary>
    /// <value>注釈の代表色を書式バーの色のボタンへ渡す構成。</value>
    public StyledTextFormatBarOptions FormatBarOptions { get; } = new()
    {
        CreateColorButton = (color, apply) => AnnotationEditorControls.ColorButton(color, apply),
    };

    /// <summary>書式バーを作る。</summary>
    /// <returns>書式バー。</returns>
    private IStyledTextFormatBar CreateFormatBar()
    {
        var bar = _formatBarFactory.Create(new LayerFormatBarContext(_layer), FormatBarOptions);

        // **キーとフォーカスは本文コントロールが持つ**: バーは操作を転送し、適用後に本文へ戻すだけにする
        bar.SetKeyHandler(_textEditor.TryHandleEditingKey);
        bar.SetFocusRequest(() =>
        {
            if (_layer.IsEditingText)
            {
                _textEditor.Focus();
            }
        });
        bar.TextFormat = FormatBarOptions.TextFormat;
        return bar;
    }

    /// <summary>書式バーが書式を読み書きする相手（注釈の編集セッションへつなぐ）。</summary>
    /// <param name="layer">注釈のレイヤー。</param>
    private sealed class LayerFormatBarContext(AnnotationLayerControl layer) : ITextFormatBarContext
    {
        /// <inheritdoc/>
        public TextStyle SelectedTextStyle => layer.Session.SelectedTextStyle;

        /// <inheritdoc/>
        public bool HasTextSelection => layer.HasTextSelection;

        /// <inheritdoc/>
        public bool SelectedTextMatches(Func<TextStyle, bool> match) => layer.Session.SelectedTextMatches(match);

        /// <inheritdoc/>
        public bool Transform(Func<TextStyle, TextStyle> transform) => layer.Session.TransformTextStyle(transform);

        /// <inheritdoc/>
        public bool Apply(TextStyle style) => layer.Session.ApplyTextStyle(style);
    }

    /// <summary>注釈の表示と編集を担うレイヤーを取得する。</summary>
    public AnnotationLayerControl Layer => _layer;

    /// <summary>ログの出力先を取得または設定する（レイヤーへ委譲する）。</summary>
    /// <value>ロガー。 null を設定すると出力しない。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ILogger Log
    {
        get => _layer.Log;
        set => _layer.Log = value;
    }

    /// <summary>ログの接頭辞を取得または設定する（レイヤーへ委譲する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? LogPrefix
    {
        get => _layer.LogPrefix;
        set => _layer.LogPrefix = value;
    }
    /// <summary>本文を編集中かどうかを取得する。</summary>
    /// <value>編集中の場合は <see langword="true"/>。 書式バーは 1 文字以上を選んでいる間だけ出る。</value>
    public bool IsEditingText => _layer.IsEditingText;

    /// <summary>編集中に書式バーを自動で表示するかどうかを取得または設定する。</summary>
    /// <value>自動で表示する場合は <see langword="true"/>（既定）。 ホストが独自にバーを出す場合は <see langword="false"/>。</value>
    /// <remarks>
    /// <b>表示の条件は本文コントロールが単一所有する</b>: 編集中であること・自動表示が有効であること・
    /// 1 文字以上を選んでいることの 3 つを満たす間だけ表示する（<c>StyledTextControl.ShouldShowFormatBar</c>）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowFormatBarWhileEditing
    {
        get => _textEditor.ShowFormatBarWhileEditing;
        set
        {
            var previous = _textEditor.ShowFormatBarWhileEditing;
            if (previous != value)
            {
                _textEditor.ShowFormatBarWhileEditing = value;
                _layer.Log.LogSettingChanged(nameof(ShowFormatBarWhileEditing), previous, value);
            }

            UpdateFormatBarVisibility();
        }
    }

    /// <summary>書式バーの色の入力欄へ出す表記を取得または設定する。</summary>
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public ColorTextFormat TextFormat
    {
        get => _formatBar.TextFormat;
        set
        {
            _formatBar.TextFormat = value;
            _layer.ColorTextFormat = value;
        }
    }

    /// <summary>書式バーを表示中かどうかを取得する。</summary>
    /// <value>編集中で 1 文字以上を選んでいる間は <see langword="true"/>。</value>
    public bool IsFormatBarOpen => _formatBarPanel.IsOpen;

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>このコントロールの範囲を外側へ広げ、画面の見えている範囲との交わりを取ったもの。</value>
    /// <remarks>
    /// <b>レイヤーの外側へも置ける</b>: 範囲だけで挟むと、端にある注釈の書式バーが内側へ押し戻され、
    /// 本文に重なる。 求めた領域の外へは出さず、画面の外へ出ないことは <see cref="ApplyPosition"/> が
    /// 重ねて止める。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            var layer = new EsRect(0, 0, Width, Height);
            return AnnotationPanelPlacement.ResolveAvailableArea(
                layer, VisibleBounds(), AnnotationPanelMetrics.OutsideAllowance);
        }
    }

    /// <summary>アプリのウィンドウの内側を、このコントロールの座標（DIP）で取得する。</summary>
    /// <returns>見えている範囲。 ウィンドウへ繋がっていない場合は既定値。</returns>
    private EsRect VisibleBounds()
    {
        if (FindForm() is not { ClientSize.Width: > 0, ClientSize.Height: > 0 } form)
        {
            return default;
        }

        var local = RectangleToClient(form.RectangleToScreen(form.ClientRectangle));
        return new EsRect(local.X, local.Y, local.Width, local.Height);
    }

    /// <summary>表示中のパネルの左上（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>左上の位置。 表示していない場合は原点。</returns>
    public EsPoint GetPanelPosition(AnnotationPanelKind kind)
        => _positions.TryGetValue(kind, out var position) ? position : default;

    /// <summary>表示中のパネルの大きさ（DIP）を取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>パネルの大きさ。 表示していない場合は中身の希望寸法。</returns>
    public EsSize GetPanelSize(AnnotationPanelKind kind)
        => _windows.TryGetValue(kind, out var window) && window.Visible && window.Width > 0
            ? new EsSize(window.Width, window.Height)
            : MeasurePanel(kind);

    /// <summary>パネルを作って表示する。</summary>
    /// <param name="kind">パネルの種別（中身を決める）。</param>
    /// <param name="appearance">外観（枠・余白・幅）。</param>
    /// <param name="position">左上の位置（このコントロールの左上を原点とする DIP）。</param>
    public void CreatePanel(
        AnnotationPanelKind kind, AnnotationPanelAppearance appearance, EsPoint position)
    {
        RemovePanel(kind);

        var content = BuildContent(kind, appearance);

        if (!_windows.TryGetValue(kind, out var window))
        {
            window = CreateWindow();
            _windows[kind] = window;

            // **フォーカスが外れたら閉じる**: 他のアプリを押した場合、このコントロールへ押下は
            // 届かないため、外側の押下の監視だけでは閉じられない。
            window.Deactivate += (_, _) => CloseOnFocusLost(kind);
        }

        window.SuspendLayout();
        window.Controls.Clear();
        content.Dock = DockStyle.Fill;
        window.Controls.Add(content);
        window.ResumeLayout();

        ApplyPosition(kind, position);
        if (!window.Visible)
        {
            window.Show();
        }

        // **レイアウト後の実寸で置き直す**: 中身の大きさは出した後でしか決まらない
        ApplyPosition(kind, position);
    }

    /// <summary>パネルを移動する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（このコントロールの左上を原点とする DIP）。</param>
    public void MovePanel(AnnotationPanelKind kind, EsPoint position) => ApplyPosition(kind, position);

    /// <summary>パネルを画面から外す。</summary>
    /// <param name="kind">パネルの種別。</param>
    public void DestroyPanel(AnnotationPanelKind kind) => RemovePanel(kind);

    /// <summary>右クリック位置へ外観の編集パネルを表示する。</summary>
    /// <param name="x">位置（このコントロールの座標）。</param>
    /// <param name="y">位置（このコントロールの座標）。</param>
    public void ShowStylePanel(double x, double y)
    {
        var point = new EsPoint(Math.Max(0, x), Math.Max(0, y));
        _stylePanel.Show(AnnotationPanelKind.StyleEditor, point);

        // 中身の実寸で収め直す（右クリックの位置が画面の端だと、はみ出すため）
        _stylePanel.Constrain(MeasurePanel(AnnotationPanelKind.StyleEditor));
    }

    /// <summary>外観・重なり順の編集パネルを閉じる。</summary>
    public void CloseStylePanel() => _stylePanel.Close();

    /// <summary>現在の編集状態に合わせて書式バーを出すか閉じるかを決める。</summary>
    /// <remarks>
    /// <b>条件は本文コントロールが持つ</b>: 編集中で、自動表示が有効で、1 文字以上を選んでいる間だけ出す。
    /// 選択が無い（適用先が決まらない）間は出さない。 位置は <see cref="ShowFormatBar"/> が決める。
    /// </remarks>
    private void UpdateFormatBarVisibility()
    {
        if (!_textEditor.ShouldShowFormatBar)
        {
            _formatBarPanel.Close();
            return;
        }

        _formatBar.Refresh();
        ShowFormatBar();
    }

    /// <summary>選択中の注釈を基準に、書式バーを本文の外へ出す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない。 位置の判断はコア
    /// （<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。
    /// </remarks>
    private void ShowFormatBar()
    {
        var body = _layer.GetSelectedBoundsRect();

        // **表示中は位置だけ移す**: 表示し直すとパネルの再作成が挟まり、選択のたびに書式バーがちらつく。
        if (_formatBarPanel.IsOpen)
        {
            _formatBarPanel.RepositionOutside(body, AnnotationPanelMetrics.FormatBarGap);
            return;
        }

        _formatBarPanel.ShowOutside(
            AnnotationPanelKind.FormatBar, body, MeasurePanel(AnnotationPanelKind.FormatBar),
            AnnotationPanelMetrics.FormatBarGap);

        // レイアウト後の実寸で置き直す
        _formatBarPanel.RepositionOutside(body, AnnotationPanelMetrics.FormatBarGap);
    }

    /// <summary>種別のパネルの中身を作る。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="appearance">外観（外観パネルだけが使う）。</param>
    /// <returns>中身。</returns>
    /// <remarks>
    /// <b>外観パネルの中身は表示のたびに作り直す</b>: 吹き出しの設定は「吹き出しを選んでいるとき」だけ
    /// 出すため、一度作った中身を使い回すと、最初に枠線を右クリックした後は吹き出しの設定が出ない。
    /// </remarks>
    private Control BuildContent(AnnotationPanelKind kind, AnnotationPanelAppearance appearance)
    {
        if (kind == AnnotationPanelKind.FormatBar)
        {
            return _formatBar.View;
        }

        var panel = new AnnotationStylePanel(_layer, CloseStylePanel, appearance);
        HookDrag(kind, panel);
        return panel;
    }

    /// <summary>枠なしウィンドウを作る。</summary>
    /// <returns>作ったウィンドウ。</returns>
    private static Form CreateWindow() => new()
    {
        FormBorderStyle = FormBorderStyle.None,
        ShowInTaskbar = false,
        StartPosition = FormStartPosition.Manual,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        TopMost = false,
        Padding = Padding.Empty,
    };

    /// <summary>パネルの中身をドラッグで動かせるようにする。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="content">中身。</param>
    /// <remarks>
    /// ボタンや入力欄の上では子のコントロールが押下を受けるため、余白を掴んだときだけ動く
    /// （4 つの UI 版で同じ）。
    /// </remarks>
    private void HookDrag(AnnotationPanelKind kind, Control content)
    {
        content.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                Controller(kind).BeginDrag(ToLocal(content.PointToScreen(e.Location)));
            }
        };

        content.MouseMove += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                Controller(kind).DragTo(ToLocal(content.PointToScreen(e.Location)));
            }
        };

        content.MouseUp += (_, _) => Controller(kind).EndDrag();
    }

    /// <summary>パネルを画面の作業領域の内側へ収めて置く。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（このコントロールの左上を原点とする DIP）。</param>
    /// <remarks>
    /// <b>画面の外へは出さない</b>: パネルは別のウィンドウなので、画面の外へ出ると見えなくなる。
    /// 置いた後の位置を控えるため、外側の押下の判定と実際の位置が食い違わない。
    /// </remarks>
    private void ApplyPosition(AnnotationPanelKind kind, EsPoint position)
    {
        _positions[kind] = position;

        if (!_windows.TryGetValue(kind, out var window))
        {
            return;
        }

        var size = GetPanelSize(kind);
        var location = PointToScreen(new Point((int)Math.Round(position.X), (int)Math.Round(position.Y)));
        var area = Screen.FromPoint(location).WorkingArea;
        // 収める規則は提示層の AnnotationPanelPlacement.Clamp が単一所有する
        var clamped = AnnotationPanelPlacement.Clamp(
            new EsPoint(location.X, location.Y),
            Math.Max(0, Math.Round(size.Width)),
            Math.Max(0, Math.Round(size.Height)),
            new EsRect(area.X, area.Y, area.Width, area.Height));
        var placed = new Point((int)Math.Round(clamped.X), (int)Math.Round(clamped.Y));

        window.Location = placed;
        _positions[kind] = ToLocal(placed);
    }

    /// <summary>画面の座標をこのコントロールの座標（DIP）へ変換する。</summary>
    /// <param name="screen">画面の座標（ピクセル）。</param>
    /// <returns>このコントロールの左上を原点とする座標。</returns>
    private EsPoint ToLocal(Point screen)
    {
        var local = PointToClient(screen);
        return new EsPoint(local.X, local.Y);
    }

    /// <summary>パネルの大きさ（DIP）を測る。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>実寸。 まだ出していない場合は中身の希望寸法。</returns>
    private EsSize MeasurePanel(AnnotationPanelKind kind)
    {
        if (kind != AnnotationPanelKind.FormatBar)
        {
            return default;
        }

        var preferred = _formatBar.View.GetPreferredSize(Size.Empty);
        return new EsSize(preferred.Width, preferred.Height);
    }

    /// <summary>種別のパネルを画面から外す（ウィンドウと中身は残す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    private void RemovePanel(AnnotationPanelKind kind)
    {
        if (_windows.TryGetValue(kind, out var window) && window.Visible)
        {
            window.Hide();
        }
    }

    /// <summary>パネルのウィンドウからフォーカスが外れたときに、外観パネルを閉じる。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <remarks>
    /// <b>書式バーは閉じない</b>: 本文の編集中だけ出すもので、押すたびにフォーカスが移るため
    /// （閉じると書式を変えられない）。
    /// </remarks>
    private void CloseOnFocusLost(AnnotationPanelKind kind)
    {
        if (kind != AnnotationPanelKind.StyleEditor || !_stylePanel.IsOpen)
        {
            return;
        }

        BeginInvoke(() =>
        {
            if (_stylePanel.IsOpen)
            {
                _stylePanel.Close();
            }
        });
    }

    /// <summary>種別ごとのコントローラーを取得する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <returns>コントローラー。</returns>
    private AnnotationPanelController Controller(AnnotationPanelKind kind)
        => kind == AnnotationPanelKind.FormatBar ? _formatBarPanel : _stylePanel;
}
