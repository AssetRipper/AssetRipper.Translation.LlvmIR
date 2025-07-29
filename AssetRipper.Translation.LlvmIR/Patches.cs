using HarmonyLib;
using LLVMSharp.Interop;
using System.Diagnostics;

namespace AssetRipper.Translation.LlvmIR;

// Todo: remove this class after next LLVMSharp release
// https://github.com/dotnet/LLVMSharp/pull/233
[HarmonyPatch]
internal static class Patches
{
	[Conditional("DEBUG")]
	public static void Apply() => Harmony.CreateAndPatchAll(typeof(Patches));

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LLVMValueRef), $"get_{nameof(LLVMValueRef.SuccessorsCount)}")]
	public static bool GetSuccessorsCountPatch(ref uint __result)
	{
		__result = 0;
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LLVMValueRef), $"get_{nameof(LLVMValueRef.InstructionClone)}")]
	public static bool GetInstructionClonePatch(ref LLVMValueRef __result)
	{
		__result = default;
		return false;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(LLVMModuleRef), nameof(LLVMModuleRef.ToString))]
	public static bool ToStringPatch(ref string __result)
	{
		__result = "";
		return false;
	}
}
