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
		changed |= RunPass_MergeIndirect(basicBlock);
		changed |= RunPass_EliminateUnnessaryInitialization(basicBlock, temporaryVariables);
		changed |= RunPass_RemoveUnnecessaryTemporaryVariables(basicBlock, temporaryVariables);
		return changed;
	}

	private static bool RunPass_MergeIndirect(BasicBlock basicBlock)
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
								if (addressOf.Variable.SupportsLoad && AreCompatible(addressOf.Variable.VariableType, loadIndirect.Type))
								{
									LoadVariableInstruction replacement = new(addressOf.Variable);
									basicBlock[i - 1] = replacement;
									basicBlock.RemoveAt(i);
									changed = true;
									i--;
								}
								break;
							case LoadFieldAddressInstruction loadFieldAddress:
								if (AreCompatible(loadFieldAddress.Field.Signature?.FieldType, loadIndirect.Type))
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
										if (addressOf.Variable.SupportsLoad && AreCompatible(addressOf.Variable.VariableType, storeIndirect.Type))
										{
											StoreVariableInstruction replacement = new(addressOf.Variable);
											basicBlock[i] = replacement;
											basicBlock.RemoveAt(index);
											changed = true;
											i--;
										}
										break;
									case LoadFieldAddressInstruction loadFieldAddress:
										if (AreCompatible(loadFieldAddress.Field.Signature?.FieldType, storeIndirect.Type))
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
				case PopInstruction:
					{
						Debug.Assert(i > 0);
						switch (basicBlock[i - 1])
						{
							case AddressOfInstruction:
							case LoadVariableInstruction:
							case LoadTokenInstruction:
								{
									basicBlock.RemoveRange(i - 1, 2);
									changed = true;
									i--;
								}
								break;
							case LoadFieldInstruction loadField:
								if (loadField.Field.IsStatic)
								{
									basicBlock.RemoveRange(i - 1, 2);
									changed = true;
									i--;
								}
								break;
							case LoadFieldAddressInstruction loadFieldAddress:
								if (loadFieldAddress.Field.IsStatic)
								{
									basicBlock.RemoveRange(i - 1, 2);
									changed = true;
									i--;
								}
								break;
						}
					}
					break;
			}
		}
		return changed;
	}

	private static bool RunPass_EliminateUnnessaryInitialization(BasicBlock basicBlock, HashSet<IVariable> temporaryVariables)
	{
		if (temporaryVariables.Count == 0)
		{
			return false;
		}

		bool changed = false;
		HashSet<IVariable> protectedVariables = new();
		for (int i = 0; i < basicBlock.Count; i++)
		{
			Instruction instruction = basicBlock[i];
			if (instruction is not InitializeInstruction initialize)
			{
				// Check backwards
				switch (instruction)
				{
					case StoreVariableInstruction store:
						if (temporaryVariables.Contains(store.Variable))
						{
							// If the variable has already been stored to, removing the initialization would change its value.
							protectedVariables.Add(store.Variable);
						}
						break;
					case AddressOfInstruction addressOf:
						if (temporaryVariables.Contains(addressOf.Variable))
						{
							// If the address of the variable is taken, we cannot remove the initialization.
							protectedVariables.Add(addressOf.Variable);
						}
						break;
				}
				continue;
			}

			if (!temporaryVariables.Contains(initialize.Variable))
			{
				continue;
			}

			if (protectedVariables.Contains(initialize.Variable))
			{
				continue;
			}

			bool shouldContinue = false;

			// Check forwards
			for (int j = i + 1; j < basicBlock.Count; j++)
			{
				bool shouldStop = false;
				switch (basicBlock[j])
				{
					case LoadVariableInstruction load:
						if (load.Variable == initialize.Variable)
						{
							// If the variable is loaded after initialization, we cannot remove the initialization.
							shouldContinue = true;
						}
						break;
					case StoreVariableInstruction store:
						if (store.Variable == initialize.Variable)
						{
							// The variable is stored to after initialization, so we can remove the initialization.
							shouldStop = true;
						}
						break;
					case AddressOfInstruction addressOf:
						if (addressOf.Variable == initialize.Variable)
						{
							// If the address of the variable is taken, we cannot remove the initialization.
							shouldContinue = true;
						}
						break;
					case InitializeInstruction previousInitialize:
						if (previousInitialize.Variable == initialize.Variable)
						{
							// The variable is stored to after initialization, so we can remove the initialization.
							shouldStop = true;
						}
						break;
				}

				if (shouldContinue || shouldStop)
				{
					break;
				}
			}

			if (shouldContinue)
			{
				continue;
			}

			basicBlock.RemoveAt(i);
			changed = true;
		}
		return changed;
	}

	private readonly record struct VariableUsage(int LoadCount, int StoreCount, int LoadIndex, int StoreIndex)
	{
		public static VariableUsage Default => new(0, 0, -1, -1);
		public bool CanBeRemoved => LoadCount == 1 && StoreCount == 1 && LoadIndex > StoreIndex;
		public VariableUsage WithLoad(int index) => this with { LoadCount = LoadCount + 1, LoadIndex = index };
		public VariableUsage WithStore(int index) => this with { StoreCount = StoreCount + 1, StoreIndex = index };
	}

	private static bool RunPass_RemoveUnnecessaryTemporaryVariables(BasicBlock basicBlock, HashSet<IVariable> temporaryVariables)
	{
		if (temporaryVariables.Count == 0)
		{
			return false;
		}

		// To prove that a pair of load/store instructions can be removed, we need to show that:
		// 1. The variable is only used in this block. (Guaranteed by temporaryVariables)
		// 2. The variable is only loaded and stored once.
		// 3. The variable store comes before the load.
		// 4. The stack height is the same before the store as it is after the load.
		// 5. There are no instructions between the store and load that depend on the exact stack height.
		// 6. If the store is removed, the value being stored will remain on the stack until the load is reached.

		bool changed = false;

		Dictionary<IVariable, VariableUsage> variableUsages = [];
		HashSet<IVariable> variablesWithAddressOrInitialization = [];
		List<int> stackHeightBarriers = [];
		int[] intraInstructionStackHeight = new int[basicBlock.Count];
		int stackHeight = 0;
		for (int i = 0; i < basicBlock.Count; i++)
		{
			Instruction instruction = basicBlock[i];

			// Track stack height
			intraInstructionStackHeight[i] = stackHeight - instruction.PopCount;
			stackHeight += instruction.StackEffect;

			if (instruction.StackHeightDependent)
			{
				stackHeightBarriers.Add(i);
			}

			switch (instruction)
			{
				case LoadVariableInstruction load:
					if (temporaryVariables.Contains(load.Variable) && !variablesWithAddressOrInitialization.Contains(load.Variable))
					{
						if (!variableUsages.TryGetValue(load.Variable, out VariableUsage usage))
						{
							usage = VariableUsage.Default;
						}
						variableUsages[load.Variable] = usage.WithLoad(i);
					}
					break;
				case StoreVariableInstruction store:
					if (temporaryVariables.Contains(store.Variable) && !variablesWithAddressOrInitialization.Contains(store.Variable))
					{
						if (!variableUsages.TryGetValue(store.Variable, out VariableUsage usage))
						{
							usage = VariableUsage.Default;
						}
						variableUsages[store.Variable] = usage.WithStore(i);
					}
					break;
				case AddressOfInstruction addressOf:
					if (temporaryVariables.Contains(addressOf.Variable))
					{
						variablesWithAddressOrInitialization.Add(addressOf.Variable);
						variableUsages.Remove(addressOf.Variable);
					}
					break;
				case InitializeInstruction initialize:
					if (temporaryVariables.Contains(initialize.Variable))
					{
						variablesWithAddressOrInitialization.Add(initialize.Variable);
						variableUsages.Remove(initialize.Variable);
					}
					break;
			}
		}

		List<int> indicesToRemove = [];
		List<IVariable> variableStoresToReplaceWithPop = [];
		foreach ((IVariable variable, VariableUsage usage) in variableUsages)
		{
			if (usage.LoadCount == 0)
			{
				// Variable is stored to but never loaded. Remove all stores.
				Debug.Assert(usage.StoreCount >= 1);
				Debug.Assert(usage.StoreIndex >= 0);

				basicBlock[usage.StoreIndex] = PopInstruction.Instance;
				if (usage.StoreCount > 1)
				{
					variableStoresToReplaceWithPop.Add(variable);
				}

				changed = true;

				continue;
			}

			// Check 2 and 3
			if (!usage.CanBeRemoved)
			{
				continue;
			}

			// Check 4
			if (intraInstructionStackHeight[usage.LoadIndex] != intraInstructionStackHeight[usage.StoreIndex])
			{
				continue;
			}

			if (usage.LoadIndex - usage.StoreIndex > 1)
			{
				// Check 5
				bool hasBarrier = false;
				foreach (int barrierIndex in stackHeightBarriers)
				{
					if (barrierIndex > usage.StoreIndex && barrierIndex < usage.LoadIndex)
					{
						hasBarrier = true;
						break;
					}
				}
				if (hasBarrier)
				{
					continue;
				}
				foreach (int index in indicesToRemove)
				{
					if (index > usage.StoreIndex && index < usage.LoadIndex)
					{
						hasBarrier = true;
						break;
					}
				}
				if (hasBarrier)
				{
					continue;
				}

				// Check 6
				bool valueWillRemainOnStack = true;
				int minimumStackHeight = intraInstructionStackHeight[usage.StoreIndex];
				for (int i = usage.StoreIndex + 1; i < usage.LoadIndex; i++)
				{
					if (intraInstructionStackHeight[i] < minimumStackHeight)
					{
						valueWillRemainOnStack = false;
						break;
					}
				}
				if (!valueWillRemainOnStack)
				{
					continue;
				}
			}

			// All checks passed, we can remove the load and store.
			indicesToRemove.Add(usage.LoadIndex);
			indicesToRemove.Add(usage.StoreIndex);
		}

		if (indicesToRemove.Count > 0)
		{
			indicesToRemove.Sort();
			for (int i = indicesToRemove.Count - 1; i >= 0; i--)
			{
				basicBlock.RemoveAt(indicesToRemove[i]);
			}
			changed = true;
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

	private static bool AreCompatible(TypeSignature? type1, TypeSignature? type2)
	{
		if (type1 is PointerTypeSignature)
		{
			return type2 is PointerTypeSignature;
		}
		return SignatureComparer.Default.Equals(type1, type2);
	}
}
