using EsUtil.Feature.Marquee.Core.Interaction;
using EsUtil.Feature.Marquee.Core.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Native;
using Microsoft.Extensions.Logging;
using static EsUtil.Feature.Marquee.MarqueeCore.Limits;


namespace EsUtil.Feature.Marquee;

/// <summary>Marquee（ドラッグ矩形の選択）の操作を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの状態機械・描画内容の解決・設定を 1 つの窓口へ集約する）。<br/>
/// <br/>
/// <b>責務</b>: Marquee の「操作（入力の通知・外部からの状態操作・描画内容の解決・設定）」を単一の窓口で提供し、<br/>
/// UI 層とホストは本クラスだけを参照する（コアの内部契約である状態機械 <see cref="MarqueeSession"/> へ直接触れない）。<br/>
/// <br/>
/// <b>本クラスが単一所有するもの</b>:<br/>
/// - セッション（入力の解釈と状態遷移の判断）の生成と保持<br/>
/// - 環境値の保持（座標変換ポート・倍率）と、ポート未指定時に倍率を使う規則の適用<br/>
/// - 効果（フォーカス・カーソル操作・カーソル形状・ポインターキャプチャ・再描画）の適用<br/>
/// - 1 フレーム分の描画内容の解決（描くかどうかの判断）<br/>
/// <br/>
/// <b>本クラスが持つもの</b>: 選択の判断・状態・描画内容（選択矩形・装飾・ラベル）と描画順のデータ、<br/>
/// 装飾の設定の保持、ラベルの文言プロバイダの保持。<br/>
/// <br/>
/// <b>描画は本コアの関心事ではない</b>: 本コアは描画 API も描画面も知らない（幾何の値（<c>MarqueeGeometry</c>）と<br/>
/// その描画順を表す層のデータを返すだけである）。 描画は UI 層が行い、<br/>
/// 層の順序に従った描画手順は描画層（本コアを参照する上位の層）が単一所有する。<br/>
/// <br/>
/// <b>UI 層の責務</b>: 入力イベントの受付と座標変換（<see cref="ToSource"/> / <see cref="ToDip"/>）、<br/>
/// <see cref="IMarqueeEffects"/> の実装、通知（<see cref="Subscribe"/>）の公開イベントへの転送、<br/>
/// 返された描画内容の反映（図形は描画実装へ委譲、文字はテキスト要素の配置）。<b>描画順はコアの指示に従う</b>。<br/>
/// <br/>
/// <b>依存分離</b>: UI 非依存のコア型（<see cref="EsPoint"/> / <see cref="EsRect"/> / <see cref="EsKey"/>）と<br/>
/// 抽象（<see cref="ICursorService"/> / <see cref="ICoordTransform"/> / <see cref="IMarqueeEffects"/>）にのみ依存する。<br/>
/// UI フレームワークに依存しないため、UI 初期化なしの <c>[Fact]</c> で検証できる。
/// </remarks>
public sealed partial class MarqueeCore
{
    /// <summary>入力の解釈と状態遷移の判断を担うセッション（コア内部の状態機械）。</summary>
    private readonly MarqueeSession _session;

    /// <summary>効果（フォーカス・再描画・カーソル操作）の適用先（UI 層が実装する）。</summary>
    private readonly IMarqueeEffects _effects;

    /// <summary>カーソルサービス（nudge のカーソル移動・表示切替）。 null の場合はカーソル操作を行わない。</summary>
    private readonly ICursorService? _cursor;

    /// <summary>外部から指定された座標変換ポート（null = 未指定。 未指定時は <see cref="ClientToSourceScaling"/> を倍率として使う）。</summary>
    /// <remarks>
    /// セッションは null を等価変換に置換して保持するため、<b>指定の有無（null かどうか）は本フィールドだけが保持する</b>。<br/>
    /// この区別が必要なのは、ポート未指定時に倍率（<see cref="ClientToSourceScaling"/>）を使う規則をコアが単一所有するためである。
    /// </remarks>
    private ICoordTransform? _coordPort;

    /// <summary>ポート未指定時に適用する倍率（0 以下・等倍は等価変換）。</summary>
    private double _dipToSourceScaling = ClientToSourceScalingDefault;

    /// <summary>生成の記録（<c>MarqueeLogMessage.LogFacadeCreated</c>）を済ませたかどうか。</summary>
    /// <remarks>出力先の設定は複数回起こりうるため、生成の記録は 1 度だけにする。</remarks>
    private bool _createdLogged;

    /// <summary>ファサードを初期化する。</summary>
    /// <param name="effects">効果（フォーカス・再描画・カーソル形状・キャプチャ・座標変換）の適用先。 null 不可。</param>
    /// <param name="cursor">カーソルサービス。 null 可（null の場合はカーソル操作を行わない）。</param>
    /// <param name="options">初期設定。 null 可（null の場合は <see cref="MarqueeOptions.Default"/>）。</param>
    /// <param name="logger">ロガー。 null 可（null の場合は出力なし。 後から <see cref="Log"/> でも設定できる）。</param>
    /// <exception cref="ArgumentNullException"><paramref name="effects"/> が null の場合。</exception>
    /// <remarks>
    /// <b>ロガーを注入した場合は、その場で生成の記録を出力する</b>（コンストラクター呼び出しの記録と、<br/>
    /// 生成時の設定を 1 度だけ出力する規則を兼ねる）。 注入しない場合は出力先が未設定のため、<br/>
    /// <see cref="Log"/> が初めて設定された時点で生成の記録を出力する（<see cref="LogCreatedOnce"/>）。
    /// </remarks>
    public MarqueeCore(
        IMarqueeEffects effects,
        ICursorService? cursor = null,
        MarqueeOptions? options = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(effects);

        _effects = effects;
        _cursor = cursor;
        _session = new MarqueeSession(options ?? MarqueeOptions.Default);

        if (logger is not null)
        {
            // コンストラクター由来の注入（生成の記録を今すぐ出力する）
            Log = logger;
        }
    }

    /// <summary>上位（UI 層）へ伝える通知を購読する。 UI 層は自 UI の公開イベントへ転送する。</summary>
    /// <param name="notify">通知を受け取る処理。</param>
    /// <returns>購読を解除する <see cref="IDisposable"/>。</returns>
    /// <remarks>
    /// 通知の種別と公開イベントの対応は UI 層が持ち、判断（確定・キャンセル等）は本クラスが済ませている。<br/>
    /// <b><c>event</c> を使わない理由</b>: <see cref="MarqueeNotice"/> は値型であり <see cref="System.EventHandler{T}"/> の
    /// <c>T</c>（<see cref="EventArgs"/> 派生）にできない（値型の通知でヒープを確保しないための設計）。<br/>
    /// UI 層は本通知を自分の <see cref="System.EventHandler{T}"/> イベントへ変換して公開する。
    /// </remarks>
    public IDisposable Subscribe(Action<MarqueeNotice> notify) => _session.Subscribe(notify);

    /// <summary>設定の変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は出力なし（<see cref="LoggerFallback"/> が吸収する）。</value>
    /// <remarks>
    /// 保持はセッション（コアの状態機械）が単一所有する（本クラスは委譲し、生成の記録だけを自前で行う）。<br/>
    /// <b>生成の記録</b>: コンストラクターでロガーが注入されていない場合、本プロパティの <b>初回の設定時</b>に
    /// 生成時の設定を 1 度だけ記録する（生成時は出力先が未設定のため、その場では記録できない。 2 回目以降の設定では繰り返さない）。
    /// </remarks>
    public ILogger Log
    {
        get => _session.Log;
        set
        {
            _session.Log = value;
            LogCreatedOnce();
            Log.LogLoggerAttached();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Marquee]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本コアのログ定義（<see cref="MarqueeLogMessage"/>）の接頭辞を設定する（本層は最下層のため伝搬先は無い）。</remarks>
    public string? LogPrefix
    {
        get => field;
        set
        {
            field = value;
            MarqueeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? MarqueeLogMessage.DefaultPrefix : value;
        }
    }

    // ====================================================================
    // 設定・環境値（設定・倍率・プロバイダ・座標変換）
    // ====================================================================

    /// <summary>現在の設定（寸法・対話パラメータ）を取得または設定する。</summary>
    /// <value>反映済みの設定。 既定は <see cref="MarqueeOptions.Default"/>。</value>
    /// <remarks>
    /// 設定すると対話パラメータをセッションへ伝搬し（境界外選択の禁止の即時クリップを含む）、再描画を要求する。<br/>
    /// 同値の設定を再適用しても効果は発生しない（判断はコアが単一所有する）。
    /// </remarks>
    public MarqueeOptions Options
    {
        get => _session.Options;
        set => Apply(_session.ApplyOptions(value));
    }

    /// <summary>ポート未指定時に適用する倍率を取得または設定する。</summary>
    /// <value>対象座標 / ローカル DIP の倍率。 既定は 1.0。 設定時は 0.1〜8.0 へ補正される（0 以下・等倍は等価変換として扱う）。</value>
    /// <remarks>
    /// 環境値（DPI・マルチスクリーン）のため <see cref="Options"/> には含めない。<br/>
    /// 座標変換ポート（<see cref="SetCoordTransform"/>）が設定されている場合は使われない（ポート優先。 規則はコアが単一所有する）。<br/>
    /// 選択矩形と十字マーカーの描画はセッションのポートのみを使うため本値の影響を受けない<br/>
    /// （本値が影響するのはサイズラベル・座標ラベルの位置と寸法）。
    /// </remarks>
    public double ClientToSourceScaling
    {
        get => _dipToSourceScaling;
        set
        {
            if (SetIfChanged(ref _dipToSourceScaling, CoerceClientToSourceScaling(value), out var previous))
            {
                Log.LogSettingChanged(nameof(ClientToSourceScaling), previous, _dipToSourceScaling);
            }
        }
    }

    /// <summary>ハイライトする矩形（対象座標）を取得または設定する。</summary>
    /// <value>ハイライト矩形。 null（既定）の場合はハイライトしない。</value>
    /// <remarks>
    /// ホストが与える実行時状態である（選択矩形と異なり選択コントロールの操作では変化しない）。<br/>
    /// 注目矩形の優先順（ハイライト → 選択）の規則はコアが単一所有する。<br/>
    /// 終点の加算の判断は本値の <see cref="EsRect.Interval"/> が持つため、検出結果の全ピクセルを含む矩形は<br/>
    /// <see cref="EsRectInterval.HalfOpen"/>（既定）のままでよく、<c>+1</c> を打ち消す指定は不要である。<br/>
    /// <b>最も頻度が高い設定</b>（ホバーのたびに変わりうる）ため、変更の記録は最も低いレベル（Trace）で行う。<br/>
    /// 変更は本コアが検出して出力する（同値判定を UI 層に置かない）。
    /// </remarks>
    public EsRect? HighlightSourceRect
    {
        get;
        set
        {
            if (SetIfChanged(ref field, value, out var previous))
            {
                Log.LogHighlightSourceRectChanged(previous, value);
            }
        }
    }

    // ====================================================================
    // 状態（選択の有無・選択矩形・スナップショット）
    // ====================================================================

    /// <summary>選択矩形があるかどうかを取得する。</summary>
    /// <value>選択中の場合は true。</value>
    public bool HasSelection => _session.HasSelection;

    /// <summary>現在の選択矩形（対象座標）を取得する。</summary>
    /// <value>選択矩形。 未選択時は 0x0 の空矩形。</value>
    /// <remarks>
    /// 区間の流儀（<see cref="EsRect.Interval"/>）は選択の作成元が決める（ドラッグ由来は <see cref="EsRectInterval.Closed"/>）。<br/>
    /// 本値をそのまま他の用途へ渡せば、終点の加算の判断も付いて回る（呼び出し側で再判定しない）。
    /// </remarks>
    public EsRect SelectionSourceRect => _session.SelectionSourceRect;

    /// <summary>現在の選択状態のスナップショット（ポーリング不要の完全状態）を生成する。</summary>
    /// <param name="mousePosition">スナップショットに含めるマウス位置（対象座標）。 null の場合は直近の位置。</param>
    /// <returns>状態・選択矩形・アクティブハンドル・マウス位置・操作フラグを含むスナップショット。</returns>
    public MarqueeSnapshot CreateSnapshot(EsPoint? mousePosition = null) => _session.CreateSnapshot(mousePosition);

    /// <summary>対象 ↔ ローカル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート。 null の場合は等価変換（1 対象ピクセル = 1 DIP・無制限）。</param>
    /// <remarks>
    /// ホストがモードに応じた変換ポートを設定する（範囲選択: 仮想スクリーンピクセル / 切出し: 元画像ピクセル）。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化を反映できる。<br/>
    /// 設定すると描画（選択矩形・ハンドル・十字線・外側の装飾）へ即時反映するため再描画を要求する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            var previous = _coordPort;
            _coordPort = port;
            _session.SetPort(port);
            _effects.RequestRedraw();
            Log.LogCoordTransformChanged(previous, port);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>ローカル DIP 座標を対象座標へ変換する。</summary>
    /// <param name="localDip">ローカル DIP 座標。</param>
    /// <returns>対象座標。</returns>
    public EsPoint ToSource(EsPoint localDip) => _session.ToSource(localDip);

    /// <summary>対象座標をローカル DIP 座標へ変換する。</summary>
    /// <param name="source">対象座標。</param>
    /// <returns>ローカル DIP 座標。</returns>
    public EsPoint ToDip(EsPoint source) => _session.ToDip(source);

    // ====================================================================
    // 入力の通知（UI 層 → コア）
    // ====================================================================

    /// <summary>ポインター押下（左ボタン）を通知する。</summary>
    /// <param name="position">押下位置（対象座標）。</param>
    /// <param name="clickCount">クリック数（2 以上 = ダブルクリック）。</param>
    /// <remarks>
    /// 入力の解釈（nudge の終了・ダブルクリックの確定/キャンセル・ハンドルのヒットテストとスナップ・新規選択の開始）は<br/>
    /// セッションが単一所有し、返された効果を本クラスが <see cref="IMarqueeEffects"/> 経由で UI へ適用する。
    /// </remarks>
    public void Press(EsPoint position, int clickCount) => Apply(_session.Press(position, clickCount));

    /// <summary>ポインター移動を通知する。</summary>
    /// <param name="position">マウス位置（対象座標）。</param>
    /// <remarks>ドラッグの適用・nudge 中のエコー移動の抑制・カーソル形状の決定はセッションが単一所有する。</remarks>
    public void Move(EsPoint position) => Apply(_session.Move(position));

    /// <summary>ポインター解放（左ボタン）を通知する。</summary>
    /// <param name="position">解放位置（対象座標）。</param>
    /// <remarks>
    /// 最小サイズによるキャンセル・確定・クリックの検出・nudge の終了はセッションが単一所有する。<br/>
    /// ドラッグの終了に伴うポインターキャプチャの解放もセッションが要求する（取得と解放が同じ経路になる）。
    /// </remarks>
    public void Release(EsPoint position) => Apply(_session.Release(position));

    /// <summary>キー入力を通知する（Escape のキャンセル・Enter の確定・矢印キーの微調整）。</summary>
    /// <param name="key">押下されたキー。</param>
    /// <param name="cursorPosition">押下時点のカーソル位置（対象座標）。 取得できない場合は null。</param>
    /// <returns>入力が処理された場合は true（UI 層は自 UI の「処理済み」フラグへ設定する）。</returns>
    /// <remarks>
    /// キーの解釈（nudge の許可条件を含む）はセッションが単一所有し、返された効果を本クラスが UI へ適用する。<br/>
    /// 確定・キャンセルは常に処理済みとして扱われる（UI 層へキーを流さない）。
    /// </remarks>
    public bool KeyDown(EsKey key, EsPoint? cursorPosition)
    {
        var feedback = _session.KeyDown(key, cursorPosition);
        Apply(feedback);
        return feedback.IsHandled;
    }

    /// <summary>nudge を終了する（カーソルを再表示する）。</summary>
    /// <remarks>nudge 中の状態破棄（フォーカス喪失）で使用する。</remarks>
    public void EndNudge() => Apply(_session.EndNudge());

    // ====================================================================
    // 外部（ホスト）からの操作
    // ====================================================================

    /// <summary>選択矩形を外部状態から復元する。</summary>
    /// <param name="rect">選択矩形（対象座標）。</param>
    /// <remarks>
    /// 境界外選択の禁止が有効な場合は、はみ出す矩形を境界内へクリップしてから設定する（判断はセッションが単一所有する）。<br/>
    /// 設定後は選択変更が通知され、再描画される。
    /// </remarks>
    public void SetSelection(EsRect rect) => Apply(_session.SetSelection(rect));

    /// <summary>選択状態と nudge 状態を初期化する（再描画される）。</summary>
    /// <remarks>
    /// nudge 中だった場合はカーソルを再表示する（非表示の残留を防ぐ。 判断はセッションが単一所有する）。<br/>
    /// 上位（UI 層）が対話を終了する場合の後片付けにも本メソッドを使う（「終了」自体は UI のライフサイクルであり、<br/>
    /// コアが必要とするのは「対話状態の初期化」だけである）。
    /// </remarks>
    public void ResetSelection() => Apply(_session.Reset());

    /// <summary>確定要求を処理する（Enter キー・ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <remarks>ドラッグ中は無視し、選択確定後のみ確定要求を通知する（確定後は選択状態をクリアする）。</remarks>
    public void RequestConfirm() => Apply(_session.RequestConfirm());

    /// <summary>キャンセル要求を処理する（Escape キー・枠外ダブルクリック・ホストからの API 呼び出しと同じ経路）。</summary>
    /// <remarks>ドラッグ中または選択ありの場合は選択を破棄した後にキャンセル要求を通知する。</remarks>
    public void RequestCancel() => Apply(_session.RequestCancel());

    // ====================================================================
    // 描画の幾何（1 フレーム分）
    // ====================================================================

    /// <summary>現在の状態から描画 1 フレーム分の入力を生成する。</summary>
    /// <param name="clientSize">描画領域のサイズ（DIP。 UI 実装の描画先のサイズ）。</param>
    /// <returns>描画の入力（設定・状態と、描画時に決まる値を解決済み）。</returns>
    /// <remarks>
    /// 設定・状態の読み出し（選択矩形・マーカー位置・マウス位置・ハイライト矩形）は本メソッドが単一所有するため、<br/>
    /// UI 層は描画領域のサイズを渡すだけでよい（内部状態の組み立て順を持たない）。<br/>
    /// 描画の幾何の解決と描画計画の解決が同じ入力を使うため、UI 層は本メソッドの結果を <see cref="ResolveGeometry"/> へ渡す。
    /// </remarks>
    public MarqueeRenderRequest CreateRequest(EsSize clientSize)
        => MarqueeRenderRequest.Create(
            _session.Options,
            clientSize,
            _session.HasSelection ? _session.SelectionSourceRect : null,
            _session.IsNudgeActive,
            _session.MarkerPosition,
            _session.LastMousePosition,
            _coordPort,
            _dipToSourceScaling,
            HighlightSourceRect);

    // ====================================================================
    // 内部ヘルパー
    // ====================================================================

    /// <summary>コアが返す効果を UI 層へ適用する。</summary>
    /// <param name="feedback">適用する効果（カーソル操作・フォーカス・カーソル形状・再描画・キャプチャ）。</param>
    /// <remarks>
    /// 適用の順序（カーソル操作 → フォーカス → カーソル形状 → 再描画）はコア（<see cref="MarqueeFeedback"/>）が単一所有する。<br/>
    /// カーソルサービス未指定（null）の場合はカーソル操作を適用しない（UI 初期化なしの検証を可能にする）。<br/>
    /// 効果はデリゲートではなく <see cref="IMarqueeEffects"/> へ直接適用する（入力イベントのたびにデリゲートを生成しない）。
    /// </remarks>
    private void Apply(in MarqueeFeedback feedback)
    {
        if (_cursor is { } cursor)
        {
            feedback.ApplyCursor(cursor, _effects);
        }

        feedback.ApplyVisual(_effects);

        if (feedback.Capture != MarqueeCaptureAction.None)
        {
            _effects.SetPointerCapture(feedback.Capture == MarqueeCaptureAction.Capture);
        }
    }

    /// <summary>生成時の設定を 1 度だけ記録する（既に記録済みの場合は何もしない）。</summary>
    /// <remarks>
    /// 生成時にロガーが注入されていない場合、出力先（<see cref="Log"/>）が未設定のためその場では記録できず、<br/>
    /// ロガーが設定された時点で生成時の設定を出力する（「どの設定で生成されたファサードからのログか」を追えるようにする。 記録は 1 回だけとする）。<br/>
    /// コンストラクターから注入された場合はその時点で既に記録済みであるため、本メソッドは何もしない。
    /// </remarks>
    private void LogCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        Log.LogFacadeCreated(_session.Options.HandleSize, _session.Options.MinSelectionSize);
    }

    /// <summary>新しい値を保持する（同値なら何もしない）。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">保持先のフィールド。</param>
    /// <param name="value">設定する値。</param>
    /// <param name="previous">変更前の値（同値の場合は現在値）。</param>
    /// <returns>値を変更した場合は true（同値だった場合は false）。</returns>
    /// <remarks>
    /// 「同値なら何もしない」という規則を単一所有する<br/>
    /// （各プロパティが同じ数行を書くと、片方だけ同値判定が抜けて無駄な再評価が溢れる）。<br/>
    /// 変更前の値も返すため、変更を検出した側（設定の変更ログ）が前後を比較できる。<br/>
    /// <b>再描画は要求しない</b>: 設定は UI の公開プロパティ（変更通知を持つ）から渡されるため、<br/>
    /// 描画の更新は UI が観測して行う（コアは入力に伴う再描画だけを効果として要求する）。
    /// </remarks>
    private static bool SetIfChanged<T>(ref T field, T value, out T previous)
    {
        previous = field;
        if (EqualityComparer<T>.Default.Equals(previous, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
