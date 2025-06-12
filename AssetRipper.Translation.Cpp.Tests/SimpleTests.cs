using NUnit.Framework;
using System.Reflection;

namespace AssetRipper.Translation.Cpp.Tests;

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
			Type? type = assembly.GetType("GlobalFunctions");
			Assert.That(type, Is.Not.Null);
			MethodInfo? method = type.GetMethod("do_nothing", BindingFlags.Public | BindingFlags.Static);
			Assert.That(method, Is.Not.Null);
			int result = (int)method.Invoke(null, [42])!;
			Assert.That(result, Is.EqualTo(42));
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
}
