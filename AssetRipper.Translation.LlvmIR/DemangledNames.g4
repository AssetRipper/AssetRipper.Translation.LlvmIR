grammar DemangledNames;

function
    : functionPrefix functionReturnType CallingConvention functionDeclaringScope functionName LeftParen functionParameters RightParen functionSuffix
    ;

functionPrefix
    : (AccessModifier Colon)? (Virtual | Static)?
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
    | AccessModifier
    | CallingConvention
    | NewOrDelete
    | TypeKeyword
    | Bool
    | Char
    | Const
    | Float
    | Int
    | Int64
    | Long
    | Operator
    | Short
    | Signed
    | Static
    | Unsigned
    | Virtual
    | Void
    ;

functionIdentifier
    : operator
    | Tilde? identifier+
    | '_Static' SingleQuote SingleQuote
    ;

type
    : TypeKeyword? qualifiedTypeIdentifier Const? (Star | And)*
    | type LeftParen Star Const RightParen LeftBracket Number RightBracket // constant array reference
    | type LeftParen And RightParen LeftBracket Number RightBracket // mutable array reference
    | type LeftBracket Number RightBracket // array type
    | type LeftParen CallingConvention Star RightParen LeftParen functionParameters RightParen // function pointer type
    ;

typeIdentifier
    : Void
    | DeclTypeAuto
    | numericType
    | identifier+ template
    | Less identifier+ Greater
    | Less identifier (Minus identifier)* Greater
    ;

qualifiedTypeIdentifier
    : qualifiedTypeIdentifier Colon Colon typeIdentifier
    | typeIdentifier
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
    : Operator operatorName
    ;

operatorName
    : NewOrDelete LeftBracket RightBracket
    | NewOrDelete
    | numericType
    | shiftOperator Equals?
    | arithmeticOperator Equals?
    | logicalOperator Equals?
    | relationalOperator
    | Exclamation
    | Equals
    | Plus Plus
    | Minus Minus
    | LeftBracket RightBracket
    | LeftParen RightParen
    ;

arithmeticOperator
    : Plus
    | Minus
    | Star
    | Slash
    | Modulo
    ;

relationalOperator
    : Less Equals?
    | Greater Equals?
    | Equals Equals
    | Exclamation Equals
    ;

logicalOperator
    : And
    | Pipe
    | Caret
    | Tilde
    ;

shiftOperator
    : Less Less
    | Greater Greater
    ;

EscapedString
    : '`' ~[']* '\''
    ;

AccessModifier
    : 'public'
    | 'protected'
    | 'private'
    ;

CallingConvention
    : '__cdecl'
    | '__clrcall'
    | '__stdcall'
    | '__fastcall'
    | '__thiscall'
    | '__vectorcall'
    ;

NewOrDelete
    : 'new'
    | 'delete'
    ;

TypeKeyword
    : 'class'
    | 'struct'
    | 'union'
    | 'enum'
    ;

Bool
    : 'bool'
    ;

Char
    : 'char'
    ;

Const
    : 'const'
    ;

DeclTypeAuto
    : 'decltype(auto)'
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

Operator
    : 'operator'
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

Unsigned
    : 'unsigned'
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

Equals
    : '='
    ;

Plus
    : '+'
    ;

Minus
    : '-'
    ;

Star
    : '*'
    ;

Slash
    : '/'
    ;

Modulo
    : '%'
    ;

And
    : '&'
    ;

Pipe
    : '|'
    ;

Caret
    : '^'
    ;

Tilde
    : '~'
    ;

Exclamation
    : '!'
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

