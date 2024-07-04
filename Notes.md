# Notes

* Calling conventions are stored in attributes. See: https://llvm.org/docs/LangRef.html#function-attributes
* There are workarounds to compile msvc projects with `clang`. `clangcl` is a replacement for `cl.exe`.
  * `cmake -S . -B build -G "Visual Studio 16 2019" -T ClangCL`