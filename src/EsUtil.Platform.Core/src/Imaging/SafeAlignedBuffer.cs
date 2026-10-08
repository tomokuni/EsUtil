using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace EsUtil.Platform.Core.Imaging;

/// <summary>整列したアンマネージド ピクセル バッファを所有する <see cref="SafeHandle"/>（解放の唯一の実装）。</summary>
/// <remarks>
/// <b>解放の担い手</b>: <see cref="ReleaseHandle"/> が唯一の解放実装であり、<c>Dispose</c> と
/// <see cref="SafeHandle"/> が持つファイナライザの両方から呼ばれる。 自前のファイナライザは書かない
/// （MA0055 が禁止する。 <see cref="SafeHandle"/> が同じ役割を果たし、解放の実装も 1 つに保てる）。<br/>
/// <b>ピクセルを持たない場合</b>（サイズ 0）はハンドルが 0 のままで、<see cref="ReleaseHandle"/> は呼ばれない。<br/>
/// <b>GC への通知</b>: 確保量を <see cref="GC.AddMemoryPressure"/> へ伝え、解放時に <see cref="GC.RemoveMemoryPressure"/> で戻す
/// （伝えないと GC がアンマネージドの量を認識できず、回収が遅れてメモリが膨らむ）。<br/>
/// <b>整列</b>: SIMD・GPU 転送の要件を満たすため 64 バイト整列で確保する（整列は基底アドレスのみに適用し、
/// 行のストライドは <c>幅 × 4</c> のままにする）。
/// </remarks>
internal sealed class SafeAlignedBuffer : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>確保したバイト数（<see cref="GC.AddMemoryPressure"/> へ渡した値。 解放時に同じ値を戻す）。</summary>
    private readonly long _byteCount;

    /// <summary>確保済みのハンドルを受け取る（確保は <see cref="Allocate"/> が行う）。</summary>
    /// <param name="handle">確保したアドレス（0 は未確保）。</param>
    /// <param name="byteCount">確保したバイト数（0 の場合は確保していない）。</param>
    private SafeAlignedBuffer(nint handle, long byteCount)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
        _byteCount = byteCount;

        if (byteCount > 0)
        {
            GC.AddMemoryPressure(byteCount);
        }
    }

    /// <summary>指定バイト数・整列でピクセル バッファを確保する。</summary>
    /// <param name="byteCount">確保するバイト数（0 以下の場合は確保しない）。</param>
    /// <param name="alignmentBytes">整列バイト数。</param>
    /// <returns>確保したバッファ。 バイト数が 0 以下の場合は無効なハンドル（<see cref="SafeHandle.IsInvalid"/>）。</returns>
    /// <exception cref="OutOfMemoryException">確保に失敗した場合（<see cref="NativeMemory.AlignedAlloc"/> が投げる）。</exception>
    public static unsafe SafeAlignedBuffer Allocate(long byteCount, int alignmentBytes)
        => byteCount <= 0
            ? new SafeAlignedBuffer(0, 0)
            : new SafeAlignedBuffer((nint)NativeMemory.AlignedAlloc((nuint)byteCount, (nuint)alignmentBytes), byteCount);

    /// <inheritdoc/>
    protected override unsafe bool ReleaseHandle()
    {
        NativeMemory.AlignedFree((void*)handle);

        if (_byteCount > 0)
        {
            GC.RemoveMemoryPressure(_byteCount);
        }

        return true;
    }
}
