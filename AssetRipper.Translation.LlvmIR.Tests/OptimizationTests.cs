using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Instructions;
using AssetRipper.Translation.LlvmIR.Variables;
using NUnit.Framework;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal class OptimizationTests
{
	public ModuleDefinition Module { get; private set; }
	public TypeSignature Int32 => Module.CorLibTypeFactory.Int32;

	[SetUp]
	public void SetUp()
	{
		Module = new("TestModule");
	}

	[Test]
	public void LoadIndirect()
	{
		ImportantVariable data = new(Int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadIndirectInstruction(Int32),
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
		ImportantVariable data = new(Int32);
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreIndirectInstruction(Int32),
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
		ImportantVariable data = new(Int32);
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);
		ImportantVariable z = new(Int32);
		ImportantVariable w = new(Int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(z),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(w),
			new StoreIndirectInstruction(Int32),
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

	[Test]
	public void Pop_1()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(data),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			PopInstruction.Instance,
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Pop_2()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
		];

		BasicBlock optimizedInstructions =
		[
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldBeRemovedIfItsTheOnlyInstruction()
	{
		LocalVariable data = new(Int32);

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
		];

		BasicBlock optimizedInstructions =
		[
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldNotBeRemovedIfAddressStoredBefore()
	{
		LocalVariable data = new(Int32);
		ImportantVariable pointer = new(Int32.MakePointerType());

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
			new InitializeInstruction(data),
		];

		BasicBlock optimizedInstructions =
		[
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
			new InitializeInstruction(data),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldNotBeRemovedIfAddressOccursAfter()
	{
		LocalVariable data = new(Int32);
		ImportantVariable pointer = new(Int32.MakePointerType());

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(data),
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldBeRemovedIfStoreOccursAfter_1()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(data),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(data),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldBeRemovedIfStoreOccursAfter_2()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);
		ImportantVariable pointer = new(Int32.MakePointerType());

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
			new AddressOfInstruction(data),
			new StoreVariableInstruction(pointer),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_ShouldNotBeRemovedIfLoadOccursAfter()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
			new LoadVariableInstruction(data),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(data),
			new LoadVariableInstruction(data),
			new StoreVariableInstruction(x),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Initialize_DoubleInitializationShouldBeRemoved()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new InitializeInstruction(data),
			new InitializeInstruction(data),
			new LoadVariableInstruction(data),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(data),
			new LoadVariableInstruction(data),
			new StoreVariableInstruction(x),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Temporary_1()
	{
		LocalVariable data = new(Int32);
		ImportantVariable x = new(Int32);
		ConstantI4 zero = new(0, Module);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(zero),
			new StoreVariableInstruction(data),
			new LoadVariableInstruction(data),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(zero),
			new StoreVariableInstruction(x),
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	[Test]
	public void Temporary_2()
	{
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);
		ImportantVariable z = new(Int32);
		ImportantVariable w = new(Int32);

		LocalVariable temp1 = new(Int32);
		LocalVariable temp2 = new(Int32);
		LocalVariable temp3 = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(z),
			new LoadVariableInstruction(w),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(temp2),
			new LoadVariableInstruction(temp1),
			new LoadVariableInstruction(temp2),
			Instruction.FromOpCode(CilOpCodes.Add),
			new StoreVariableInstruction(temp3),
			new LoadVariableInstruction(temp3),
			ReturnInstruction.Value,
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			new LoadVariableInstruction(z),
			new LoadVariableInstruction(w),
			Instruction.FromOpCode(CilOpCodes.Add),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		Optimize(instructions);

		Assert.That(instructions, Is.EqualTo(optimizedInstructions));
	}

	private static void Optimize(BasicBlock instructions) => InstructionOptimizer.Optimize([instructions]);

	private sealed class ImportantVariable(TypeSignature type) : LocalVariable(type), IVariable
	{
		bool IVariable.IsTemporary => false;
	}
}
