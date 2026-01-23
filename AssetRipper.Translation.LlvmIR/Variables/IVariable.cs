using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AssetRipper.CIL;

namespace AssetRipper.Translation.LlvmIR.Variables;

public interface IVariable
{
	/// <summary>
	/// Can this variable be eliminated during optimization?
	/// </summary>
	bool IsTemporary => false;
	bool SupportsLoad => true;
	bool SupportsStore => true;
	bool SupportsLoadAddress => false;
	TypeSignature VariableType { get; }
	void AddLoad(CilInstructionCollection instructions);
	void AddStore(CilInstructionCollection instructions);
	void AddStoreDefault(CilInstructionCollection instructions)
	{
		if (SupportsStore)
		{
			instructions.AddDefaultValue(VariableType);
			AddStore(instructions);
		}
		else if (SupportsLoadAddress)
		{
			AddLoadAddress(instructions);
			instructions.AddDefaultValue(VariableType);
			instructions.AddStoreIndirect(VariableType);
		}
		else
		{
			throw new NotSupportedException("Store default is not supported for this variable type.");
		}
	}
	void AddLoadAddress(CilInstructionCollection instructions)
	{
		throw new NotSupportedException("Load address is not supported for this variable type.");
	}
}
