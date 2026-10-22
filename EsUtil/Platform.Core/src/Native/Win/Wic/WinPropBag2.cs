using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win.Wic;

/// <summary><c>PROPBAG2</c>（<c>IPropertyBag2::Write</c> へ渡す 1 件分の指定）。</summary>
/// <remarks>
/// 並びは <c>ocidl.h</c> の定義順であり、位置は実行時に決まる（32/64 ビットで異なる）。
/// <b><c>IPropertyBag::Write</c> の <c>tagPROPBAG2</c> とは別の型である</b>（名前・値の型・形式・
/// クラス ID を持つ点が異なる）。
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct WinPropBag2
{
    /// <summary>種類（0 は既定）。</summary>
    public uint Type;

    /// <summary>値の型（<c>VARTYPE</c>）。</summary>
    public ushort ValueType;

    /// <summary>クリップボード形式（指定しない）。</summary>
    public ushort ClipFormat;

    /// <summary>ヒント（指定しない）。</summary>
    public uint Hint;

    /// <summary>オプションの名前。</summary>
    public IntPtr Name;

    /// <summary>対象のクラス（指定しない）。</summary>
    public Guid Clsid;
}
