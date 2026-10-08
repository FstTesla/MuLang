# 15. Grammar summary

This grammar describes the complete syntax for language version 1.2. Language
version 1.1 treats `catch`, `finally`, `primitive`, `throw`, and `try` as
identifiers, and language version 1 also rejects the read-only array additions.
A selected language profile may reject otherwise recognized function, loop,
mutation, open-object, trailing-comma, or exception-handling constructs as
defined in [Section 14](14-language-profiles.md).

```ebnf
expression-root
    = expression end-of-file ;

program-root
    = function-declaration* statement* end-of-file ;

function-declaration
    = "func" identifier
      "(" parameter-list? ")"
      ":" function-return-type
      block ;

parameter-list
    = parameter ("," parameter)* ;

parameter
    = identifier ":" type ;

function-return-type
    = type
    | "void" ;

statement
    = block
    | variable-declaration ";"
    | assignment ";"
    | removal ";"
    | call-expression ";"
    | if-statement
    | while-statement
    | for-statement
    | try-statement
    | throw-statement ";"
    | "break" integer-literal? ";"
    | "continue" integer-literal? ";"
    | return-statement ";"
    | ";" ;

block
    = "{" statement* "}" ;

variable-declaration
    = "var" identifier "$"?
      (":" type ("=" expression)?
      | "=" expression) ;

assignment
    = assignable "=" expression ;

assignable
    = identifier
    | property-access
    | element-access ;

removal
    = removable "~" ;

removable
    = property-access
    | element-access ;

if-statement
    = "if" "(" expression ")" statement
      ("else" statement)? ;

while-statement
    = "while" "(" expression ")" statement ;

for-statement
    = "for" "(" for-initializer? ";"
                expression? ";"
                for-iterator? ")"
      statement ;

for-initializer
    = variable-declaration
    | assignment
    | call-expression ;

for-iterator
    = assignment
    | call-expression ;

return-statement
    = "return" expression? ;

try-statement
    = "try" block
      (catch-clause finally-clause?
      | finally-clause) ;

catch-clause
    = "catch" ("(" identifier ")")? block ;

finally-clause
    = "finally" block ;

throw-statement
    = "throw" expression? ;

type
    = primary-type nullable-suffix?
      (array-suffix read-only-suffix? nullable-suffix?)* ;

primary-type
    = "bool"
    | "int"
    | "float"
    | "number"
    | "primitive"
    | "error"
    | "string"
    | "unknown"
    | "object"
    | host-type-name ;

array-suffix
    = "[]" ;

read-only-suffix
    = "$" ;

nullable-suffix
    = "?" ;

array-literal
    = "[" (expression ("," expression)* ","?)? "]" ;

read-only-array-literal
    = "$[" (expression ("," expression)* ","?)? "]" ;

non-finite-float-literal
    = ("+" | "-")? ("infty" | "nan") ;

closed-object-literal
    = "{" property-initializer-list? "}" ;

open-object-literal
    = "@{" property-initializer-list? "}" ;

property-initializer-list
    = property-initializer ("," property-initializer)* ","? ;

property-initializer
    = (identifier | string-literal)
      "$"? "?"?
      (":" type)?
      ("=" expression)? ;
```

Expression grammar is defined by the precedence table rather than expanded in this summary.

The property modifier order is fixed. Required properties and every property
without an explicit type require an initializer. Profiles selecting
`ObjectLiteralSyntax.Legacy` instead use
`(identifier | string-literal) (":" | "?" ":") expression`.
