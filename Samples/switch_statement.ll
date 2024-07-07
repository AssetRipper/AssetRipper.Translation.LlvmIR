; ModuleID = '../../../../Samples\switch_statement.cpp'
source_filename = "../../../../Samples\\switch_statement.cpp"
target datalayout = "e-m:w-p270:32:32-p271:32:32-p272:64:64-i64:64-i128:128-f80:128-n8:16:32:64-S128"
target triple = "x86_64-pc-windows-msvc19.41.33923"

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i32 @fake_fibonacci(i32 noundef %0) #0 {
  %2 = alloca i32, align 4
  %3 = alloca i32, align 4
  store i32 %0, ptr %3, align 4
  %4 = load i32, ptr %3, align 4
  switch i32 %4, label %12 [
    i32 0, label %5
    i32 1, label %6
    i32 2, label %7
    i32 3, label %8
    i32 4, label %9
    i32 5, label %10
    i32 6, label %11
  ]

5:                                                ; preds = %1
  store i32 0, ptr %2, align 4
  br label %13

6:                                                ; preds = %1
  store i32 1, ptr %2, align 4
  br label %13

7:                                                ; preds = %1
  store i32 1, ptr %2, align 4
  br label %13

8:                                                ; preds = %1
  store i32 2, ptr %2, align 4
  br label %13

9:                                                ; preds = %1
  store i32 3, ptr %2, align 4
  br label %13

10:                                               ; preds = %1
  store i32 5, ptr %2, align 4
  br label %13

11:                                               ; preds = %1
  store i32 8, ptr %2, align 4
  br label %13

12:                                               ; preds = %1
  store i32 0, ptr %2, align 4
  br label %13

13:                                               ; preds = %12, %11, %10, %9, %8, %7, %6, %5
  %14 = load i32, ptr %2, align 4
  ret i32 %14
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i32 @switch_with_holes(i32 noundef %0) #0 {
  %2 = alloca i32, align 4
  %3 = alloca i32, align 4
  store i32 %0, ptr %3, align 4
  %4 = load i32, ptr %3, align 4
  switch i32 %4, label %14 [
    i32 0, label %5
    i32 2, label %6
    i32 3, label %7
    i32 4, label %8
    i32 6, label %9
    i32 7, label %10
    i32 8, label %11
    i32 10, label %12
    i32 11, label %13
  ]

5:                                                ; preds = %1
  store i32 0, ptr %2, align 4
  br label %15

6:                                                ; preds = %1
  store i32 1, ptr %2, align 4
  br label %15

7:                                                ; preds = %1
  store i32 2, ptr %2, align 4
  br label %15

8:                                                ; preds = %1
  store i32 3, ptr %2, align 4
  br label %15

9:                                                ; preds = %1
  store i32 5, ptr %2, align 4
  br label %15

10:                                               ; preds = %1
  store i32 8, ptr %2, align 4
  br label %15

11:                                               ; preds = %1
  store i32 9, ptr %2, align 4
  br label %15

12:                                               ; preds = %1
  store i32 10, ptr %2, align 4
  br label %15

13:                                               ; preds = %1
  store i32 11, ptr %2, align 4
  br label %15

14:                                               ; preds = %1
  store i32 0, ptr %2, align 4
  br label %15

15:                                               ; preds = %14, %13, %12, %11, %10, %9, %8, %7, %6, %5
  %16 = load i32, ptr %2, align 4
  ret i32 %16
}

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
