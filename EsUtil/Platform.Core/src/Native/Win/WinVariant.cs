using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>VARIANT</c>（<c>PROPBAG2</c> と対になる値）。</summary>
/// <remarks>型は先頭、値は 8 バイト目に置かれる（32/64 ビットで同じ）。</remarks>
[StructLayout(LayoutKind.Explicit)]
internal struct WinVariant
{
    /// <summary>値の型（<c>VARTYPE</c>）。</summary>
    [FieldOffset(0)]
    public ushort Type;

    /// <summary>単精度の実数の値。</summary>
    [FieldOffset(8)]
    public float Float;
}
