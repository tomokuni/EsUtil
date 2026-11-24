using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Feature.Annotation.Presenter.Panel;

/// <summary>フローティング パネル（書式バー・右クリックの外観フライアウト）の開閉とドラッグを単一所有する。</summary>
/// <remarks>
/// <b>この型が「いつ・どこへ出すか・どう動かすか」を決める</b>: 4 つの UI 版は
/// <see cref="IAnnotationPanelHost"/> を実装するだけで、同じ動作になる。 UI ごとに判断を持つと、
/// 「ウィンドウの内側へ収める」「ポインタが外れたら閉じる」といった差が出る。<br/>
/// <b>閉じるのは外側のクリックだけ</b>: パネルの外を押したら UI が <see cref="Close"/> を呼ぶ。
/// ポインタが外れただけでは閉じない。<br/>
/// <b>1 つのコントローラーが持つパネルは 1 つ</b>: 書式バーと外観フライアウトは寿命が違うため、
/// UI は種別ごとにコントローラーを持つ。
/// </remarks>
public sealed class AnnotationPanelController
{
    /// <summary>パネルを置く窓口。</summary>
    private readonly IAnnotationPanelHost _host;

    /// <summary>パネルの外観（ホストへ渡す値）。</summary>
    private readonly AnnotationPanelAppearance _appearance;

    /// <summary>ログの出力先を返す関数（未設定は出力しない。 ホストのロガーを設定のたびに読む）。</summary>
    private readonly Func<ILogger>? _logger;

    /// <summary>ドラッグ開始時のパネルの左上（DIP）。</summary>
    private EsPoint _dragOrigin;

    /// <summary>ドラッグ開始時のポインターの位置（DIP）。</summary>
    private EsPoint _dragOriginCursor;

    /// <summary>フローティング パネルの制御を初期化する。</summary>
    /// <param name="host">パネルを置く窓口。</param>
    /// <param name="appearance">パネルの外観（省略すると <see cref="AnnotationPanelAppearance.StyleEditor"/>）。</param>
    /// <param name="logger">ログの出力先を返す関数（省略すると出力しない）。 ホストのロガーを設定のたびに読む。</param>
    public AnnotationPanelController(
        IAnnotationPanelHost host,
        AnnotationPanelAppearance? appearance = null,
        Func<ILogger>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
        _appearance = appearance ?? AnnotationPanelAppearance.StyleEditor;
        _logger = logger;
    }

    /// <summary>パネルを表示しているかどうかを取得する。</summary>
    /// <value>表示中の場合は <see langword="true"/>。</value>
    public bool IsOpen { get; private set; }

    /// <summary>表示中のパネルの種別を取得する。</summary>
    /// <value>表示していない場合は <see langword="null"/>。</value>
    public AnnotationPanelKind? Kind { get; private set; }

    /// <summary>パネルをドラッグしている最中かどうかを取得する。</summary>
    /// <value>ドラッグ中の場合は <see langword="true"/>。</value>
    public bool IsDragging { get; private set; }

    /// <summary>アンカーの位置へパネルを表示する（既に出ている場合は作り直す）。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="anchor">左上にする位置（DIP）。</param>
    public void Show(AnnotationPanelKind kind, EsPoint anchor)
        => ShowAt(kind, AnnotationPanelPlacement.ResolveInitial(anchor));

    /// <summary>対象（編集中の注釈）に被さらない位置へパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="target">被せたくない矩形（DIP）。</param>
    /// <param name="panelSize">パネルの大きさ（DIP）。</param>
    /// <param name="gap">対象とパネルの間の余白（DIP）。</param>
    /// <remarks>
    /// <b>書式バーはこれを使う</b>: 編集中の本文の上へ重ねると文字が隠れて読めない（実測 2026-11-20）。
    /// 位置の判断は <see cref="AnnotationPanelPlacement.ResolveOutside"/> が単一所有する。
    /// </remarks>
    public void ShowOutside(AnnotationPanelKind kind, EsRect target, EsSize panelSize, double gap)
        => ShowAt(
            kind,
            AnnotationPanelPlacement.ResolveOutside(target, panelSize, _host.AvailableArea, gap));

    /// <summary>配置済みのパネルを、ホストが報告する実寸で置き直す。</summary>
    /// <param name="target">被せたくない矩形（DIP）。</param>
    /// <param name="gap">対象とパネルの間の余白（DIP）。</param>
    /// <remarks>
    /// <b>実寸はレイアウト後にしか分からない</b>: レイアウト前の測定は枠の高さを正しく返さないことがある
    /// （実測 2026-11-22: WinUI 3 版で高さ 16040 DIP と測定され、パネルが左上へ寄る）。 UI は
    /// <see cref="ShowOutside"/> で仮に置いた後、レイアウトを確定させてから本メソッドを呼ぶ。
    /// </remarks>
    public void RepositionOutside(EsRect target, double gap)
    {
        if (Kind is not { } kind)
        {
            return;
        }

        var size = _host.GetPanelSize(kind);
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var position = AnnotationPanelPlacement.ResolveOutside(
            target, size, _host.AvailableArea, gap);
        _host.MovePanel(kind, position);
        (_logger?.Invoke() ?? NullLogger.Instance).LogPanelPlaced(
            kind.ToString(), position, size, _host.AvailableArea);
    }

    /// <summary>位置を指定してパネルを表示する。</summary>
    /// <param name="kind">パネルの種別。</param>
    /// <param name="position">左上の位置（DIP）。</param>
    private void ShowAt(AnnotationPanelKind kind, EsPoint position)
    {
        if (IsOpen)
        {
            Close();
        }

        _host.CreatePanel(kind, _appearance, position);
        IsOpen = true;
        Kind = kind;
        (_logger?.Invoke() ?? NullLogger.Instance).LogPanelVisibility(kind.ToString(), visible: true);
    }

    /// <summary>パネルを閉じる（出ていなければ何もしない）。</summary>
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        var kind = Kind!.Value;
        IsOpen = false;
        Kind = null;
        IsDragging = false;
        _host.DestroyPanel(kind);
        (_logger?.Invoke() ?? NullLogger.Instance).LogPanelVisibility(kind.ToString(), visible: false);
    }

    /// <summary>パネルの外側を押されたときに閉じる。</summary>
    /// <param name="point">押された位置（パネルを置く座標系の DIP）。</param>
    /// <returns>閉じた場合は <see langword="true"/>（表示していない・内側の場合は <see langword="false"/>）。</returns>
    /// <remarks>
    /// <b>閉じるのは外側の押下だけ</b>で、ポインタが外れただけでは閉じない。 判定は本メソッドが単一所有する
    /// （4 つの UI 版が「自分のパネルの矩形」と「押した位置」を渡すだけで同じ動作になる）。
    /// </remarks>
    public bool CloseIfOutside(EsPoint point)
    {
        if (Kind is not { } kind)
        {
            return false;
        }

        var position = _host.GetPanelPosition(kind);
        var size = _host.GetPanelSize(kind);
        if (size.Width <= 0 || size.Height <= 0)
        {
            return false;
        }

        if (new EsRect(position.X, position.Y, size.Width, size.Height).Contains(point))
        {
            return false;
        }

        Close();
        return true;
    }

    /// <summary>実寸が分かったパネルを置ける領域の内側へ収める。</summary>
    /// <param name="panelSize">パネルの実寸（DIP）。</param>
    /// <remarks>
    /// <b>実寸は置いた後でしか分からない</b>（中身の幅はフォント名・大きさ・色で決まる）。 UI はパネルを
    /// 置いて測った実寸を渡すだけでよく、位置の補正は本メソッドが単一所有する。
    /// </remarks>
    public void Constrain(EsSize panelSize)
    {
        if (Kind is not { } kind || panelSize.Width <= 0 || panelSize.Height <= 0)
        {
            return;
        }

        var current = _host.GetPanelPosition(kind);
        var clamped = AnnotationPanelPlacement.Clamp(
            current, panelSize.Width, panelSize.Height, _host.AvailableArea);
        if (clamped.X != current.X || clamped.Y != current.Y)
        {
            _host.MovePanel(kind, clamped);
            (_logger?.Invoke() ?? NullLogger.Instance).LogPanelPlaced(
                kind.ToString(), clamped, panelSize, _host.AvailableArea);
        }
    }

    /// <summary>パネルのドラッグを始める。</summary>
    /// <param name="cursor">押下した位置（パネルを置く座標系の DIP）。</param>
    /// <remarks>表示していない場合は何もしない（ドラッグの状態だけが残るのを防ぐ）。</remarks>
    public void BeginDrag(EsPoint cursor)
    {
        if (!IsOpen)
        {
            return;
        }

        IsDragging = true;
        _dragOrigin = Kind is { } kind ? _host.GetPanelPosition(kind) : cursor;
        _dragOriginCursor = cursor;
    }

    /// <summary>パネルをドラッグして動かす。</summary>
    /// <param name="cursor">現在のポインターの位置（パネルを置く座標系の DIP）。</param>
    /// <remarks>ドラッグしていない場合は何もしない。</remarks>
    public void DragTo(EsPoint cursor)
    {
        if (!IsOpen || !IsDragging || Kind is not { } kind)
        {
            return;
        }

        var moved = AnnotationPanelPlacement.ResolveDrag(_dragOrigin, _dragOriginCursor, cursor);
        var size = _host.GetPanelSize(kind);
        if (size.Width > 0 && size.Height > 0)
        {
            // 掴めなくなる位置へは動かさない（置ける領域の内側へ収める）
            moved = AnnotationPanelPlacement.Clamp(
                moved, size.Width, size.Height, _host.AvailableArea);
        }

        _host.MovePanel(kind, moved);
    }

    /// <summary>パネルのドラッグを終える。</summary>
    public void EndDrag() => IsDragging = false;
}
