using System.ComponentModel;
using Microsoft.Extensions.Logging;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Palette;
using EsUtil.Feature.Annotation.Presenter.Panel;

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
public sealed class AnnotationTextEditor : UserControl, IAnnotationPanelHost
{
    /// <summary>注釈の表示と編集を担うレイヤー。</summary>
    private readonly AnnotationLayerControl _layer;

    /// <summary>本文の書式を操作するバー。</summary>
    private readonly AnnotationFormatBar _formatBar;

    /// <summary>書式バーを出すコントローラー。</summary>
    private readonly AnnotationPanelController _formatBarPanel;

    /// <summary>右クリックの外観パネルを出すコントローラー。</summary>
    private readonly AnnotationPanelController _stylePanel;

    /// <summary>パネルを載せる枠なしウィンドウ（種別ごとに 1 つ。 使い回す）。</summary>
    private readonly Dictionary<AnnotationPanelKind, Form> _windows = [];

    /// <summary>パネルを置いた位置（種別ごと。 このコントロールの左上を原点とする DIP）。</summary>
    private readonly Dictionary<AnnotationPanelKind, EsPoint> _positions = [];

    /// <summary>注釈の本文編集と書式バーをまとめたコントロールを初期化する。</summary>
    public AnnotationTextEditor()
    {
        _layer = new AnnotationLayerControl { Dock = DockStyle.Fill };
        _formatBar = new AnnotationFormatBar(_layer);
        _formatBarPanel = new AnnotationPanelController(this, AnnotationPanelAppearance.FormatBar, () => _layer.Log);
        _stylePanel = new AnnotationPanelController(this, AnnotationPanelAppearance.StyleEditor, () => _layer.Log);

        Controls.Add(_layer);
        HookDrag(AnnotationPanelKind.FormatBar, _formatBar);

        _layer.EditingChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
                ShowFormatBar();
                return;
            }

            _formatBarPanel.Close();
        };

        _layer.TextSelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
            }
        };

        _layer.SelectionChanged += (_, _) =>
        {
            if (_layer.IsEditingText)
            {
                _formatBar.RefreshState();
            }

            // 外観パネルは「選んでいる注釈」を編集するため、選択が変わったら閉じる
            if (!_layer.IsEditingText)
            {
                _stylePanel.Close();
            }
        };

        _layer.StyleEditorRequested += (_, e) => ShowStylePanel(e.X, e.Y);
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
    public bool IsEditingText => _layer.IsEditingText;

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

    /// <summary>外観の編集パネルが出ているかどうかを取得する。</summary>
    public bool IsStylePanelOpen => _stylePanel.IsOpen;

    /// <summary>パネルを置ける領域（DIP）を取得する。</summary>
    /// <value>このコントロールの範囲を、外側へ <see cref="AnnotationPanelMetrics.OutsideAllowance"/> だけ広げたもの。</value>
    /// <remarks>
    /// <b>ウィンドウの外へも出せる</b>: パネルは別のウィンドウへ載せるため、レイヤーの範囲へ挟むと
    /// ウィンドウの端で中へ押し戻される。 画面の外へ出ないことは <see cref="ApplyPosition"/> が止める。
    /// </remarks>
    public EsRect AvailableArea
    {
        get
        {
            var pad = AnnotationPanelMetrics.OutsideAllowance;
            return new EsRect(-pad, -pad, Width + (pad * 2), Height + (pad * 2));
        }
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
            // 届かないため、外側の押下の監視だけでは閉じられない（実測 2026-11-23）。
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

    /// <summary>選択中の注釈を基準に、書式バーを本文の外へ出す。</summary>
    /// <remarks>
    /// <b>本文に被せない</b>: 編集中の文字が隠れて読めない。 位置の判断はコア
    /// （<see cref="AnnotationPanelPlacement.ResolveOutside"/>）が単一所有する。
    /// </remarks>
    private void ShowFormatBar()
    {
        var bounds = _layer.GetSelectedBoundsRect();
        var body = new EsRect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
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
            return _formatBar;
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
        var width = Math.Max(0, (int)Math.Round(size.Width));
        var height = Math.Max(0, (int)Math.Round(size.Height));
        var x = Math.Clamp(location.X, area.X, Math.Max(area.X, area.Right - width));
        var y = Math.Clamp(location.Y, area.Y, Math.Max(area.Y, area.Bottom - height));

        window.Location = new Point(x, y);
        _positions[kind] = ToLocal(new Point(x, y));
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

        var preferred = _formatBar.GetPreferredSize(Size.Empty);
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
