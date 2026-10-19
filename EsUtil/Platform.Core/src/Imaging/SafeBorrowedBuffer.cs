using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>外部が確保した画素領域を、解放のコールバック付きで保持する <see cref="SafeHandle"/>。</summary>
/// <remarks>
/// <b>領域の解放は行わない</b>（呼び出し側が渡した <c>release</c> を 1 回だけ呼ぶ）。 解放の実装を
/// <see cref="SafeHandle"/> へ委ねるのは、解放の経路を <c>Dispose</c> と GC の 1 か所に保つためである
/// （<see cref="SafeAlignedBuffer"/> と同じ方針。 自前のファイナライザを書かない）。<br/>
/// <b>解放のコールバックが null の場合</b>は何も解放しない（呼び出し側が自分のタイミングで解放する）。
/// </remarks>
internal sealed class SafeBorrowedBuffer : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>解放時に呼ぶ処理（null の場合は何もしない）。</summary>
    private readonly Action<nint>? _release;

    /// <summary>借用する領域を保持するハンドルを生成する。</summary>
    /// <param name="handle">借用する領域の先頭アドレス。</param>
    /// <param name="release">解放時に 1 回だけ呼ぶ処理（アドレスを受け取る）。</param>
    internal SafeBorrowedBuffer(nint handle, Action<nint>? release)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _release = release;
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        _release?.Invoke(handle);
        return true;
    }
}
