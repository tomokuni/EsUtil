using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EsUtil.Feature.Marquee.AvaloniaUI.Sample.ViewModels;

/// <summary>メインウィンドウのビューモデル。</summary>
/// <remarks>
/// MarqueeControl の選択矩形（対象座標）と表示用の説明テキスト、操作ログ、<br/>
/// コントロールへ反映する外観・動作設定を保持する。<br/>
/// イベント購読による選択状態の同期とログの追加は View（コードビハインド）が行う。<br/>
/// </remarks>
public sealed class MainWindowViewModel : ObservableObject
{
    private string _statusText = "画像上をドラッグして選択してください。";

    /// <summary>操作ガイドと選択状態の表示テキスト。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _zoomPercent = 100;

    /// <summary>背景画像の拡大率（%）。</summary>
    public int ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            if (SetProperty(ref _zoomPercent, value))
            {
                // ZoomText は ZoomPercent から算出されるため変更を通知する
                OnPropertyChanged(nameof(ZoomText));
            }
        }
    }

    /// <summary>拡大率の表示文字列（例: 100%）。</summary>
    public string ZoomText => $"{ZoomPercent}%";

    private bool _hasSelection;

    /// <summary>選択矩形が存在するかどうか。</summary>
    public bool HasSelection
    {
        get => _hasSelection;
        set => SetProperty(ref _hasSelection, value);
    }

    private Rect _selectionRect;

    /// <summary>現在の選択矩形（対象座標）。</summary>
    public Rect SelectionSourceRect
    {
        get => _selectionRect;
        set => SetProperty(ref _selectionRect, value);
    }

    // ====================================================================
    // コントロール設定（View が MarqueeControl へ反映する）
    // ====================================================================

    /// <summary>色選択肢。</summary>
    public IReadOnlyList<ColorOption> ColorChoices { get; } = ColorOptions.All;

    private double _borderThickness = 2.0;

    /// <summary>枠線の太さ。</summary>
    public double BorderThickness
    {
        get => _borderThickness;
        set
        {
            if (SetProperty(ref _borderThickness, value))
            {
                OnPropertyChanged(nameof(BorderThicknessText));
            }
        }
    }

    private double _handleSize = 10.0;

    /// <summary>ハンドルのサイズ。</summary>
    public double HandleSize
    {
        get => _handleSize;
        set
        {
            if (SetProperty(ref _handleSize, value))
            {
                OnPropertyChanged(nameof(HandleSizeText));
            }
        }
    }

    private double _handleHitRadius = 12.0;

    /// <summary>ハンドル判定半径（DIP 座標）。</summary>
    public double HandleHitRadius
    {
        get => _handleHitRadius;
        set
        {
            if (SetProperty(ref _handleHitRadius, value))
            {
                OnPropertyChanged(nameof(HandleHitRadiusText));
            }
        }
    }

    private double _minSelectionSize = 4.0;

    /// <summary>最小選択サイズ（対象座標）。</summary>
    public double MinSelectionSize
    {
        get => _minSelectionSize;
        set
        {
            if (SetProperty(ref _minSelectionSize, value))
            {
                OnPropertyChanged(nameof(MinSelectionSizeText));
            }
        }
    }

    private bool _clampToSourceBounds;

    /// <summary>画像外の選択を禁止するかどうか（ON: 領域選択・移動・nudge を画像境界内にクリップ）。</summary>
    public bool ClampToSourceBounds
    {
        get => _clampToSourceBounds;
        set => SetProperty(ref _clampToSourceBounds, value);
    }

    private ColorOption _borderColorOption = ColorOptions.Blue;

    /// <summary>枠線の色選択。</summary>
    public ColorOption BorderColorOption
    {
        get => _borderColorOption;
        set => SetProperty(ref _borderColorOption, value);
    }

    private ColorOption _handleColorOption = ColorOptions.Blue;

    /// <summary>ハンドルの色選択。</summary>
    public ColorOption HandleColorOption
    {
        get => _handleColorOption;
        set => SetProperty(ref _handleColorOption, value);
    }

    private ColorOption _markerColorOption = ColorOptions.Blue;

    /// <summary>十字マーカー（nudge 位置表示）の色選択。</summary>
    public ColorOption MarkerColorOption
    {
        get => _markerColorOption;
        set => SetProperty(ref _markerColorOption, value);
    }

    private double _markerLength = 16.0;

    /// <summary>十字マーカー（nudge 位置表示）の長さ（全長・DIP 座標）。</summary>
    public double MarkerLength
    {
        get => _markerLength;
        set
        {
            if (SetProperty(ref _markerLength, value))
            {
                OnPropertyChanged(nameof(MarkerLengthText));
            }
        }
    }

    /// <summary>枠線の太さの表示文字列。</summary>
    public string BorderThicknessText => $"{BorderThickness:F1}";

    /// <summary>ハンドルサイズの表示文字列。</summary>
    public string HandleSizeText => $"{HandleSize:F1}";

    /// <summary>ハンドル判定半径の表示文字列。</summary>
    public string HandleHitRadiusText => $"{HandleHitRadius:F1}";

    /// <summary>最小選択サイズの表示文字列。</summary>
    public string MinSelectionSizeText => $"{MinSelectionSize:F1}";

    /// <summary>十字マーカーの長さの表示文字列。</summary>
    public string MarkerLengthText => $"{MarkerLength:F0}";

    // ====================================================================
    // 操作ログ
    // ====================================================================

    /// <summary>MarqueeControl の操作ログ表示行。</summary>
    public ObservableCollection<string> LogLines { get; } = [];

    /// <summary>ログ行数の表示文字列。</summary>
    public string LogCountText => $"{LogLines.Count} 行";

    /// <summary>操作ログを追加する（時分秒ミリ秒付き）。</summary>
    /// <param name="message">ログ本文。</param>
    /// <remarks>上限（500行）を超えた場合は古い行から削除してメモリを抑制する。</remarks>
    public void AddLog(string message)
    {
        LogLines.Add($"{DateTime.Now:HH:mm:ss.fff} {message}");
        if (LogLines.Count > 500)
        {
            LogLines.RemoveAt(0);
        }

        OnPropertyChanged(nameof(LogCountText));
    }

    /// <summary>操作ログをクリアする。</summary>
    public void ClearLog()
    {
        LogLines.Clear();
        OnPropertyChanged(nameof(LogCountText));
    }

    /// <summary>選択状態の説明テキストを更新する。</summary>
    /// <param name="hasSelection">選択矩形の有無。</param>
    /// <param name="rect">選択矩形（対象座標）。</param>
    public void UpdateSelection(bool hasSelection, Rect rect)
    {
        HasSelection = hasSelection;
        SelectionSourceRect = rect;

        if (!hasSelection)
        {
            StatusText = "選択がクリアされました。画像上をドラッグして選択してください。";
        }
        else
        {
            StatusText = $"選択矩形: X={rect.X:F0}, Y={rect.Y:F0}, 幅={rect.Width:F0}, 高さ={rect.Height:F0}（対象座標）";
        }
    }
}
