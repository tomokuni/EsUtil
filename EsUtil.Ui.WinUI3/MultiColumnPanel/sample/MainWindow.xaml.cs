using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;

namespace EsUtil.Ui.WinUI3.MultiColumn.Sample;

/// <summary>サンプルのメイン ウィンドウ（<see cref="MultiColumnPanel"/> の使い方を示す）。</summary>
/// <remarks>
/// <b>示すこと</b>: <see cref="DualSourceItemsPanel.ItemsSource"/>（データ）と直接の子（XAML に書いた要素）を
/// 同時に並べられること、<see cref="MultiColumnPanel.ColumnLimit"/> と余白で配置が変わること、
/// レイアウト結果（<see cref="MultiColumnPanel.LayoutMinHeight"/> など）が依存関係プロパティとして読めることです。<br/>
/// <b>項目の高さ</b>: 高さの違う項目を混ぜ、列ごとの高さの偏りが結果に出るようにしています。
/// </remarks>
public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    /// <summary>項目を生成するときの名前の一覧。</summary>
    private static readonly string[] s_labels =
    [
        "丸括弧", "角括弧", "波括弧", "引用符", "カンマ", "ピリオド",
        "コロン", "セミコロン", "不等号", "イコール", "プラス", "マイナス",
        "はてな", "感嘆符", "シャープ", "ドル", "パーセント", "アンパサンド",
        "アスタリスク", "スラッシュ",
    ];

    /// <summary>列の上限。</summary>
    private int _columnLimit = 4;

    /// <summary>行間（DIP）。</summary>
    private double _rowSpace = 8;

    /// <summary>列間（DIP）。</summary>
    private double _columnSpace = 8;

    /// <summary>並べる項目の数。</summary>
    private int _itemCount = 20;

    /// <summary>ItemsSource を直接の子より先に置くかどうか。</summary>
    private bool _itemsSourceFirst = true;

    /// <summary>メイン ウィンドウを初期化する。</summary>
    public MainWindow()
    {
        InitializeComponent();
        Items = CreateItems(_itemCount);
    }

    /// <summary>プロパティの変更を通知する。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>並べる項目（<see cref="DualSourceItemsPanel.ItemsSource"/> へ渡す）。</summary>
    public ObservableCollection<SampleItem> Items { get; private set; }

    /// <summary>列の上限を取得または設定する。</summary>
    public int ColumnLimit
    {
        get => _columnLimit;
        set => SetProperty(ref _columnLimit, value);
    }

    /// <summary>行間（DIP）を取得または設定する。</summary>
    public double RowSpace
    {
        get => _rowSpace;
        set => SetProperty(ref _rowSpace, value);
    }

    /// <summary>列間（DIP）を取得または設定する。</summary>
    public double ColumnSpace
    {
        get => _columnSpace;
        set => SetProperty(ref _columnSpace, value);
    }

    /// <summary>並べる項目の数を取得または設定する（変更すると <see cref="Items"/> を作り直す）。</summary>
    public int ItemCount
    {
        get => _itemCount;
        set
        {
            if (!SetProperty(ref _itemCount, value))
            {
                return;
            }

            Items = CreateItems(value);
            OnPropertyChanged(nameof(Items));
        }
    }

    /// <summary>ItemsSource を直接の子より先に置くかどうかを取得または設定する。</summary>
    public bool ItemsSourceFirst
    {
        get => _itemsSourceFirst;
        set => SetProperty(ref _itemsSourceFirst, value);
    }

    /// <summary>指定した件数の項目を作る（高さは 40〜96 DIP で巡回させる）。</summary>
    /// <param name="count">作る件数。</param>
    /// <returns>作った項目。</returns>
    private static ObservableCollection<SampleItem> CreateItems(int count)
    {
        var items = new ObservableCollection<SampleItem>();
        for (var index = 0; index < count; index++)
        {
            var label = string.Create(
                CultureInfo.InvariantCulture,
                $"{s_labels[index % s_labels.Length]} {index + 1}");
            items.Add(new SampleItem(label, 40 + ((index % 8) * 8)));
        }

        return items;
    }

    /// <summary>プロパティの値を設定し、変更を通知する。</summary>
    /// <typeparam name="T">プロパティの型。</typeparam>
    /// <param name="field">保持している値。</param>
    /// <param name="value">新しい値。</param>
    /// <param name="propertyName">プロパティ名。</param>
    /// <returns>値が変わった場合は true。</returns>
    private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>プロパティの変更を通知する。</summary>
    /// <param name="propertyName">プロパティ名。</param>
    private void OnPropertyChanged(string? propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}