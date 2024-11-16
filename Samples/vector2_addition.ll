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

attributes #0 = { mustprogress noinline nounwind optnone uwtable "min-legal-vector-width"="0" "no-trapping-math"="true" "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+cmov,+cx8,+fxsr,+mmx,+sse,+sse2,+x87" "tune-cpu"="generic" }
attributes #1 = { nocallback nofree nounwind willreturn memory(argmem: readwrite) }

!llvm.module.flags = !{!0, !1, !2, !3}
!llvm.ident = !{!4}

!0 = !{i32 1, !"wchar_size", i32 2}
!1 = !{i32 8, !"PIC Level", i32 2}
!2 = !{i32 7, !"uwtable", i32 2}
!3 = !{i32 1, !"MaxTLSAlign", i32 65536}
!4 = !{!"clang version 18.1.8"}
