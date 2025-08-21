using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Instructions;
using AssetRipper.Translation.LlvmIR.Variables;
using NUnit.Framework;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal class OptimizationTests
{
	[Test]
	public void LoadIndirect()
	{
		TypeSignature int32 = CreateModule().CorLibTypeFactory.Int32;

		LocalVariable data = new(int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadIndirectInstruction(int32),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(data),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void StoreIndirect_1()
	{
		TypeSignature int32 = CreateModule().CorLibTypeFactory.Int32;

		LocalVariable data = new(int32);
		LocalVariable x = new(int32);
		LocalVariable y = new(int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreIndirectInstruction(int32),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(data),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void StoreIndirect_2()
	{
		TypeSignature int32 = CreateModule().CorLibTypeFactory.Int32;

		LocalVariable data = new(int32);
		LocalVariable x = new(int32);
		LocalVariable y = new(int32);
		LocalVariable z = new(int32);
		LocalVariable w = new(int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(z),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(w),
			new StoreIndirectInstruction(int32),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(z),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(w),
			new StoreVariableInstruction(data),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	private static void Optimize(BasicBlock instructions) => InstructionOptimizer.Optimize([instructions]);

	private static ModuleDefinition CreateModule() => new("TestModule");
}
