using System.ComponentModel;
using static EsUtil.Feature.Loupe.LoupeCore.Limits;
using EsUtil.Platform.Core.Common;
using EsUtil.Platform.Core.Coord;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;
using EsUtil.Ui.WinForms.Loupe.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Ui.WinForms.Loupe;

/// <summary>拡大鏡（ルーペ）コントロール（WinForms 版）。</summary>
/// <remarks>
/// 他の UI 版と同一の UI 非依存コア（<c>EsUtil.Feature.Loupe.Core</c>）と提示層（<c>EsUtil.Feature.Loupe.Presenter</c>）を使い、<br/>
/// 描画だけを GDI+（<see cref="LoupeGraphicsPainter"/>）で行う（同じ設定・同じ見た目になる）。<br/>
/// <br/>
/// <b>入力</b>: <see cref="Source"/>（拡大元の 100% 画像）と <see cref="CenterPoint"/>（元画像ピクセル座標）。<br/>
/// <b>設定</b>: <see cref="Options"/>（コアの <c>LoupeOptions</c> が単一所有する表示サイズ・拡大率）。<br/>
/// <b>環境</b>: <see cref="ClientToPixelScaling"/>（DPI 追従）。<br/>
/// <b>描画</b>: <see cref="OnPaint"/> が描画計画（<see cref="LoupeDrawPlan"/>）を解決し、GDI+ で直接描く<br/>
/// （中間ビットマップを持たない）。 数値の安全域への補正は <c>LoupeOptions</c>（コア）が、<br/>
/// 描画サイズの決定は <c>LoupePresenter.ResolveDrawPlan</c>（描画層）が、寸法値（枠線の太さ・フォントサイズ）と<br/>
/// ラベル表示の可否は <c>LoupeAppearance</c>（描画層）が単一所有する。<br/>
/// <br/>
/// <b>基底クラス</b>: 自前で描くため <see cref="Control"/> を継承し、コンストラクターで<br/>
/// <see cref="ControlStyles.UserPaint"/> / <see cref="ControlStyles.AllPaintingInWmPaint"/> /<br/>
/// <see cref="ControlStyles.OptimizedDoubleBuffer"/> / <see cref="ControlStyles.ResizeRedraw"/> を有効にする<br/>
/// （ちらつきを抑え、サイズ変更でも描き直す）。<br/>
/// <br/>
/// <b>デザイナーには出さない</b>: 本コントロールの公開プロパティはいずれも実行時のオブジェクト（画像・設定・<br/>
/// ロガー・差し替え関数）を持つため、デザイナーのコード生成の対象外とする<br/>
/// （<see cref="DesignerSerializationVisibilityAttribute"/> を各プロパティへ付ける）。<br/>
/// <br/>
/// <b>ホスト側の操作</b>: <see cref="Source"/> を設定して <see cref="CenterPoint"/> を現在位置へ更新すると、<br/>
/// ルーペがその位置を拡大表示する。 <b>表示位置と表示サイズはホストが決める</b>:<br/>
/// <see cref="ResolveLayout"/> の結果（DIP）を <see cref="ToClientPixel"/> で物理ピクセルへ換算し、<br/>
/// <see cref="Control.Bounds"/> へ設定する（WinForms のレイアウトは親が所有するため、コントロール自身は動かない）。<br/>
/// <br/>
/// <b>座標ラベル</b>: 表示可否は <c>LoupeAppearance.ShouldShowCoordLabel</c>（描画層）が判断し、<br/>
/// 文言・基準点・配置は描画層が実寸（<see cref="DrawingLabelText"/>）から単一所有する<br/>
/// （本コントロールは実寸測定の入口を渡すだけである）。<br/>
/// <br/>
/// <b>見た目のカスタマイズ</b>: 色・太さ・表示可否は <see cref="Appearance"/> で、<br/>
/// 矩形の並びで表せる装飾とラベルの文言・位置は <see cref="DrawPlanResolver"/> で差し替えられる。<br/>
/// 矩形で表せない描画（円・斜線等）は <see cref="DrawPlan"/> を呼んで自前で描く（装飾は外観の <c>Show*</c> で止める）<br/>
/// （描画の直後に呼ばれる <see cref="Rendered"/> の描画面へ描き、必要なら <see cref="DrawPlan"/> で既定の描画を行う）。
/// </remarks>
public sealed class LoupeControl : Control
{
    /// <summary>提示層のファサード（設定・表示レイアウト・描画計画の窓口）。</summary>
    /// <remarks>
    /// 表示サイズ・伸長入力・座標ラベル・表示位置の計算はすべて <c>LoupePresenter</c> が単一所有する<br/>
    /// （本コントロールは公開プロパティで受け取った値を転送し、返された描画計画を描画へ使うだけ）。
    /// </remarks>
    private readonly LoupePresenter _presenter;

    /// <summary>直近に解決した描画計画（null = 未解決）。</summary>
    private LoupeDrawPlan? _plan;

    /// <summary>直近に解決した描画計画の基準（表示サイズ（DIP））。</summary>
    private double _planDipSize;

    /// <summary>直近に解決した描画計画の基準（スケーリング係数）。</summary>
    private double _planScaling = ClientToPixelScalingDefault;

    /// <summary>ルーペコントロールを初期化する。</summary>
    /// <remarks>
    /// <b>自前で描くためのスタイル設定</b>: <see cref="ControlStyles.UserPaint"/>（<see cref="OnPaint"/> が描く）と<br/>
    /// <see cref="ControlStyles.AllPaintingInWmPaint"/>（<c>WM_ERASEBKGND</c> で消さない）で前のフレームのちらつきを抑え、<br/>
    /// <see cref="ControlStyles.OptimizedDoubleBuffer"/> で描画面を 1 枚にまとめ、<br/>
    /// <see cref="ControlStyles.ResizeRedraw"/> でサイズ変更時に描き直す。<br/>
    /// <b>下地はフレームワークが塗る</b>（<see cref="ControlStyles.Opaque"/> を有効にしない）: ペーストボードを非表示にした場合は<br/>
    /// 下地（<see cref="Control.BackColor"/>）が見えるため、毎フレームの描き直しで前のフレームも消える。
    /// </remarks>
    public LoupeControl()
    {
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw,
            true);

        // 提示層（描画計画の窓口）を生成する（以降のプロパティ設定で参照されるため）
        // ロガーは Log プロパティの setter が提示層へ伝搬する（生成時点では未設定のため引き継がない）
        _presenter = new LoupePresenter();
    }

    /// <summary>ルーペの描画完了を通知するイベント（描画面と描画計画を渡す）。</summary>
    /// <remarks>
    /// 拡大画像の描画結果（<see cref="LoupeRenderedEventArgs.Graphics"/>）と描画計画を通知する。<br/>
    /// イベントは本コントロールの描画直後（同一フレーム内）に発火するため、ホスト側は<br/>
    /// イベント ハンドラー内で描画面へオーバーレイ（選択矩形の破線等）を追加描画できる。<br/>
    /// 描画位置の計算は <see cref="LoupeDrawPlan.CaptureTopLeft"/> と <see cref="LoupeDrawPlan.ZoomLevel"/> から求める。
    /// </remarks>
    public event EventHandler<LoupeRenderedEventArgs>? Rendered;

    /// <summary>拡大元の 100% 画像を取得または設定する。</summary>
    /// <value>拡大元の画像。 null の場合は下地のみを表示する。</value>
    /// <remarks>
    /// <b>画像の所有権はホストにある</b>（本コントロールは破棄しない）: ホストが画像を破棄した後に描画した場合は<br/>
    /// 画像の層を描かずに継続する（下地のみが残る）。<br/>
    /// 設定すると再描画され、変更は <see cref="Log"/> へ記録される（同値の再設定では記録しない）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsReadOnlyBitmap? Source
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            var previous = field;
            field = value;
            Log.LogSourceChanged(previous, value);
            Invalidate();
        }
    }

    /// <summary>拡大表示する中心座標（元画像ピクセル座標）を取得または設定する。</summary>
    /// <value>拡大中心。 null の場合は下地のみを表示する。</value>
    /// <remarks>
    /// 保持と変更の記録はコア（ファサード）が単一所有する。<br/>
    /// 設定すると描画計画を破棄して再描画する（表示位置は親が <see cref="ResolveLayout"/> の結果で決める）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public EsPoint? CenterPoint
    {
        get => _presenter.CenterPoint;
        set
        {
            _presenter.CenterPoint = value;
            InvalidatePlan();
            Invalidate();
        }
    }

    /// <summary>ルーペの設定（表示サイズ・拡大率）を取得または設定する。</summary>
    /// <value>設定の単一値。 既定はコアの既定値（<c>LoupeOptions.Default</c>）。</value>
    /// <remarks>
    /// 一部の項目だけを変える場合は <c>LoupeOptions.Default with { ZoomLevel = 8 }</c> の形で指定する（指定しない項目は既定値を保つ）。<br/>
    /// 設定の値・既定値・数値項目の安全域への補正はコアが単一所有するため、本コントロールは既定値を持たず、補正の登録も行わない。<br/>
    /// 変更すると描画計画を破棄して再描画する。 <b>表示サイズは本値（DIP）であり、ホストが <see cref="ResolveLayout"/> の結果で配置する</b>。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LoupeOptions Options
    {
        get => _presenter.Options;
        set
        {
            _presenter.Options = value;
            InvalidatePlan();
            Invalidate();
        }
    }

    /// <summary>外観（色・フォント・角丸・枠線の太さ・表示可否）を取得または設定する。</summary>
    /// <value>外観の単一値。 既定は <c>LoupeAppearance.Default</c>。</value>
    /// <remarks>
    /// 外観はコアの計算に入らない（描画にだけ必要）ため、コアの設定（<see cref="Options"/>）ではなく本プロパティが単一所有する<br/>
    /// （保持は提示層が行い、変更の記録は提示層のログ定義が行う）。<br/>
    /// 変更すると描画計画を破棄して再描画する（ラベルの体裁も外観が与えるため、計画の再解決が要る）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LoupeAppearance Appearance
    {
        get => _presenter.Appearance;
        set
        {
            _presenter.Appearance = value;
            InvalidatePlan();
            Invalidate();
        }
    }

    /// <summary>表示 DIP を物理ピクセルへ変換するスケーリング係数を取得または設定する。</summary>
    /// <value>スケーリング係数。 範囲外・非有限は 0.1〜8.0 または既定値へ補正される。 既定は 1.0（ハンドル生成後は DPI に追随）。</value>
    /// <remarks>
    /// 保持・補正・変更の記録はコア（ファサード）が単一所有する（本プロパティは補正済みの値を転送し、補正済みの値を返す）。<br/>
    /// <b>DPI からは自動で追随する</b>: ハンドルの生成時と DPI の変更時に、実測した倍率へ更新される<br/>
    /// （ホストが明示設定した値もそのときに上書きされる。 <see cref="ApplyClientPixelScaling"/>）。<br/>
    /// 本値は DIP で書かれた寸法（枠線の太さ・ラベル）の換算に効く（表示位置・表示サイズは <see cref="ResolveLayout"/> が決める）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ClientToPixelScaling
    {
        get => _presenter.ClientToPixelScaling;
        set
        {
            _presenter.ClientToPixelScaling = CoerceClientToPixelScaling(value);
            InvalidatePlan();
            Invalidate();
        }
    }

    /// <summary>ルーペ中心からの右下オフセット（DIP）を取得または設定する。</summary>
    /// <value>カーソル位置（拡大中心）からの右下方向へのオフセット量（DIP）。 既定は 0。</value>
    /// <remarks>
    /// 保持と位置の補正はコア（ファサード）が単一所有する。<br/>
    /// 本コントロールは位置を持たない（ホストが <see cref="ResolveLayout"/> の結果で配置する）ため、<br/>
    /// 設定後はホストが <see cref="ResolveLayout"/> を呼び直して配置を更新する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double PositionOffset
    {
        get => _presenter.PositionOffset;
        set => _presenter.PositionOffset = value;
    }

    /// <summary>設定変更ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定した場合は <see cref="NullLogger"/> へフォールバックする（出力なし）。</value>
    /// <remarks>
    /// C#14 の <c>field</c> キーワードでバッキング フィールドを暗黙定義する。<br/>
    /// getter / setter とも null を <see cref="NullLogger"/> へ安全に変換する。<br/>
    /// 設定・環境値・拡大中心の変更ログはコアが出力するため、同じロガーをコアへも渡す。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
    /// 本コントロールのログ定義（<see cref="LoupeLogMessage"/>）の接頭辞を設定し、提示層・コアへ伝搬する<br/>
    /// （集約した接頭辞で全層のログを揃える）。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    /// <summary>描画計画を差し替える関数を取得または設定する。</summary>
    /// <value>計画（幾何・外観・層の順序・ラベル配置）を受け取り、差し替えた計画を返す関数。 null の場合は既定の計画をそのまま使う。</value>
    /// <remarks>
    /// <b>矩形の並びで表せる装飾と、ラベルの文言・位置を差し替える入口</b>である（色・太さ・表示可否は <see cref="Appearance"/>）。<br/>
    /// 受け取る計画は提示層が解決済みであるため、本関数は値の置き換えだけで完結する<br/>
    /// （<c>plan with { CrosshairPixelRects = …, Labels = … }</c>）。<br/>
    /// <b>純粋関数（同じ入力に同じ出力）にすること</b>（同じ入力では 1 フレーム 1 回だけ解決されるため、<br/>
    /// 呼び出しの時点に依存する関数を渡すと差し替えが反映されない）。 設定すると保持中の計画を破棄して再描画する。
    /// </remarks>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<LoupeDrawPlan, LoupeDrawPlan>? DrawPlanResolver
    {
        get;
        set
        {
            field = value;
            InvalidatePlan();
            Invalidate();
        }
    }

    /// <summary>対象 ↔ 親領域 DIP の座標変換ポートを設定する。</summary>
    /// <param name="port">座標変換ポート（ToDip = 対象 → 親領域 DIP）。 null の場合は等価変換。</param>
    /// <remarks>
    /// 保持と、未設定（null）を等価変換として扱う規則はコア（ファサード）が単一所有する。<br/>
    /// 変換関数は呼び出し毎に評価されるため、ズーム・スクロール等の動的変化に追従する。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="port"/> が null の場合（内部で発生した例外を記録して伝搬する）。</exception>
    public void SetCoordTransform(ICoordTransform? port)
    {
        try
        {
            _presenter.SetCoordTransform(port);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
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
    /// 結果は DIP であるため、物理ピクセルへ換算する場合は <see cref="ToClientPixel"/> を使う。
    /// </remarks>
    public LoupeLayout ResolveLayout(EsSize parentSize) => _presenter.ResolveLayout(parentSize);

    /// <summary>DIP の長さを本コントロールのクライアント座標（物理ピクセル）へ換算する。</summary>
    /// <param name="dipValue">換算する長さ（DIP）。</param>
    /// <returns>物理ピクセル（切り上げ・最低 1）。</returns>
    /// <remarks>
    /// <see cref="ResolveLayout"/> が返す値（DIP）を配置（<see cref="Control.Bounds"/>）へ写すための入口である<br/>
    /// （換算の規則はコアの <c>DpiScaler</c> が単一所有し、枠線の太さの換算と同じ規則になる）。
    /// </remarks>
    public int ToClientPixel(double dipValue) => DpiScaler.ScaleToPixel(dipValue, ClientToPixelScaling);

    /// <summary>クライアント座標（物理ピクセル）のサイズを DIP へ換算する。</summary>
    /// <param name="clientPixelSize">親領域のサイズ（物理ピクセル）。 通常は親のクライアント領域のサイズ。</param>
    /// <returns>親領域のサイズ（DIP）。</returns>
    /// <remarks>
    /// <see cref="ResolveLayout"/> は DIP の親領域を要求するため、WinForms（クライアント領域が物理ピクセル）では<br/>
    /// 本メソッドで換算してから渡す。
    /// </remarks>
    public EsSize ToDipSize(Size clientPixelSize)
        => new(
            DpiScaler.ToDip(clientPixelSize.Width, ClientToPixelScaling),
            DpiScaler.ToDip(clientPixelSize.Height, ClientToPixelScaling));

    /// <summary>描画計画を指定の描画面へ描く（計画どおりに描く）。</summary>
    /// <param name="graphics">描画先の描画面（物理ピクセル・拡大画像と同じ座標系）。 null 不可。</param>
    /// <param name="source">拡大元の 100% 画像。 null の場合は拡大画像を描かない。</param>
    /// <param name="plan">描く描画計画。 null 不可。</param>
    /// <remarks>
    /// <b>既定の描画をホストへ提供する入口</b>である（ホストが任意のタイミングと順序で既定の描画を行える）。<br/>
    /// 描画実装（<see cref="LoupeGraphicsPainter"/>）は本プロジェクトの内部であるため、外部へは本メソッドが窓口になる。<br/>
    /// 計画を必要とする場合は <see cref="Rendered"/> の <see cref="LoupeRenderedEventArgs.Content"/> を使う。
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="graphics"/> が null の場合。</exception>
    public static void DrawPlan(Graphics graphics, EsReadOnlyBitmap? source, in LoupeDrawPlan plan)
        => LoupeGraphicsPainter.Paint(graphics, source, in plan);

    /// <summary>描画面の実測からスケーリング係数を解決し、保持値へも反映する。</summary>
    /// <returns>描画に使うスケーリング係数（保持値へ反映済みの値）。</returns>
    /// <remarks>
    /// <b>なぜ DPI の換算値だけでなく描画面の実測を通すか</b>: DPI の変更通知は<b>追加直後のコントロールへ届かないことがある</b><br/>
    /// （他 UI での実測: ウィンドウ表示後に追加したコントロールは通知を受けず、既定値 1.0 のままになる）。<br/>
    /// その状態で描画すると図形が 100% で描かれ、DPI を反映済みの画像と重ねたときにずれる。<br/>
    /// 本メソッドは「クライアント領域（物理ピクセル）」と「その DIP 換算」の比から実効倍率を解決する<br/>
    /// （クライアント領域が未確定（0）の間は DPI から換算した値を使う。 規則は <see cref="ClientPixelScale"/> が単一所有する）。<br/>
    /// WinForms のクライアント領域は物理ピクセルであり独立した DIP の寸法を持たないため、DIP 換算は DPI に依る<br/>
    /// （比を通すのは、描画面と DIP の対応が崩れた場合（未確定・0 割り）に既定値へ落とすためである）。<br/>
    /// <b>本メソッドは internal である</b>: 保持値の更新はハンドルの生成時と DPI の変更時に自動で行われ、<br/>
    /// ホストが明示的に呼ぶ必要はない（単体テストが描画面の実測を再現するために参照する）。
    /// </remarks>
    internal double ApplyClientPixelScaling()
    {
        var fallback = DpiScaler.ToScaling(DeviceDpi);
        var dipSize = DpiScaler.ToDip(ClientSize.Width, fallback);
        ClientToPixelScaling = ClientPixelScale.Resolve(ClientSize.Width, dipSize, fallback);

        // 保持値の補正（範囲外は安全域へ）を通した値を返す（計算に使う値と保持値を一致させる）
        return ClientToPixelScaling;
    }

    /// <summary>直近の状態から描画計画（1 フレーム分）を解決する。</summary>
    /// <returns>差し替え（<see cref="DrawPlanResolver"/>）を適用した描画計画。</returns>
    /// <remarks>
    /// <b>同じ入力では再解決しない</b>: キーは「表示サイズ（DIP）」と「スケーリング係数」である<br/>
    /// （解決のたびに十字線・枠線の矩形とラベルの実寸測定をやり直さない）。<br/>
    /// それ以外の入力（拡大中心・設定・外観・差し替え関数）の変更は <see cref="InvalidatePlan"/> が破棄する。<br/>
    /// <b>本メソッドは internal である</b>: 描画（<see cref="OnPaint"/>）が使い、単体テストが計画の解決を検証するために参照する。
    /// </remarks>
    internal LoupeDrawPlan ResolveCurrentPlan()
    {
        var scaling = ClientToPixelScaling;
        var dipSize = DpiScaler.ToDip(ClientSize.Width, scaling);
        if (_plan is not { } cached || !_planDipSize.Equals(dipSize) || !_planScaling.Equals(scaling))
        {
            cached = _presenter.ResolveDrawPlan(dipSize, MeasureCoordLabelText);
            cached = DrawPlanResolver?.Invoke(cached) ?? cached;
            _plan = cached;
            _planDipSize = dipSize;
            _planScaling = scaling;
        }

        return cached;
    }

    /// <summary>中心座標ラベルの実寸を測る（描画層へ渡す測定関数）。</summary>
    /// <param name="text">測る文字列。</param>
    /// <param name="fontSize">フォントサイズ（DIP）。</param>
    /// <returns>文字の実寸（DIP）。</returns>
    /// <remarks>
    /// 測る API は描く API（<see cref="DrawingLabelText"/>）と同じである必要があるため、本コントロールが入口を提供する<br/>
    /// （描画層は実寸の測定を描画 API に依存するため、UI 層から関数として受け取る）。
    /// </remarks>
    private EsSize MeasureCoordLabelText(string text, double fontSize)
        => DrawingLabelText.Measure(text, fontSize, ClientToPixelScaling);

    /// <summary>保持している描画計画を破棄する（次の解決で最新の状態を反映する）。</summary>
    private void InvalidatePlan() => _plan = null;

    /// <summary>ルーペを描画する。</summary>
    /// <param name="e">描画のイベント引数（描画面を持つ）。</param>
    /// <remarks>
    /// 描画計画（描画層が解決した幾何・外観・層の順序）を解決して GDI+ の描画実装へ渡す<br/>
    /// （DIP → 物理ピクセルの換算は描画層と <see cref="LoupeGraphicsPainter"/> が単一所有する）。<br/>
    /// <b>描く層とその順序は描画層が単一所有する</b>（<see cref="LoupeDrawPlan.Layers"/> の順に処理する）。<br/>
    /// <b>描画面を拡大しない</b>: 描画計画の矩形（描画面・拡大画像の描画先・枠線の太さ）は物理ピクセルで解決されるため、<br/>
    /// 拡大すると設定した拡大率より大きい画像をルーペ領域へ描くことになり、切り取られてずれる。<br/>
    /// 中心座標ラベルは描画層（<see cref="LoupeGraphicsPainter"/>）が GDI+ で描く<br/>
    /// （WinForms の子コントロールは半透明の背景を持てないため、要素として置かず描画面へ描く）。
    /// </remarks>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var plan = ResolveCurrentPlan();
        try
        {
            // 描画の手順（層の順序と描画 API への写し）は LoupeGraphicsPainter が単一所有する
            LoupeGraphicsPainter.Paint(e.Graphics, Source, in plan);
        }
        catch (Exception ex) when (BoundaryCatch.IsRecoverable(ex))
        {
            // 描画の例外はアプリを落とさずログへ記録する（描画面は中途半端な状態になりうるが、次の描画で復帰する）
            Log.LogUpdateError(ex);
            return;
        }

        Rendered?.Invoke(this, new LoupeRenderedEventArgs(e.Graphics, plan));
    }

    /// <summary>ハンドルの生成時にスケーリング係数を DPI へ追随させる。</summary>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// ハンドルの生成前は DPI を取得できないため、生成時に実効倍率を解決して保持する<br/>
    /// （クライアント領域が未確定の間は DPI から換算した値になる）。
    /// </remarks>
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyClientPixelScaling();
    }

    /// <summary>親の DPI が変更された後にスケーリング係数を追随させる。</summary>
    /// <param name="e">イベント引数。</param>
    /// <remarks>
    /// モニター間の移動で DPI が変わっても物理ピクセル基準の見た目を保つため、<see cref="ClientToPixelScaling"/> を<br/>
    /// DPI から換算した値へ更新する（ホストが明示設定した値も DPI 変更時は上書きされる）。<br/>
    /// ホストがスケーリングを明示する必要はない（DPI はコントロールが自動取得する）。
    /// </remarks>
    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyClientPixelScaling();
        Invalidate();
    }
}
