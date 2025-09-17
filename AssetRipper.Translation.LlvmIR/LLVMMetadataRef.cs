using AssetRipper.Translation.LlvmIR.Extensions;
using LLVMSharp.Interop;

namespace AssetRipper.Translation.LlvmIR;

public unsafe partial struct LLVMMetadataRef(IntPtr handle) : IEquatable<LLVMMetadataRef>
{
	public IntPtr Handle = handle;

	public static implicit operator LLVMMetadataRef(LLVMOpaqueMetadata* value) => new LLVMMetadataRef((IntPtr)value);

	public static implicit operator LLVMOpaqueMetadata*(LLVMMetadataRef value) => (LLVMOpaqueMetadata*)value.Handle;

	public static bool operator ==(LLVMMetadataRef left, LLVMMetadataRef right) => left.Handle == right.Handle;

	public static bool operator !=(LLVMMetadataRef left, LLVMMetadataRef right) => !(left == right);

	public override readonly bool Equals(object? obj) => (obj is LLVMMetadataRef other) && Equals(other);

	public readonly bool Equals(LLVMMetadataRef other) => this == other;

	public override readonly int GetHashCode() => Handle.GetHashCode();

	public override readonly string ToString() => Handle == default ? "null" : nameof(LLVMMetadataRef);

	public readonly LLVMMetadataKind Kind => Handle == default
		? (LLVMMetadataKind)(-1)
		: (LLVMMetadataKind)LLVM.GetMetadataKind(this);

	public readonly bool IsType => Kind switch
	{
		LLVMMetadataKind.LLVMDICompositeTypeMetadataKind => true,
		LLVMMetadataKind.LLVMDIDerivedTypeMetadataKind => true,
		LLVMMetadataKind.LLVMDIStringTypeMetadataKind => true,
		LLVMMetadataKind.LLVMDIBasicTypeMetadataKind => true,
		LLVMMetadataKind.LLVMDISubroutineTypeMetadataKind => true,
		_ => false,
	};

	public readonly bool IsTemplateParameter => Kind switch
	{
		LLVMMetadataKind.LLVMDITemplateTypeParameterMetadataKind => true,
		LLVMMetadataKind.LLVMDITemplateValueParameterMetadataKind => true,
		_ => false,
	};

	public readonly bool IsVariable => Kind switch
	{
		LLVMMetadataKind.LLVMDILocalVariableMetadataKind => true,
		LLVMMetadataKind.LLVMDIGlobalVariableMetadataKind => true,
		_ => false,
	};

	public readonly bool IsDINode => Kind is >= LLVMMetadataKind.LLVMDILocationMetadataKind and <= LLVMMetadataKind.LLVMDIAssignIDMetadataKind;

	public readonly uint AlignInBits => IsType
		? LLVM.DITypeGetAlignInBits(this)
		: default;

	public readonly LLVMMetadataRef BaseType => Kind switch
	{
		LLVMMetadataKind.LLVMDIDerivedTypeMetadataKind => LibLLVMSharp.DIDerivedTypeGetBaseType(this),
		LLVMMetadataKind.LLVMDICompositeTypeMetadataKind => LibLLVMSharp.DICompositeTypeGetBaseType(this),
		_ => default,
	};

	public readonly uint Column => Kind switch
	{
		LLVMMetadataKind.LLVMDILocationMetadataKind => LLVM.DILocationGetColumn(this),
		_ => default,
	};

	public readonly LLVMMetadataRef[] Elements => Kind switch
	{
		LLVMMetadataKind.LLVMDICompositeTypeMetadataKind => LibLLVMSharp.DICompositeTypeGetElements(this),
		_ => [],
	};

	public readonly uint Encoding => Kind switch
	{
		LLVMMetadataKind.LLVMDIDerivedTypeMetadataKind => LibLLVMSharp.DIDerivedTypeGetEncoding(this),
		_ => default,
	};

	public readonly LLVMMetadataRef Expression => Kind switch
	{
		LLVMMetadataKind.LLVMDIGlobalVariableExpressionMetadataKind => LLVM.DIGlobalVariableExpressionGetExpression(this),
		_ => default,
	};

	public readonly LLVMMetadataRef File => Kind switch
	{
		LLVMMetadataKind.LLVMDIFileMetadataKind => this,
		LLVMMetadataKind.LLVMDISubprogramMetadataKind => LLVM.DIScopeGetFile(this),
		_ when IsVariable => LLVM.DIVariableGetFile(this),
		_ when IsType => LLVM.DIScopeGetFile(this),
		_ => default,
	};

	public readonly LLVMDIFlags Flags => IsType
		? LLVM.DITypeGetFlags(this)
		: Kind is LLVMMetadataKind.LLVMDISubprogramMetadataKind
			? LibLLVMSharp.DISubprogramGetFlags(this)
			: default;

	public readonly string Identifier => Kind switch
	{
		LLVMMetadataKind.LLVMDICompositeTypeMetadataKind => LibLLVMSharp.DICompositeTypeGetIdentifier(this) ?? "",
		_ => "",
	};

	public readonly LLVMMetadataRef InlinedAt => Kind switch
	{
		LLVMMetadataKind.LLVMDILocationMetadataKind => LLVM.DILocationGetInlinedAt(this),
		_ => default,
	};

	public readonly uint Line => Kind switch
	{
		LLVMMetadataKind.LLVMDISubprogramMetadataKind => LLVM.DISubprogramGetLine(this),
		LLVMMetadataKind.LLVMDILocationMetadataKind => LLVM.DILocationGetLine(this),
		_ when IsType => LLVM.DITypeGetLine(this),
		_ when IsVariable => LLVM.DIVariableGetLine(this),
		_ => default,
	};

	public readonly string Name
	{
		get
		{
			if (IsType)
			{
				return LibLLVMSharp.DITypeGetName(this) ?? "";
			}
			else if (IsVariable)
			{
				return LibLLVMSharp.DIVariableGetName(this) ?? "";
			}
			else if (Kind == LLVMMetadataKind.LLVMDISubprogramMetadataKind)
			{
				return LibLLVMSharp.DISubprogramGetName(this) ?? "";
			}
			else
			{
				return "";
			}
		}
	}

	public readonly ulong OffsetInBits => IsType
		? LLVM.DITypeGetOffsetInBits(this)
		: default;

	public readonly LLVMMetadataRef Scope => Kind switch
	{
		LLVMMetadataKind.LLVMDILocationMetadataKind => LLVM.DILocationGetScope(this),
		_ when IsVariable => LLVM.DIVariableGetScope(this),
		_ => default,
	};

	public readonly ulong SizeInBits => IsType
		? LLVM.DITypeGetSizeInBits(this)
		: default;

	public readonly uint SPFlags => Kind is LLVMMetadataKind.LLVMDISubprogramMetadataKind
		? LibLLVMSharp.DISubprogramGetSPFlags(this)
		: default;

	public readonly ushort Tag => IsDINode
		? LLVM.GetDINodeTag(this)
		: default;

	public readonly string TagString => IsDINode
		? LibLLVMSharp.DINodeGetTagString(this) ?? ""
		: "";

	public readonly LLVMMetadataRef Type => Kind switch
	{
		LLVMMetadataKind.LLVMDILocalVariableMetadataKind => LibLLVMSharp.DIVariableGetType(this),
		LLVMMetadataKind.LLVMDIGlobalVariableMetadataKind => LibLLVMSharp.DIVariableGetType(this),
		LLVMMetadataKind.LLVMDISubprogramMetadataKind => LibLLVMSharp.DISubprogramGetType(this),
		_ when IsTemplateParameter => LibLLVMSharp.DITemplateParameterGetType(this),
		_ => default,
	};

	public readonly LLVMMetadataRef[] TypeArray => Kind switch
	{
		LLVMMetadataKind.LLVMDISubroutineTypeMetadataKind => LibLLVMSharp.DISubroutineTypeGetTypeArray(this),
		_ => [],
	};

	public readonly LLVMMetadataRef Value => Kind switch
	{
		LLVMMetadataKind.LLVMDITemplateValueParameterMetadataKind => LibLLVMSharp.DITemplateValueParameterGetValue(this),
		_ => default,
	};

	public readonly LLVMMetadataRef Variable => Kind switch
	{
		LLVMMetadataKind.LLVMDIGlobalVariableExpressionMetadataKind => LLVM.DIGlobalVariableExpressionGetVariable(this),
		_ => default,
	};
}
