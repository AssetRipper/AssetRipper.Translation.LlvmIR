using AsmResolver.DotNet.Signatures;
using AssetRipper.Translation.LlvmIR.Extensions;
using AssetRipper.Translation.LlvmIR.Variables;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR.Instructions;

public static class InstructionOptimizer
{
	public static void Optimize(IReadOnlyList<BasicBlock> basicBlocks)
	{
		HashSet<IVariable> temporaryVariables = GetTemporaryVariablesEligibleForRemoval(basicBlocks).ToHashSet();
		foreach (BasicBlock basicBlock in basicBlocks)
		{
			while (TryOptimize(basicBlock, temporaryVariables))
			{
				// Keep optimizing until no more optimizations can be made.
			}
		}
	}

	private static bool TryOptimize(BasicBlock basicBlock, HashSet<IVariable> temporaryVariables)
	{
		bool changed = false;
		for (int i = basicBlock.Count - 1; i >= 0; i--)
		{
			switch (basicBlock[i])
			{
				case LoadIndirectInstruction loadIndirect:
					{
						Debug.Assert(i > 0);
						switch (basicBlock[i - 1])
						{
							case AddressOfInstruction addressOf:
								if (addressOf.Variable.SupportsLoad && SignatureComparer.Default.Equals(addressOf.Variable.VariableType, loadIndirect.Type))
								{
									LoadVariableInstruction replacement = new(addressOf.Variable);
									basicBlock[i - 1] = replacement;
									basicBlock.RemoveAt(i);
									changed = true;
									i--;
								}
								break;
							case LoadFieldAddressInstruction loadFieldAddress:
								if (SignatureComparer.Default.Equals(loadFieldAddress.Field.Signature?.FieldType, loadIndirect.Type))
								{
									LoadFieldInstruction replacement = new(loadFieldAddress.Field);
									basicBlock[i - 1] = replacement;
									basicBlock.RemoveAt(i);
									changed = true;
									i--;
								}
								break;
						}
					}
					break;
				case StoreIndirectInstruction storeIndirect:
					{
						// Find the source address of the store.
						int stackHeight = 2;
						int index = i - 1;
						for (; index >= 0; index--)
						{
							Instruction current = basicBlock[index];
							if (current.StackHeightDependent)
							{
								break;
							}

							stackHeight -= current.PushCount;

							if (stackHeight == 0)
							{
								switch (current)
								{
									case AddressOfInstruction addressOf:
										if (addressOf.Variable.SupportsLoad && SignatureComparer.Default.Equals(addressOf.Variable.VariableType, storeIndirect.Type))
										{
											StoreVariableInstruction replacement = new(addressOf.Variable);
											basicBlock[i] = replacement;
											basicBlock.RemoveAt(index);
											changed = true;
											i--;
										}
										break;
									case LoadFieldAddressInstruction loadFieldAddress:
										if (SignatureComparer.Default.Equals(loadFieldAddress.Field.Signature?.FieldType, storeIndirect.Type))
										{
											StoreFieldInstruction replacement = new(loadFieldAddress.Field);
											basicBlock[i] = replacement;
											basicBlock.RemoveAt(index);
											changed = true;
											i--;
										}
										break;
								}
								break;
							}
							else if (stackHeight < 0)
							{
								break;
							}

							stackHeight += current.PopCount;
						}
					}
					break;
			}
		}
		return changed;
	}

	private static IEnumerable<IVariable> GetTemporaryVariablesEligibleForRemoval(IReadOnlyList<BasicBlock> basicBlocks)
	{
		HashSet<IVariable> variablesInMultipleBlocks = new();
		Dictionary<IVariable, BasicBlock> variableBlockMap = new();
		foreach (BasicBlock basicBlock in basicBlocks)
		{
			foreach (Instruction instruction in basicBlock.Instructions)
			{
				IVariable? variable = instruction switch
				{
					LoadVariableInstruction load => load.Variable,
					StoreVariableInstruction store => store.Variable,
					AddressOfInstruction addressOf => addressOf.Variable,
					InitializeInstruction initialize => initialize.Variable,
					_ => null,
				};

				if (variable is null or { IsTemporary: false } || variablesInMultipleBlocks.Contains(variable))
				{
				}
				else if (variableBlockMap.TryGetValue(variable, out BasicBlock? containingBlock))
				{
					if (containingBlock != basicBlock)
					{
						variablesInMultipleBlocks.Add(variable);
						variableBlockMap.Remove(variable);
					}
				}
				else
				{
					variableBlockMap[variable] = basicBlock;
				}
			}
		}
		return variableBlockMap.Keys;
	}
}
