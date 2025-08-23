using NUnit.Framework;
using System.Reflection;

namespace AssetRipper.Translation.LlvmIR.Tests;

[NonParallelizable]
public partial class SimpleTests
{
	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string Noop = """
		define dso_local i32 @do_nothing(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  ret i32 %3
		}
		""";

	[Test]
	public void Noop_ExecutesCorrectly()
	{
		ExecutionHelpers.RunTest(Noop.TranslateToCIL(), assembly =>
		{
			Func<int, int> method = ExecutionHelpers.GetMethod<Func<int, int>>(assembly, "do_nothing");
			Assert.That(method.Invoke(42), Is.EqualTo(42));
		});
	}

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string BitCast = """
		define dso_local noundef i32 @"?bitcastExample@@YAHM@Z"(float noundef %0) {
		  %2 = alloca float, align 4
		  %3 = alloca i32, align 4
		  store float %0, ptr %2, align 4
		  %4 = load i32, ptr %2, align 4
		  store i32 %4, ptr %3, align 4
		  %5 = load i32, ptr %3, align 4
		  ret i32 %5
		}

		define dso_local noundef i32 @main() {
		  %1 = alloca i32, align 4
		  %2 = alloca float, align 4
		  store i32 0, ptr %1, align 4
		  store float 0x40091EB860000000, ptr %2, align 4
		  %3 = load float, ptr %2, align 4
		  %4 = call noundef i32 @"?bitcastExample@@YAHM@Z"(float noundef %3)
		  ret i32 %4
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string FloatMathWithConstant = """
		define dso_local float @incrementF(float noundef %0) {
		  %2 = alloca float, align 4
		  store float %0, ptr %2, align 4
		  %3 = load float, ptr %2, align 4
		  %4 = fadd float %3, 1.500000e+00
		  ret float %4
		}

		define dso_local double @decrementD(double noundef %0) {
		  %2 = alloca double, align 8
		  store double %0, ptr %2, align 8
		  %3 = load double, ptr %2, align 8
		  %4 = fsub double %3, 1.500000e+00
		  ret double %4
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string IntegerCasts = """
		define dso_local i64 @i32_to_i64(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  %4 = sext i32 %3 to i64
		  ret i64 %4
		}

		define dso_local i64 @u32_to_u64(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  %4 = zext i32 %3 to i64
		  ret i64 %4
		}

		define dso_local i64 @u32_to_i64(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  %4 = zext i32 %3 to i64
		  ret i64 %4
		}

		define dso_local zeroext i1 @i32_to_i1(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  %4 = icmp ne i32 %3, 0
		  ret i1 %4
		}

		define dso_local i32 @i64_to_i32(i64 noundef %0) {
		  %2 = alloca i64, align 8
		  store i64 %0, ptr %2, align 8
		  %3 = load i64, ptr %2, align 8
		  %4 = trunc i64 %3 to i32
		  ret i32 %4
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string BooleanCasts = """
		define dso_local i8 @i1_to_i8(i1 noundef %0) {
		  %2 = sext i1 %0 to i8
		  ret i8 %2
		}

		define dso_local i8 @u1_to_u8(i1 noundef %0) {
		  %2 = zext i1 %0 to i8
		  ret i8 %2
		}
		
		define dso_local i16 @i1_to_i16(i1 noundef %0) {
		  %2 = sext i1 %0 to i16
		  ret i16 %2
		}
		
		define dso_local i16 @u1_to_u16(i1 noundef %0) {
		  %2 = zext i1 %0 to i16
		  ret i16 %2
		}
		
		define dso_local i32 @i1_to_i32(i1 noundef %0) {
		  %2 = sext i1 %0 to i32
		  ret i32 %2
		}
		
		define dso_local i32 @u1_to_u32(i1 noundef %0) {
		  %2 = zext i1 %0 to i32
		  ret i32 %2
		}
		
		define dso_local i64 @i1_to_i64(i1 noundef %0) {
		  %2 = sext i1 %0 to i64
		  ret i64 %2
		}

		define dso_local i64 @u1_to_u64(i1 noundef %0) {
		  %2 = zext i1 %0 to i64
		  ret i64 %2
		}

		define dso_local i1 @i8_to_i1(i8 noundef %0) {
		  %2 = trunc i8 %0 to i1
		  ret i1 %2
		}
		
		define dso_local i1 @i16_to_i1(i16 noundef %0) {
		  %2 = trunc i16 %0 to i1
		  ret i1 %2
		}
		
		define dso_local i1 @i32_to_i1(i32 noundef %0) {
		  %2 = trunc i32 %0 to i1
		  ret i1 %2
		}
		
		define dso_local i1 @i64_to_i1(i64 noundef %0) {
		  %2 = trunc i64 %0 to i1
		  ret i1 %2
		}
		""";

	[TestCase(["i1_to_i8", false, (sbyte)0])]
	[TestCase(["i1_to_i8", true, (sbyte)1])]
	[TestCase(["u1_to_u8", false, (sbyte)0])]
	[TestCase(["u1_to_u8", true, (sbyte)1])]
	[TestCase(["i1_to_i16", false, (short)0])]
	[TestCase(["i1_to_i16", true, (short)1])]
	[TestCase(["u1_to_u16", false, (ushort)0])]
	[TestCase(["u1_to_u16", true, (ushort)1])]
	[TestCase(["i1_to_i32", false, 0])]
	[TestCase(["i1_to_i32", true, 1])]
	[TestCase(["u1_to_u32", false, 0])]
	[TestCase(["u1_to_u32", true, 1])]
	[TestCase(["i1_to_i64", false, 0L])]
	[TestCase(["i1_to_i64", true, 1L])]
	[TestCase(["u1_to_u64", false, 0L])]
	[TestCase(["u1_to_u64", true, 1L])]
	[TestCase(["i8_to_i1", (sbyte)0, false])]
	[TestCase(["i8_to_i1", (sbyte)1, true])]
	[TestCase(["i8_to_i1", (sbyte)2, false])]
	[TestCase(["i8_to_i1", (sbyte)3, true])]
	[TestCase(["i16_to_i1", (short)0, false])]
	[TestCase(["i16_to_i1", (short)1, true])]
	[TestCase(["i16_to_i1", (short)2, false])]
	[TestCase(["i16_to_i1", (short)3, true])]
	[TestCase(["i32_to_i1", 0, false])]
	[TestCase(["i32_to_i1", 1, true])]
	[TestCase(["i32_to_i1", 2, false])]
	[TestCase(["i32_to_i1", 3, true])]
	[TestCase(["i64_to_i1", 0L, false])]
	[TestCase(["i64_to_i1", 1L, true])]
	[TestCase(["i64_to_i1", 2L, false])]
	[TestCase(["i64_to_i1", 3L, true])]
	public void BooleanCasts_ExecutesCorrectly(string methodName, object input, object expectedOutput)
	{
		ExecutionHelpers.RunTest(BooleanCasts.TranslateToCIL(), assembly =>
		{
			MethodInfo method = ExecutionHelpers.GetMethod(assembly, methodName);
			Assert.That(method.Invoke(null, [input]), Is.EqualTo(expectedOutput));
		});
	}

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string ForLoop = """
		define dso_local i32 @sum(ptr noundef %0, i32 noundef %1) {
		  %3 = alloca i32, align 4
		  %4 = alloca ptr, align 8
		  %5 = alloca i32, align 4
		  %6 = alloca i32, align 4
		  store i32 %1, ptr %3, align 4
		  store ptr %0, ptr %4, align 8
		  store i32 0, ptr %5, align 4
		  store i32 0, ptr %6, align 4
		  br label %7

		7:                                                ; preds = %19, %2
		  %8 = load i32, ptr %6, align 4
		  %9 = load i32, ptr %3, align 4
		  %10 = icmp slt i32 %8, %9
		  br i1 %10, label %11, label %22

		11:                                               ; preds = %7
		  %12 = load ptr, ptr %4, align 8
		  %13 = load i32, ptr %6, align 4
		  %14 = sext i32 %13 to i64
		  %15 = getelementptr inbounds i32, ptr %12, i64 %14
		  %16 = load i32, ptr %15, align 4
		  %17 = load i32, ptr %5, align 4
		  %18 = add nsw i32 %17, %16
		  store i32 %18, ptr %5, align 4
		  br label %19

		19:                                               ; preds = %11
		  %20 = load i32, ptr %6, align 4
		  %21 = add nsw i32 %20, 1
		  store i32 %21, ptr %6, align 4
		  br label %7

		22:                                               ; preds = %7
		  %23 = load i32, ptr %5, align 4
		  ret i32 %23
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string HalfAdd = """
		define dso_local half @add(half noundef %0, half noundef %1) {
		  %3 = fadd half %0, %1
		  ret half %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string Int128Add = """
		define dso_local i128 @add(i128 noundef %0, i128 noundef %1) {
		  %3 = add i128 %0, %1
		  ret i128 %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string VectorAdd = """
		define linkonce_odr dso_local noundef <4 x i32> @add(<4 x i32> noundef %0, <4 x i32> noundef %1) {
		  %3 = add <4 x i32> %0, %1
		  ret <4 x i32> %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string VectorVariableSizeAdd = """
		define linkonce_odr dso_local noundef <vscale x 4 x i32> @add(<vscale x 4 x i32> noundef %0, <vscale x 4 x i32> noundef %1) {
		  %3 = add <vscale x 4 x i32> %0, %1
		  ret <vscale x 4 x i32> %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string VectorExtractElement = """
		define linkonce_odr dso_local noundef i32 @extract(<4 x i32> noundef %0) {
		  %3 = extractelement <4 x i32> %0, i64 2
		  ret i32 %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string VectorInsertElement = """
		define linkonce_odr dso_local noundef <4 x i32> @insert(<4 x i32> noundef %0, i32 noundef %1) {
		  %3 = insertelement <4 x i32> %0, i32 %1, i16 2
		  ret <4 x i32> %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string ArrayExtractValue = """
		define linkonce_odr dso_local noundef i32 @extract([4 x i32] noundef %0) {
		  %3 = extractvalue [4 x i32] %0, 2
		  ret i32 %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string ArrayInsertValue = """
		define linkonce_odr dso_local noundef [4 x i32] @insert([4 x i32] noundef %0, i32 noundef %1) {
		  %3 = insertvalue [4 x i32] %0, i32 %1, 2
		  ret [4 x i32] %3
		}
		""";

	[SavesSuccessfully]
	[DecompilesSuccessfully]
	private const string StaticIntVariable = """
		@value = internal global i32 1, align 4

		define dso_local void @increment() {
		  %1 = load i32, ptr @value, align 4
		  %2 = add nsw i32 %1, 1
		  store i32 %2, ptr @value, align 4
		  ret void
		}

		define dso_local void @decrement() {
		  %1 = load i32, ptr @value, align 4
		  %2 = add nsw i32 %1, -1
		  store i32 %2, ptr @value, align 4
		  ret void
		}

		define dso_local i32 @get() {
		  %1 = load i32, ptr @value, align 4
		  ret i32 %1
		}

		define dso_local void @set(i32 noundef %0) {
		  %2 = alloca i32, align 4
		  store i32 %0, ptr %2, align 4
		  %3 = load i32, ptr %2, align 4
		  store i32 %3, ptr @value, align 4
		  ret void
		}
		""";

	[Test]
	public void StaticIntVariable_ExecutesCorrectly()
	{
		ExecutionHelpers.RunTest(StaticIntVariable.TranslateToCIL(), assembly =>
		{
			Func<int> method = ExecutionHelpers.GetMethod<Func<int>>(assembly, "get");
			Assert.That(method.Invoke(), Is.EqualTo(1));
		});
	}
}
