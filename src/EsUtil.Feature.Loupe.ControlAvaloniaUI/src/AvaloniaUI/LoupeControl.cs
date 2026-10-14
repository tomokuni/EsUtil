using Avalonia;
using EsUtil.Feature.Loupe.AvaloniaUI.Internal;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using EsUtil.Feature.Loupe.AvaloniaUI.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;


namespace EsUtil.Feature.Loupe.AvaloniaUI;

/// <summary>拡大鏡（ルーペ）コントロール。</summary>
/// <remarks>
/// 領域選択時とプレビュー画像切出し時の両方で<br/>
/// 共通利用するための単一のコントロール。<br/>
/// 入力はすべて StyledProperty（<see cref="Source"/> / <see cref="CenterPoint"/> / <see cref="Options"/>）として公開され、<br/>
/// バインディングまたはコードから制御できる。<br/>
/// <see cref="Source"/>（元画像）と <see cref="CenterPoint"/>（拡大中心の元画像ピクセル座標）を与えると、<br/>
/// 中心周辺領域を切り出して拡大描画する（Binding 方式）。<br/>
/// 表示は内部の <see cref="Image"/> 要素へ生成Bitmapを設定する方式に統一する。<br/>
/// <br/>
/// <b>描画の委譲</b>: 描画（抽出→拡大→十字線→枠線）の手順は UI 非依存の描画層（<c>EsUtil.Feature.Loupe.Presenter</c>）が<br/>
/// 本コントロールは状態（DP）とライフサイクル（ビットマップ生成/解放）に専念する。<br/>
/// <br/>
/// <b>再描画のバッチ化</b>: 複数プロパティの連続変更は <see cref="Dispatcher"/> へのポストにより<br/>
/// 1フレームに1回の再描画へ結合される（複数プロパティの多重再描画を防止）。<br/>
/// <b>表示状態</b>: 表示・非表示の指示は標準プロパティ <see cref="Visual.IsVisible"/> で行う。<br/>
/// 表示時（true）は最新の画像を非同期生成し、非表示時（false）はビットマップを解放する。<br/>
/// 非表示中のプロパティ変更では画像を生成しない。<br/>
/// <br/>
/// <b>基底クラス</b>: ルーペ画像とラベル要素を子として重ねるため <see cref="Panel"/> を採用する。<br/>
/// 子は親より後に描かれるため、後から子へ加えるラベル要素が常に最上位になる（<b>ラベルは常に最上位</b>の契約が構造で成立する）。<br/>
/// <br/>
/// <b>要素の描画順</b>: 図形（下地・拡大画像・十字線・枠線）は生成ビットマップへ焼き、<br/>
/// ラベル（中心座標）だけをテキスト要素として子へ置く（描画順はコアの <see cref="LoupeDrawLayer"/> が単一所有する）。<br/>
/// <br/>
/// <b>色指定</b>: 外観の色は UI 非依存の <see cref="System.Drawing.Color"/> として <see cref="Options"/> で受け取り、<br/>
/// 描画時にブラシへ変換する（色の文字列解析は行わない）。<br/>
/// 不透明度は色のアルファに統合する（例: 十字線の赤 40% = <c>#66FF0000</c>）。<br/>
/// 設定（<see cref="Options"/>）の数値項目はコアの型が生成時に安全域へ補正する<br/>
/// （UI 側に補正の登録を置かない）。 環境値の <see cref="ClientToPixelScaling"/> のみ本コントロールが補正（coerce）する。<br/>
/// <br/>
/// **公開 API 一覧**:<br/>
/// - 入力: <see cref="Source"/> / <see cref="CenterPoint"/><br/>
/// - 設定: <see cref="Options"/>（コアの <see cref="LoupeOptions"/> が単一所有。 表示サイズ・拡大率）<br/>
/// - 環境: <see cref="ClientToPixelScaling"/>（DPI スケーリング係数）<br/>
/// - 位置: <see cref="SetCoordTransform"/> / <see cref="PositionOffset"/> / <see cref="RefreshPosition"/><br/>
/// - ログ: <see cref="Log"/><br/>
/// - イベント: <see cref="Rendered"/>（描画完了時に描画済み Bitmap と表示情報を通知。 ホストが Bitmap に直接オーバーレイを描画できる）<br/>
/// - 操作: <see cref="Clear()"/>（非表示にしてビットマップを解放） / <see cref="RequestRender()"/>（プロパティ変更を伴わない再生成要求）<br/>
/// - 見た目のカスタマイズ: <see cref="Appearance"/>（色・太さ・表示可否） / <see cref="DrawPlanResolver"/>（矩形の並び・文言・位置） / <see cref="DrawPlan"/>（自前描画への既定描画の提供）
/// </remarks>
public sealed class LoupeControl : Panel
{
    // ====================================================================
    // 数値プロパティの安全域・既定値（LoupeCore.Limits から委譲）
    // ====================================================================

    /// <summary><see cref="LoupeControl"/> クラスの新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// 内容（ペーストボード・拡大画像・十字線・枠線）を描く子要素（<c>RenderLayer</c>）を生成し、初期状態は非表示にする。<br/>
    /// ラベル要素は配置フェーズ（<see cref="ArrangeOverride"/>）でラベルの有無に追従して追加・削除する。
    /// </remarks>
    public LoupeControl()
    {
        // 提示層（描画計画の窓口）を先に初期化する（以降のプロパティ設定で参照されるため）
        // ロガーは Log プロパティの setter が提示層へ伝搬する（生成時点では未設定のため引き継がない）
        _presenter = new LoupePresenter();

        // 内容を描く子要素を最初に加える（子は親より後に描かれるため、ラベル要素より下になる）
        _renderLayer = new RenderLayer(this);
        Children.Add(_renderLayer);

        // 初期状態は非表示（表示指示は IsVisible=true で行う）
        IsVisible = false;
    }

    // ====================================================================
    // コア（ファサード）と表示位置
    // ====================================================================

    /// <summary>提示層のファサード（設定・表示レイアウト・描画計画の窓口）。</summary>
    /// <remarks>
    /// 表示サイズ・位置・幾何・文言・層の順序はすべて <see cref="LoupePresenter"/> が単一所有する<br/>
    /// （本コントロールは自 UI の公開プロパティで受け取った値を転送し、返された描画計画を反映するだけ）。
    /// </remarks>
    private readonly LoupePresenter _presenter;

    /// <summary>表示位置（RenderTransform）のインスタンス（座標更新時は値を書き換えて再利用する）。</summary>
    private readonly TranslateTransform _positionTransform = new();

    /// <summary>ルーペの内容を描く子要素（窓の描画面へ直接描く）。</summary>
    private readonly RenderLayer _renderLayer;

    /// <summary>配置中のラベル要素（背景 + テキスト）。 子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

    /// <summary>再描画の要求を実行キューへポスト済みかどうか（true の間は追加要求を 1 回のポストへ結合する）。</summary>
    private bool _renderQueued;

    // ====================================================================
    // 見た目のカスタマイズ（描画計画の差し替えと既定描画の可否）
    // ====================================================================

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { CrosshairPixelRects = …, Labels = … }</c>）。<br/>
    /// <b>配置と描画の両方に適用される</b>ため、<see cref="LoupeDrawPlan.Labels"/> を差し替えるとラベル要素も追随する。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して再生成を要求する。
    /// </remarks>
    public Func<LoupeDrawPlan, LoupeDrawPlan>? DrawPlanResolver
    {
        get;
        set
        {
            field = value;
            RequestRender();
        }
    }
    /// <summary>描画計画を指定の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="context">描画先の描画コンテキスト（<c>Render</c> の実行中のみ有効）。 null 不可。</param>
    /// <param name="source">拡大元の画像（<see cref="Source"/> と同じ型）。 null の場合は拡大画像を描かない。</param>
    /// <param name="plan">描く描画計画。 null 不可。</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> が null の場合。</exception>
    /// <exception cref="NotSupportedException"><paramref name="source"/> が対応していない型の場合。</exception>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが任意のタイミングと順序で既定の描画を行える）。<br/>
    /// 元画像（<see cref="IImage"/>）から切り出し元への解決は本コントロールが内部で行う（ホストは元画像の型を知らなくてよい）。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <c>Content</c> を使う。
    /// </remarks>
    public static void DrawPlan(DrawingContext context, IImage? source, in LoupeDrawPlan plan)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 未対応型は例外にする（対応型の判定は LoupeSourceResolver が単一所有する）
        LoupeSourceResolver.EnsureSupported(source);

        var resolved = LoupeSourceResolver.Resolve(source);
        try
        {
            // 元の IImage は拡大元の全体を写しているため、そのまま元矩形つきの描画に使える
            // （切り出しの複製も表示用の変換も作らない。 寸法が一致しない場合は 0 コピーのビューから描く）
            LoupeContextPainter.Paint(context, in plan, resolved.Size, display: source, source: resolved.Bitmap);
        }
        finally
        {
            // 複製を返した場合のみ破棄する（表示アダプタからの借用は破棄しない）
            if (resolved.IsSourceBitmap)
            {
                resolved.Bitmap?.Dispose();
            }
        }
    }

    // ====================================================================
    // 拡大中心の取り直し（描画の直前）
    // ====================================================================

    /// <summary>描画の直前に拡大中心を取り直す関数を取得または設定する。</summary>
    /// <value>拡大中心（<see cref="CenterPoint"/> と同じ対象座標）を返す関数。 null を返すと現在の中心を使う。 null（既定）の場合は取り直さない。</value>
    /// <remarks>
    /// <b>ポインター移動イベントの座標は入力へ積まれた分だけ古い</b>（最新の移動がまだ届いていない）ため、<br/>
    /// ホストは本関数で実カーソルから中心を取り直せる（本コントロールは再描画のジョブの先頭で本関数を呼ぶ）。<br/>
    /// <b>返された中心は <see cref="CenterPoint"/> へ反映する</b>（中心座標ラベル・表示位置・拡大画像を同じ中心で揃えるため）。<br/>
    /// 中心を反映しても再描画の要求は増えない（未実行の要求は 1 回へ結合されている）。<br/>
    /// 取り直しの可否（ポインターを追従している最中かどうか）はホストが判断する（本コントロールは理由を知らない）。<br/>
    /// 実カーソルの取得と対象座標への変換もホストの責務である。
    /// </remarks>
    public Func<Point?>? CenterPointProvider { get; set; }

    /// <summary>描画の直前に拡大中心を <see cref="CenterPointProvider"/> から取り直す。</summary>
    /// <remarks>
    /// 返された中心は <see cref="CenterPoint"/> へ反映する（同じ値の場合は何も起きない）。<br/>
    /// <b>要求の結合フラグが下りる前に呼ぶ</b>（中心の変更で再描画が二重に要求されないようにする）。<br/>
    /// 反映は描画（<see cref="RenderLayer.Render"/>）より前に行われるため、中心座標ラベル・表示位置・拡大画像が<br/>
    /// 同じフレームで同じ中心になる。
    /// </remarks>
    private void RefreshCenterPoint()
    {
        if (CenterPointProvider?.Invoke() is not { } center)
        {
            return;
        }

        CenterPoint = center;
    }

    /// <summary>ルーペ中心（<see cref="CenterPoint"/>）からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（ルーペ中心）からの右下方向へのオフセット量（DIP）。</value>
    /// <remarks>
    /// ホスト（上位 AP）が設定する。 設定後の位置反映は <see cref="UpdatePosition"/> で行う（保持はコアが単一所有する）。<br/>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。
    /// </remarks>
    public double PositionOffset
    {
        get;
        set
        {
            field = value;
            _presenter.PositionOffset = value;
            UpdatePosition();
        }
    }

    /// <summary>論理座標（対象）↔ 親パネル DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（ToDip = 論理→親パネルDIP）。 null の場合は等価変換。</param>
    /// <remarks>
    /// 対になるコントロールと同様に、外部から与えたポートで表示位置（<see cref="Visual.RenderTransform"/>）と<br/>
    /// 表示サイズ（要求サイズ）と表示位置（オフセット・領域内への補正）を計算する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等の<br/>
    /// レイアウト変化に追従できる（イベント時の DIP キャッシュは使わない）。<br/>
    /// ポートの保持と、未設定（null）を等価変換として扱う規則はコア（ファサード）が単一所有する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _presenter.SetCoordTransform(port);
            UpdatePosition();
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>表示位置と表示サイズを最新のレイアウトから再計算する（外部からの更新要求）。</summary>
    /// <remarks>
    /// パン・ズーム等のレイアウト非変更なビューポート変化時に、ホスト（上位 AP）が呼び出す。<br/>
    /// 座標変換ポートは呼び出し毎に最新の表示状態を評価するため、再計算だけで追従する。
    /// </remarks>
    public void RefreshPosition() => UpdatePosition();

    /// <summary>表示位置と表示サイズを最新のレイアウトから再計算する。</summary>
    /// <remarks>
    /// 表示サイズ・位置の計算（要求サイズと、オフセット・親領域内への位置の補正）は<br/>
    /// コア（<see cref="LoupePresenter.ResolveLayout"/>）が単一所有する（未確定の扱いもコアが判断する）。<br/>
    /// 呼び出しタイミング: <see cref="CenterPoint"/> 変更・ポート設定・<see cref="PositionOffset"/> 変更・<see cref="Options"/> 変更・レイアウト（Arrange）。<br/>
    /// 要素サイズ（Width / Height）も同じ値から設定する（描画ビットマップと必ず一致する）。
    /// </remarks>
    private void UpdatePosition()
    {
        // 親領域未接続（配置前・テスト等）は位置の補正基準が無いため補正されない（規則はコアが単一所有する）
        var layout = _presenter.ResolveLayout(ParentAreaSize);

        // 表示サイズは要素サイズ・描画サイズ（物理ピクセルへの換算元）で同じ値を使う
        // （別々に求めると丸めの違いで要素と描画ビットマップの大きさが食い違う）
        Width = layout.ClientSize;
        Height = layout.ClientSize;

        // 配置は親が接続され表示中の場合のみ行う（拡大中心が未設定か、親領域が表示サイズを収められない場合はコアが位置を返さない）
        if (Parent is not Panel || !IsVisible || layout.Position is not { } position)
        {
            return;
        }

        // 同一インスタンスの値を更新して再利用する（毎回の生成を回避。 StyledProperty 変更で再適用される）
        _positionTransform.X = position.X;
        _positionTransform.Y = position.Y;
        RenderTransform = _positionTransform;
    }

    /// <summary>レイアウト（Arrange）時に表示位置・サイズを再計算する（ウィンドウサイズ変更等に追従）。</summary>
    /// <param name="finalRect">最終レイアウト矩形。</param>
    protected override void ArrangeCore(Rect finalRect)
    {
        base.ArrangeCore(finalRect);
        UpdatePosition();
    }

    // ====================================================================
    // ラベル（テキスト要素として配置する）
    // ====================================================================

    /// <summary>子要素（画像・ラベル）の配置を確定する。</summary>
    /// <param name="finalSize">本コントロールへ割り当てられたサイズ（DIP）。</param>
    /// <returns>割り当てサイズ。</returns>
    /// <remarks>
    /// <b>位置はコアと描画層が決める</b>: コアのテキスト配置から <c>LoupePresenter</c> が求めた配置指示に従って<br/>
    /// ラベル要素を配置するだけであり、本クラスは位置の計算を持たない。 要素は <see cref="Panel.Children"/> の末尾へ<br/>
    /// 置かれるため常に最上位になる。 実寸は自 UI の描画 API（<see cref="FormattedText"/>）で測って渡す。
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        // テキストは描画層が文言と基準点を渡し、要素として置くか画像として描くかは本層が決める
        // （実寸は要素の大きさ（親から割り当てられたサイズ）を基準に測る）
        var plan = ResolvePlan(finalSize.Width);
        SyncLabels(plan.Labels);

        return base.ArrangeOverride(finalSize);
    }

    // ====================================================================
    // 1 フレーム分の描画計画（1 フレーム = 1 回だけ解決する）
    // ====================================================================

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private LoupeDrawPlan? _plan;

    /// <summary>直近に解決した描画計画の基準（表示サイズ（DIP））。</summary>
    private double _planSize;

    /// <summary>直近に解決した描画計画の基準（スケーリング係数）。</summary>
    private double _planScaling;

    /// <summary>描画計画を解決し、<see cref="DrawPlanResolver"/> を適用する（同じ入力では再解決しない）。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP）。</param>
    /// <returns>差し替え後の描画計画。</returns>
    /// <remarks>
    /// <b>配置と描画の両経路が本メソッドを通る</b>（配置は <see cref="ArrangeOverride"/>、描画は <see cref="DrawLoupe"/>）。<br/>
    /// どちらの経路も同じ表示サイズを渡すため（配置は割り当てサイズ、描画は <see cref="PositionOffset"/> と同じくコアのレイアウトが返す値）、<br/>
    /// <b>同じ入力ではキャッシュを返して 1 フレーム 1 回の解決に留める</b>（解決のたびに十字線・枠線の矩形とラベルの実寸測定をやり直さない）。<br/>
    /// キャッシュのキーは「表示サイズ」と「スケーリング係数」である（後者は計画の物理ピクセルへ効く入力のため）。<br/>
    /// それ以外の入力（拡大中心・設定・外観・<see cref="DrawPlanResolver"/>）の変更は <see cref="RequestRender"/> が<br/>
    /// <see cref="InvalidatePlan"/> で破棄する（同じ計画を保持し続けない）。
    /// </remarks>
    private LoupeDrawPlan ResolvePlan(double displaySize)
    {
        var scaling = _presenter.ClientToPixelScaling;
        if (_plan is not { } cached || _planSize != displaySize || _planScaling != scaling)
        {
            cached = _presenter.ResolveDrawPlan(displaySize, MeasureText);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _plan = cached;
            _planSize = displaySize;
            _planScaling = scaling;
        }

        return cached;
    }

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidatePlan() => _plan = null;

    /// <summary>ラベル要素（背景 + テキスト）を配置指示へ同期する。</summary>
    /// <param name="placements">配置するラベル（描画順に並ぶ）。</param>
    /// <remarks>
    /// 余分な要素は親から外し、不足分は生成して子の末尾へ追加する（配置のたびに呼ばれる）。<br/>
    /// 各要素は左上寄せと <c>Margin</c> で絶対配置する（配置矩形はコアと描画層が単一所有する）。
    /// </remarks>
    private void SyncLabels(IReadOnlyList<LoupeLabelPlacement> placements)
    {
        while (_labels.Count > placements.Count)
        {
            var last = _labels[^1];
            _labels.RemoveAt(_labels.Count - 1);
            Children.Remove(last);
        }

        while (_labels.Count < placements.Count)
        {
            var border = new Border { Child = new TextBlock { TextWrapping = TextWrapping.NoWrap } };
            _labels.Add(border);
            Children.Add(border);
        }

        for (var i = 0; i < placements.Count; i++)
        {
            Apply(_labels[i], placements[i]);
        }
    }

    /// <summary>1 つのラベル要素の内容と配置を配置指示に合わせる。</summary>
    /// <param name="border">更新する要素（背景）。</param>
    /// <param name="placement">配置指示（寸法は実寸を反映済み）。</param>
    /// <remarks>
    /// <b>表示可否は本層が反映する</b>: 計画は表示可否（外観の <c>ShowCoordLabel</c>）でも指示を消さないため、<br/>
    /// 要素として置くかどうかを要素の <see cref="Visual.IsVisible"/> で表す。
    /// </remarks>
    private static void Apply(Border border, in LoupeLabelPlacement placement)
    {
        border.IsVisible = placement.IsVisible;
        border.Background = SolidColorBrushCache.GetOrCreate(placement.BackgroundColor);
        border.CornerRadius = new CornerRadius(placement.CornerRadius);
        border.Padding = new Thickness(placement.PaddingX, placement.PaddingY);
        border.Width = placement.DipRect.Width;
        border.Height = placement.DipRect.Height;
        border.Margin = new Thickness(placement.DipRect.X, placement.DipRect.Y, 0, 0);
        border.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        border.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;

        if (border.Child is TextBlock text)
        {
            text.Text = placement.Text;
            text.FontSize = placement.FontSize;
            text.Foreground = SolidColorBrushCache.GetOrCreate(placement.TextColor);
        }
    }

    /// <summary>テキストの実寸を測定する（描画 API でしか測れない値）。</summary>
    /// <param name="text">測定するテキスト。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>テキストの実寸（DIP）。</returns>
    /// <remarks>測定の実装は <see cref="AvaloniaTextMeasure"/> が単一所有する（UI ごとの実寸の食い違いを防ぐ）。</remarks>
    private static EsSize MeasureText(string text, double fontSize) => AvaloniaTextMeasure.Measure(text, fontSize);

    /// <summary>入力（Source / CenterPoint / 設定）の変更で再描画を要求する。</summary>
    /// <remarks>
    /// 各プロパティの変更は <see cref="RequestRender"/> を呼び出し、<br/>
    /// 複数プロパティの連続変更を1回の再描画に結合する（バッチ化）。<br/>
    /// 設定（<see cref="LoupeOptions"/>）の変更は表示サイズ・外観も変わるため、表示サイズ・位置の再計算と<br/>
    /// 座標ラベルの更新も要求する（変更された項目の判定はコアの変更ログが単一所有する）。<br/>
    /// </remarks>
    static LoupeControl()
    {
        SourceProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o.Log.LogSourceChanged(e.OldValue as IImage, e.NewValue as IImage);
            o.RequestRender();
        });
        // CenterPoint 変更時は再描画に加えて表示位置（RenderTransform）も更新する（コアが座標変換ポートと親パネルサイズから算出する）
        CenterPointProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o._presenter.CenterPoint = ToEsPoint(e.NewValue as Point?);
            o.RequestRender();
            o.UpdatePosition();
            // 中心座標ラベルの文言は中心に依存するため、ラベル要素を配置し直す
            o.InvalidateArrange();
        });
        // 設定（LoupeOptions）は表示サイズ・拡大率をまとめて持つため、要素サイズと描画へ波及する
        OptionsProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o._presenter.Options = e.GetNewValue<LoupeOptions>();
            o.RequestRender();
            o.UpdatePosition();
            // 表示サイズとラベルの外観が変わるため、ラベル要素を配置し直す（サイズが同じでも必要）
            o.InvalidateArrange();
        });
        ClientToPixelScalingProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            // OldValue / NewValue は StyledProperty&lt;double&gt; により常に非 null（boxed double）
            o._presenter.ClientToPixelScaling = e.GetNewValue<double>();
            o.RequestRender();
            // ラベルの配置の基準（表示サイズ）が物理ピクセル換算で変わるため、配置し直す
            o.InvalidateArrange();
        });

        IsVisibleProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) => o.OnIsVisibleChanged(e));
        // 外観（色・フォント・角丸・枠線の太さ・表示可否）の変更は描画内容とラベル体裁へ波及する
        AppearanceProperty.Changed.AddClassHandler<LoupeControl>(static (o, e) =>
        {
            o._presenter.Appearance = e.GetNewValue<LoupeAppearance>();
            o.RequestRender();
            // 中心座標ラベルの色・体裁も外観が与えるため、ラベル要素を配置し直す
            o.InvalidateArrange();
        });
    }

    // ====================================================================
    // 依存関係プロパティ
    // ====================================================================

    /// <summary><see cref="Source"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<LoupeControl, IImage?>(nameof(Source));

    /// <summary><see cref="CenterPoint"/> 依存関係プロパティ。</summary>
    public static readonly StyledProperty<Point?> CenterPointProperty =
        AvaloniaProperty.Register<LoupeControl, Point?>(nameof(CenterPoint));

    /// <summary>表示DIPを物理ピクセルへ変換するスケーリング係数。</summary>
    /// <remarks>
    /// 環境値（DPI）のため設定（<see cref="LoupeOptions"/>）には含めず、補正の規則はコアの <see cref="EsUtil.Feature.Loupe.LoupeCore.Limits"/> が単一所有する。
    /// </remarks>
    public static readonly StyledProperty<double> ClientToPixelScalingProperty =
        AvaloniaProperty.Register<LoupeControl, double>(
            nameof(ClientToPixelScaling), defaultValue: ClientToPixelScalingDefault, coerce: static (_, value) => CoerceClientToPixelScaling(value));

    /// <summary><see cref="Options"/> 依存関係プロパティ（表示サイズ・拡大率。 コアの <see cref="LoupeOptions"/> が単一所有）。</summary>
    /// <remarks>
    /// 個別に公開していた設定（表示サイズ・拡大率）を 1 つの不変オブジェクトへ統合し、公開プロパティを 1 つに縮小する。<br/>
    /// 設定値の既定値と数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有するため、<br/>
    /// 本コントロールは既定値を持たず、補正の登録も行わない（どの設定経路でも範囲内の値だけが保持される）。
    /// </remarks>
    public static readonly StyledProperty<LoupeOptions> OptionsProperty =
        AvaloniaProperty.Register<LoupeControl, LoupeOptions>(nameof(Options), defaultValue: LoupeOptions.Default);

    /// <summary><see cref="Appearance"/> 依存関係プロパティ（外観。 描画にだけ必要な設定）。</summary>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="LoupeOptions"/>）には含めず、<br/>
    /// 描画の設定（<see cref="LoupeAppearance"/>）として本コントロールが公開する（描画層が単一所有する）。
    /// </remarks>
    public static readonly StyledProperty<LoupeAppearance> AppearanceProperty =
        AvaloniaProperty.Register<LoupeControl, LoupeAppearance>(nameof(Appearance), defaultValue: LoupeAppearance.Default);

    // ====================================================================
    // 公開プロパティ（入力）
    // ====================================================================

    /// <summary>拡大元の元画像（100%画像）。 Binding で与える。</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）。 Binding で与える。</summary>
    public Point? CenterPoint
    {
        get => GetValue(CenterPointProperty);
        set => SetValue(CenterPointProperty, value);
    }

    // ====================================================================
    // 公開プロパティ（設定・環境）
    // ====================================================================

    /// <summary>レイアウト（位置の補正）の基準になる親パネルのサイズ。</summary>
    /// <value>親パネルのサイズ（DIP）。 親が未接続（配置前・テスト等）の場合は 0x0（補正しない）。</value>
    /// <remarks>親のサイズが確定しているかの判定と位置の補正はコア（<see cref="LoupePresenter.ResolveLayout"/>）が単一所有する。</remarks>
    private EsSize ParentAreaSize => (Parent as Panel)?.Bounds.Size.ToEsSize() ?? default;

    /// <summary>ルーペの設定（表示サイズ・拡大率）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定はコアの既定値（<see cref="LoupeOptions.Default"/>）。</value>
    /// <remarks>
    /// 設定の値・既定値・数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有する（本コントロールは保持しない）。<br/>
    /// 一部の項目だけを変える場合は <c>LoupeOptions.Default with { ZoomLevel = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 表示サイズは本値（要求サイズ）がそのまま使われる（領域に合わせた縮小はしない）。<br/>
    /// 変更すると再描画・表示位置の再計算・座標ラベルの更新が行われる。<br/>
    /// 色は UI 非依存の <see cref="System.Drawing.Color"/> で受け取り、描画時に <see cref="Brush"/> へ変換する。
    /// </remarks>
    public LoupeOptions Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>外観（色・フォント・角丸・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="LoupeOptions"/>）ではなく本プロパティが単一所有する。<br/>
    /// 変更すると再生成とラベル要素の再配置が行われる。
    /// </remarks>
    public LoupeAppearance Appearance
    {
        get => GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>表示DIPを物理ピクセルへ変換するスケーリング係数を取得または設定します。</summary>
    public double ClientToPixelScaling
    {
        get => GetValue(ClientToPixelScalingProperty);
        set => SetValue(ClientToPixelScalingProperty, value);
    }

    /// <summary>ログ用に Avalonia 座標をコア座標へ変換する。</summary>
    /// <param name="point">変換する座標。 未設定は null。</param>
    /// <returns>コア座標。 未設定は null。</returns>
    /// <remarks>値の書式（<c>(X, Y)</c>）はコア（<c>LoupeLogMessage.LogCenterPointChanged</c>）が単一所有する。</remarks>
    private static EsPoint? ToEsPoint(Point? point) => point?.ToEsPoint();

    // ====================================================================
    // 公開メソッド（操作）
    // ====================================================================

    /// <summary>ルーペを非表示にする（表示の解除のみ。 解放する中間ビットマップは無い）。</summary>
    /// <remarks>
    /// <see cref="Visual.IsVisible"/> を false に設定する（既に非表示の場合は何も起きない。 冪等）。<br/>
    /// 拡大元画像（<see cref="Source"/>）はホストが所有するため、破棄の判断もホストが行う。
    /// </remarks>
    public void Clear() => IsVisible = false;

    /// <summary>現在の状態からルーペの再描画を要求する（プロパティ変更を伴わない再描画要求）。</summary>
    /// <remarks>
    /// ホストが <see cref="Rendered"/> で付加情報（線分の破線等）を描く場合、重ねるデータの変化<br/>
    /// （一覧の選択・外部からの状態変更等）ではプロパティが変わらないため、本メソッドで再描画を要求する。<br/>
    /// 描画は次のフレームで行われ、完了は <see cref="Rendered"/> で通知される（戻り値を待つ必要はない）。<br/>
    /// 複数の要求は 1 フレーム 1 回の描画へ結合される（呼び出し頻度を気にせず呼べる）。<br/>
    /// 非表示中は描画を行わない（状態は保持し、次の表示で反映される）。<br/>
    /// <b>保持している描画計画も破棄する</b>（本メソッドは「計画に効く入力が変わった」ことを表す唯一の入口である。 <see cref="ResolvePlan"/>）。
    /// </remarks>
    public void RequestRender()
    {
        // 非表示中でも計画を破棄する（破棄しないと非表示中に変えた状態が次の表示で反映されない）
        InvalidatePlan();
        QueueRender();
    }

    /// <summary>描画面の再描画を要求する（実行キューへのポストを 1 回へ結合する）。</summary>
    /// <remarks>
    /// <b>ポストは 1 回だけ</b>である（このジョブの中で拡大中心の取り直し（<see cref="RefreshCenterPoint"/>）と<br/>
    /// 描画面の無効化（<c>RenderLayer.InvalidateVisual()</c>）を行う。 描画そのものはフレームの描画フェーズで 1 回行われる）。<br/>
    /// <b>Render 優先度を使う理由</b>: マウス移動中の連続再描画では、既定の Normal 優先度のポストが<br/>
    /// 入力イベント（Input 優先度）に後回しにされ、反映が移動停止まで実行されない問題を防ぐ。<br/>
    /// 中心の取り直しは結合フラグ（<see cref="_renderQueued"/>）が下りる前に走らせる<br/>
    /// （中心の変更が同じフレームの再描画要求として二重にポストされないようにする）。
    /// </remarks>
    private void QueueRender()
    {
        if (_renderQueued)
        {
            return;
        }

        _renderQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                RefreshCenterPoint();
                _renderQueued = false;
                _renderLayer.InvalidateVisual();
            },
            DispatcherPriority.Render);
    }

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（描画エラーの記録と UI 固有のログに使う）。</summary>
    /// <value>注入されたロガー。 null 可（null の場合は <see cref="NullLogger"/> へフォールバックする）。</value>
    /// <remarks>設定・環境値・拡大中心の変更ログはコアが出力するため、同じロガーをコアへも渡す。</remarks>
    public ILogger Log
    {
        get => _log ?? NullLogger.Instance;
        set
        {
            _log = value;
            _presenter.Log = value;
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Loupe]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>
    /// 本コントロールのログ定義（<see cref="LoupeLogMessage"/>）の接頭辞を設定し、<br/>
    /// 提示層・コアへ伝搬する（集約した接頭辞で全層のログを揃える）。
    /// </remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            LoupeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? LoupeLogMessage.DefaultPrefix : value;
            _presenter.LogPrefix = value;
        }
    }

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>内部ロガー（未設定は null。 読み取り時に <c>?? NullLogger.Instance</c> で吸収する）。</summary>
    private ILogger? _log;

    // ====================================================================
    // イベント
    // ====================================================================

    /// <summary>ルーペの描画完了を通知するイベント（描画先の描画コンテキストと描画計画を渡す）。</summary>
    /// <remarks>
    /// ルーペの内容（ペーストボード・拡大画像・十字線・枠線）を描いた直後に、同じ描画コンテキストを通知する。<br/>
    /// ホスト側はイベントハンドラ内で付加情報（選択矩形の破線・検出した線分等）を<b>描画 API で</b>描ける<br/>
    /// （描いた内容はルーペの内容の上・中心座標ラベル（テキスト要素）の下に置かれる）。<br/>
    /// <b>描画コンテキストは描画中のみ有効</b>である（ハンドラ内で描画を完了させること。 保持しない）。<br/>
    /// <b>中心座標ラベルは本コントロールがテキスト要素として表示する</b>ため、通知に含めない。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary><see cref="Rendered"/> イベントを発火する（描画先と描画計画を通知）。</summary>
    /// <param name="context">ルーペの内容を描いた描画コンテキスト（描画中のみ有効）。</param>
    /// <param name="plan">実際に描画した描画計画（切り出し領域の左上と拡大率を持つ）。</param>
    /// <remarks>
    /// 切り出し領域の左上・拡大率は描画計画（<see cref="LoupeDrawPlan.CaptureTopLeft"/> / <see cref="LoupeDrawPlan.ZoomLevel"/>）が単一所有する。<br/>
    /// 中心未設定時は <see cref="LoupeDrawPlan.CaptureTopLeft"/> が null になる（付加情報の配置基準が無い）。<br/>
    /// <b>中心座標ラベルは本コントロールがテキスト要素として表示する</b>ため、通知に含めない。
    /// </remarks>
    private void RaiseRendered(DrawingContext context, in LoupeDrawPlan plan)
    {
        Rendered?.Invoke(this, new LoupeRenderedEventArgs(context, plan));
    }

    // ====================================================================
    // 表示制御（IsVisible による表示・非表示の指示）
    // ====================================================================

    /// <summary>表示状態（<see cref="Visual.IsVisible"/>）が変更された時の処理。</summary>
    /// <param name="e">変更イベント引数。</param>
    /// <remarks>
    /// 表示・非表示の指示は標準プロパティ <see cref="Visual.IsVisible"/> に一元化する。<br/>
    /// - 表示時: 最新の <see cref="Source"/> / <see cref="CenterPoint"/> で再描画を要求し、表示位置を更新する。<br/>
    /// - 非表示時: 保留中の再描画要求を破棄する（解放する中間ビットマップは無い）。<br/>
    /// 非表示中も状態（拡大中心・設定・ソース）は保持するため、表示時に最新の状態で描かれる。
    /// </remarks>
    private void OnIsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            RequestRender();
            UpdatePosition();
            return;
        }

        // 保留中の要求を破棄する（非表示のまま描画フェーズを迎えても描かれない）
        _renderQueued = false;
    }

    // ====================================================================
    // 描画（描画面へ直接描く）
    // ====================================================================

    /// <summary>ルーペの内容を描画面へ描き、描画完了を通知する。</summary>
    /// <param name="context">描画先の描画コンテキスト（描画中のみ有効）。</param>
    /// <remarks>
    /// <b>中間ビットマップを作らない</b>: 描画計画をそのまま窓の描画面へ写す（MewUI 版と同じ描画モデル）。<br/>
    /// 切り出し元の解決は <see cref="LoupeSourceResolver.Resolve"/> が単一所有し、表示用の射影をそのまま使える場合は<br/>
    /// 複製を作らない（<see cref="LoupeContextPainter.Paint"/>）。<br/>
    /// <see cref="Source"/> / <see cref="CenterPoint"/> のいずれかが未設定、または切り出せない場合は<br/>
    /// 下地（ペーストボード）のみを描く（判定は描画層が単一所有する）。<br/>
    /// 描画の例外はアプリを落とさず <see cref="LoupeLogMessage.LogUpdateError"/> で記録する（そのフレームは下地までで終える）。
    /// </remarks>
    private void DrawLoupe(DrawingContext context)
    {
        // 描画計画（描画層が解決した幾何・外観・層の順序）は現在のレイアウト（要求サイズ）から組み立てる
        // （値・単位・換算は描画層が単一所有する。 開始時に固定しない）
        var plan = ResolvePlan(_presenter.ResolveLayout(ParentAreaSize).ClientSize);

        // 計画の座標は描画面の物理ピクセル、コンテキストの座標は DIP である
        // （DPI が 100% 以外では一致しないため、ピクセル → DIP の倍率を適用する）
        var pixelToDip = 1.0 / plan.ClientToPixelScaling;

        // 切り出し元を解決する（未対応型は例外。 例外の記録は本メソッドが行う）
        var resolved = default(LoupeSourceResolver.Result);
        try
        {
            LoupeSourceResolver.EnsureSupported(Source);
            resolved = LoupeSourceResolver.Resolve(Source);

            using (context.PushTransform(Matrix.CreateScale(pixelToDip, pixelToDip)))
            {
                // 元の IImage は拡大元の全体を写しているため、そのまま元矩形つきの描画に使える
                // （切り出しの複製も表示用の変換も作らない）
                LoupeContextPainter.Paint(context, in plan, resolved.Size, display: Source, source: resolved.Bitmap);

                // ホストが付加情報（選択矩形の破線等）を描くための入口（ルーペの内容の上へ重ねる）
                RaiseRendered(context, in plan);
            }
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 描画の例外は記録して、そのフレームは下地までで終える（アプリを落とさない）
            Log.LogUpdateError(ex);
        }
        finally
        {
            // 複製を返した場合のみ破棄する（表示アダプタからの借用は破棄しない）
            if (resolved.IsSourceBitmap)
            {
                resolved.Bitmap?.Dispose();
            }
        }
    }

    /// <summary>ルーペの内容を描く子要素（窓の描画面へ直接描く）。</summary>
    /// <remarks>
    /// 親（<see cref="LoupeControl"/>）は <see cref="Panel"/> であり <c>Render</c> をシールするため、描画は子が担う。<br/>
    /// 子は<b>自分の描画リスト</b>を持つため、内容が変わるたびに本インスタンスを無効化する必要がある<br/>
    /// （要求の集約は <see cref="QueueRender"/> が単一所有する）。<br/>
    /// ラベル要素（さらに後から追加する子）は本要素より後に描かれるため、常に本要素の内容の上になる。<br/>
    /// <b>切り取り（<see cref="Visual.ClipToBounds"/>）を有効にする</b>: 拡大画像・装飾・ホストの付加情報が<br/>
    /// ルーペの矩形の外へ描かれないようにする（中間ビットマップを持たないため、描画の範囲は本要素の矩形が決める）。<br/>
    /// 入力は親が受けるため、ヒットテストの対象からは外す。
    /// </remarks>
    private sealed class RenderLayer : Control
    {
        /// <summary>描画内容の取得元（親コントロール）。</summary>
        private readonly LoupeControl _owner;

        /// <summary>描画先の親を指定して初期化する。</summary>
        /// <param name="owner">描画内容の取得元（親コントロール）。</param>
        public RenderLayer(LoupeControl owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
            ClipToBounds = true;
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context) => _owner.DrawLoupe(context);
    }
}
