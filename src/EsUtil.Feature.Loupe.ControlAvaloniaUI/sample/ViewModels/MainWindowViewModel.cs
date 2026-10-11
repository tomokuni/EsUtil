using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DrawingColor = System.Drawing.Color;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Imaging;
using EsUtil.Feature.Loupe.AvaloniaUI.Sample.Internal;
using EsUtil.Platform.Core.Geometry;
using EsUtil.Platform.Core.Imaging;

namespace EsUtil.Feature.Loupe.AvaloniaUI.Sample.ViewModels;

/// <summary>メインウィンドウのビューモデル。</summary>
/// <remarks>
/// テスト画像・ルーペの外観設定・中心座標を保持する。<br/>
/// マウス操作（PointerMoved 等）による中心座標の更新は View（コードビハインド）が行う。<br/>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    /// <summary>テスト画像の元（本 VM が生存する間保持する。 <see cref="SourceImage"/> のアダプタは本値を借用する）。</summary>
    private readonly EsBitmap _sourceBitmap = TestImageFactory.Create();

    /// <summary>テスト画像（ルーペの <c>Source</c> にバインドする。 元画像を借りて包む非所有アダプタ）。</summary>
    /// <remarks><see cref="IImage"/> は非所有の契約のため、元画像の破棄は本 VM（<see cref="_sourceBitmap"/>）が行う。</remarks>
    public IImage SourceImage => _sourceImage;

    /// <summary>表示用のアダプタ（コンストラクタで組み立てる。 フィールド初期化では <see cref="_sourceBitmap"/> を参照できない）。</summary>
    private readonly EsBitmapAvaloniaAdapter _sourceImage;

    /// <summary>メインウィンドウのビューモデルを生成する。</summary>
    public MainWindowViewModel() => _sourceImage = new EsBitmapAvaloniaAdapter(_sourceBitmap);

    /// <summary>十字線の色選択肢。</summary>
    public IReadOnlyList<BrushOption> CrossColorOptions { get; } = BrushOptions.All;

    /// <summary>画像外（ペーストボード）の色選択肢。</summary>
    public IReadOnlyList<BrushOption> PasteboardColorOptions { get; } = BrushOptions.All;

    /// <summary>枠線の色選択肢。</summary>
    public IReadOnlyList<BrushOption> BorderColorOptions { get; } = BrushOptions.All;

    // ====================================================================
    // 表示制御
    // ====================================================================

    private bool _isLoupeVisible = true;

    /// <summary>ルーペの表示状態（LoupeControl.IsVisible と TwoWay バインド）。</summary>
    public bool IsLoupeVisible
    {
        get => _isLoupeVisible;
        set => SetProperty(ref _isLoupeVisible, value);
    }

    // ====================================================================
    // ルーペ状態
    // ====================================================================

    private Point? _centerPoint;

    /// <summary>拡大中心（元画像ピクセル座標）。 画像の外では null。</summary>
    public Point? CenterPoint
    {
        get => _centerPoint;
        set
        {
            if (SetProperty(ref _centerPoint, value))
            {
                OnPropertyChanged(nameof(CoordText));
            }
        }
    }

    // ====================================================================
    // 外観設定
    // ====================================================================

    private int _zoomLevel = 4;

    /// <summary>拡大率（1〜16）。</summary>
    public int ZoomLevel
    {
        get => _zoomLevel;
        set
        {
            if (SetProperty(ref _zoomLevel, value))
            {
                OnPropertyChanged(nameof(ZoomText));
                OnPropertyChanged(nameof(CoordText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    private int _requestedSize = 240;

    /// <summary>ルーペの基本サイズ（DIP・ユーザー設定値）。 表示サイズはコントロール（LoupeControl）がクランプを内包して導出する。</summary>
    public int RequestedSize
    {
        get => _requestedSize;
        set
        {
            if (SetProperty(ref _requestedSize, value))
            {
                OnPropertyChanged(nameof(RequestedSizeText));
                OnPropertyChanged(nameof(CoordText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    // ====================================================================
    // 選択矩形の破線オーバーレイ（SelectionRectX / Y / Width / Height）
    // ====================================================================

    private bool _selectionRectVisible = true;

    /// <summary>選択矩形の破線オーバーレイを表示するかどうか（SelectionRectVisible）。</summary>
    /// <remarks>
    /// true にすると 選択矩形（<see cref="SelectionRectX"/> / <see cref="SelectionRectY"/> / <see cref="SelectionRectWidth"/> / <see cref="SelectionRectHeight"/>。 対象座標系 = 元画像ピクセル座標）の枠線を
    /// ルーペ画像上に破線で描画する（ズーム 2 倍以上で有効）。<br/>
    /// </remarks>
    public bool SelectionRectVisible
    {
        get => _selectionRectVisible;
        set
        {
            if (SetProperty(ref _selectionRectVisible, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectX = 100;

    /// <summary>選択矩形の X 座標（対象座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectX
    {
        get => _selectionRectX;
        set
        {
            if (SetProperty(ref _selectionRectX, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectY = 80;

    /// <summary>選択矩形の Y 座標（対象座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectY
    {
        get => _selectionRectY;
        set
        {
            if (SetProperty(ref _selectionRectY, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectWidth = 200;

    /// <summary>選択矩形の幅（対象座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectWidth
    {
        get => _selectionRectWidth;
        set
        {
            if (SetProperty(ref _selectionRectWidth, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    private double _selectionRectHeight = 120;

    /// <summary>選択矩形の高さ（対象座標系 = 元画像ピクセル）。</summary>
    public double SelectionRectHeight
    {
        get => _selectionRectHeight;
        set
        {
            if (SetProperty(ref _selectionRectHeight, value))
            {
                OnPropertyChanged(nameof(SelectionRectLabel));
            }
        }
    }

    /// <summary>選択矩形の座標ラベル（テスト画像上のオーバーレイ枠に表示）。</summary>
    public string SelectionRectLabel =>
        $"({SelectionRectX:F0}, {SelectionRectY:F0}) {SelectionRectWidth:F0}x{SelectionRectHeight:F0}";

    /// <summary>選択矩形（対象座標 = 元画像ピクセル座標）。</summary>
    /// <remarks>ルーペ画像上の破線（<c>LoupeControl.Rendered</c> で描く）の元データになる。</remarks>
    public EsRect SelectionRect => new(SelectionRectX, SelectionRectY, SelectionRectWidth, SelectionRectHeight);

    private int _borderThickness = 2;

    /// <summary>枠線の太さ（ピクセル）。</summary>
    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            if (SetProperty(ref _borderThickness, value))
            {
                OnPropertyChanged(nameof(BorderThicknessText));
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    private double _dipToDeviceScaling = 1.0;

    /// <summary>DIP→物理ピクセル変換のスケーリング係数。</summary>
    public double ClientToPixelScaling
    {
        get => _dipToDeviceScaling;
        set
        {
            if (SetProperty(ref _dipToDeviceScaling, value))
            {
                OnPropertyChanged(nameof(ClientToPixelScalingText));
            }
        }
    }

    // ====================================================================
    // 色
    // ====================================================================

    private BrushOption _crossColorOption = BrushOptions.Red;

    /// <summary>十字線の色選択。</summary>
    public BrushOption CrossColorOption
    {
        get => _crossColorOption;
        set
        {
            if (SetProperty(ref _crossColorOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    private BrushOption _pasteboardColorOption = BrushOptions.Pasteboard;

    /// <summary>画像外（ペーストボード）の色選択。</summary>
    public BrushOption PasteboardColorOption
    {
        get => _pasteboardColorOption;
        set
        {
            if (SetProperty(ref _pasteboardColorOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    private BrushOption _borderColorOption = BrushOptions.LightGray;

    /// <summary>枠線の色選択。</summary>
    public BrushOption BorderColorOption
    {
        get => _borderColorOption;
        set
        {
            if (SetProperty(ref _borderColorOption, value))
            {
                OnPropertyChanged(nameof(Options));
                OnPropertyChanged(nameof(Appearance));
            }
        }
    }

    /// <summary>ルーペの外観（色・枠線の太さ。 描画にだけ必要な設定）。</summary>
    /// <value>選択中の色と枠線の太さから組み立てた外観。 依存する選択肢の変更で再評価される。</value>
    /// <remarks>外観の既定値・型は描画の設定（<c>LoupeAppearance</c>）が単一所有する（コアは外観を持たない）。</remarks>
    public LoupeAppearance Appearance => new()
    {
        CrossColor = ToDrawingColor(CrossColorOption),
        PasteboardColor = ToDrawingColor(PasteboardColorOption),
        BorderColor = ToDrawingColor(BorderColorOption),
        BorderThickness = BorderThickness,
    };

    /// <summary>ルーペの設定（計算に必要な値。 コアの <see cref="LoupeOptions"/> が単一所有する）。</summary>
    /// <value>現在の設定（コントロールの <c>Options</c> へ一括でバインドする）。</value>
    /// <remarks>
    /// 設定を 1 つの値にまとめて公開する（コントロール側の公開プロパティも 1 つである）。<br/>
    /// 依存する値（拡大率・サイズ）の変更は各 setter が <c>Options</c> の変更を通知する。<br/>
    /// 色と枠線の太さはコアの設定に含めない（描画にだけ必要な設定のため、コントロールの <c>Appearance</c> へ与える）。
    /// </remarks>
    public LoupeOptions Options => new()
    {
        RequestedSize = RequestedSize,
        ZoomLevel = ZoomLevel,
    };

    /// <summary>色選択肢をコアの色型へ変換する（「なし」はアルファ 0 = 描画しない）。</summary>
    /// <param name="option">色選択肢。</param>
    /// <returns>コアの色。</returns>
    private static DrawingColor ToDrawingColor(BrushOption option)
        => option.Color is { } color ? DrawingColor.FromArgb(color.A, color.R, color.G, color.B) : DrawingColor.Transparent;

    // ====================================================================
    // 表示テキスト
    // ====================================================================

    /// <summary>拡大率の表示文字列。</summary>
    public string ZoomText => $"{ZoomLevel}x";

    /// <summary>ルーペサイズの表示文字列。</summary>
    public string RequestedSizeText => $"{RequestedSize} DIP";

    /// <summary>枠線の太さの表示文字列。</summary>
    public string BorderThicknessText => $"{BorderThickness} px";

    /// <summary>描画スケールの表示文字列。</summary>
    public string ClientToPixelScalingText => $"{ClientToPixelScaling:F2}";

    /// <summary>中心座標の表示文字列。</summary>
    /// <remarks>選択矩形の破線オーバーレイの表示状態（SelectionRectVisible）も付加する。</remarks>
    public string CoordText => CenterPoint is { } p
        ? $"中心座標: ({p.X:F0}, {p.Y:F0})  拡大率: {ZoomLevel}x  サイズ: {RequestedSize} DIP"
        + (SelectionRectVisible ? $"  選択矩形: ({SelectionRectX:F0}, {SelectionRectY:F0}) {SelectionRectWidth:F0}x{SelectionRectHeight:F0}" : "")
        : "画像の外です（画像外の色のみ表示）";
}
