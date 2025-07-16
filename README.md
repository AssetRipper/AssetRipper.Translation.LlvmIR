This project transpiles LLVM IR to .NET CIL.

# Clang Compilation Options

While this project is intended to handle any LLVM IR, it works best when the input is generated on Windows with the following Clang command:

```
clang -g -fno-discard-value-names -fstandalone-debug -S -emit-llvm -o {outputFile} {inputFile}
```

This ensures that debug information is included and that parameter names are preserved, which helps with generating code that is more readable.
