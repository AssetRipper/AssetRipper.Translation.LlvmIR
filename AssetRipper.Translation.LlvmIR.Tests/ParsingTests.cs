using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using NUnit.Framework;

namespace AssetRipper.Translation.LlvmIR.Tests;

public class ParsingTests
{
	static readonly string[] demangledStrings =
	[
		"void __cdecl fpng::fpng_init(void)",
		"bool __cdecl fpng::fpng_cpu_supports_sse41(void)",
		"unsigned int __cdecl fpng::fpng_crc32(void const *, unsigned __int64, unsigned int)",
		"bool __cdecl fpng::fpng_encode_image_to_memory(void const *, unsigned int, unsigned int, unsigned int, class std::vector<unsigned char, class std::allocator<unsigned char>> &, unsigned int)",
		"public: __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::vector<unsigned char, class std::allocator<unsigned char>>(void)",
		"public: unsigned char & __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::operator[](unsigned __int64)",
		"public: __cdecl std::allocator<unsigned char>::allocator<unsigned char>(void)",
		"public: virtual __cdecl std::allocator<unsigned char>::~allocator<unsigned char>(void)",
		"private: void __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::_Tidy(void)",
		"void __cdecl std::_Throw_bad_array_new_length(void)",
		"public: unsigned __int64 __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::size(void) const",
		"public: virtual char const * __cdecl std::exception::what(void) const",
		"public: virtual void * __cdecl std::bad_alloc::`scalar deleting dtor'(unsigned int)",
		"public: __cdecl std::vector<unsigned char, class std::allocator<unsigned char>>::_Reallocation_guard::~_Reallocation_guard(void)",
		"auto __cdecl std::_To_address<unsigned char *>(unsigned char *const &)",
		"private: static void __cdecl std::vector<unsigned int, class std::allocator<unsigned int>>::_Xlength(void)",
		"decltype(auto) __cdecl std::_Get_unwrapped<unsigned char *const &>(unsigned char *const &)",
		"union __clang::__vector<int, 4> __cdecl add<union __clang::__vector<int, 4>>(union __clang::__vector<int, 4>, union __clang::__vector<int, 4>)",
		"void __cdecl stbi__start_write_callbacks(struct stbi__write_context *, void (__cdecl *)(void *, void *, int), void *)",
		"int __cdecl stbiw__jpg_processDU(struct stbi__write_context *, int *, int *, float *, int, float *, int, unsigned short const (*const)[2], unsigned short const (*const)[2])",
		"class std::basic_ostream<char, struct std::char_traits<char>> & __cdecl std::operator<<<struct std::char_traits<char>>(class std::basic_ostream<char, struct std::char_traits<char>> &, char const *)",
		"public: class std::basic_ostream<char, struct std::char_traits<char>> & __cdecl std::basic_ostream<char, struct std::char_traits<char>>::operator<<(class std::basic_ostream<char, struct std::char_traits<char>> & (__cdecl *)(class std::basic_ostream<char, struct std::char_traits<char>> &))",
		"public: bool __cdecl std::basic_ostream<char, struct std::char_traits<char>>::sentry::operator bool(void) const",
		"class std::error_code __cdecl std::make_error_code(enum std::io_errc)",
		"void __cdecl `dynamic atexit destructor for '`class std::_Iostream_error_category2 const & __cdecl std::_Immortalize_memcpy_image<class std::_Iostream_error_category2>(void)'::`2'::_Static''(void)",
		"private: class std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>> & __cdecl std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>>::_Reallocate_grow_by<class `public: class std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>> & __cdecl std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>>::append(char const *const, unsigned __int64)'::`1'::<lambda_1>, char const *, unsigned __int64>(unsigned __int64, class `public: class std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>> & __cdecl std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>>::append(char const *const, unsigned __int64)'::`1'::<lambda_1>, char const *, unsigned __int64)",
		"public: static <auto> __cdecl `public: class std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>> & __cdecl std::basic_string<char, struct std::char_traits<char>, class std::allocator<char>>::append(char const *const, unsigned __int64)'::`1'::<lambda_1>::operator()(char *const, char const *const, unsigned __int64, char const *const, unsigned __int64)",
		"void * __cdecl operator new(unsigned __int64)",
		"void __cdecl operator delete(void *, unsigned __int64)",
		"void * __cdecl operator new[](unsigned __int64)",
		"void __cdecl operator delete[](void *)",
		"void __cdecl crnd::utils::zero_object<unsigned int[17]>(unsigned int (&)[17])",
		"unsigned int __cdecl crnd::crnd_crn_format_to_fourcc(enum crn_format)",
		"public: unsigned int __cdecl crnd::crn_packed_uint<2>::operator unsigned int(void) const",
	];

	[TestCaseSource(nameof(demangledStrings))]
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
			Assert.That(functionIdentifier, Is.EqualTo("operator[]"));
			Assert.That(functionName, Is.EqualTo("operator[]")); // Maybe want operator[] instead?
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
			Assert.Fail($"Parsing error: {node}");
		}

		void IParseTreeListener.VisitTerminal(ITerminalNode node)
		{
		}
	}
}
