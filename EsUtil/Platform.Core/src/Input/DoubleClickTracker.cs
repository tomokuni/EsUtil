using EsUtil.Platform.Core.Geometry;

namespace EsUtil.Platform.Core.Input;

/// <summary>続けて押された位置と時刻からダブルクリックを判定する（クリック回数を返さない UI 向け）。</summary>
/// <remarks>
/// <b>判定の規則を 1 か所に置く</b>: 入力の仕組みがクリック回数を返さない UI は、直前の押下からの時間と距離で
/// ダブルクリックを決める。 同じ規則を各 UI が書き写すと、時間・距離の値が UI ごとにずれる。<br/>
/// <b>座標はクライアント座標（DIP）</b>である。 位置は押下のたびに更新し、<see cref="Reset"/> で履歴を捨てる。
/// </remarks>
public sealed class DoubleClickTracker
{
    /// <summary>ダブルクリックと見なす最大の間隔（既定）。</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>ダブルクリックと見なす最大の移動距離（DIP。 既定）。</summary>
    public const double DefaultDistance = 4;

    /// <summary>ダブルクリックと見なす最大の間隔。</summary>
    private readonly TimeSpan _interval;

    /// <summary>ダブルクリックと見なす最大の移動距離（DIP）。</summary>
    private readonly double _distance;

    /// <summary>直前の押下を保持しているかどうか。</summary>
    private bool _hasLast;

    /// <summary>直前の押下の時刻。</summary>
    private DateTimeOffset _lastAt;

    /// <summary>直前の押下の位置（クライアント座標・DIP）。</summary>
    private EsPoint _lastPosition;

    /// <summary>ダブルクリックの判定を初期化する。</summary>
    /// <param name="interval">ダブルクリックと見なす最大の間隔。 null の場合は <see cref="DefaultInterval"/>。</param>
    /// <param name="distance">ダブルクリックと見なす最大の移動距離（DIP。 0 以上）。</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="interval"/> が 0 以下、または <paramref name="distance"/> が 0 未満の場合。</exception>
    public DoubleClickTracker(TimeSpan? interval = null, double distance = DefaultDistance)
    {
        var resolved = interval ?? DefaultInterval;
        if (resolved <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), resolved, "間隔は 0 より大きい必要があります。");
        }

        if (distance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(distance), distance, "距離は 0 以上である必要があります。");
        }

        _interval = resolved;
        _distance = distance;
    }

    /// <summary>押下を受け取り、直前の押下との関係からダブルクリックかどうかを返す。</summary>
    /// <param name="position">押下の位置（クライアント座標・DIP）。</param>
    /// <param name="now">押下の時刻。</param>
    /// <returns>直前の押下から設定した間隔以内かつ距離以内の場合は <see langword="true"/>。</returns>
    /// <remarks>判定のために直前の押下を常に更新する（3 回目の押下は 2 回目との関係で判定する）。</remarks>
    public bool Press(EsPoint position, DateTimeOffset now)
    {
        var isDoubleClick = _hasLast
            && now - _lastAt < _interval
            && Math.Abs(position.X - _lastPosition.X) < _distance
            && Math.Abs(position.Y - _lastPosition.Y) < _distance;

        _hasLast = true;
        _lastAt = now;
        _lastPosition = position;
        return isDoubleClick;
    }

    /// <summary>直前の押下の記録を捨てる（次の押下をダブルクリックと判定しない）。</summary>
    public void Reset() => _hasLast = false;
}
