using EsUtil.Feature.Thumbnail.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Feature.Thumbnail;

/// <summary>一覧の選択（単一選択）の状態。</summary>
/// <remarks>
/// <b>選択の規則を 1 か所に置く</b>: 「選択できるのは 1 件だけ」「範囲外の添字は選択しない」という判断を<br/>
/// UI ごとに書くと、UI によって選択の残り方が変わる（一覧を組み直したとき・項目が減ったとき）。<br/>
/// <br/>
/// <b>本クラスは UI フレームワークに依存しない</b>: 変更は <see cref="Changed"/> で通知し、<br/>
/// 見た目の切り替えは UI が行う。<br/>
/// <br/>
/// <b>選択の変更は本クラスが記録する</b>: 値を所有する主体が記録する規則（ログ規約 §6）により、<br/>
/// UI 層は選択のログを重複して出力しない（<see cref="Log"/> を設定するだけ）。
/// </remarks>
public sealed class ThumbnailSelection
{
    /// <summary>選択中の添字。</summary>
    private int _index = -1;

    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>選択が変わったときに発生する。</summary>
    /// <remarks>同じ添字を再設定した場合は発生しない（無駄な再描画を起こさない）。</remarks>
    public event EventHandler? Changed;

    /// <summary>選択中の添字を取得する。</summary>
    /// <value>項目の添字。 選択が無い場合は -1。</value>
    public int Index => _index;

    /// <summary>選択があるかどうかを取得する。</summary>
    /// <value>選択中の項目がある場合は true。</value>
    public bool HasSelection => _index >= 0;

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>選択の変更は本クラスが記録する（ホストはロガーを設定するだけでよい）。</remarks>
    public ILogger Log
    {
        get => _log;
        set => _log = value ?? NullLogger.Instance;
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Thumbnail]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>本層のログ定義（<see cref="ThumbnailLogMessage"/>）の接頭辞を設定する（本層は最下層のため伝搬先は無い）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            ThumbnailLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? ThumbnailLogMessage.DefaultPrefix : value;
        }
    }

    /// <summary>選択を設定する。</summary>
    /// <param name="index">選択する項目の添字。 負の値は選択の解除として扱う。</param>
    /// <param name="count">項目数。 範囲外の添字は選択の解除として扱う。</param>
    /// <returns>選択が変わった場合は true。</returns>
    public bool Select(int index, int count)
    {
        var next = index >= 0 && index < count ? index : -1;
        if (next == _index)
        {
            return false;
        }

        var previous = _index;
        _index = next;
        _log.LogSelectionChanged(previous, next);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>選択を解除する。</summary>
    /// <returns>選択が変わった場合は true。</returns>
    public bool Clear() => Select(-1, int.MaxValue);
}
