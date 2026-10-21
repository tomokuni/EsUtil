using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>PROPVARIANT</c>（メタデータの値を 1 件受け取る）。</summary>
/// <remarks>
/// 型（<c>vt</c>）が先頭、値が 8 バイト目に置かれる（32/64 ビットで同じ）。
/// 数値の型だけを扱うため、解放が必要な値（文字列など）は受け取らない。
/// </remarks>
[StructLayout(LayoutKind.Explicit)]
internal struct WinPropVariant
{
    /// <summary>値の型（<c>VARTYPE</c>）。</summary>
    [FieldOffset(0)]
    public ushort Type;

    /// <summary>16 ビットの符号なし整数の値。</summary>
    [FieldOffset(8)]
    public ushort UnsignedShort;

    /// <summary>32 ビットの符号なし整数の値。</summary>
    [FieldOffset(8)]
    public uint UnsignedInt;
}
