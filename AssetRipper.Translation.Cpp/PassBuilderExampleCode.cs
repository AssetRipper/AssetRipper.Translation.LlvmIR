namespace AssetRipper.Translation.Cpp;

public static class PassBuilderExampleCode
{
	private const string CppSampleCode = """
		#include "llvm/IR/Function.h"
		#include "llvm/IR/LLVMContext.h"
		#include "llvm/IR/Module.h"
		#include "llvm/Passes/PassBuilder.h"

		llvm::Function *func;  // Assuming func is your LLVM function object
		llvm::LLVMContext context;

		llvm::PassBuilder passBuilder;
		auto functionPassManager = llvm::make_unique<llvm::FunctionPassManager>(func->getParent());
		functionPassManager->add(llvm::createPromoteMemoryToRegisterPass());  // Mem2Reg

		functionPassManager->doInitialization();
		functionPassManager->run(*func);
		functionPassManager->doFinalization();
		""";
}
