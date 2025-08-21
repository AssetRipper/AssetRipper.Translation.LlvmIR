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
		LocalVariable data = new(Int32);

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
		LocalVariable data = new(Int32);
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
		LocalVariable data = new(Int32);
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
		];

		BasicBlock optimizedInstructions =
		[
			new LoadVariableInstruction(x),
			new StoreVariableInstruction(data),
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

	private static void Optimize(BasicBlock instructions) => InstructionOptimizer.Optimize([instructions]);

	private sealed class ImportantVariable(TypeSignature type) : LocalVariable(type), IVariable
	{
		bool IVariable.IsTemporary => false;
	}
}
