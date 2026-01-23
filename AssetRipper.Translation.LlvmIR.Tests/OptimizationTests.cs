using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.Translation.LlvmIR.Instructions;
using AssetRipper.Translation.LlvmIR.Variables;

namespace AssetRipper.Translation.LlvmIR.Tests;

internal class OptimizationTests
{
	public ModuleDefinition Module { get; private set; } = null!;
	public TypeSignature Void => Module.CorLibTypeFactory.Void;
	public TypeSignature Int8 => Module.CorLibTypeFactory.SByte;
	public TypeSignature Int32 => Module.CorLibTypeFactory.Int32;
	public TypeSignature Int64 => Module.CorLibTypeFactory.Int64;
	public TypeSignature Single => Module.CorLibTypeFactory.Single;
	public TypeSignature Double => Module.CorLibTypeFactory.Double;

	[Before(HookType.Test)]
	public void SetUp()
	{
		Module = new("TestModule");
	}

	[Test]
	public async Task LoadIndirect()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreIndirect_1()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreIndirect_2()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreIndirect_3()
	{
		ImportantVariable data = new(Int64);
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(x),
			new StoreIndirectInstruction(Int32),
		];

		BasicBlock optimizedInstructions =
		[
			new AddressOfInstruction(data),
			new LoadVariableInstruction(x),
			new StoreIndirectInstruction(Int32),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreZero_Int32()
	{
		ImportantVariable x = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(new ConstantI4(0, Module)),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreZero_Int8()
	{
		ImportantVariable x = new(Int8);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(new ConstantI4(0, Module)),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreZero_Int64()
	{
		ImportantVariable x = new(Int64);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(new ConstantI8(0, Module)),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreZero_Single()
	{
		ImportantVariable x = new(Single);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(new ConstantR4(0, Module)),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task StoreZero_Double()
	{
		ImportantVariable x = new(Double);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(new ConstantR8(0, Module)),
			new StoreVariableInstruction(x),
		];

		BasicBlock optimizedInstructions =
		[
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Pop_1()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Pop_2()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldBeRemovedIfItsTheOnlyInstruction()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldNotBeRemovedIfAddressStoredBefore()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldNotBeRemovedIfAddressOccursAfter()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldWorkOnMultipleVariables()
	{
		LocalVariable data1 = new(Int32);
		LocalVariable data2 = new(Int32);

		BasicBlock instructions =
		[
			new InitializeInstruction(data1),
			new InitializeInstruction(data2),
		];

		BasicBlock optimizedInstructions =
		[
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldBeRemovedIfStoreOccursAfter_1()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldBeRemovedIfStoreOccursAfter_2()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldNotBeRemovedIfLoadOccursAfter()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_DoubleInitializationShouldBeRemoved()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Initialize_ShouldNotRemoveAcrossBasicBlocksWithoutTwoStores()
	{
		LocalVariable temp = new(Int32);

		BasicBlock instructions1 =
		[
			new InitializeInstruction(temp),
		];

		BasicBlock instructions2 =
		[
			new LoadVariableInstruction(temp),
			ReturnInstruction.Value,
		];

		BasicBlock optimizedInstructions1 =
		[
			new InitializeInstruction(temp),
		];

		BasicBlock optimizedInstructions2 =
		[
			new LoadVariableInstruction(temp),
			ReturnInstruction.Value,
		];

		Optimize(instructions1, instructions2);

		using (Assert.Multiple())
		{
			await Assert.That(instructions1).IsEquivalentTo(optimizedInstructions1);
			await Assert.That(instructions2).IsEquivalentTo(optimizedInstructions2);
		}
	}

	[Test]
	public async Task Temporary_1()
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
			new InitializeInstruction(x),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Temporary_2()
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

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Temporary_3()
	{
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);

		LocalVariable temp1 = new(Int32);
		LocalVariable temp2 = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(y),
			new StoreVariableInstruction(temp2),
			new LoadVariableInstruction(temp2),
			new LoadVariableInstruction(temp1),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(y),
			new LoadVariableInstruction(temp1),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Temporary_4()
	{
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);

		LocalVariable temp1 = new(Int32);
		LocalVariable temp2 = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(y),
			new StoreVariableInstruction(temp2),
			new LoadVariableInstruction(temp1),
			new LoadVariableInstruction(temp2),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			Instruction.FromOpCode(CilOpCodes.Add),
			ReturnInstruction.Value,
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions);
	}

	[Test]
	public async Task Temporary_5()
	{
		ImportantVariable x = new(Int32);
		ImportantVariable y = new(Int32);
		ImportantVariable z = new(Int32);
		ImportantVariable w = new(Int32);

		LocalVariable temp1 = new(Int32);
		LocalVariable temp2 = new(Int32);

		BasicBlock instructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(y),
			new StoreVariableInstruction(temp2),
			new LoadVariableInstruction(temp1),
			new StoreVariableInstruction(z),
			new LoadVariableInstruction(temp2),
			new StoreVariableInstruction(w),
		];

		// Both of these are semantically equivalent to the original, so either is valid.
		BasicBlock optimizedInstructions_1 =
		[
			new LoadVariableInstruction(x),
			new LoadVariableInstruction(y),
			new StoreVariableInstruction(temp2),
			new StoreVariableInstruction(z),
			new LoadVariableInstruction(temp2),
			new StoreVariableInstruction(w),
		];
		BasicBlock optimizedInstructions_2 =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(temp1),
			new LoadVariableInstruction(y),
			new LoadVariableInstruction(temp1),
			new StoreVariableInstruction(z),
			new StoreVariableInstruction(w),
		];

		Optimize(instructions);

		await Assert.That(instructions).IsEquivalentTo(optimizedInstructions_1).Or.IsEquivalentTo(optimizedInstructions_2);
	}

	private static void Optimize(BasicBlock instructions) => InstructionOptimizer.Optimize([instructions]);

	private static void Optimize(params IReadOnlyList<BasicBlock> basicBlocks) => InstructionOptimizer.Optimize(basicBlocks);

	private sealed class ImportantVariable(TypeSignature type) : LocalVariable(type), IVariable
	{
		bool IVariable.IsTemporary => false;
	}
}
