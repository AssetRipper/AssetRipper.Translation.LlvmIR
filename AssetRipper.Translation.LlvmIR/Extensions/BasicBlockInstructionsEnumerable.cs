using LLVMSharp.Interop;
using System.Collections;

namespace AssetRipper.Translation.LlvmIR.Extensions;

internal readonly record struct BasicBlockInstructionsEnumerable(LLVMBasicBlockRef BasicBlock) : IEnumerable<LLVMValueRef>
{
	public Enumerator GetEnumerator() => new(BasicBlock);
	IEnumerator<LLVMValueRef> IEnumerable<LLVMValueRef>.GetEnumerator() => GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	public struct Enumerator(LLVMBasicBlockRef basicBlock) : IEnumerator<LLVMValueRef>
	{
		public LLVMValueRef Current { get; private set; }
		readonly object IEnumerator.Current => Current;
		readonly void IDisposable.Dispose() { }
		public bool MoveNext()
		{
			if (Current.Handle == 0)
			{
				Current = basicBlock.FirstInstruction;
			}
			else
			{
				Current = Current.NextInstruction;
			}
			return Current.Handle != 0;
		}
		public void Reset()
		{
			Current = default;
		}
	}
}
