using EsUtil.Feature.Annotation.Logging;
using EsUtil.Platform.Core;
using EsUtil.Platform.Core.Geometry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EsUtil.Feature.Annotation;

/// <summary>注釈の操作（文書の生成・注釈の追加・保存形式）を UI 層・ホストへ提供するファサード。</summary>
/// <remarks>
/// <b>適用パターン</b>: Facade（コアの操作と提示層の入口を 1 つの窓口へ集約する）。<br/>
/// <b>責務</b>: 文書の生成、注釈の生成と追加、JSON の読み書きを行う。<br/>
/// 描画計画の解決は <see cref="Presenter"/>（<see cref="AnnotationPresenter"/>）が担う。 文書の値操作（差し替え・削除・並び順・<br/>
/// しっぽの移動）は <see cref="AnnotationDocument"/> が持ち、当たり判定は <see cref="AnnotationHitTest"/> が持つ<br/>
/// （ファサードは入口を集約するだけで、値の操作を写し取らない）。<br/>
/// <b>ログ</b>: 本クラスが定義（<c>AnnotationLogMessage</c>）の接頭辞と出力先を所有する。 定義はアセンブリに 1 つであり、<br/>
/// 提示層と共有する（層ごとに定義を持たない）。<br/>
/// <b>例外</b>: 公開ファサードのため、下位で発生した例外を記録して再スローする（<c>.github/例外処理規約.md</c> §4）。
/// </remarks>
public sealed class AnnotationCore(AnnotationPresenter? presenter = null)
{
    /// <summary>ログの出力先（未設定は <see cref="NullLogger"/>）。</summary>
    private ILogger _log = NullLogger.Instance;

    /// <summary>ログの接頭辞（未設定は null。 既定値は定義クラスが持つ）。</summary>
    private string? _logPrefix;

    /// <summary>生成の記録を出力済みかどうか（ロガーの初回設定時に 1 度だけ出力する）。</summary>
    private bool _createdLogged;

    /// <summary>描画計画を解決する提示層を取得する（アセンブリに 1 つ）。</summary>
    /// <value>本アセンブリの提示層。 ロガーと接頭辞は本クラスから伝搬する。</value>
    /// <remarks>
    /// UI 層が提示層を先に生成して設定を済ませる場合は、そのインスタンスをコンストラクターへ渡す<br/>
    /// （ファサードと UI が別々の提示層を持つと、設定が片方にしか効かない）。
    /// </remarks>
    public AnnotationPresenter Presenter { get; } = presenter ?? new AnnotationPresenter();

    /// <summary>ログの出力先を取得または設定する。</summary>
    /// <value>ロガー。 null を設定すると <see cref="NullLogger"/> になる。</value>
    /// <remarks>
    /// <b>生成の記録</b>: 本プロパティの初回の設定時にファサードと提示層の生成を 1 度だけ記録する<br/>
    /// （生成時は出力先が未設定のため、その場では記録できない。 2 回目以降の設定では繰り返さない）。<br/>
    /// 同じロガーを提示層（<see cref="Presenter"/>）へ渡す（コアと提示層は 1 つの定義を共有する）。
    /// </remarks>
    public ILogger Log
    {
        get => _log;
        set
        {
            _log = value ?? NullLogger.Instance;
            Presenter.Log = _log;
            LogFacadeCreatedOnce();
            _log.LogLoggerAttached();
        }
    }

    /// <summary>ログの接頭辞を取得または設定する（ロガーと一緒に設定する）。</summary>
    /// <value>接頭辞（例: <c>[Annotation]</c>）。 null または空白を設定すると既定値へ戻す。</value>
    /// <remarks>ログ定義（<see cref="AnnotationLogMessage"/>）の接頭辞を設定する（コアと提示層は同じ定義を共有する）。</remarks>
    public string? LogPrefix
    {
        get => _logPrefix;
        set
        {
            _logPrefix = value;
            AnnotationLogMessage.Prefix = string.IsNullOrWhiteSpace(value) ? AnnotationLogMessage.DefaultPrefix : value;
            Presenter.LogPrefix = value;
        }
    }

    /// <summary>注釈の文書を生成する。</summary>
    /// <param name="imageWidth">元画像の幅（ピクセル）。</param>
    /// <param name="imageHeight">元画像の高さ（ピクセル）。</param>
    /// <param name="imageFileName">元画像のファイル名（再編集の手掛かり）。 null 可。</param>
    /// <returns>注釈を持たない文書。</returns>
    public AnnotationDocument CreateDocument(int imageWidth, int imageHeight, string? imageFileName = null)
    {
        _log.LogDocumentCreated(imageWidth, imageHeight);
        return new(imageWidth, imageHeight, imageFileName);
    }

    /// <summary>枠線の注釈を生成する（文書へは追加しない）。</summary>
    /// <param name="document">対象の文書（画像の寸法を決める）。</param>
    /// <param name="bounds">枠線の矩形（元画像ピクセル）。</param>
    /// <param name="defaults">新規作成時の既定値。 null の場合は <see cref="AnnotationDraftDefaults.Default"/>。</param>
    /// <returns>画像の内側に収まった枠線。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    /// <remarks>矩形は <see cref="AnnotationGeometry.CreateFrame"/> が画像の内側へ収める（表示されない注釈を作らない）。</remarks>
    public FrameAnnotation CreateFrame(AnnotationDocument document, EsRect bounds, AnnotationDraftDefaults? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(document);

        var resolved = defaults ?? AnnotationDraftDefaults.Default;
        var frame = AnnotationGeometry.CreateFrame(bounds, resolved.FrameAppearance, document.ImageSize);
        _log.LogAnnotationAdded(AnnotationKind.Frame, frame.Id);
        return frame;
    }

    /// <summary>吹き出しの注釈を生成する（文書へは追加しない）。</summary>
    /// <param name="document">対象の文書（画像の寸法を決める）。</param>
    /// <param name="bounds">本文の矩形（元画像ピクセル）。</param>
    /// <param name="text">本文。</param>
    /// <param name="defaults">新規作成時の既定値。 null の場合は <see cref="AnnotationDraftDefaults.Default"/>。</param>
    /// <param name="tail">しっぽ。 null の場合は既定の辺・幅で作る。</param>
    /// <returns>画像の内側に収まった吹き出し。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> または <paramref name="text"/> が null の場合。</exception>
    /// <remarks>
    /// 矩形もしっぽの先端も <see cref="AnnotationGeometry.CreateBubble"/> が画像の内側へ収める
    /// （しっぽが画像の外へ出ると掴めなくなる）。
    /// </remarks>
    public BubbleAnnotation CreateBubble(
        AnnotationDocument document,
        EsRect bounds,
        RichText text,
        AnnotationDraftDefaults? defaults = null,
        BubbleTail? tail = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);

        var resolved = defaults ?? AnnotationDraftDefaults.Default;
        var bubble = AnnotationGeometry.CreateBubble(bounds, text, resolved, document.ImageSize, tail);
        _log.LogAnnotationAdded(AnnotationKind.Bubble, bubble.Id);
        return bubble;
    }

    /// <summary>注釈ファイルを読み込む。</summary>
    /// <param name="documentPath">注釈ファイルのパス。</param>
    /// <returns>読み込んだ文書。</returns>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> が空白の場合。</exception>
    /// <exception cref="IOException">ファイルを読めない場合。</exception>
    /// <exception cref="System.Text.Json.JsonException">JSON として解釈できない場合。</exception>
    /// <remarks>読み込んだ件数を <c>Information</c> で記録する（利用者の明示的な操作のため）。</remarks>
    public AnnotationDocument Load(string documentPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        try
        {
            var document = AnnotationJson.Load(documentPath);
            _log.LogDocumentLoaded(Path.GetFileName(documentPath), document.Items.Length);
            return document;
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(Load));
            throw;
        }
    }

    /// <summary>文書を注釈ファイルへ書き出す。</summary>
    /// <param name="documentPath">注釈ファイルのパス。</param>
    /// <param name="document">書き出す文書。</param>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> が空白の場合。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> が null の場合。</exception>
    /// <exception cref="IOException">ファイルへ書けない場合。</exception>
    /// <remarks>書き出した件数を <c>Information</c> で記録する（利用者の明示的な操作のため）。</remarks>
    public void Save(string documentPath, AnnotationDocument document)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentNullException.ThrowIfNull(document);

        try
        {
            AnnotationJson.Save(documentPath, document);
            _log.LogDocumentSaved(Path.GetFileName(documentPath), document.Items.Length);
        }
        catch (Exception ex) when (EsErrors.IsRecoverable(ex))
        {
            _log.LogOperationError(ex, nameof(Save));
            throw;
        }
    }

    /// <summary>ロガーが設定された時点で生成を 1 度だけ出力する。</summary>
    private void LogFacadeCreatedOnce()
    {
        if (_createdLogged)
        {
            return;
        }

        _createdLogged = true;
        _log.LogFacadeCreated();
    }
}
