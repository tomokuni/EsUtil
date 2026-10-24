namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary>実行環境の WIC が書き出せる 1 つの形式（コンテナ 1 つと、その拡張子）。</summary>
/// <remarks>
/// <b>形式の一覧は OS から取る</b>（<see cref="WinWicFormats"/>）。 本型はその 1 件であり、
/// <b>拡張子は WIC のエンコーダーが申告したもの</b>である（同じ形式に複数の拡張子がある場合は、
/// 先頭を補完に使う）。
/// </remarks>
internal readonly record struct WinWicFormat
{
    /// <summary>コンテナ形式の GUID（WIC のエンコーダーが申告した値）。</summary>
    internal required Guid Container { get; init; }

    /// <summary>エンコーダーの表示名（例外メッセージに使う）。</summary>
    internal required string Name { get; init; }

    /// <summary>この形式の拡張子（小文字・先頭のドットを含む。 空にならない）。</summary>
    internal required IReadOnlyList<string> Extensions { get; init; }
}
