# Type Inference Situations

> Unless explicitly said otherwise, examples in this document are self-contained. Overlapping names will often be coincidental since clang uses numbers.
>
> I have ignored vector types when writing this document.

## 3 Type Categories

* `void`
* `struct`s, including numeric primitives
* Pointers

Anything can be placed into one of these three categories without inference. This is useful for ensuring that type violations don't occur, like assigning a pointer type to a struct variable.

## Bit Vector Integer Types

Integer types like `i64` do not exactly specify the type of a value. A type with the same bit-width might have been replaced with this integer type during compiler optimization.

We contrain this value's type to be all non-pointer types with the correct bit-width.

In the objective function, we have a slight preference for the integer type.

## Opaque Pointer Types

LLVM pointers are opaque, ie they contain no type information, not even in struct definitions.

```llvm
%struct.Vector2F = type { float, float }
%struct.WeirdStruct = type { ptr, %struct.Vector2F }
```

In the objective function, we give a slight preference for types with lower pointer indirection and a strong preference against `void*`.

## Numeric Constants

We treat these as implicitly convertable to any primitive type.

```llvm
%5 = icmp slt i32 %4, 0
```

```llvm
%8 = sub nsw i32 0, %7
```

LLVM assigns them a natural type (typically `i32`), which we give a slight preference for.

## Intra-Instruction Pointer Relationship in Memory Instructions

Some instructions imply a pointer-based relationship between their inputs and/or output. This relationship is exact and therefore does not affect the objective function.

```llvm
%3 = alloca %struct.Vector2F, align 4
```

`%3` should have type `Vector2F*`.

```llvm
%4 = alloca ptr, align 8
```

In this case, a relationship still exists, but in the reverse direction. The allocated type will be inferred from the pointer type.

```llvm
%7 = load float, ptr %6, align 4
```

The information in this instruction implies that `%7` is `float` and the operand (not `%6`) is `float*`.

```llvm
%10 = load i64, ptr %7, align 4
```

This looks very similar to the previous one, but it's slightly different. The relationship between the result and the operand is still the same, but [integer types like `i64` do not exactly specify their type](#bit-vector-integer-types).

```llvm
store i64 %0, ptr %5, align 4
```

As above, the second operand is the pointer type of the first operand.

## Math Instructions

For math instructions, the result and the operands all have the same type. This includes the bit shift instructions, which one might expect to be an exception.

```llvm
%12 = fadd float %9, %11
```

Math instructions are an exception to the [integer type ambiguity](#bit-vector-integer-types). Types here are exactly as they say.

## Phi Instruction

Similar to the math instructions, the result and the operands all have the same type.

```llvm
%11 = phi i32 [ %7, %5 ], [ %9, %8 ]
```

LLVM decodes this as two operands and two labels. `%5` and `%8` are labels.

## GEP Index Operands

These are all `int`.

## GEP Source Element Type

This is an exact struct defined by LLVM.

## GEP Final Type

This can only be determined when all the field indices are constant. Array indices can be variable.

## GEP Result

This is the pointer type for the final type.

## Numeric Conversion Result

These specify the result type explicitly.

```llvm
%4 = zext i32 %3 to i64
```

```llvm
%4 = trunc i64 %3 to i32
```

## Numeric Comparision Result

This is always a boolean.

```llvm
%4 = icmp ne i32 %3, 0
```

## Struct Return

Some structs are too large (more than 64 bits) to return. When this happens, the compiler converts them into the first parameter and provides some type information.

```llvm
define dso_local void @vector4f_add(ptr dead_on_unwind noalias writable sret(%struct.Vector4F) align 4 %0, ptr noundef %1, ptr noundef %2) #0 {
```

In this case, the "real" return type is `Vector4F`, so `%0` is `Vector4F*`.

## Intrinsic Functions

These functions are predefined by LLVM and may already have a C# implementation. The C# implementation will have the correct method signature.

```llvm
; Function Attrs: nocallback nofree nounwind willreturn memory(argmem: readwrite)
declare void @llvm.memcpy.p0.p0.i64(ptr noalias nocapture writeonly, ptr noalias nocapture readonly, i64, i1 immarg) #1
```

```cs
internal static partial class IntrinsicFunctions
{
    public unsafe static void llvm_memcpy_p0_p0_i64(void* destination, void* source, long length, bool isVolatile)
    {
        Unsafe.CopyBlock(destination, source, (uint)length);
    }
}
```

Intrinsic functions have unique names.

## Value Passing

In various places, we transfer a value from a provider to a receiver.

* Instruction Results as Operands
* Return Operand as Function Return
* Call Operands as Function Parameters
* Function Return as Call Result

This provides additional information we can use for inference.

### Struct Types

At the very least, these must have the same bit-width.

### Pointer Types

There don't *have* to be any hard restrictions.

Rules for the objective function:

* Obviously, it would be best if they're identically typed.
* Changing types is penalized. This is the order of preference from most-prefered to least-prefered:
  1. General to specific (`void*` -> `char*`)
  2. Specific to specific (`int*` -> `char*`)
  3. Specific to general (`int*` -> `void*`)
* Changing the indirection level is penalized.
