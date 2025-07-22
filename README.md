This project transpiles LLVM IR to .NET CIL.

# Clang Compilation Options

While this project is intended to handle any LLVM IR, it works best when the input is generated on Windows with the following Clang command:

```
clang -g -fno-discard-value-names -fstandalone-debug -S -emit-llvm -o {outputFile} {inputFile}
```

This ensures that debug information is included and that parameter names are preserved, which helps with generating code that is more readable.

# Large Projects with CMake on Windows

## Install [Ninja](https://ninja-build.org/)

Use `ninja --version` to verify that it's been added to the PATH.

## Create `clang-msvc.cmake`

It should be placed in the same directory as the C++ source files and contain the following:

```cmake
set(CMAKE_SYSTEM_NAME Windows)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

set(CMAKE_C_COMPILER "clang-cl")
set(CMAKE_CXX_COMPILER "clang-cl")

set(CMAKE_LINKER "lld-link")
```

## Run CMake

```
cmake -G "Ninja" \
  -S ./path-to-directory-with-source-files/ \
  -B ./path-to-build-output-directory/ \
  -DCMAKE_TOOLCHAIN_FILE=./clang-msvc.cmake \
  -DCMAKE_C_FLAGS_DEBUG="-g -fno-discard-value-names -fstandalone-debug -emit-llvm -w" \
  -DCMAKE_CXX_FLAGS_DEBUG="-g -fno-discard-value-names -fstandalone-debug -emit-llvm -w" \
  -DCMAKE_EXPORT_COMPILE_COMMANDS=ON
```

This will generate a `compile_commands.json` file in the build output directory, which is necessary for the next step.

### Flags

* `-g` ensures that debug information is included.
* `-fno-discard-value-names` preserves parameter names in the generated LLVM IR.
* `-fstandalone-debug` ensures that the debug information is self-contained.
* `-emit-llvm` generates LLVM IR instead of object files.
* `-w` suppresses warnings, which can be useful for large projects.

## Compile the project

```
LargeProjectCompiler.exe ./path-to/compile_commands.json
```

Use `--help` to see additional options.
