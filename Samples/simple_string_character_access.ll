source_filename = "simple_string_character_access.cpp"

$"??_C@_0N@KNIDPCKA@Hello?5world?$CB?$AA@" = comdat any

@"??_C@_0N@KNIDPCKA@Hello?5world?$CB?$AA@" = linkonce_odr dso_local unnamed_addr constant [13 x i8] c"Hello world!\00", comdat, align 1

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i8 @get_hello_world_character(i32 noundef %0) #0 {
  %2 = alloca i32, align 4
  %3 = alloca ptr, align 8
  store i32 %0, ptr %2, align 4
  store ptr @"??_C@_0N@KNIDPCKA@Hello?5world?$CB?$AA@", ptr %3, align 8
  %4 = load i32, ptr %2, align 4
  %5 = icmp slt i32 %4, 0
  br i1 %5, label %9, label %6

6:                                                ; preds = %1
  %7 = load i32, ptr %2, align 4
  %8 = icmp sge i32 %7, 12
  br i1 %8, label %9, label %10

9:                                                ; preds = %6, %1
  br label %16

10:                                               ; preds = %6
  %11 = load ptr, ptr %3, align 8
  %12 = load i32, ptr %2, align 4
  %13 = sext i32 %12 to i64
  %14 = getelementptr inbounds i8, ptr %11, i64 %13
  %15 = load i8, ptr %14, align 1
  br label %16

16:                                               ; preds = %10, %9
  %17 = phi i8 [ 0, %9 ], [ %15, %10 ]
  ret i8 %17
}

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
