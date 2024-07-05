; ModuleID = '../../../../Samples\pointer_arithmetic.cpp'
source_filename = "../../../../Samples\\pointer_arithmetic.cpp"
target datalayout = "e-m:w-p270:32:32-p271:32:32-p272:64:64-i64:64-i128:128-f80:128-n8:16:32:64-S128"
target triple = "x86_64-pc-windows-msvc19.41.33923"

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @char_pointer_plus_one(ptr noundef %0) #0 {
  %2 = alloca ptr, align 8
  store ptr %0, ptr %2, align 8
  %3 = load ptr, ptr %2, align 8
  %4 = getelementptr inbounds i8, ptr %3, i64 1
  ret ptr %4
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @one_plus_char_pointer(ptr noundef %0) #0 {
  %2 = alloca ptr, align 8
  store ptr %0, ptr %2, align 8
  %3 = load ptr, ptr %2, align 8
  %4 = getelementptr inbounds i8, ptr %3, i64 1
  ret ptr %4
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @char_pointer_plus_value(ptr noundef %0, i32 noundef %1) #0 {
  %3 = alloca i32, align 4
  %4 = alloca ptr, align 8
  store i32 %1, ptr %3, align 4
  store ptr %0, ptr %4, align 8
  %5 = load ptr, ptr %4, align 8
  %6 = load i32, ptr %3, align 4
  %7 = sext i32 %6 to i64
  %8 = getelementptr inbounds i8, ptr %5, i64 %7
  ret ptr %8
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @value_plus_char_pointer(ptr noundef %0, i32 noundef %1) #0 {
  %3 = alloca i32, align 4
  %4 = alloca ptr, align 8
  store i32 %1, ptr %3, align 4
  store ptr %0, ptr %4, align 8
  %5 = load i32, ptr %3, align 4
  %6 = load ptr, ptr %4, align 8
  %7 = sext i32 %5 to i64
  %8 = getelementptr inbounds i8, ptr %6, i64 %7
  ret ptr %8
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @int_pointer_plus_one(ptr noundef %0) #0 {
  %2 = alloca ptr, align 8
  store ptr %0, ptr %2, align 8
  %3 = load ptr, ptr %2, align 8
  %4 = getelementptr inbounds i32, ptr %3, i64 1
  ret ptr %4
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @one_plus_int_pointer(ptr noundef %0) #0 {
  %2 = alloca ptr, align 8
  store ptr %0, ptr %2, align 8
  %3 = load ptr, ptr %2, align 8
  %4 = getelementptr inbounds i32, ptr %3, i64 1
  ret ptr %4
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @int_pointer_plus_value(ptr noundef %0, i32 noundef %1) #0 {
  %3 = alloca i32, align 4
  %4 = alloca ptr, align 8
  store i32 %1, ptr %3, align 4
  store ptr %0, ptr %4, align 8
  %5 = load ptr, ptr %4, align 8
  %6 = load i32, ptr %3, align 4
  %7 = sext i32 %6 to i64
  %8 = getelementptr inbounds i32, ptr %5, i64 %7
  ret ptr %8
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local ptr @value_plus_int_pointer(ptr noundef %0, i32 noundef %1) #0 {
  %3 = alloca i32, align 4
  %4 = alloca ptr, align 8
  store i32 %1, ptr %3, align 4
  store ptr %0, ptr %4, align 8
  %5 = load i32, ptr %3, align 4
  %6 = load ptr, ptr %4, align 8
  %7 = sext i32 %5 to i64
  %8 = getelementptr inbounds i32, ptr %6, i64 %7
  ret ptr %8
}

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
