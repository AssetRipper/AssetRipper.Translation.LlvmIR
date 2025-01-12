source_filename = "vector4_addition.cpp"

%struct.Vector4F = type { float, float, float, float }

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @vector4f_add(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2) #0 {
  %4 = alloca ptr, align 8
  %5 = alloca ptr, align 8
  %6 = alloca ptr, align 8
  store ptr %0, ptr %4, align 8
  store ptr %2, ptr %5, align 8
  store ptr %1, ptr %6, align 8
  %7 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 0
  %8 = load float, ptr %7, align 4
  %9 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 0
  %10 = load float, ptr %9, align 4
  %11 = fadd float %8, %10
  %12 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 0
  store float %11, ptr %12, align 4
  %13 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 1
  %14 = load float, ptr %13, align 4
  %15 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 1
  %16 = load float, ptr %15, align 4
  %17 = fadd float %14, %16
  %18 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 1
  store float %17, ptr %18, align 4
  %19 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 2
  %20 = load float, ptr %19, align 4
  %21 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 2
  %22 = load float, ptr %21, align 4
  %23 = fadd float %20, %22
  %24 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 2
  store float %23, ptr %24, align 4
  %25 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 3
  %26 = load float, ptr %25, align 4
  %27 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 3
  %28 = load float, ptr %27, align 4
  %29 = fadd float %26, %28
  %30 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 3
  store float %29, ptr %30, align 4
  ret void
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @vector4f_pointer_add(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2) #0 {
  %4 = alloca ptr, align 8
  %5 = alloca ptr, align 8
  %6 = alloca ptr, align 8
  %7 = alloca %struct.Vector4F, align 4
  %8 = alloca %struct.Vector4F, align 4
  store ptr %0, ptr %4, align 8
  store ptr %2, ptr %5, align 8
  store ptr %1, ptr %6, align 8
  %9 = load ptr, ptr %5, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %7, ptr align 4 %9, i64 16, i1 false)
  %10 = load ptr, ptr %6, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %8, ptr align 4 %10, i64 16, i1 false)
  call void @vector4f_add(ptr dead_on_unwind writable sret(%struct.Vector4F) align 4 %0, ptr noundef %8, ptr noundef %7)
  ret void
}

; Function Attrs: nocallback nofree nounwind willreturn memory(argmem: readwrite)
declare void @llvm.memcpy.p0.p0.i64(ptr noalias nocapture writeonly, ptr noalias nocapture readonly, i64, i1 immarg) #1

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @Vector4F_add_3(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2, ptr noundef %3) #0 {
  %5 = alloca ptr, align 8
  %6 = alloca ptr, align 8
  %7 = alloca ptr, align 8
  %8 = alloca ptr, align 8
  store ptr %0, ptr %5, align 8
  store ptr %3, ptr %6, align 8
  store ptr %2, ptr %7, align 8
  store ptr %1, ptr %8, align 8
  %9 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 0
  %10 = load float, ptr %9, align 4
  %11 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 0
  %12 = load float, ptr %11, align 4
  %13 = fadd float %10, %12
  %14 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 0
  %15 = load float, ptr %14, align 4
  %16 = fadd float %13, %15
  %17 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 0
  store float %16, ptr %17, align 4
  %18 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 1
  %19 = load float, ptr %18, align 4
  %20 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 1
  %21 = load float, ptr %20, align 4
  %22 = fadd float %19, %21
  %23 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 1
  %24 = load float, ptr %23, align 4
  %25 = fadd float %22, %24
  %26 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 1
  store float %25, ptr %26, align 4
  %27 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 2
  %28 = load float, ptr %27, align 4
  %29 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 2
  %30 = load float, ptr %29, align 4
  %31 = fadd float %28, %30
  %32 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 2
  %33 = load float, ptr %32, align 4
  %34 = fadd float %31, %33
  %35 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 2
  store float %34, ptr %35, align 4
  %36 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 3
  %37 = load float, ptr %36, align 4
  %38 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 3
  %39 = load float, ptr %38, align 4
  %40 = fadd float %37, %39
  %41 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 3
  %42 = load float, ptr %41, align 4
  %43 = fadd float %40, %42
  %44 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 3
  store float %43, ptr %44, align 4
  ret void
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @Vector4F_add_4(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2, ptr noundef %3, ptr noundef %4) #0 {
  %6 = alloca ptr, align 8
  %7 = alloca ptr, align 8
  %8 = alloca ptr, align 8
  %9 = alloca ptr, align 8
  %10 = alloca ptr, align 8
  store ptr %0, ptr %6, align 8
  store ptr %4, ptr %7, align 8
  store ptr %3, ptr %8, align 8
  store ptr %2, ptr %9, align 8
  store ptr %1, ptr %10, align 8
  %11 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 0
  %12 = load float, ptr %11, align 4
  %13 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 0
  %14 = load float, ptr %13, align 4
  %15 = fadd float %12, %14
  %16 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 0
  %17 = load float, ptr %16, align 4
  %18 = fadd float %15, %17
  %19 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 0
  %20 = load float, ptr %19, align 4
  %21 = fadd float %18, %20
  %22 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 0
  store float %21, ptr %22, align 4
  %23 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 1
  %24 = load float, ptr %23, align 4
  %25 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 1
  %26 = load float, ptr %25, align 4
  %27 = fadd float %24, %26
  %28 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 1
  %29 = load float, ptr %28, align 4
  %30 = fadd float %27, %29
  %31 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 1
  %32 = load float, ptr %31, align 4
  %33 = fadd float %30, %32
  %34 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 1
  store float %33, ptr %34, align 4
  %35 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 2
  %36 = load float, ptr %35, align 4
  %37 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 2
  %38 = load float, ptr %37, align 4
  %39 = fadd float %36, %38
  %40 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 2
  %41 = load float, ptr %40, align 4
  %42 = fadd float %39, %41
  %43 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 2
  %44 = load float, ptr %43, align 4
  %45 = fadd float %42, %44
  %46 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 2
  store float %45, ptr %46, align 4
  %47 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 3
  %48 = load float, ptr %47, align 4
  %49 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 3
  %50 = load float, ptr %49, align 4
  %51 = fadd float %48, %50
  %52 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 3
  %53 = load float, ptr %52, align 4
  %54 = fadd float %51, %53
  %55 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 3
  %56 = load float, ptr %55, align 4
  %57 = fadd float %54, %56
  %58 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 3
  store float %57, ptr %58, align 4
  ret void
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @Vector4F_add_5(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2, ptr noundef %3, ptr noundef %4, ptr noundef %5) #0 {
  %7 = alloca ptr, align 8
  %8 = alloca ptr, align 8
  %9 = alloca ptr, align 8
  %10 = alloca ptr, align 8
  %11 = alloca ptr, align 8
  %12 = alloca ptr, align 8
  store ptr %0, ptr %7, align 8
  store ptr %5, ptr %8, align 8
  store ptr %4, ptr %9, align 8
  store ptr %3, ptr %10, align 8
  store ptr %2, ptr %11, align 8
  store ptr %1, ptr %12, align 8
  %13 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 0
  %14 = load float, ptr %13, align 4
  %15 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 0
  %16 = load float, ptr %15, align 4
  %17 = fadd float %14, %16
  %18 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 0
  %19 = load float, ptr %18, align 4
  %20 = fadd float %17, %19
  %21 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 0
  %22 = load float, ptr %21, align 4
  %23 = fadd float %20, %22
  %24 = getelementptr inbounds %struct.Vector4F, ptr %5, i32 0, i32 0
  %25 = load float, ptr %24, align 4
  %26 = fadd float %23, %25
  %27 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 0
  store float %26, ptr %27, align 4
  %28 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 1
  %29 = load float, ptr %28, align 4
  %30 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 1
  %31 = load float, ptr %30, align 4
  %32 = fadd float %29, %31
  %33 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 1
  %34 = load float, ptr %33, align 4
  %35 = fadd float %32, %34
  %36 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 1
  %37 = load float, ptr %36, align 4
  %38 = fadd float %35, %37
  %39 = getelementptr inbounds %struct.Vector4F, ptr %5, i32 0, i32 1
  %40 = load float, ptr %39, align 4
  %41 = fadd float %38, %40
  %42 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 1
  store float %41, ptr %42, align 4
  %43 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 2
  %44 = load float, ptr %43, align 4
  %45 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 2
  %46 = load float, ptr %45, align 4
  %47 = fadd float %44, %46
  %48 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 2
  %49 = load float, ptr %48, align 4
  %50 = fadd float %47, %49
  %51 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 2
  %52 = load float, ptr %51, align 4
  %53 = fadd float %50, %52
  %54 = getelementptr inbounds %struct.Vector4F, ptr %5, i32 0, i32 2
  %55 = load float, ptr %54, align 4
  %56 = fadd float %53, %55
  %57 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 2
  store float %56, ptr %57, align 4
  %58 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 3
  %59 = load float, ptr %58, align 4
  %60 = getelementptr inbounds %struct.Vector4F, ptr %2, i32 0, i32 3
  %61 = load float, ptr %60, align 4
  %62 = fadd float %59, %61
  %63 = getelementptr inbounds %struct.Vector4F, ptr %3, i32 0, i32 3
  %64 = load float, ptr %63, align 4
  %65 = fadd float %62, %64
  %66 = getelementptr inbounds %struct.Vector4F, ptr %4, i32 0, i32 3
  %67 = load float, ptr %66, align 4
  %68 = fadd float %65, %67
  %69 = getelementptr inbounds %struct.Vector4F, ptr %5, i32 0, i32 3
  %70 = load float, ptr %69, align 4
  %71 = fadd float %68, %70
  %72 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 3
  store float %71, ptr %72, align 4
  ret void
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @vector4f_negate(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1) #0 {
  %3 = alloca ptr, align 8
  %4 = alloca ptr, align 8
  store ptr %0, ptr %3, align 8
  store ptr %1, ptr %4, align 8
  %5 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 0
  %6 = load float, ptr %5, align 4
  %7 = fneg float %6
  %8 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 0
  store float %7, ptr %8, align 4
  %9 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 1
  %10 = load float, ptr %9, align 4
  %11 = fneg float %10
  %12 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 1
  store float %11, ptr %12, align 4
  %13 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 2
  %14 = load float, ptr %13, align 4
  %15 = fneg float %14
  %16 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 2
  store float %15, ptr %16, align 4
  %17 = getelementptr inbounds %struct.Vector4F, ptr %1, i32 0, i32 3
  %18 = load float, ptr %17, align 4
  %19 = fneg float %18
  %20 = getelementptr inbounds %struct.Vector4F, ptr %0, i32 0, i32 3
  store float %19, ptr %20, align 4
  ret void
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
