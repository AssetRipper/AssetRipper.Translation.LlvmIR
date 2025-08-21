using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using System.Collections;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public sealed class BasicBlock : IReadOnlyList<Instruction>, IList<Instruction>
{
	public Instruction this[int index]
	{
		get => Instructions[index];
		set => Instructions[index] = value;
	}

	public CilInstructionLabel Label { get; } = new();
	public List<Instruction> Instructions { get; } = new();

	public int Count => Instructions.Count;

	bool ICollection<Instruction>.IsReadOnly => ((ICollection<Instruction>)Instructions).IsReadOnly;

	public void Add(Instruction item) => Instructions.Add(item);

	public void AddInstructions(CilInstructionCollection instructions)
	{
		int labelIndex = instructions.Count;

		int stackHeight = 0;
		foreach (Instruction instruction in Instructions)
		{
			Debug.Assert(stackHeight >= instruction.PopCount, "Stack underflow when adding instructions");
			instruction.AddInstructions(instructions);
			stackHeight += instruction.StackEffect;
		}
		Debug.Assert(stackHeight == 0, "Stack should be empty after adding instructions");

		if (instructions.Count > labelIndex)
		{
			Label.Instruction = instructions[labelIndex];
		}
		else
		{
			Label.Instruction = instructions.Add(CilOpCodes.Nop);
		}
	}

	public void Clear() => Instructions.Clear();

	public bool Contains(Instruction item)
	{
		return Instructions.Contains(item);
	}

	public void CopyTo(Instruction[] array, int arrayIndex)
	{
		Instructions.CopyTo(array, arrayIndex);
	}

	public IEnumerator<Instruction> GetEnumerator()
	{
		return Instructions.GetEnumerator();
	}

	public int IndexOf(Instruction item)
	{
		return Instructions.IndexOf(item);
	}

	public void Insert(int index, Instruction item)
	{
		Instructions.Insert(index, item);
	}

	public bool Remove(Instruction item)
	{
		return Instructions.Remove(item);
	}

	public void RemoveAt(int index)
	{
		Instructions.RemoveAt(index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return ((IEnumerable)Instructions).GetEnumerator();
	}
}
