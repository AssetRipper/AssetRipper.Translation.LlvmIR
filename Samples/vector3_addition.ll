source_filename = "vector3_addition.cpp"

%struct.Vector3F = type { float, float, float }

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @vector3f_add(ptr dead_on_unwind noalias writable sret(%struct.Vector3F) align 4 %0, ptr noundef %1, ptr noundef %2) #0 {
  %4 = alloca ptr, align 8
  %5 = alloca ptr, align 8
  %6 = alloca ptr, align 8
  store ptr %0, ptr %4, align 8
  store ptr %2, ptr %5, align 8
  store ptr %1, ptr %6, align 8
  %7 = getelementptr inbounds %struct.Vector3F, ptr %1, i32 0, i32 0
  %8 = load float, ptr %7, align 4
  %9 = getelementptr inbounds %struct.Vector3F, ptr %2, i32 0, i32 0
  %10 = load float, ptr %9, align 4
  %11 = fadd float %8, %10
  %12 = getelementptr inbounds %struct.Vector3F, ptr %0, i32 0, i32 0
  store float %11, ptr %12, align 4
  %13 = getelementptr inbounds %struct.Vector3F, ptr %1, i32 0, i32 1
  %14 = load float, ptr %13, align 4
  %15 = getelementptr inbounds %struct.Vector3F, ptr %2, i32 0, i32 1
  %16 = load float, ptr %15, align 4
  %17 = fadd float %14, %16
  %18 = getelementptr inbounds %struct.Vector3F, ptr %0, i32 0, i32 1
  store float %17, ptr %18, align 4
  %19 = getelementptr inbounds %struct.Vector3F, ptr %1, i32 0, i32 2
  %20 = load float, ptr %19, align 4
  %21 = getelementptr inbounds %struct.Vector3F, ptr %2, i32 0, i32 2
  %22 = load float, ptr %21, align 4
  %23 = fadd float %20, %22
  %24 = getelementptr inbounds %struct.Vector3F, ptr %0, i32 0, i32 2
  store float %23, ptr %24, align 4
  ret void
}

; Function Attrs: mustprogress noinline nounwind optnone uwtable
define dso_local void @vector3f_pointer_add(ptr dead_on_unwind noalias writable sret(%struct.Vector3F) align 4 %0, ptr noundef %1, ptr noundef %2) #0 {
  %4 = alloca ptr, align 8
  %5 = alloca ptr, align 8
  %6 = alloca ptr, align 8
  %7 = alloca %struct.Vector3F, align 4
  %8 = alloca %struct.Vector3F, align 4
  store ptr %0, ptr %4, align 8
  store ptr %2, ptr %5, align 8
  store ptr %1, ptr %6, align 8
  %9 = load ptr, ptr %5, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %7, ptr align 4 %9, i64 12, i1 false)
  %10 = load ptr, ptr %6, align 8
  call void @llvm.memcpy.p0.p0.i64(ptr align 4 %8, ptr align 4 %10, i64 12, i1 false)
  call void @vector3f_add(ptr dead_on_unwind writable sret(%struct.Vector3F) align 4 %0, ptr noundef %8, ptr noundef %7)
  ret void
}

; Function Attrs: nocallback nofree nounwind willreturn memory(argmem: readwrite)
declare void @llvm.memcpy.p0.p0.i64(ptr noalias nocapture writeonly, ptr noalias nocapture readonly, i64, i1 immarg) #1

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }
attributes #1 = { nocallback nofree nounwind willreturn memory(argmem: readwrite) }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
