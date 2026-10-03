using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using EsUtil.Feature.Loupe.AvaloniaUI.Logging;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Imaging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using EsUtil.SaveEvidence.Platform.AvaloniaUI.Interop;


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
/// <b>描画の委譲</b>: 描画（抽出→拡大→十字線→市松→枠線）の手順は UI 非依存の描画層（<c>EsUtil.Feature.Loupe.Presenter</c>）が<br/>
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
/// <b>要素の描画順</b>: 図形（下地・市松模様・拡大画像・十字線・枠線）は生成ビットマップへ焼き、<br/>
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
/// </remarks>
public sealed class LoupeControl : Panel
{
    // ====================================================================
    // 数値プロパティの安全域・既定値（LoupeCore.Limits から委譲）
    // ====================================================================

    /// <summary><see cref="LoupeControl"/> クラスの新しいインスタンスを初期化する。</summary>
    /// <remarks>
    /// 子要素（ルーペ画像を表示する <see cref="Image"/>）を生成し、初期状態を非表示にする。<br/>
    /// ラベル要素は配置フェーズ（<see cref="ArrangeOverride"/>）でラベルの有無に追従して追加・削除する。
    /// </remarks>
    public LoupeControl()
    {
        // コーディネーターを先に初期化する（以降のプロパティ設定で参照されるため）
        _renderCoordinator = CreateRenderCoordinator();

        // ルーペ画像を表示する子要素を最初に加える（子は親より後に描かれるため、ラベル要素より下になる）
        _image = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        Children.Add(_image);

        // 初期状態は非表示（表示指示は IsVisible=true で行う）
        IsVisible = false;
    }

    /// <summary>再描画コーディネーターを、現在の表示状態・ロガー・解放処理を注入して生成する。</summary>
    /// <remarks>
    /// ログの有効・無効はロガー自身が判定する（<see cref="NullLogger"/> では生成コードが早期に戻る）ため、<br/>
    /// 呼び出し側に「ログを記録するか」の判断を持たせない。<br/>
    /// 実行キューへのポストは本 UI が指定する（描画フェーズ（Render 優先度）で実行させる）。<br/>
    /// <b>Render 優先度を使う理由</b>: マウス移動中の連続再描画では、既定の Normal 優先度のポストが<br/>
    /// 入力イベント（Input 優先度）に後回しにされ、描画反映が移動停止まで実行されない問題を防ぐ。<br/>
    /// ポストの注入により、コーディネーター自体は UI フレームワークに依存しない。
    /// </remarks>
    private LoupeRenderCoordinator CreateRenderCoordinator()
        => new(
            isVisible: () => IsVisible,
            releaseBitmap: ReleaseBitmap,
            startRender: StartRender,
            logRenderCore: generation => Log.LogRenderCore(generation),
            post: action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));

    // ====================================================================
    // コア（ファサード）と表示位置
    // ====================================================================

    /// <summary>提示層のファサード（設定・表示レイアウト・描画計画の窓口）。</summary>
    /// <remarks>
    /// 表示サイズ・位置・幾何・文言・層の順序はすべて <see cref="LoupePresenter"/> が単一所有する<br/>
    /// （本コントロールは自 UI の公開プロパティで受け取った値を転送し、返された描画計画を反映するだけ）。<br/>
    /// フィールド初期化子で生成する（AXAML の初期化でプロパティ変更が発火しても参照できるようにするため）。
    /// </remarks>
    private readonly LoupePresenter _presenter = new();

    /// <summary>表示位置（RenderTransform）のインスタンス（座標更新時は値を書き換えて再利用する）。</summary>
    private readonly TranslateTransform _positionTransform = new();

    /// <summary>ルーペ画像を表示する子要素（生成ビットマップを <see cref="Image.Source"/> へ設定する）。</summary>
    private readonly Image _image;

    /// <summary>配置中のラベル要素（背景 + テキスト）。 子の末尾側へ置くため常に最上位になる。</summary>
    private readonly List<Border> _labels = [];

    /// <summary>再描画のバッチ化コーディネーター（Dispatcher へのポストで 1 フレームに 1 回に結合）。</summary>
    private LoupeRenderCoordinator _renderCoordinator;

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
    /// MarqueeControl と同様に、外部から与えたポートで表示位置（<see cref="Visual.RenderTransform"/>）と<br/>
    /// 表示サイズ（要求サイズ）と表示位置（オフセット・領域内への補正）を計算する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ウィンドウサイズ変更・ズーム・スクロール等の<br/>
    /// レイアウト変化に追従できる（イベント時の DIP キャッシュは使わない）。<br/>
    /// ポートの保持と、未設定（null）を等価変換として扱う規則はコア（ファサード）が単一所有する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        _presenter.SetCoordTransform(port);
        UpdatePosition();
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
        var parent = Parent as Panel;

        // 親領域未接続（配置前・テスト等）は位置の補正基準が無いため補正されない（規則はコアが単一所有する）
        var layout = _presenter.ResolveLayout(ParentAreaSize);

        // 表示サイズは要素サイズ・描画サイズ（物理ピクセルへの換算元）で同じ値を使う
        // （別々に求めると丸めの違いで要素と描画ビットマップの大きさが食い違う）
        Width = layout.ClientSize;
        Height = layout.ClientSize;

        // 配置は親が接続され表示中の場合のみ行う（拡大中心が未設定か、親領域が表示サイズを収められない場合はコアが位置を返さない）
        if (parent is null || !IsVisible || layout.Position is not { } position)
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
        var plan = _presenter.ResolveDrawPlan(finalSize.Width, MeasureText);
        SyncLabels(plan.Labels);

        return base.ArrangeOverride(finalSize);
    }

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
    private static void Apply(Border border, in LoupeLabelPlacement placement)
    {
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

    /// <summary>ルーペを非表示にしてリソースを解放する。</summary>
    /// <remarks>
    /// <see cref="Visual.IsVisible"/> を false に設定し、ビットマップを解放する。<br/>
    /// 既に非表示の場合は <see cref="OnIsVisibleChanged"/> が発火しないため、直接解放する（冪等）。<br/>
    /// </remarks>
    public void Clear()
    {
        IsVisible = false;
        _renderCoordinator.CancelAndRelease();
    }

    /// <summary>現在の状態からルーペ画像の再生成を要求する（プロパティ変更を伴わない再描画要求）。</summary>
    /// <remarks>
    /// ホストが <see cref="Rendered"/> でビットマップへオーバーレイを描画する場合、重ねるデータの変化<br/>
    /// （一覧の選択・外部からの状態変更等）ではプロパティが変わらないため、本メソッドで再生成を要求する。<br/>
    /// 生成は非同期で行われ、完了は <see cref="Rendered"/> で通知される（戻り値を待つ必要はない）。<br/>
    /// プロパティ変更と同じ経路（<see cref="LoupeRenderCoordinator.RequestRender"/>）を通るため、<br/>
    /// 複数の要求は1フレームに1回の生成へ結合される（呼び出し頻度を気にせず呼べる）。<br/>
    /// 非表示中に呼び出した場合はビットマップを解放して終了する（生成しない）。<br/>
    /// 破線の色・表示可否など、ルーペのプロパティに現れないオーバーレイの元データを変更した場合は、<br/>
    /// ルーペのプロパティ変更では再生成されないため、本メソッドの呼び出しが必要になる。
    /// </remarks>
    public void RequestRender()
    {
        _renderCoordinator.RequestRender();
    }

    // ====================================================================
    // 出力（internal）
    // ====================================================================

    /// <summary>表示用の画像アダプタ（EsBitmapAvaloniaImage）。 所有権は本コントロールが保持し、外部へ公開しない。</summary>
    /// <remarks>
    /// <see cref="Image.Source"/> へ設定する <see cref="EsBitmapAvaloniaImage"/> を保持する。<br/>
    /// <c>WriteableBitmap</c> を生成せず Skia 直描画するため、破棄は <see cref="ReleaseBitmap"/> で行う。
    /// </remarks>
    private EsBitmapAvaloniaImage? _loupeImage;

    /// <summary>内部描画結果（EsBitmap）。 所有権は本コントロールが保持し、外部へ公開しない。</summary>
    /// <remarks><see cref="_loupeImage"/> が内包する EsBitmap を返す（共有参照。 破棄はしないこと）。</remarks>
    internal EsBitmap? LoupeBitmap => _loupeImage?.Bitmap;

    // ====================================================================
    // ログ
    // ====================================================================

    /// <summary>内部ロガー（描画エラーの記録と UI 固有のログに使う）。</summary>
    /// <value>注入されたロガー。 null 可（null の場合は <see cref="NullLogger"/> へフォールバックする）。</value>
    /// <remarks>設定・環境値・拡大中心の変更ログはコアが出力するため、同じロガーをコアへも渡す。</remarks>
    public ILogger Log
    {
        get => _log.OrNullLogger();
        set
        {
            _log = value;
            _presenter.Log = value;
        }
    }

    /// <summary>内部ロガー（未設定は null。 読み取り時に <see cref="LoggerFallback"/> が吸収する）。</summary>
    private ILogger? _log;

    // ====================================================================
    // イベント
    // ====================================================================

    /// <summary>ルーペの描画完了を通知するイベント（描画済み Bitmap と切り出し領域を渡す）。</summary>
    /// <remarks>
    /// 拡大画像の描画結果（<see cref="LoupeRenderedEventArgs.Bitmap"/>）と、描画に必要な値（切り出し領域・スケール）を通知する。<br/>
    /// イベントは表示へ反映する前に同期的に発火するため、ホスト側はイベントハンドラ内で<br/>
    /// Bitmap を <c>Lock</c> してオーバーレイ（線分の破線等）を直接描画できる（描画後は表示へ反映される）。<br/>
    /// <b>中心座標ラベルは本コントロールがテキスト要素として表示する</b>ため、通知に含めない。<br/>
    /// 描画は非同期・バッチ化されるため、プロパティ変更より 1 フレーム遅れて発火する。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary><see cref="Rendered"/> イベントを発火する（描画済み EsBitmap と表示情報を構築して通知）。</summary>
    /// <param name="bitmap">描画済みのルーペ画像（EsBitmap）。 ホストがピクセルにオーバーレイを描画できる。</param>
    /// <param name="plan">実際に描画した描画計画（切り出し領域の左上と拡大率を持つ）。</param>
    /// <remarks>
    /// 切り出し領域の左上・拡大率は描画計画（<see cref="LoupeDrawPlan.CaptureTopLeft"/> / <see cref="LoupeDrawPlan.ZoomLevel"/>）が単一所有する。<br/>
    /// 中心未設定時は <see cref="LoupeRenderedEventArgs.CaptureTopLeft"/> が null になる（オーバーレイ描画不可）。<br/>
    /// <b>中心座標ラベルは本コントロールがテキスト要素として表示する</b>ため、通知に含めない。
    /// </remarks>
    private void RaiseRendered(EsBitmap bitmap, in LoupeDrawPlan plan)
    {
        Rendered?.Invoke(this, new LoupeRenderedEventArgs(
            bitmap,
            plan.CaptureTopLeft?.ToAvaloniaPoint(),
            plan.ZoomLevel));
    }

    // ====================================================================
    // 表示制御（IsVisible による表示・非表示の指示）
    // ====================================================================

    /// <summary>表示状態（<see cref="Visual.IsVisible"/>）が変更された時の処理。</summary>
    /// <param name="e">変更イベント引数。</param>
    /// <remarks>
    /// 表示・非表示の指示は標準プロパティ <see cref="Visual.IsVisible"/> に一元化する。<br/>
    /// - 表示時: 最新の <see cref="Source"/> / <see cref="CenterPoint"/> からビットマップを生成し、表示位置を更新する。<br/>
    /// - 非表示時: 未実行の再描画要求を破棄し、ビットマップを解放する。<br/>
    /// 要求の破棄と解放の手順はコーディネーター（<see cref="LoupeRenderCoordinator.CancelAndRelease"/>）が単一所有する。<br/>
    /// 非表示中は再描画要求がキューに残らない（<see cref="LoupeRenderCoordinator.RequestRender"/> が早期に戻る）ため、
    /// 表示時は再描画の要求のみを行う。
    /// </remarks>
    private void OnIsVisibleChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (e.GetNewValue<bool>())
        {
            RequestRender();
            UpdatePosition();
            return;
        }

        _renderCoordinator.CancelAndRelease();
    }

    /// <summary>ビットマップを解放して画像表示をクリアする（冪等）。</summary>
    private void ReleaseBitmap()
    {
        // 破棄済み EsBitmap を Image.Source が参照し続けないよう、先に Source を解除してから解放する。
        _image.Source = null;
        _loupeImage?.Dispose();
        _loupeImage = null;
    }

    /// <summary>自動再描画を開始し、失敗を観測済みタスクとして処理する。</summary>
    private void StartRender(long generation)
    {
        _ = GenerateAsync(generation).ContinueWith(
            static task => _ = task.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // ====================================================================
    // 描画（LoupeRenderPipeline への委譲）
    // ====================================================================

    /// <summary><see cref="Source"/> と <see cref="CenterPoint"/> からルーペ画像を生成して表示する。</summary>
    /// <remarks>
    /// 生成は <see cref="LoupeRenderPipeline.RenderAsync"/> に一元化する（ソース解決・抽出・描画合成）。<br/>
    /// <see cref="Source"/> / <see cref="CenterPoint"/> のいずれかが未設定、または切り出せない場合は市松模様のみを表示する<br/>
    /// （判定は描画層が単一所有する）。<br/>
    /// 例外は <see cref="LoupeUiLogMessage.LogUpdateError"/> で記録し、前回のビットマップを維持する。<br/>
    /// </remarks>
    private async Task GenerateAsync(long generation)
    {
        try
        {
            // 描画計画（描画層が解決した幾何・外観・層の順序）は現在のレイアウト（要求サイズ）から組み立てる
            // （値・単位・換算は描画層が単一所有する。 開始時に固定しない）
            var plan = _presenter.ResolveDrawPlan(
                _presenter.ResolveLayout(ParentAreaSize).ClientSize, MeasureText);

            var bitmap = await LoupeRenderPipeline.RenderAsync(Source, plan);

            // 描画反映（SetLoupeBitmap）は描画フェーズ（Render 優先度）で実行する。
            // マウス移動中の連続再描画では、既定の Normal 優先度の継続が入力イベント（Input 優先度）に
            // 後回しにされ、描画反映が移動停止まで実行されない（ルーペが移動中に更新されない）問題が生じる。
            // Render 優先度は描画フェーズで毎フレーム実行されるため、連続入力中でも描画が追従する。
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var current = _renderCoordinator.Generation;
                var visible = IsVisible;
                if (!_renderCoordinator.ShouldApply(generation))
                {
                    bitmap.Dispose();
                    Log.LogGenerateAsyncDone(false, true, visible, generation, current);
                    return;
                }

                SetLoupeBitmap(bitmap, plan);
                Log.LogGenerateAsyncDone(true, false, visible, generation, current);
            }, DispatcherPriority.Render);
        }
        catch (Exception ex)
        {
            Log.LogUpdateError(ex);
            throw;
        }
    }

    /// <summary>生成したルーペビットマップをコントロールへ反映する。</summary>
    /// <param name="bmp">生成したルーペビットマップ（EsBitmap）。</param>
    /// <param name="plan">実際に描画した描画計画（切り出し領域の左上と拡大率を持つ）。</param>
    /// <remarks>
    /// 前回のビットマップを破棄し、<see cref="LoupeBitmap"/> と表示内容（画像）を更新する。<br/>
    /// 表示状態（IsVisible）と要素サイズ（Width / Height）は変更しない（サイズは <see cref="UpdatePosition"/> が単一所有する）。<br/>
    /// 表示への反映前に <see cref="Rendered"/> イベントを発火し、ホスト側が EsBitmap のピクセルに<br/>
    /// オーバーレイ（線分の破線等）を描画できるようにする（イベント後に表示へ反映）。<br/>
    /// <b>チラつき防止</b>: <c>Image.Source</c> が前回のアダプタを参照し続ける間に、<br/>
    /// その内包 <see cref="EsBitmap"/> を破棄すると、描画時に破棄済みピクセルへアクセスして例外・チラつきが生じる。<br/>
    /// そのため、新しいアダプタを先に <c>Image.Source</c> へ設定し、旧アダプタは後から破棄する<br/>
    /// （描画が参照するアダプタを連続的に保ち、破棄済み Bitmap の参照を防ぐ）。
    /// </remarks>
    private void SetLoupeBitmap(EsBitmap bmp, in LoupeDrawPlan plan)
    {
        // ホストが EsBitmap のピクセルへオーバーレイ（破線等）を描画するフック。 イベント内でピクセルが更新される
        RaiseRendered(bmp, plan);

        // イベント後の EsBitmap（ホストのオーバーレイ描画を含む）を表示境界で Avalonia 画像として反映する。
        // EsBitmapAvaloniaImage（Skia 直描画・ゼロコピー）を Source に設定する。
        // 旧アダプタを破棄する前に、新しいアダプタを先に Source へ設定する（破棄済み Bitmap の参照防止・チラつき防止）。
        var previousImage = _loupeImage;
        _loupeImage = new EsBitmapAvaloniaImage(bmp);
        _image.Source = _loupeImage;
        // 旧アダプタは新しい Source 設定後に破棄する（Image.Source が旧アダプタを参照しなくなった後）。
        previousImage?.Dispose();
    }
}
