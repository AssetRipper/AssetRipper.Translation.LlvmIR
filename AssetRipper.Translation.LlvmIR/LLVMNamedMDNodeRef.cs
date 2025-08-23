using LLVMSharp.Interop;
using System.Text;

namespace AssetRipper.Translation.LlvmIR;

public unsafe partial struct LLVMNamedMDNodeRef(IntPtr handle) : IEquatable<LLVMNamedMDNodeRef>
{
	public IntPtr Handle = handle;

	public static implicit operator LLVMNamedMDNodeRef(LLVMOpaqueNamedMDNode* value) => new LLVMNamedMDNodeRef((IntPtr)value);

	public static implicit operator LLVMOpaqueNamedMDNode*(LLVMNamedMDNodeRef value) => (LLVMOpaqueNamedMDNode*)value.Handle;

	public static bool operator ==(LLVMNamedMDNodeRef left, LLVMNamedMDNodeRef right) => left.Handle == right.Handle;

	public static bool operator !=(LLVMNamedMDNodeRef left, LLVMNamedMDNodeRef right) => !(left == right);

	public override readonly bool Equals(object? obj) => (obj is LLVMNamedMDNodeRef other) && Equals(other);

	public readonly bool Equals(LLVMNamedMDNodeRef other) => this == other;

	public override readonly int GetHashCode() => Handle.GetHashCode();

	public override readonly string ToString() => $"{nameof(LLVMNamedMDNodeRef)}: {Handle:X}";

	public readonly string Name
	{
		get
		{
			nuint nameLength = 0;
			sbyte* namePtr = LLVM.GetNamedMetadataName(this, &nameLength);
			if (namePtr == null || nameLength == 0)
			{
				return "";
			}

			return new string(namePtr, 0, (int)nameLength, Encoding.UTF8);
		}
	}
}
