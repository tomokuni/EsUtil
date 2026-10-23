using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Platform.Core;
using EsUtil.Ui.WinUI3.Interop.Drawing;
using EsUtil.Ui.WinUI3.Interop.Imaging;
using EsUtil.Ui.WinUI3.Loupe.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace EsUtil.Ui.WinUI3.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール（WinUI 3 版）。</summary>
/// <remarks>
/// 他の UI 版の <c>LoupeControl</c> と同一の UI 非依存コア（<c>EsUtil.Feature.Loupe.Core</c>）を使い、<br/>
/// 描画層のみを XAML と Composition の要素で実装する（同じ設定・同じ見た目になる）。<br/>
/// <br/>
/// <b>入力</b>: <see cref="Source"/>（拡大元の 100% 画像）と <see cref="CenterPoint"/>（元画像ピクセル座標）。<br/>
/// <b>設定</b>: <see cref="Options"/>（コアの <see cref="LoupeOptions"/> が単一所有する表示サイズ・拡大率）。<br/>
/// <b>環境</b>: <see cref="ClientToPixelScaling"/>（DPI 追従）。<br/>
/// <b>描画</b>: 中間ビットマップを生成せず、本コントロール自身が画面のキャンバスへ直接描画する。<br/>
/// 数値の安全域への補正は <see cref="LoupeOptions"/>（コア）が、描画サイズの決定は<br/>
/// <see cref="LoupePresenter.ResolveDrawPlan"/>（描画層）が、寸法値（枠線の太さ）と<br/>
/// ラベル表示の可否は <see cref="LoupeAppearance"/>（描画層）が単一所有する。<br/>
/// <br/>
/// <b>基底クラス</b>: 子要素を重ねる <see cref="Canvas"/> を採用する。<br/>
/// <b>中心座標ラベルも要素として置く</b>（<see cref="LoupeVisualLayer"/>）。 子要素の順序が描画順になるため、
/// ラベルは常に図形の後に置かれ最上位になる（層の順序は描画層の <see cref="LoupeDrawPlan.Layers"/> が決める）。
/// <br/>
/// <b>プロパティは CLR プロパティである</b>: WinUI 3 の <see cref="DependencyProperty"/> は WinRT へ投影できる型だけを<br/>
/// 受け付けるため、コアと描画層の型（<see cref="EsReadOnlyBitmap"/> / <see cref="LoupeOptions"/> / <see cref="LoupeAppearance"/>）は<br/>
/// 依存関係プロパティとして登録できない。 したがって本コントロールは CLR プロパティで公開し、<br/>
/// ホストはコードビハインドから設定する（XAML のバインディング対象にはしない）。<br/>
/// <br/>
/// <b>ホスト側の操作</b>: <see cref="Source"/> を設定して <see cref="CenterPoint"/> を現在位置へ更新すると、<br/>
/// ルーペがその位置を拡大表示する（描画はプロパティ変更で自動的に無効化される）。<br/>
/// <b>座標ラベル</b>: 表示可否は <see cref="LoupeAppearance.ShouldShowCoordLabel"/>（描画層）が判断し、<br/>
/// 文言・基準点・配置は描画層（<see cref="LoupePresenter"/>）が実寸（Skia の実測）から単一所有する。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は、<see cref="Rendered"/> で受け取る追加描画の入力先へ<b>矩形を積んで表す</b><br/>
/// （描画 API は公開しない。 装飾は外観の <c>Show*</c> で止める）。
/// </remarks>
public sealed partial class LoupeControl : Canvas
{
    /// <summary>提示層のファサード（設定・表示レイアウト・描画計画の窓口）。</summary>
    /// <remarks>
    /// 表示サイズ・伸長入力・座標ラベル・表示位置の計算はすべて <see cref="LoupePresenter"/> が単一所有する<br/>
    /// （本コントロールは公開プロパティで受け取った値を転送し、返された描画計画を描画へ使うだけ）。
    /// </remarks>
    private readonly LoupePresenter _presenter;

    /// <summary>拡大元の 100% 画像（<see cref="Source"/> の保持値）。</summary>
    private EsReadOnlyBitmap? _source;

    /// <summary>拡大中心（<see cref="CenterPoint"/> の保持値）。</summary>
    private EsPoint? _centerPoint;

    /// <summary>設定（<see cref="Options"/> の保持値）。</summary>
    private LoupeOptions _options = LoupeOptions.Default;

    /// <summary>外観（<see cref="Appearance"/> の保持値）。</summary>
    private LoupeAppearance _appearance = LoupeAppearance.Default;

    /// <summary>表示スケーリング係数（<see cref="ClientToPixelScaling"/> の保持値）。</summary>
    private double _clientToPixelScaling = ClientToPixelScalingDefault;

    /// <summary>計画を要素へ写す層（子要素の順序が描画順になる）。</summary>
    private readonly LoupeVisualLayer _visual = new();

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private LoupeDrawPlan? _plan;

    /// <summary>直近に解決した描画計画の基準（表示サイズ（DIP））。</summary>
    private double _planSize;

    /// <summary>直近に解決した描画計画の基準（スケーリング係数）。</summary>
    private double _planScaling;

    /// <summary>ルーペコントロールを初期化する。</summary>
    /// <remarks>
    /// 提示層（描画計画の窓口）を生成し、DPI の変更を追従するために <c>XamlRoot</c> の変更を購読する<br/>
    /// （購読は <see cref="FrameworkElement.Loaded"/> から行う。 <c>XamlRoot</c> はツリーへ接続されるまで null である）。
    /// </remarks>
    public LoupeControl()
    {
        // 提示層（描画計画の窓口）を先に生成する（以降のプロパティ設定で参照されるため）
        // ロガーは Log プロパティの setter が提示層へ伝搬する（生成時点では未設定のため引き継がない）
        _presenter = new LoupePresenter();

        Children.Add(_visual);

        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    /// <summary>表示を最新の状態へ更新し直す。</summary>
    /// <remarks>
    /// プロパティの変更を伴わない再表示（DPI の変更・親の再配置など）を要求する入口である。<br/>
    /// 移行前は描画の無効化（<c>Invalidate</c>）を呼んでいたが、要素で描く現在は同じ更新手順を直接呼ぶ。
    /// </remarks>
    public void RequestRedraw() => ApplyVisual();
    /// <summary>ルーペの描画完了を通知するイベント（追加描画の入力先と描画計画を渡す）。</summary>
    /// <remarks>
    /// 描画計画と、追加描画を受け取る矩形の列（<see cref="LoupeRenderedEventArgs.AddRect"/>）を通知する。<br/>
    /// イベントは表示の更新中に発火するため、ホスト側はハンドラ内でオーバーレイ（選択矩形の破線等）を追加できる。<br/>
    /// 描画位置の計算は <see cref="LoupeDrawPlan.CaptureTopLeft"/> と <see cref="LoupeDrawPlan.ZoomLevel"/> から求める。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { CrosshairPixelRects = …, Labels = … }</c>）。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して描画をやり直す。
    /// </remarks>
    public Func<LoupeDrawPlan, LoupeDrawPlan>? DrawPlanResolver { get; set; }

    /// <summary>拡大元の 100% 画像を取得または設定する。</summary>
    /// <value>拡大元の画像（借用であり破棄しない）。 null の場合は画像外の色のみを表示する。</value>
    /// <remarks>
    /// 変更すると描画を無効化する（核となる描画は次のフレームで行われる）。<br/>
    /// 変更の記録は本プロジェクトのログ定義（<see cref="LoupeLogMessage"/>）が行い、<br/>
    /// 同一インスタンスの再設定では出力しない（判断もログ定義が単一所有する）。
    /// </remarks>
    public EsReadOnlyBitmap? Source
    {
        get => _source;
        set
        {
            var previous = _source;
            if (!SetIfChanged(ref _source, value))
            {
                return;
            }

            Log.LogSourceChanged(previous, value);
            ApplyVisual();
        }
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）を取得または設定する。</summary>
    /// <value>拡大中心（元画像ピクセル座標）。 null の場合は画像外の色のみを表示する。</value>
    /// <remarks>
    /// <b><see cref="UIElement.CenterPoint"/> を隠す</b>（基底の同名プロパティは描画変換の中心点（<c>Vector3</c>）であり、<br/>
    /// 本プロパティは拡大の中心（元画像ピクセル座標）である）。 名前を揃えるため <c>new</c> で隠し、<br/>
    /// 描画変換の中心点が必要な場合は基底の実装（<c>UIElement</c>）として扱う。<br/>
    /// 保持と変更の記録はコア（ファサード）が単一所有する（本コントロールは転送するだけである）。<br/>
    /// 変更すると保持中の描画計画を破棄する（文言・切り出し領域が中心に依存するため）。
    /// </remarks>
    public new EsPoint? CenterPoint
    {
        get => _centerPoint;
        set
        {
            if (!SetIfChanged(ref _centerPoint, value))
            {
                return;
            }

            _presenter.CenterPoint = value;
            InvalidatePlan();
            ApplyVisual();
        }
    }

    /// <summary>ルーペの設定（表示サイズ・拡大率）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定はコアの既定値（<see cref="LoupeOptions.Default"/>）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>LoupeOptions.Default with { ZoomLevel = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定の値・既定値・数値項目の安全域への補正はコアの <see cref="LoupeOptions"/> が単一所有する。<br/>
    /// 変更すると要求サイズが変わるためレイアウトを無効化し、あわせて描画もやり直す。
    /// </remarks>
    public LoupeOptions Options
    {
        get => _options;
        set
        {
            if (!SetIfChanged(ref _options, value))
            {
                return;
            }

            _presenter.Options = value;
            InvalidatePlan();
            InvalidateMeasure();
            ApplyVisual();
        }
    }

    /// <summary>外観（色・フォント・角丸・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <see cref="LoupeAppearance.Default"/>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="LoupeOptions"/>）ではなく本プロパティが単一所有する。<br/>
    /// 一部の項目だけを変える場合は <c>LoupeAppearance.Default with { ShowBorder = false }</c> の形で指定する。<br/>
    /// 変更すると保持中の描画計画を破棄して描画をやり直す（装飾の寸法とラベルの体裁が計画へ解決されるため）。
    /// </remarks>
    public LoupeAppearance Appearance
    {
        get => _appearance;
        set
        {
            if (!SetIfChanged(ref _appearance, value))
            {
                return;
            }

            _presenter.Appearance = value;
            InvalidatePlan();
            ApplyVisual();
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 範囲外・非有限は 0.1〜8.0 または既定値（1.0）へ補正される。</value>
    /// <remarks>
    /// 環境値（DPI）のため設定（<see cref="LoupeOptions"/>）には含めない。<br/>
    /// <b>描画のたびに描画面（物理ピクセル）と割り当て領域（DIP）の比へ合わせる</b>ため、<br/>
    /// ホストが明示的に設定する必要はない（DPI の変更通知が届かない状況でも整合する）。<br/>
    /// 値の補正はコアの <c>LoupeCore.Limits</c> が、変更の記録はコアのログ定義が単一所有する。
    /// </remarks>
    public double ClientToPixelScaling
    {
        get => _clientToPixelScaling;
        set
        {
            var coerced = CoerceClientToPixelScaling(value);
            if (coerced == _clientToPixelScaling)
            {
                return;
            }

            _clientToPixelScaling = coerced;
            _presenter.ClientToPixelScaling = coerced;
            ApplyVisual();
        }
    }

    /// <summary>ルーペ中心からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。 既定は 0。</value>
    /// <remarks>
    /// 保持と位置の補正はコア（ファサード）が単一所有する。<br/>
    /// 本コントロールは位置を持たない（ホストが <see cref="ResolveLayout"/> の結果で配置する）ため、<br/>
    /// 変更後にホストが再配置する（本コントロールはレイアウトを持たないので何も要求しない）。
    /// </remarks>
    public double PositionOffset
    {
        get => _presenter.PositionOffset;
        set => _presenter.PositionOffset = value;
    }

    /// <summary>設定変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキングフィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。<br/>
    /// 設定・環境値・拡大中心の変更ログはコアが出力するため、同じロガーをコアへも渡す。
    /// </remarks>
    public ILogger Log
    {
        get => field ?? NullLogger.Instance;
        set
        {
            field = value ?? NullLogger.Instance;
            _presenter.Log = field;
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
        get;
        set
        {
            field = value;
            LoupeLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? LoupeLogMessage.DefaultPrefix : value;
            _presenter.LogPrefix = value;
        }
    }

    /// <summary>描画計画を指定の描画面へ描く（計画どおりに描く）。</summary>

    /// <summary>対象 ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（ToDip = 対象 → 親領域 DIP）。 null の場合は等価変換。</param>
    /// <remarks>
    /// 保持と、未設定（null）を等価変換として扱う規則はコア（ファサード）が単一所有する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化に追従する。<br/>
    /// 本コントロールは位置を持たないため、変更後にホストが再配置する。
    /// </remarks>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _presenter.SetCoordTransform(port);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 公開ファサードとして内部の例外を集約して記録し、呼び出し側へ伝搬する（例外処理規約 §4）
            Log.LogOperationError(ex, nameof(SetCoordTransform));
            throw;
        }
    }

    /// <summary>親領域のサイズから表示レイアウト（表示サイズ・表示位置）を解決する。</summary>
    /// <param name="parentSize">親領域のサイズ（DIP）。 未確定（幅または高さが 0 以下）の場合は位置を補正しない。</param>
    /// <returns>表示レイアウト（表示サイズと表示位置。 拡大中心未設定時と、親領域が表示サイズを収められない場合は位置が null）。</returns>
    /// <remarks>
    /// 本コントロールは位置を持たないため、ホストが本メソッドの結果で配置する（オフセット・位置の補正の規則はコアが単一所有する）。<br/>
    /// 描画サイズも同じ値から決まる（描画面と配置サイズを一致させる）。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize) => _presenter.ResolveLayout(parentSize);

    /// <summary>ルーペの要求サイズ（DIP）を返す。</summary>
    /// <param name="availableSize">親から提示された利用可能サイズ。</param>
    /// <returns>一辺が <see cref="LoupeOptions.RequestedSize"/> の正方形。</returns>
    /// <remarks>
    /// ルーペは一辺が <see cref="LoupeOptions.RequestedSize"/> の正方形であるため、自身の要求サイズを返す。<br/>
    /// 表示位置はホストが <see cref="ResolveLayout"/>（コアの計算）の結果で配置する（本コントロールは位置を持たない）。
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        // 基底の計測（描画面の準備）を先に行い、そのうえでルーペ固有の要求サイズを返す
        base.MeasureOverride(availableSize);

        return new Size(Options.RequestedSize, Options.RequestedSize);
    }

    /// <summary>ルーペの表示を最新の状態へ反映する。</summary>
    /// <remarks>
    /// 描画計画（描画層が解決した幾何・外観・層の順序）を公開プロパティから組み立て、要素へ写す<br/>
    /// （DIP → 物理ピクセルの換算は描画層が単一所有する。 要素へ写すときだけ 1 / 倍率で DIP へ戻す）。<br/>
    /// <b>要素の順序が描画順になる</b>（画像 → 十字線 → 枠線 → ホストの追加分 → ラベル）。<br/>
    /// <b>追加描画も要素として置く</b>: 移行前は描画 API をホストへ渡していたが、<br/>
    /// 現在は <see cref="LoupeRenderedEventArgs.AddRect"/> で受け取った矩形を追加の層へ置く。
    /// </remarks>
    private void ApplyVisual()
    {
        try
        {
            // 倍率と表示サイズ（DIP）から描画計画を組み立てる
            var scaling = ApplyClientPixelScaling();
            var plan = ResolvePlan(ActualWidth);

            // 層の順序と描画内容は LoupeVisualLayer が計画に従って写す
            Background = new SolidColorBrush(ToColor(plan.Appearance.ResolvePasteboardColor()));
            _visual.Image.SetSource(Source);
            _visual.Apply(plan, 1 / scaling);

            var args = new LoupeRenderedEventArgs(new DrawList(), plan);
            RaiseRendered(args);
            _visual.OverlayLayer.Apply(args.Overlay, 1 / scaling);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            // 表示の例外はアプリを落とさずログへ記録する（要素は中途半端な状態になりうるが、次の更新で復帰する）
            Log.LogUpdateError(ex);
        }
    }

    /// <summary>色を WinUI の色へ変換する。</summary>
    /// <param name="color">変換する色。</param>
    /// <returns>変換した色。</returns>
    private static Windows.UI.Color ToColor(System.Drawing.Color color)
        => Windows.UI.Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary><see cref="Rendered"/> イベントを発火する。</summary>
    /// <param name="args">通知する引数（描画先のキャンバスと描画計画）。</param>
    /// <remarks>ハンドラーの例外が描画の完了通知を妨げないよう、購読の有無を先に判定してから発火する。</remarks>
    private void RaiseRendered(LoupeRenderedEventArgs args)
    {
        var handler = Rendered;
        handler?.Invoke(this, args);
    }

    /// <summary>DPI の変更を追従するため、ツリーへ接続された時点で <c>XamlRoot</c> の変更を購読する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is { } root)
        {
            root.Changed += HandleXamlRootChanged;
        }
    }

    /// <summary>ツリーから外れた時点で <c>XamlRoot</c> の変更の購読を解除する。</summary>
    /// <param name="sender">イベントの送信元（本コントロール）。</param>
    /// <param name="e">イベント引数。</param>
    private void HandleUnloaded(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is { } root)
        {
            root.Changed -= HandleXamlRootChanged;
        }
    }

    /// <summary>モニター間の移動で表示倍率が変わったときに表示スケーリング係数を追従させる。</summary>
    /// <param name="sender">変更された <c>XamlRoot</c>。</param>
    /// <param name="args">イベント引数。</param>
    /// <remarks>
    /// 物理ピクセル基準の見た目を保つため、<see cref="ClientToPixelScaling"/> を表示倍率へ更新する<br/>
    /// （ホストが明示設定した値も表示倍率の変更時は上書きされる）。<br/>
    /// 描画の直前に描画面から求める倍率（<see cref="ApplyClientPixelScaling"/>）と同じ規則である<br/>
    /// （描画面から求めた値が最終的な正であり、本通知は保持値を早めに合わせるためだけのものである）。
    /// </remarks>
    private void HandleXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        => ClientToPixelScaling = sender.RasterizationScale;

    /// <summary>描画に使う実効倍率を解決し、保持値へも反映する。</summary>
    /// <returns>描画に使うスケーリング係数（保持値へ反映済みの値）。</returns>
    /// <remarks>
    /// <b>なぜ XAML のルートから求めるか</b>: DPI の変更通知は<b>追加直後のコントロールへ届かない</b>ことがある。<br/>
    /// その状態で保持値だけを使うと、拡大画像がルーペ領域より小さい倍率で表示される<br/>
    /// （実測: 125% の環境で 1/1.25 の大きさになり、右と下に画像外の帯が残る）。<br/>
    /// ルートの拡大率は実際に表示するときの倍率そのものであるため、こちらを保持値とする<br/>
    /// （保持値の変更はファサードへ伝搬されるため、拡大画像のピクセルサイズの算出元は 1 つに保たれる）。
    /// </remarks>
    private double ApplyClientPixelScaling()
    {
        var resolved = DpiScaler.ResolveClientScale(
            XamlRoot?.RasterizationScale ?? ClientToPixelScaling, ActualWidth, ClientToPixelScaling);

        if (resolved == ClientToPixelScaling)
        {
            return ClientToPixelScaling;
        }

        ClientToPixelScaling = resolved;

        // 保持値の補正（範囲外は安全域へ）を通した値を返す（計算に使う値と保持値を一致させる）
        return ClientToPixelScaling;
    }
    /// <summary>描画計画を解決し、<see cref="DrawPlanResolver"/> を適用する（同じ入力では再解決しない）。</summary>
    /// <param name="displaySize">ルーペの一辺の表示サイズ（DIP）。</param>
    /// <returns>差し替え後の描画計画。</returns>
    /// <remarks>
    /// <b>表示の経路は本メソッドを通る</b>（表示サイズは要素の幅（DIP）である）。<br/>
    /// <b>同じ入力ではキャッシュを返して 1 フレーム 1 回の解決に留める</b><br/>
    /// （解決のたびに十字線・枠線の矩形とラベルの実寸測定をやり直さない）。<br/>
    /// キャッシュのキーは「表示サイズ」と「スケーリング係数」である（後者は描画の直前に書き戻されるため、<br/>
    /// 書き戻し後の解決を取りこぼさないようキーに含める）。<br/>
    /// それ以外の入力（拡大中心・設定・外観・<see cref="DrawPlanResolver"/>）の変更は <see cref="InvalidatePlan"/> で破棄する。<br/>
    /// ラベルの実寸は WinUI 3 のテキスト エンジン（<see cref="TextMeasure.Measure"/>）で測る<br/>
    /// </remarks>
    private LoupeDrawPlan ResolvePlan(double displaySize)
    {
        var scaling = ClientToPixelScaling;
        if (_plan is not { } cached || _planSize != displaySize || _planScaling != scaling)
        {
            cached = _presenter.ResolveDrawPlan(displaySize, TextMeasure.Measure);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _plan = cached;
            _planSize = displaySize;
            _planScaling = scaling;
        }

        return cached;
    }

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidatePlan() => _plan = null;

    /// <summary>値が変化した場合だけ保持値を置き換える。</summary>
    /// <typeparam name="T">値の型。</typeparam>
    /// <param name="field">置き換える保持値。</param>
    /// <param name="value">設定する値。</param>
    /// <returns>置き換えた場合は true。 同値（参照の同一性・値の等価性）の場合は false。</returns>
    /// <remarks>
    /// 同値の再設定で再描画を起こさないための判定を単一所有する<br/>
    /// （値の型ごとに等価性の判定方法が異なるため、各プロパティへ同じ判定を書かない）。
    /// </remarks>
    private static bool SetIfChanged<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        return true;
    }
}
