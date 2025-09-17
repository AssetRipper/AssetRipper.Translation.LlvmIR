This project transpiles LLVM IR to .NET CIL.

# Clang Compilation Options

While this project is intended to handle any LLVM IR, it works best when the input is generated on Windows with the following Clang command:

```
clang -g -fno-discard-value-names -fstandalone-debug -S -emit-llvm {inputFile} -o {outputFile}
```

This ensures that debug information is included and that parameter names are preserved, which helps with generating code that is more readable.

# Large Projects with CMake on Windows

## Install [Ninja](https://ninja-build.org/)

Use `ninja --version` to verify that it's been added to the PATH.

## Run CMake

```
cmake -G "Ninja" \
  -S ./path-to-directory-with-source-files/ \
  -B ./path-to-build-output-directory/ \
  -DCMAKE_C_COMPILER=clang \
  -DCMAKE_CXX_COMPILER=clang++ \
  -DCMAKE_C_FLAGS="-DNOMINMAX" \
  -DCMAKE_CXX_FLAGS="-DNOMINMAX" \
  -DCMAKE_EXPORT_COMPILE_COMMANDS=ON
```

This will generate a `compile_commands.json` file in the build output directory, which is necessary for a future step.

### Flags

* `-DNOMINMAX` ensures that the `min` and `max` macros do no interfere with compilation.

## Compile the project normally

```
cmake --build ./path-to-build-output-directory/ --config Debug
```

This ensures that any generated files get generated.

## Compile the project to LLVM IR

```
LargeProjectCompiler.exe ./path-to/compile_commands.json
```

Use `--help` to see additional options.
