using System.Runtime.InteropServices;

namespace EsUtil.Platform.Core.Native.Win;

/// <summary><c>STATSTG</c>（ストリームの大きさを受け取る）。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct WinStatStg
{
    /// <summary>名前。</summary>
    public IntPtr Name;

    /// <summary>種類。</summary>
    public uint Type;

    /// <summary>大きさ（バイト）。</summary>
    public ulong Size;

    /// <summary>更新日時。</summary>
    public long ModifiedTime;

    /// <summary>作成日時。</summary>
    public long CreatedTime;

    /// <summary>アクセス日時。</summary>
    public long AccessedTime;

    /// <summary>開き方。</summary>
    public uint Mode;

    /// <summary>ロックの種類。</summary>
    public uint LocksSupported;

    /// <summary>クラス ID。</summary>
    public Guid Clsid;

    /// <summary>状態。</summary>
    public uint StateBits;

    /// <summary>予約。</summary>
    public uint Reserved;
}
