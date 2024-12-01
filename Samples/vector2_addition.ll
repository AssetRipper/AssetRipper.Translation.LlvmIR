source_filename = "vector2_addition.cpp"

%struct.Vector2F = type { float, float }

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i64 @vector2f_add(i64 %0, i64 %1) #0 {
  %3 = alloca %struct.Vector2F, align 4
  %4 = alloca %struct.Vector2F, align 4
  %5 = alloca %struct.Vector2F, align 4
  store i64 %0, ptr %4, align 4
  store i64 %1, ptr %5, align 4
  %6 = getelementptr inbounds %struct.Vector2F, ptr %4, i32 0, i32 0
  %7 = load float, ptr %6, align 4
  %8 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 0
  %9 = load float, ptr %8, align 4
  %10 = fadd float %7, %9
  %11 = getelementptr inbounds %struct.Vector2F, ptr %3, i32 0, i32 0
  store float %10, ptr %11, align 4
  %12 = getelementptr inbounds %struct.Vector2F, ptr %4, i32 0, i32 1
  %13 = load float, ptr %12, align 4
  %14 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 1
  %15 = load float, ptr %14, align 4
  %16 = fadd float %13, %15
  %17 = getelementptr inbounds %struct.Vector2F, ptr %3, i32 0, i32 1
  store float %16, ptr %17, align 4
  %18 = load i64, ptr %3, align 4
  ret i64 %18
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i64 @vector2f_pointer_add(ptr noundef %0, ptr noundef %1) #0 {
  %3 = alloca %struct.Vector2F, align 4
  %4 = alloca ptr, align 8
  %5 = alloca ptr, align 8
  %6 = alloca %struct.Vector2F, align 4
  %7 = alloca %struct.Vector2F, align 4
  store ptr %1, ptr %4, align 8
  store ptr %0, ptr %5, align 8
  %8 = load ptr, ptr %4, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %6, ptr align 4 %8, i64 8, i1 false)
  %9 = load ptr, ptr %5, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %7, ptr align 4 %9, i64 8, i1 false)
  %10 = load i64, ptr %7, align 4
  %11 = load i64, ptr %6, align 4
  %12 = call i64 @vector2f_add(i64 %10, i64 %11)
  store i64 %12, ptr %3, align 4
  %13 = load i64, ptr %3, align 4
  ret i64 %13
}

; Function Attrs: nocallback nofree nounwind willreturn memory(argmem: readwrite)
declare void @llvm.memcpy.p0.p0.i64(ptr noalias nocapture writeonly, ptr noalias nocapture readonly, i64, i1 immarg) #1

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i64 @vector2f_add_3(i64 %0, i64 %1, i64 %2) #0 {
  %4 = alloca %struct.Vector2F, align 4
  %5 = alloca %struct.Vector2F, align 4
  %6 = alloca %struct.Vector2F, align 4
  %7 = alloca %struct.Vector2F, align 4
  store i64 %0, ptr %5, align 4
  store i64 %1, ptr %6, align 4
  store i64 %2, ptr %7, align 4
  %8 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 0
  %9 = load float, ptr %8, align 4
  %10 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 0
  %11 = load float, ptr %10, align 4
  %12 = fadd float %9, %11
  %13 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 0
  %14 = load float, ptr %13, align 4
  %15 = fadd float %12, %14
  %16 = getelementptr inbounds %struct.Vector2F, ptr %4, i32 0, i32 0
  store float %15, ptr %16, align 4
  %17 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 1
  %18 = load float, ptr %17, align 4
  %19 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 1
  %20 = load float, ptr %19, align 4
  %21 = fadd float %18, %20
  %22 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 1
  %23 = load float, ptr %22, align 4
  %24 = fadd float %21, %23
  %25 = getelementptr inbounds %struct.Vector2F, ptr %4, i32 0, i32 1
  store float %24, ptr %25, align 4
  %26 = load i64, ptr %4, align 4
  ret i64 %26
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i64 @vector2f_add_4(i64 %0, i64 %1, i64 %2, i64 %3) #0 {
  %5 = alloca %struct.Vector2F, align 4
  %6 = alloca %struct.Vector2F, align 4
  %7 = alloca %struct.Vector2F, align 4
  %8 = alloca %struct.Vector2F, align 4
  %9 = alloca %struct.Vector2F, align 4
  store i64 %0, ptr %6, align 4
  store i64 %1, ptr %7, align 4
  store i64 %2, ptr %8, align 4
  store i64 %3, ptr %9, align 4
  %10 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 0
  %11 = load float, ptr %10, align 4
  %12 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 0
  %13 = load float, ptr %12, align 4
  %14 = fadd float %11, %13
  %15 = getelementptr inbounds %struct.Vector2F, ptr %8, i32 0, i32 0
  %16 = load float, ptr %15, align 4
  %17 = fadd float %14, %16
  %18 = getelementptr inbounds %struct.Vector2F, ptr %9, i32 0, i32 0
  %19 = load float, ptr %18, align 4
  %20 = fadd float %17, %19
  %21 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 0
  store float %20, ptr %21, align 4
  %22 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 1
  %23 = load float, ptr %22, align 4
  %24 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 1
  %25 = load float, ptr %24, align 4
  %26 = fadd float %23, %25
  %27 = getelementptr inbounds %struct.Vector2F, ptr %8, i32 0, i32 1
  %28 = load float, ptr %27, align 4
  %29 = fadd float %26, %28
  %30 = getelementptr inbounds %struct.Vector2F, ptr %9, i32 0, i32 1
  %31 = load float, ptr %30, align 4
  %32 = fadd float %29, %31
  %33 = getelementptr inbounds %struct.Vector2F, ptr %5, i32 0, i32 1
  store float %32, ptr %33, align 4
  %34 = load i64, ptr %5, align 4
  ret i64 %34
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local i64 @vector2f_add_5(i64 %0, i64 %1, i64 %2, i64 %3, i64 %4) #0 {
  %6 = alloca %struct.Vector2F, align 4
  %7 = alloca %struct.Vector2F, align 4
  %8 = alloca %struct.Vector2F, align 4
  %9 = alloca %struct.Vector2F, align 4
  %10 = alloca %struct.Vector2F, align 4
  %11 = alloca %struct.Vector2F, align 4
  store i64 %0, ptr %7, align 4
  store i64 %1, ptr %8, align 4
  store i64 %2, ptr %9, align 4
  store i64 %3, ptr %10, align 4
  store i64 %4, ptr %11, align 4
  %12 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 0
  %13 = load float, ptr %12, align 4
  %14 = getelementptr inbounds %struct.Vector2F, ptr %8, i32 0, i32 0
  %15 = load float, ptr %14, align 4
  %16 = fadd float %13, %15
  %17 = getelementptr inbounds %struct.Vector2F, ptr %9, i32 0, i32 0
  %18 = load float, ptr %17, align 4
  %19 = fadd float %16, %18
  %20 = getelementptr inbounds %struct.Vector2F, ptr %10, i32 0, i32 0
  %21 = load float, ptr %20, align 4
  %22 = fadd float %19, %21
  %23 = getelementptr inbounds %struct.Vector2F, ptr %11, i32 0, i32 0
  %24 = load float, ptr %23, align 4
  %25 = fadd float %22, %24
  %26 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 0
  store float %25, ptr %26, align 4
  %27 = getelementptr inbounds %struct.Vector2F, ptr %7, i32 0, i32 1
  %28 = load float, ptr %27, align 4
  %29 = getelementptr inbounds %struct.Vector2F, ptr %8, i32 0, i32 1
  %30 = load float, ptr %29, align 4
  %31 = fadd float %28, %30
  %32 = getelementptr inbounds %struct.Vector2F, ptr %9, i32 0, i32 1
  %33 = load float, ptr %32, align 4
  %34 = fadd float %31, %33
  %35 = getelementptr inbounds %struct.Vector2F, ptr %10, i32 0, i32 1
  %36 = load float, ptr %35, align 4
  %37 = fadd float %34, %36
  %38 = getelementptr inbounds %struct.Vector2F, ptr %11, i32 0, i32 1
  %39 = load float, ptr %38, align 4
  %40 = fadd float %37, %39
  %41 = getelementptr inbounds %struct.Vector2F, ptr %6, i32 0, i32 1
  store float %40, ptr %41, align 4
  %42 = load i64, ptr %6, align 4
  ret i64 %42
}

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }
attributes #1 = { nocallback nofree nounwind willreturn memory(argmem: readwrite) }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
