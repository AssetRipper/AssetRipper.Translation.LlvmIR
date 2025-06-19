using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using NUnit.Framework;

namespace AssetRipper.Translation.LlvmIR.Tests;

public class ParsingTests
{
	[TestCase("void __cdecl fpng::fpng_init(void)")]
	[TestCase("bool __cdecl fpng::fpng_cpu_supports_sse41(void)")]
	[TestCase("unsigned int __cdecl fpng::fpng_crc32(void const *, unsigned __int64, unsigned int)")]
	[TestCase("bool __cdecl fpng::fpng_encode_image_to_memory(void const *, unsigned int, unsigned int, unsigned int, class std::vector<unsigned char, class std::allocator<unsigned char>> &, unsigned int)")]
	[TestCase("public: __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::vector<unsigned char, class std::allocator<unsigned char>>(void)")]
	[TestCase("public: unsigned char & __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::operator[](unsigned __int64)")]
	[TestCase("public: __cdecl std::allocator<unsigned char>::allocator<unsigned char>(void)")]
	[TestCase("public: virtual __cdecl std::allocator<unsigned char>::~allocator<unsigned char>(void)")]
	[TestCase("private: void __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::_Tidy(void)")]
	[TestCase("void __cdecl std::_Throw_bad_array_new_length(void)")]
	[TestCase("public: unsigned __int64 __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::size(void) const")]
	[TestCase("public: virtual char const * __cdecl std::exception::what(void) const")]
	[TestCase("public: virtual void * __cdecl std::bad_alloc::`scalar deleting dtor'(unsigned int)")]
	[TestCase("public: __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::_Reallocation_guard::~_Reallocation_guard(void)")]
	[TestCase("auto __cdecl std::_To_address<unsigned char *>(unsigned char *const &)")]
	[TestCase("private: static void __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::_Xlength(void)")]
	[TestCase("decltype(auto) __cdecl std::_Get_unwrapped<unsigned char *const &>(unsigned char *const &)")]
	public void ParsingSucceeds(string input)
	{
		IParseTree tree = DemangledNamesParser.ParseFunction(input);
		ErrorListener.AssertNoErrors(tree);
	}

	[Test]
	public void ExtractionSucceeds()
	{
		string input = "public: unsigned char & __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::operator[](unsigned __int64)";
		bool success = DemangledNamesParser.ParseFunction(
			input,
			out string? returnType,
			out string? @namespace,
			out string? typeName,
			out string? functionIdentifier,
			out string? functionName,
			out string[]? templateParameters,
			out string[]? normalParameters);

		Assert.That(success, Is.True);
		using (Assert.EnterMultipleScope())
		{
			Assert.That(returnType, Is.EqualTo("unsigned char &"));
			Assert.That(@namespace, Is.EqualTo("std"));
			Assert.That(typeName, Is.EqualTo("vector<unsigned char, class std::allocator<unsigned char>>"));
			Assert.That(functionIdentifier, Is.EqualTo("operator"));
			Assert.That(functionName, Is.EqualTo("operator")); // Maybe want operator[] instead?
			Assert.That(templateParameters, Is.Empty);
			Assert.That(normalParameters, Is.EqualTo(new[] { "unsigned __int64" }));
		}
	}

	private sealed class ErrorListener : IParseTreeListener
	{
		public static void AssertNoErrors(IParseTree tree)
		{
			ErrorListener listener = new();
			ParseTreeWalker.Default.Walk(listener, tree);
		}

		void IParseTreeListener.EnterEveryRule(ParserRuleContext ctx)
		{
		}

		void IParseTreeListener.ExitEveryRule(ParserRuleContext ctx)
		{
		}

		void IParseTreeListener.VisitErrorNode(IErrorNode node)
		{
			Assert.Fail($"Parsing error: {node.ToString()}");
		}

		void IParseTreeListener.VisitTerminal(ITerminalNode node)
		{
		}
	}
}
