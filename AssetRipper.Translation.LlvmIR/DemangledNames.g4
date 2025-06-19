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
    : (typeIdentifier Colon Colon)?
    ;

functionParameter
    : type Const? And?
    ;

functionParameters
    : functionParameter (Comma functionParameter)*
    | 
    ;

templaceParameter
    : type Const? And?
    | Number
    ;

template
    : Less templaceParameter (Comma templaceParameter)* Greater
    | Less Greater
    | 
    ;

templateNotNull
    : Less templaceParameter (Comma templaceParameter)* Greater
    | Less Greater
    ;

functionName
    : functionIdentifier template (LeftBracket RightBracket)
    | functionIdentifier templateNotNull templateNotNull
    | functionIdentifier template
    ;

functionIdentifier
    : Tilde? Identifier
    | ScalarDeletingDestructor
    ;

type
    : (Class | Struct)? typeIdentifier Const? (Star | And)*
    ;

typeIdentifier
    : Void
    | DeclTypeAuto
    | numericType
    | typeIdentifier Colon Colon Identifier template
    | Identifier template
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
    | 
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

ScalarDeletingDestructor
    : '`scalar deleting dtor\''
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

