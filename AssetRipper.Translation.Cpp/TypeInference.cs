using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AssetRipper.Translation.Cpp.Extensions;
using AssetRipper.Translation.Cpp.Instructions;
using Google.OrTools.Sat;
using LLVMSharp.Interop;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AssetRipper.Translation.Cpp;

internal static class TypeInference
{
	public static void Infer(ModuleContext moduleContext)
	{
		TypeSignatureCache cache = new();

		foreach (FunctionContext function in moduleContext.Methods.Values)
		{
			cache.MaybeAdd(moduleContext.GetTypeSignature(function.ReturnType));
			foreach (LLVMValueRef parameter in function.Parameters)
			{
				cache.MaybeAdd(moduleContext.GetTypeSignature(parameter.TypeOf));
			}
			foreach (InstructionContext instruction in function.Instructions)
			{
				cache.MaybeAdd(instruction.ResultTypeSignature);
				cache.MaybeAdd(instruction.SecondaryTypeSignature);
			}
		}

		cache.AddCorLibTypes(moduleContext.Definition);

		cache.AddPointerTypes();

		CpModel model = new();

		LinearExprBuilder objective = LinearExpr.NewBuilder();

		Dictionary<FunctionContext, IntVar> returnVariables = new();
		Dictionary<(FunctionContext, int), IntVar> parameterVariables = new();

		foreach (FunctionContext function in moduleContext.Methods.Values)
		{
			// Function return
			if (function.ReturnType.Kind == LLVMTypeKind.LLVMVoidTypeKind)
			{
				IntVar returnVariable = cache.MakeIntVar(model, moduleContext.Definition.CorLibTypeFactory.Void);
				returnVariables.Add(function, returnVariable);
			}
			else
			{
				IntVar returnVariable = cache.MakeIntVar(model, null, function.ReturnType);
				returnVariables.Add(function, returnVariable);
			}

			for (int i = 0; i < function.Parameters.Length; i++)
			{
				IntVar parameterVariable = cache.MakeIntVar(model, null, function.Parameters[i].TypeOf);
				parameterVariables.Add((function, i), parameterVariable);
			}
		}

		Dictionary<InstructionContext, IntVar> resultVariables = new();
		Dictionary<(InstructionContext, int), IntVar> operandVariables = new();

		foreach (FunctionContext function in moduleContext.Methods.Values)
		{
			foreach (InstructionContext instruction in function.Instructions)
			{
				IntVar resultVariable = cache.MakeIntVar(model, instruction.ResultTypeSignature, instruction.Instruction.TypeOf);
				resultVariables.Add(instruction, resultVariable);

				for (int i = 0; i < instruction.Operands.Length; i++)
				{
					IntVar operandVariable = cache.MakeIntVar(model);
					operandVariables.Add((instruction, i), operandVariable);
				}
			}
		}

		Dictionary<InstructionContext, IntVar> secondaryVariables = new();

		foreach (FunctionContext function in moduleContext.Methods.Values)
		{
			IntVar returnVariable = returnVariables[function];

			foreach (InstructionContext instruction in function.Instructions)
			{
				IntVar resultVariable = resultVariables[instruction];

				for (int i = 0; i < instruction.Operands.Length; i++)
				{
					IntVar operandVariable = operandVariables[(instruction, i)];
					LLVMValueRef operand = instruction.Operands[i];
					switch (operand.Kind)
					{
						case LLVMValueKind.LLVMInstructionValueKind:
							{
								IntVar other = resultVariables[function.InstructionLookup[operand]];

								BoolVar condition = (operandVariable == other).ToBoolean(model);
								objective.AddCondtional(condition, 1);
							}
							break;
						case LLVMValueKind.LLVMArgumentValueKind:
							{
								int parameterIndex = Array.IndexOf(function.Parameters, operand);
								IntVar parameterVariable = parameterVariables[(function, parameterIndex)];

								BoolVar condition = (operandVariable == parameterVariable).ToBoolean(model);
								objective.AddCondtional(condition, 1);
							}
							break;
					}
				}

				switch (instruction)
				{
					case UnaryMathInstructionContext:
						{
							model.Add(resultVariable == operandVariables[(instruction, 0)]);
						}
						break;
					case BinaryMathInstructionContext:
						{
							model.Add(resultVariable == operandVariables[(instruction, 0)]);
							model.Add(resultVariable == operandVariables[(instruction, 1)]);
							// The operands are also equal by transitivity.
						}
						break;
					case AllocaInstructionContext:
						{
							IntVar allocatedTypeVariable = cache.MakeIntVar(model);
							cache.AddPointerReferenceConstraint(model, allocatedTypeVariable, resultVariable);
							secondaryVariables.Add(instruction, allocatedTypeVariable);
						}
						break;
					case LoadInstructionContext:
						{
							cache.AddPointerReferenceConstraint(model, resultVariable, operandVariables[(instruction, 0)]);
						}
						break;
					case StoreInstructionContext:
						{
							IntVar sourceOperand = operandVariables[(instruction, 0)];
							IntVar destinationOperand = operandVariables[(instruction, 1)];
							cache.AddPointerReferenceConstraint(model, sourceOperand, destinationOperand);
							secondaryVariables.Add(instruction, sourceOperand);
						}
						break;
					case PhiInstructionContext:
						{
							for (int i = 0; i < instruction.Operands.Length; i++)
							{
								model.Add(resultVariable == operandVariables[(instruction, i)]);
							}
						}
						break;
					case ReturnInstructionContext when instruction.Operands.Length is 1:
						{
							model.Add(returnVariable == operandVariables[(instruction, 0)]);
						}
						break;
					case CallInstructionContext callInstructionContext:
						if (callInstructionContext.FunctionCalled is not null)
						{
							model.Add(resultVariable == returnVariables[callInstructionContext.FunctionCalled]);

							for (int i = 0; i < instruction.Operands.Length - 1; i++)
							{
								IntVar operand = operandVariables[(instruction, i)];
								IntVar parameter = parameterVariables[(callInstructionContext.FunctionCalled, i)];

								// If equal, everything is fine.
								BoolVar equal = (operand == parameter).ToBoolean(model);

								// If not equal, it's okay if the operand is a pointer and the parameter is a void pointer.
								BoolVar operandIsPointer = model.BooleanOr(cache.IsPointer(model, operand));
								BoolVar parameterIsVoidPointer = (parameter == cache.VoidPointer).ToBoolean(model);
								BoolVar compatiblePointerConversion = model.BooleanAnd(operandIsPointer, parameterIsVoidPointer);

								model.AddBoolOr([equal, compatiblePointerConversion]);
							}
						}
						break;
					case GetElementPointerInstructionContext:
						{
							for (int i = 1; i < instruction.Operands.Length; ++i)
							{
								model.Add(operandVariables[(instruction, i)] == cache.Int32);
							}
						}
						break;
				}
			}
		}

		objective.Add(model.NewConstant(0));
		model.Maximize(objective);

		// Run the solver
		CpSolver solver = new();
		CpSolverStatus status = solver.Solve(model);

		// Check that the problem has a feasible solution.
		if (status == CpSolverStatus.Optimal || status == CpSolverStatus.Feasible)
		{
			foreach (FunctionContext function in moduleContext.Methods.Values)
			{
				IntVar returnVariable = returnVariables[function];
				function.Definition.Signature!.ReturnType = cache[solver.Value(returnVariable)];
				for (int i = 0; i < function.Parameters.Length; i++)
				{
					IntVar parameterVariable = parameterVariables[(function, i)];
					function.Definition.Signature!.ParameterTypes[i] = cache[solver.Value(parameterVariable)];
				}

				foreach (InstructionContext instruction in function.Instructions)
				{
					IntVar resultVariable = resultVariables[instruction];
					instruction.ResultTypeSignature = cache[solver.Value(resultVariable)];
					if (secondaryVariables.TryGetValue(instruction, out IntVar? secondaryVariable))
					{
						instruction.SecondaryTypeSignature = cache[solver.Value(secondaryVariable)];
					}
				}
			}
		}
		else
		{
			throw new("No solution found.");
		}
	}

	private sealed record class TypeSignatureCache(List<TypeSignature> Types, Dictionary<TypeSignature, int> Indices)
	{
		private const int MaxPointerIndirection = 4;

		public int Int32
		{
			get => GetIndex(ref field, AsmResolverExtensions.IsInt32);
		} = -1;

		public int Void
		{
			get => GetIndex(ref field, AsmResolverExtensions.IsVoid);
		} = -1;

		public int VoidPointer
		{
			get => GetIndex(ref field, AsmResolverExtensions.IsVoidPointer);
		} = -1;

		private int GetIndex(ref int field, Func<TypeSignature, bool> filter)
		{
			if (field == -1)
			{
				field = Indices.FirstOrDefault(pair => filter(pair.Key)).Value;
			}
			return field;
		}

		public TypeSignatureCache() : this(new(), new(SignatureComparer.Default))
		{
		}

		public bool MaybeAdd([NotNullWhen(true)] TypeSignature? type)
		{
			if (type is not null && Indices.TryAdd(type, Types.Count))
			{
				Types.Add(type);
				Debug.Assert(Types.Count == Indices.Count);
				return true;
			}
			return false;
		}

		public void AddCorLibTypes(ModuleDefinition module)
		{
			MaybeAdd(module.CorLibTypeFactory.Void);
			MaybeAdd(module.CorLibTypeFactory.Int32);
		}

		public void AddPointerTypes()
		{
			for (int i = 0; i < Types.Count; i++)
			{
				TypeSignature type = Types[i];
				int pointerIndirection = GetPointerIndirection(type);
				if (pointerIndirection < MaxPointerIndirection)
				{
					MaybeAdd(type.MakePointerType());
				}
				else
				{
					while (pointerIndirection > 0)
					{
						PointerTypeSignature pointer = (PointerTypeSignature)type;
						MaybeAdd(pointer.BaseType);
						type = pointer.BaseType;
						pointerIndirection--;
					}
				}
			}
		}

		public int this[TypeSignature type] => Indices[type];

		public TypeSignature this[int index] => Types[index];

		public TypeSignature this[long index] => this[(int)index];

		public int Count => Types.Count;

		private static int GetPointerIndirection(TypeSignature type)
		{
			return type is PointerTypeSignature pointer
				? GetPointerIndirection(pointer.BaseType) + 1
				: 0;
		}

		public IntVar MakeIntVar(CpModel model)
		{
			return model.NewIntVar(0, Count, "");
		}

		public IntVar MakeIntVar(CpModel model, TypeSignature? type)
		{
			if (type is null)
			{
				return MakeIntVar(model);
			}
			else
			{
				return model.NewConstant(this[type]);
			}
		}

		public IntVar MakeIntVar(CpModel model, TypeSignature? type, LLVMTypeRef typeRef)
		{
			if (type is null)
			{
				IntVar result = MakeIntVar(model);
				AddTypeConstraint(model, result, typeRef);
				return result;
			}
			else
			{
				return model.NewConstant(this[type]);
			}
		}

		public TableConstraint AddPointerReferenceConstraint(CpModel model, IntVar data, IntVar pointer)
		{
			// Todo: Cache the tuples
			List<(int, int)> pairs = new();
			foreach ((TypeSignature type, int index) in Indices)
			{
				if (type is PointerTypeSignature pointerType)
				{
					pairs.Add((index, Indices[pointerType.BaseType]));
				}
			}
			int[,] tuples = new int[pairs.Count, 2];
			for (int i = 0; i < pairs.Count; i++)
			{
				(tuples[i, 0], tuples[i, 1]) = pairs[i];
			}
			return model.AddAllowedAssignments([pointer, data]).AddTuples(tuples);
		}

		public Constraint AddTypeConstraint(CpModel model, IntVar variable, LLVMTypeRef type)
		{
			if (type.Kind == LLVMTypeKind.LLVMPointerTypeKind)
			{
				return AddPointerConstraint(model, variable);
			}
			else
			{
				return AddStructConstraint(model, variable);
			}
		}

		public Constraint AddPointerConstraint(CpModel model, IntVar variable)
		{
			return model.AddBoolOr(IsPointer(model, variable));
		}

		public Constraint AddStructConstraint(CpModel model, IntVar variable)
		{
			return model.AddBoolOr(IsStruct(model, variable));
		}

		public IEnumerable<BoolVar> IsPointer(CpModel model, IntVar variable)
		{
			return GetPointerIndices().Select(index => (variable == index).ToBoolean(model));
		}

		public IEnumerable<BoolVar> IsStruct(CpModel model, IntVar variable)
		{
			return GetStructIndices().Select(index => (variable == index).ToBoolean(model));
		}

		private IEnumerable<int> GetPointerIndices()
		{
			foreach ((TypeSignature type, int index) in Indices)
			{
				if (type is PointerTypeSignature)
				{
					yield return index;
				}
			}
		}

		private IEnumerable<int> GetStructIndices()
		{
			foreach ((TypeSignature type, int index) in Indices)
			{
				if (type is not PointerTypeSignature and not CorLibTypeSignature { ElementType: ElementType.Void })
				{
					yield return index;
				}
			}
		}
	}
}
