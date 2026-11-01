using Aprillz.MewUI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using static EsUtil.Feature.ColumnPacking.ColumnPacker;

namespace EsUtil.Ui.MewUI.ColumnPacking;

/// <summary><b>垂直方向にアイテムを複数列配置し、高さを自動計算するカスタムパネル</b></summary>
/// <remarks>
/// Panel から継承し、ItemsSource とコードで直接追加された子要素の両方を同時に扱う機能を提供します。<br/>
/// ItemsControl の ItemsPanel として置き換え可能な互換性を提供します。
/// <br/>
/// 【特徴】<br/>
/// • DualSourceItemsPanel から継承（ItemsControl の ItemsPanel として使用可能）<br/>
/// • ColumnPacker.Solve() の結果を直接反映<br/>
/// • 行間・列間スペースを制御可能なプロパティ<br/>
/// • 3 種類のアルゴリズム（DynamicProgramming/BinarySearch/Greedy）をサポート<br/>
/// • レイアウト結果を読み取り専用のプロパティで公開<br/>
/// <br/>
/// 【計算フロー】<br/>
/// 1. MeasureContent：各子要素を測定し、最適レイアウトを計算<br/>
/// 2. ArrangeContent：計算結果に基づいて各子要素を配置<br/>
/// <br/>
/// 【最適化施策】<br/>
/// • Layout キャッシュで重複計算を削減し、配置のたびの再計算を回避<br/>
/// • 子要素の一覧を 1 度だけ取り出して反復し、列挙の割り当てを削減
/// </remarks>
public class ColumnPackingPanel : DualSourceItemsPanel
{
    /// <summary><b>行間スペースを指定するプロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 8.0 DIP<br/>
    /// </remarks>
    public static readonly MewProperty<double> RowSpaceProperty =
        MewProperty<double>.Register<ColumnPackingPanel>(
            nameof(RowSpace), 8.0, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnLayoutPropertyChanged(nameof(RowSpace), oldValue, newValue));

    /// <summary><b>列間スペースを指定するプロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 8.0 DIP<br/>
    /// </remarks>
    public static readonly MewProperty<double> ColumnSpaceProperty =
        MewProperty<double>.Register<ColumnPackingPanel>(
            nameof(ColumnSpace), 8.0, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnLayoutPropertyChanged(nameof(ColumnSpace), oldValue, newValue));

    /// <summary><b>最大列数を指定するプロパティ</b></summary>
    /// <remarks>
    /// デフォルト: 10<br/>
    /// </remarks>
    public static readonly MewProperty<int> ColumnLimitProperty =
        MewProperty<int>.Register<ColumnPackingPanel>(
            nameof(ColumnLimit), 10, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnLayoutPropertyChanged(nameof(ColumnLimit), oldValue, newValue));

    /// <summary><b>使用するアルゴリズムを指定するプロパティ</b></summary>
    /// <remarks>
    /// デフォルト: BinarySearch<br/>
    /// </remarks>
    public static readonly MewProperty<Method> MethodProperty =
        MewProperty<Method>.Register<ColumnPackingPanel>(
            nameof(Method), Method.BinarySearch, MewPropertyOptions.AffectsLayout,
            changed: static (panel, oldValue, newValue) => panel.OnLayoutPropertyChanged(nameof(Method), oldValue, newValue));

    /// <summary><b>使用された幅（DIP）を公開する読み取り専用プロパティ</b></summary>
    /// <remarks>
    /// 配置の計算の結果から自動更新されます。 読み取り専用です（値を書き換える入口はありません）。
    /// </remarks>
    private static readonly MewPropertyKey<double?> LayoutUsedWidthPropertyKey =
        MewProperty<double?>.RegisterReadOnly<ColumnPackingPanel>(nameof(LayoutUsedWidth), null);

    /// <summary><b>最小高さ（DIP）を公開する読み取り専用プロパティ</b></summary>
    /// <remarks>
    /// 配置の計算の結果から自動更新されます。 読み取り専用です（値を書き換える入口はありません）。
    /// </remarks>
    private static readonly MewPropertyKey<double?> LayoutMinHeightPropertyKey =
        MewProperty<double?>.RegisterReadOnly<ColumnPackingPanel>(nameof(LayoutMinHeight), null);

    /// <summary><b>計算されたレイアウトの列数を公開する読み取り専用プロパティ</b></summary>
    /// <remarks>
    /// 配置の計算の結果から自動更新されます。 読み取り専用です（値を書き換える入口はありません）。
    /// </remarks>
    private static readonly MewPropertyKey<int?> LayoutColumnCountPropertyKey =
        MewProperty<int?>.RegisterReadOnly<ColumnPackingPanel>(nameof(LayoutColumnCount), null);

    /// <summary><b>計算されたレイアウトの列セグメント情報を公開する読み取り専用プロパティ</b></summary>
    /// <remarks>
    /// 配置の計算の結果から自動更新されます。 読み取り専用です（値を書き換える入口はありません）。
    /// </remarks>
    private static readonly MewPropertyKey<string?> LayoutColumnSegmentsPropertyKey =
        MewProperty<string?>.RegisterReadOnly<ColumnPackingPanel>(nameof(LayoutColumnSegments), null);

    /// <summary><b>使用された幅のプロパティ</b></summary>
    /// <remarks>値の設定は本クラスだけが行います（読み取り専用）。</remarks>
    public static readonly MewProperty<double?> LayoutUsedWidthProperty = LayoutUsedWidthPropertyKey.Property;

    /// <summary><b>最小高さのプロパティ</b></summary>
    /// <remarks>値の設定は本クラスだけが行います（読み取り専用）。</remarks>
    public static readonly MewProperty<double?> LayoutMinHeightProperty = LayoutMinHeightPropertyKey.Property;

    /// <summary><b>列数のプロパティ</b></summary>
    /// <remarks>値の設定は本クラスだけが行います（読み取り専用）。</remarks>
    public static readonly MewProperty<int?> LayoutColumnCountProperty = LayoutColumnCountPropertyKey.Property;

    /// <summary><b>列セグメント情報のプロパティ</b></summary>
    /// <remarks>値の設定は本クラスだけが行います（読み取り専用）。</remarks>
    public static readonly MewProperty<string?> LayoutColumnSegmentsProperty = LayoutColumnSegmentsPropertyKey.Property;

    /// <summary>プロパティの変更を記録するロガー（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>生成の記録を出力済みかどうか（ロガーの初回設定で 1 度だけ出力する）。</summary>
    private bool _createdLogged;

    /// <summary>直近に記録したレイアウト結果の署名（同じ結果を繰り返し記録しないための比較キー）。</summary>
    private string? _lastLayoutSignature;

    /// <summary>直前に確定した幅の上限（DIP。 0 は未確定）。</summary>
    /// <remarks>
    /// <b>幅が確定しないパスがあるため保持する</b>: MewUI の <c>ScrollViewer</c> は内容を<br/>
    /// 「無限大の幅」と「表示領域の幅」の両方で測ることがあり、無限大のパスで別の結果を返すと<br/>
    /// 2 つのレイアウトが交互に現れて確定しない（実測 2026-11-01）。 無限大のときは直前の幅を使う。
    /// </remarks>
    private double _lastWidthLimit;

    /// <summary><b>最後に計算したレイアウト結果</b></summary>
    /// <value>直近の計算のインスタンス。 未計算・無効化済みの間は null。</value>
    /// <remarks>
    /// 配置の計算は幅の上限で変わるため、子・幅・設定のいずれかが変わったら破棄する。<br/>
    /// 派生クラスがレイアウト結果を読むための入口である。
    /// </remarks>
    protected ColumnPacker? Layout { get; set; }

    /// <summary><b>行間スペース（DIP）</b></summary>
    /// <value>行の間隔。 既定は 8.0 DIP。</value>
    public double RowSpace
    {
        get => GetValue(RowSpaceProperty);
        set => SetValue(RowSpaceProperty, value);
    }

    /// <summary><b>列間スペース（DIP）</b></summary>
    /// <value>列の間隔。 既定は 8.0 DIP。</value>
    public double ColumnSpace
    {
        get => GetValue(ColumnSpaceProperty);
        set => SetValue(ColumnSpaceProperty, value);
    }

    /// <summary><b>最大列数</b></summary>
    /// <value>列数の上限。 既定は 10。</value>
    public int ColumnLimit
    {
        get => GetValue(ColumnLimitProperty);
        set => SetValue(ColumnLimitProperty, value);
    }

    /// <summary><b>使用するアルゴリズム</b></summary>
    /// <value>配置の算法。 既定は <see cref="Method.BinarySearch"/>。</value>
    public Method Method
    {
        get => GetValue(MethodProperty);
        set => SetValue(MethodProperty, value);
    }

    /// <summary><b>使用された幅（DIP）</b></summary>
    /// <value>直前の配置で使った幅。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算（<c>ColumnPacker.Solve</c>）の結果から自動更新されます。 読み取り専用です。
    /// </remarks>
    public double? LayoutUsedWidth => GetValue(LayoutUsedWidthProperty);

    /// <summary><b>最小高さ（DIP）</b></summary>
    /// <value>直前の配置の最大列高さ（最小化された値）。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算（<c>ColumnPacker.Solve</c>）の結果から自動更新されます。 読み取り専用です。
    /// </remarks>
    public double? LayoutMinHeight => GetValue(LayoutMinHeightProperty);

    /// <summary><b>計算されたレイアウトの列数</b></summary>
    /// <value>直前の配置の列数。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算の列セグメントの長さから自動更新されます。 読み取り専用です。
    /// </remarks>
    public int? LayoutColumnCount => GetValue(LayoutColumnCountProperty);

    /// <summary><b>計算されたレイアウトの列セグメント情報</b></summary>
    /// <value>直前の配置の列の区切り（<c>[3,6,9]</c> 形式）。 未計算の間は null。</value>
    /// <remarks>
    /// 配置の計算の列セグメントから自動更新されます。 読み取り専用です。
    /// </remarks>
    public string? LayoutColumnSegments => GetValue(LayoutColumnSegmentsProperty);

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる（出力なし）。</value>
    /// <remarks>
    /// <b>頻度でレベルを分ける</b>: プロパティの変更は <c>Information</c>、レイアウト結果の変化は <c>Debug</c>、<br/>
    /// 項目ごとの座標は <c>Trace</c> で出力する（<c>.github/ログ規約.md</c> §2）。<br/>
    /// 初回の設定時に生成（初期設定の値）を 1 度だけ記録する。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            LogFacadeCreatedOnce();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[ColumnPacking]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>接頭辞は本層のログ定義（<see cref="ColumnPackingLogMessage"/>）が保持する（アセンブリで 1 つ）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ColumnPackingLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ColumnPackingLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary><b>測定をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合は (0, 0) を返す<br/>
    /// 2. 各子要素を無限サイズで測定（自然サイズ取得）<br/>
    /// 3. ColumnPacker.Solve() で最適レイアウトを計算<br/>
    /// 4. 計算結果から必要なサイズ（MinHeight, UsedWidth）を返す<br/>
    /// <br/>
    /// 【幅の扱い】<br/>
    /// 幅の上限が確定しないパス（無限大・0 以下）では、直前に確定した幅で計算する。<br/>
    /// 幅がまだ分からない最初のパスでは列数の制約が無いため 1 列（<c>SolveSingleColumnLayout</c>）として扱う。
    /// </remarks>
    /// <param name="availableSize">利用可能なサイズ</param>
    /// <returns>パネルが必要とするサイズ</returns>
    protected override Size MeasureContent(Size availableSize)
    {
        // 子要素がない場合は何も配置しない
        var childElements = Children;
        if (childElements.Count == 0)
        {
            return Size.Empty;
        }

        // 子要素を無限サイズで測定して自然サイズを取得する
        var infinity = new Size(double.PositiveInfinity, double.PositiveInfinity);
        for (var index = 0; index < childElements.Count; index++)
        {
            childElements[index].Measure(infinity);
        }

        // 測定結果からアイテムサイズを構築する
        var items = new (double Width, double Height)[childElements.Count];
        for (var index = 0; index < childElements.Count; index++)
        {
            var desiredSize = childElements[index].DesiredSize;
            items[index] = (desiredSize.Width, desiredSize.Height);
        }

        // ColumnPacking インスタンスを作成し、使用する算法を反映する
        Layout ??= new ColumnPacker(items, (RowSpace, ColumnSpace), ColumnLimit);
        Layout.CurrentMethod = Method;

        // 幅の上限を決める。 無限大・0 以下（幅が未確定のパス）は直前に確定した幅を使う
        var widthLimit = ResolveWidthLimit(availableSize.Width);

        // 幅がまだ分からない場合は列数の制約が無いため 1 列として扱う（幅の制約が無いホスト向け）
        if (!(widthLimit > 0))
        {
            var (singleUsedWidth, singleMinHeight) = Layout.SolveSingleColumnLayout();
            UpdateLayoutResultProperties();
            LogLayoutResultIfChanged([.. Layout.GetLastItemLayouts().Select(static layout => layout.Y)]);
            return new Size(singleUsedWidth, singleMinHeight);
        }

        _lastWidthLimit = widthLimit;

        // ColumnPacking で最適レイアウトを計算する
        var (usedWidth, minHeight) = Layout.Solve(widthLimit);

        // Layout 計算結果をプロパティへ反映する
        UpdateLayoutResultProperties();

        // レイアウト結果が変化したときだけ記録する（Debug。 項目ごとの座標は Trace）
        LogLayoutResultIfChanged([.. Layout.GetLastItemLayouts().Select(static layout => layout.Y)]);

        // 必要なサイズを返す（幅は実際に使った幅。 残りは親が決める）
        return new Size(usedWidth, minHeight);
    }

    /// <summary><b>配置をオーバーライドします</b></summary>
    /// <remarks>
    /// 【処理フロー】<br/>
    /// 1. 子要素がない場合は終了<br/>
    /// 2. MeasureContent で計算済みの結果をそのまま使う<br/>
    /// 3. GetLastItemLayouts() で各子要素の座標を取得し、Arrange() で配置する<br/>
    /// <br/>
    /// 【座標】<br/>
    /// 計算結果はパネルの左上を原点とする DIP であるため、割り当てられた矩形（ウィンドウ絶対座標）の左上を加算する。
    /// </remarks>
    /// <param name="bounds">本パネルへ割り当てられた矩形（ウィンドウ絶対座標・DIP）。</param>
    protected override void ArrangeContent(Rect bounds)
    {
        // 子要素がない場合またはレイアウト結果が無効な場合は何も配置しない
        if (Children.Count == 0 || Layout is null)
        {
            return;
        }

        // 計算結果から各子要素の座標とサイズを取得して配置する
        var childElements = Children;
        var itemLayouts = Layout.GetLastItemLayouts();
        for (var index = 0; index < itemLayouts.Count && index < childElements.Count; index++)
        {
            var layout = itemLayouts[index];
            childElements[index].Arrange(new Rect(
                bounds.X + layout.X,
                bounds.Y + layout.Y,
                layout.Width,
                layout.Height));
        }
    }

    /// <summary><b>ItemsSource が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を作り直した後に実行されます。
    /// </remarks>
    protected override void OnItemsSourceChangedInternal()
    {
        base.OnItemsSourceChangedInternal();

        // Layout キャッシュを無効化し、公開している結果も戻す
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemTemplate が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を作り直した後に実行されます。
    /// </remarks>
    protected override void OnItemTemplateChangedInternal()
    {
        base.OnItemTemplateChangedInternal();

        // Layout キャッシュを無効化し、公開している結果も戻す
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary><b>ItemsSourceFirst が変更されたときに呼び出されます</b></summary>
    /// <remarks>
    /// ベースクラスが子を並べ直した後に実行されます。
    /// </remarks>
    protected override void OnItemsSourceFirstChangedInternal()
    {
        base.OnItemsSourceFirstChangedInternal();

        // 並びが変わると位置も変わるため、Layout キャッシュを無効化する
        Layout = null;
        UpdateLayoutResultProperties();
    }

    /// <summary>ロガーが設定された時点で生成を 1 度だけ記録する。</summary>
    private void LogFacadeCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _log.LogFacadeCreated(ColumnLimit, RowSpace, ColumnSpace, Method.ToString());
    }

    /// <summary>配置に効くプロパティの変更を処理する。</summary>
    /// <param name="name">プロパティ名。</param>
    /// <param name="oldValue">変更前の値。</param>
    /// <param name="newValue">変更後の値。</param>
    /// <remarks>
    /// 数値は自然な小数表記（<c>0.#</c>）で整形する（同値の再設定では出力しない）。
    /// </remarks>
    private void OnLayoutPropertyChanged(string name, object? oldValue, object? newValue)
    {
        _log.LogSettingChanged(name, FormatSettingValue(oldValue), FormatSettingValue(newValue));

        // レイアウト キャッシュを無効化し、公開している結果も戻してから測り直す
        Layout = null;
        UpdateLayoutResultProperties();
        InvalidateMeasure();
    }

    /// <summary>プロパティの値をログ用の文字列へ整形する。</summary>
    /// <param name="value">整形する値。</param>
    /// <returns>不変カルチャの文字列。</returns>
    private static string FormatSettingValue(object? value) => value switch
    {
        null => "null",
        double number => ColumnPackingLogMessage.Format(number),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "null",
    };

    /// <summary>幅の上限を決める（確定しないパスでは直前に確定した幅を使う）。</summary>
    /// <param name="availableWidth">親から提示された幅。</param>
    /// <returns>幅の上限。 まだ分からない場合は 0。</returns>
    private double ResolveWidthLimit(double availableWidth)
        => !double.IsInfinity(availableWidth) && availableWidth > 0 ? availableWidth : _lastWidthLimit;

    /// <summary><b>Layout の結果をプロパティへ反映します</b></summary>
    /// <remarks>
    /// 配置の計算の直後、および計算結果を破棄したときに呼び出します。
    /// </remarks>
    private void UpdateLayoutResultProperties()
    {
        if (Layout is null)
        {
            SetValue(LayoutUsedWidthPropertyKey, null);
            SetValue(LayoutMinHeightPropertyKey, null);
            SetValue(LayoutColumnCountPropertyKey, null);
            SetValue(LayoutColumnSegmentsPropertyKey, null);
            return;
        }

        var result = Layout.GetLastResult();
        SetValue(LayoutUsedWidthPropertyKey, result.UsedWidth);
        SetValue(LayoutMinHeightPropertyKey, result.MinHeight);

        var segments = Layout.GetLastColumnSegments();
        SetValue(LayoutColumnCountPropertyKey, segments.Length);
        SetValue(LayoutColumnSegmentsPropertyKey, "[" + string.Join(",", segments.Select(static segment => segment.EndIdx)) + "]");
    }

    /// <summary>レイアウト結果が変化していれば記録する（同じ結果の繰り返しは記録しない）。</summary>
    /// <param name="itemY">項目ごとの Y 座標（<c>Trace</c> で使う。 整形は定義側で行う）。</param>
    private void LogLayoutResultIfChanged(IReadOnlyList<double> itemY)
    {
        if (Layout is null)
        {
            return;
        }

        var result = Layout.GetLastResult();
        var segments = Layout.GetLastColumnSegments();
        var segmentsText = "[" + string.Join(",", segments.Select(static segment => segment.EndIdx)) + "]";

        // 丸めた値で比較する（レイアウトの再計算で同じ結果が繰り返し届くため、変化したときだけ記録する）
        var signature = string.Join(
            "/",
            ColumnPackingLogMessage.Format(result.UsedWidth),
            ColumnPackingLogMessage.Format(result.MinHeight),
            segmentsText);
        if (string.Equals(signature, _lastLayoutSignature, StringComparison.Ordinal))
        {
            return;
        }

        _lastLayoutSignature = signature;
        _log.LogLayoutChanged(result.UsedWidth, result.MinHeight, segments.Length, segmentsText);
        _log.LogLayoutItems(itemY);
    }
}
