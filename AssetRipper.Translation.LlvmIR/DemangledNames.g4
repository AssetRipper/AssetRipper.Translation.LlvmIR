grammar DemangledNames;

function
    : functionPrefix functionReturnType vcSpecificModifer functionDeclaringScope functionName LeftParen functionParameters RightParen functionSuffix
    ;

functionPrefix
    : accessModifier? (Virtual | Static)?
    ;

functionSuffix
    : Const?
    ;

functionReturnType
    : type?
    ;

functionDeclaringScope
    : (qualifiedTypeIdentifier Colon Colon)?
    ;

functionParameter
    : type Const? And?
    ;

functionParameters
    : functionParameter (Comma functionParameter)*
    | 
    ;

templateParameter
    : type Const? And?
    | Number
    ;

template
    : Less templateParameter (Comma templateParameter)* Greater
    | Less Greater
    | 
    ;

templateNotNull
    : Less templateParameter (Comma templateParameter)* Greater
    | Less Greater
    ;

functionName
    : functionIdentifier template (LeftBracket RightBracket)
    | functionIdentifier templateNotNull templateNotNull
    | functionIdentifier template
    ;

identifier
    : Identifier
    | EscapedString
    ;

functionIdentifier
    : operator
    | Tilde? identifier+
    | '_Static' SingleQuote SingleQuote
    ;

type
    : (Class | Enum | Struct | Union)? qualifiedTypeIdentifier Const? (Star | And)*
    | type LeftParen Star Const RightParen LeftBracket Number RightBracket // constant array reference
    | type LeftParen And RightParen LeftBracket Number RightBracket // mutable array reference
    | type LeftBracket Number RightBracket // array type
    | type LeftParen vcSpecificModifer Star RightParen LeftParen functionParameters RightParen // function pointer type
    ;

typeIdentifier
    : Void
    | DeclTypeAuto
    | numericType
    | identifier+ template
    | Less identifier+ Greater
    ;

qualifiedTypeIdentifier
    : qualifiedTypeIdentifier Colon Colon typeIdentifier
    | typeIdentifier
    ;

accessModifier
    : 'public' Colon
    | 'protected' Colon
    | 'private' Colon
    ;

vcSpecificModifer
    : CDecl
    | CLRCall
    | StdCall
    | FastCall
    | ThisCall
    | VectorCall
    ;

numericType
    : Bool
    | Float
    | Unsigned integerType
    | Signed integerType
    | integerType
    ;

integerType
    : Char
    | Short
    | Int
    | Int64
    | Long Long
    | Long
    ;

operator
    : 'operator' operatorName
    ;

operatorName
    : 'new' LeftBracket RightBracket
    | 'delete' LeftBracket RightBracket
    | 'new'
    | 'delete'
    | numericType
    | Less Less
    | Greater Greater
    | Less
    | Greater
    | LeftBracket RightBracket
    | LeftParen RightParen
    ;

EscapedString
    : '`' ~[']* '\''
    ;

Bool
    : 'bool'
    ;

CDecl
    : '__cdecl'
    ;

CLRCall
    : '__clrcall'
    ;

Char
    : 'char'
    ;

Class
    : 'class'
    ;

Const
    : 'const'
    ;

DeclTypeAuto
    : 'decltype(auto)'
    ;

Enum
    : 'enum'
    ;

FastCall
    : '__fastcall'
    ;

Float
    : 'float'
    ;

Int
    : 'int'
    ;

Int64
    : '__int64'
    ;

Long
    : 'long'
    ;

Short
    : 'short'
    ;

Signed
    : 'signed'
    ;

Static
    : 'static'
    ;

StdCall
    : '__stdcall'
    ;

Struct
    : 'struct'
    ;

ThisCall
    : '__thiscall'
    ;

Union
    : 'union'
    ;

Unsigned
    : 'unsigned'
    ;

VectorCall
    : '__vectorcall'
    ;

Virtual
    : 'virtual'
    ;

Void
    : 'void'
    ;

LeftParen
    : '('
    ;

RightParen
    : ')'
    ;

Less
    : '<'
    ;

Greater
    : '>'
    ;

Star
    : '*'
    ;

And
    : '&'
    ;

Tilde
    : '~'
    ;

Colon
    : ':'
    ;

Comma
    : ','
    ;

BackTick
    : '`'
    ;

SingleQuote
    : '\''
    ;

DoubleQuote
    : '"'
    ;

LeftBrace
    : '{'
    ;

RightBrace
    : '}'
    ;

LeftBracket
    : '['
    ;

RightBracket
    : ']'
    ;

Identifier
    : IdentifierNondigit (IdentifierNondigit | Digit)*
    ;

Number
    : NonZeroDigit (Digit)*
    | '0'
    ;

fragment IdentifierNondigit
    : Nondigit
    | UniversalCharacterName
    //|   // other implementation-defined characters...
    ;

fragment Nondigit
    : [a-zA-Z_]
    ;

fragment Digit
    : [0-9]
    ;

fragment NonZeroDigit
    : [1-9]
    ;

fragment UniversalCharacterName
    : '\\u' HexQuad
    | '\\U' HexQuad HexQuad
    ;

fragment HexQuad
    : HexadecimalDigit HexadecimalDigit HexadecimalDigit HexadecimalDigit
    ;

fragment HexadecimalDigit
    : [0-9a-fA-F]
    ;

Whitespace
    : [ \t]+ -> channel(HIDDEN)
    ;

Newline
    : ('\r' '\n'? | '\n') -> channel(HIDDEN)
    ;

