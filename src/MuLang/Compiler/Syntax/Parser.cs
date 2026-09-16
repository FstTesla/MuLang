using MuLang.Compiler.Diagnostics;
using MuLang.Core;
using MuLang.Core.Diagnostics;
using MuLang.Core.Text;

namespace MuLang.Compiler.Syntax;

internal sealed class Parser
{
    private readonly SourceText source;
    private readonly IReadOnlyList<SyntaxToken> tokens;
    private readonly LanguageProfile profile;
    private readonly ICollection<Diagnostic> diagnostics = [ ];
    private int position;

    private Parser(
        SourceText source,
        IReadOnlyList<SyntaxToken> tokens,
        LanguageProfile profile
    )
    {
        this.source = source;
        this.tokens = tokens;
        this.profile = profile;
    }

    public static SyntaxTree Parse(SourceText source, CompilationMode compilationMode)
    {
        return Parse(source, compilationMode, LanguageProfiles.Version1);
    }

    public static SyntaxTree Parse(
        SourceText source,
        CompilationMode compilationMode,
        LanguageProfile profile
    )
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (!Enum.IsDefined(compilationMode))
        {
            throw new ArgumentOutOfRangeException(nameof(compilationMode));
        }

        if (profile is null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        LexResult lexResult = Lexer.Lex(source, profile);
        Parser parser = new (source, lexResult.Tokens, profile);
        RootSyntax root = compilationMode switch
        {
            CompilationMode.Expression => parser.Current.Kind == TokenKind.FuncKeyword
                ? parser.ParseRejectedFunctionDeclarationExpressionRoot()
                : parser.ParseExpressionRoot(),
            CompilationMode.Program => parser.ParseProgramRoot(),
            _ => throw new ArgumentOutOfRangeException(nameof(compilationMode)),
        };
        DiagnosticCollection diagnostics = DiagnosticCollection.Create(
            lexResult.Diagnostics.Concat(parser.diagnostics)
        );

        return new SyntaxTree(source, compilationMode, profile, root, diagnostics);
    }

    private SyntaxToken Current => Peek(0);

    private SyntaxToken ParseToken()
    {
        SyntaxToken current = Current;

        if (position < tokens.Count)
        {
            position++;
        }

        return current;
    }

    private SyntaxToken Peek(int offset)
    {
        int index = position + offset;

        return index >= tokens.Count
            ? tokens[^1]
            : tokens[index];
    }

    private ExpressionRootSyntax ParseExpressionRoot()
    {
        ExpressionSyntax expression = ParseExpression();
        SyntaxToken endOfFileToken = Match(TokenKind.EndOfFile);

        return new ExpressionRootSyntax(expression, endOfFileToken);
    }

    private ExpressionRootSyntax ParseRejectedFunctionDeclarationExpressionRoot()
    {
        SyntaxToken funcKeyword = Current;
        ReportDisabledUserDefinedFunctions(funcKeyword);
        Report(
            DiagnosticCodes.FunctionDeclarationNotAllowed,
            funcKeyword.Span,
            "Function declarations are only valid at the start of a program."
        );
        ParseFunctionDeclaration();
        SyntaxToken endOfFileToken = Match(TokenKind.EndOfFile);
        SyntaxToken missingToken = new (
            TokenKind.Identifier,
            new TextSpan(funcKeyword.Span.Start, 0),
            IsMissing: true
        );

        return new ExpressionRootSyntax(
            new MissingExpressionSyntax(missingToken),
            endOfFileToken
        );
    }

    private ProgramRootSyntax ParseProgramRoot()
    {
        IList<FunctionDeclarationSyntax> functions = [ ];
        IList<StatementSyntax> statements = [ ];
        bool hasExecutableStatement = false;

        while (Current.Kind != TokenKind.EndOfFile)
        {
            int start = position;

            if (Current.Kind == TokenKind.FuncKeyword)
            {
                ReportDisabledUserDefinedFunctions(Current);

                if (hasExecutableStatement)
                {
                    Report(
                        DiagnosticCodes.FunctionDeclarationAfterStatement,
                        Current.Span,
                        "Function declarations must appear before executable statements."
                    );
                }

                functions.Add(ParseFunctionDeclaration());
            }
            else
            {
                hasExecutableStatement = true;
                statements.Add(ParseStatement());
            }

            if (position == start)
            {
                ParseToken();
            }
        }

        SyntaxToken endOfFileToken = Match(TokenKind.EndOfFile);

        return new ProgramRootSyntax(
            functions.AsReadOnly(),
            statements.AsReadOnly(),
            endOfFileToken
        );
    }

    private FunctionDeclarationSyntax ParseFunctionDeclaration()
    {
        SyntaxToken funcKeyword = Match(TokenKind.FuncKeyword);
        SyntaxToken identifierToken = Match(TokenKind.Identifier);
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        IList<ParameterSyntax> parameters = [ ];
        IList<SyntaxToken> commaTokens = [ ];

        while (Current.Kind is not TokenKind.CloseParenthesis and not TokenKind.EndOfFile)
        {
            int start = position;
            SyntaxToken parameterName = Match(TokenKind.Identifier);
            SyntaxToken parameterColonToken = Match(TokenKind.Colon);
            TypeSyntax parameterType = ParseType(allowVoid: true);
            parameters.Add(
                new ParameterSyntax(
                    parameterName,
                    parameterColonToken,
                    parameterType
                )
            );

            if (Current.Kind != TokenKind.Comma)
            {
                break;
            }

            commaTokens.Add(ParseToken());

            if (Current.Kind == TokenKind.CloseParenthesis)
            {
                Report(
                    DiagnosticCodes.TrailingSeparator,
                    commaTokens[^1].Span,
                    "A trailing comma is not permitted in a parameter list."
                );
                break;
            }

            if (position == start)
            {
                ParseToken();
            }
        }

        if (Current.Kind != TokenKind.CloseParenthesis)
        {
            SynchronizeFunctionHeader();
        }

        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);
        SyntaxToken returnColonToken = Match(TokenKind.Colon);
        TypeSyntax returnType = ParseType(allowVoid: true);
        BlockStatementSyntax body = ParseBlockStatement(true);

        return new FunctionDeclarationSyntax(
            funcKeyword,
            identifierToken,
            openParenthesisToken,
            parameters.AsReadOnly(),
            commaTokens.AsReadOnly(),
            closeParenthesisToken,
            returnColonToken,
            returnType,
            body
        );
    }

    private StatementSyntax ParseStatement()
    {
        return Current.Kind switch
        {
            TokenKind.OpenBrace => ParseBlockStatement(),
            TokenKind.VarKeyword => ParseVariableDeclarationStatement(true),
            TokenKind.IfKeyword => ParseIfStatement(),
            TokenKind.WhileKeyword => ParseWhileStatement(),
            TokenKind.ForKeyword => ParseForStatement(),
            TokenKind.BreakKeyword => ParseBreakStatement(),
            TokenKind.ContinueKeyword => ParseContinueStatement(),
            TokenKind.ReturnKeyword => ParseReturnStatement(),
            TokenKind.FuncKeyword => ParseInvalidNestedFunctionDeclaration(),
            TokenKind.Semicolon => new EmptyStatementSyntax(ParseToken()),
            _ => ParseSimpleStatement(true),
        };
    }

    private void SynchronizeFunctionHeader()
    {
        while (
            Current.Kind is not
            TokenKind.CloseParenthesis and not
            TokenKind.OpenBrace and not
            TokenKind.FuncKeyword and not
            TokenKind.EndOfFile
        )
        {
            ParseToken();
        }
    }

    private StatementSyntax ParseInvalidNestedFunctionDeclaration()
    {
        FunctionDeclarationSyntax declaration = ParseFunctionDeclaration();
        ReportDisabledUserDefinedFunctions(declaration.FuncKeyword);
        Report(
            DiagnosticCodes.FunctionDeclarationNotAllowed,
            declaration.FuncKeyword.Span,
            "Function declarations are not valid inside another function or statement."
        );

        return new EmptyStatementSyntax(declaration.FuncKeyword);
    }

    private BlockStatementSyntax ParseBlockStatement(
        bool stopAtFunctionDeclaration = false
    )
    {
        SyntaxToken openBraceToken = Match(TokenKind.OpenBrace);
        IList<StatementSyntax> statements = [ ];

        while (
            Current.Kind is not TokenKind.CloseBrace and not TokenKind.EndOfFile &&
            !(stopAtFunctionDeclaration && Current.Kind == TokenKind.FuncKeyword)
        )
        {
            int start = position;
            statements.Add(ParseStatement());

            if (position == start)
            {
                ParseToken();
            }
        }

        SyntaxToken closeBraceToken = Match(TokenKind.CloseBrace);

        return new BlockStatementSyntax(
            openBraceToken,
            statements.AsReadOnly(),
            closeBraceToken
        );
    }

    private VariableDeclarationStatementSyntax ParseVariableDeclarationStatement(
        bool includeSemicolon
    )
    {
        SyntaxToken varKeyword = Match(TokenKind.VarKeyword);
        SyntaxToken identifierToken = Match(TokenKind.Identifier);
        SyntaxToken? colonToken = null;
        TypeSyntax? type = null;
        SyntaxToken? equalToken = null;
        ExpressionSyntax? initializer = null;

        if (Current.Kind == TokenKind.Colon)
        {
            colonToken = ParseToken();
            type = ParseType();
        }

        if (Current.Kind == TokenKind.Equal)
        {
            equalToken = ParseToken();
            initializer = ParseExpression();
        }

        if (colonToken is null && equalToken is null)
        {
            ReportUnexpectedToken(Current, TokenKind.Colon, TokenKind.Equal);
        }

        SyntaxToken? semicolonToken = includeSemicolon
            ? Match(TokenKind.Semicolon)
            : null;

        return new VariableDeclarationStatementSyntax(
            varKeyword,
            identifierToken,
            colonToken,
            type,
            equalToken,
            initializer,
            semicolonToken
        );
    }

    private StatementSyntax ParseSimpleStatement(bool includeSemicolon)
    {
        ExpressionSyntax expression = ParseExpression();
        StatementSyntax statement;

        if (Current.Kind == TokenKind.Equal)
        {
            SyntaxToken equalToken = ParseToken();
            ExpressionSyntax value = ParseExpression();

            if (!SyntaxFacts.IsAssignmentTarget(expression))
            {
                Report(
                    DiagnosticCodes.InvalidAssignmentTarget,
                    expression.Span,
                    "The expression is not a valid assignment target."
                );
            }

            statement = new AssignmentStatementSyntax(expression, equalToken, value, null);
        }
        else if (Current.Kind == TokenKind.Tilde)
        {
            SyntaxToken tildeToken = ParseToken();

            if (!profile.Mutations.HasFlag(MutationFeatures.PropertyRemoval))
            {
                Report(
                    DiagnosticCodes.DisabledPropertyRemoval,
                    tildeToken.Span,
                    "Property removal is disabled by the language profile."
                );
            }

            if (!SyntaxFacts.IsRemovalTarget(expression))
            {
                Report(
                    DiagnosticCodes.InvalidRemovalTarget,
                    expression.Span,
                    "The expression is not a valid property removal target."
                );
            }

            statement = new RemovalStatementSyntax(expression, tildeToken, null);
        }
        else
        {
            if (expression is not CallExpressionSyntax and not MissingExpressionSyntax)
            {
                Report(
                    DiagnosticCodes.InvalidExpressionStatement,
                    expression.Span,
                    "Only a function call can be used as an expression statement."
                );
            }

            statement = new ExpressionStatementSyntax(expression, null);
        }

        if (!includeSemicolon)
        {
            return statement;
        }

        SyntaxToken semicolonToken = Match(TokenKind.Semicolon);

        return statement switch
        {
            AssignmentStatementSyntax assignment => assignment with
            {
                SemicolonToken = semicolonToken,
            },
            RemovalStatementSyntax removal => removal with
            {
                SemicolonToken = semicolonToken,
            },
            ExpressionStatementSyntax expressionStatement => expressionStatement with
            {
                SemicolonToken = semicolonToken,
            },
            _ => throw new InvalidOperationException("Unexpected simple statement type."),
        };
    }

    private IfStatementSyntax ParseIfStatement()
    {
        SyntaxToken ifKeyword = Match(TokenKind.IfKeyword);
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        ExpressionSyntax condition = ParseExpression();
        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);
        StatementSyntax thenStatement = ParseStatement();
        SyntaxToken? elseKeyword = null;
        StatementSyntax? elseStatement = null;

        if (Current.Kind == TokenKind.ElseKeyword)
        {
            elseKeyword = ParseToken();
            elseStatement = ParseStatement();
        }

        return new IfStatementSyntax(
            ifKeyword,
            openParenthesisToken,
            condition,
            closeParenthesisToken,
            thenStatement,
            elseKeyword,
            elseStatement
        );
    }

    private WhileStatementSyntax ParseWhileStatement()
    {
        SyntaxToken whileKeyword = Match(TokenKind.WhileKeyword);
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        ExpressionSyntax condition = ParseExpression();
        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);
        StatementSyntax body = ParseStatement();

        return new WhileStatementSyntax(
            whileKeyword,
            openParenthesisToken,
            condition,
            closeParenthesisToken,
            body
        );
    }

    private ForStatementSyntax ParseForStatement()
    {
        SyntaxToken forKeyword = Match(TokenKind.ForKeyword);
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        StatementSyntax? initializer = null;

        if (Current.Kind != TokenKind.Semicolon)
        {
            initializer = Current.Kind == TokenKind.VarKeyword
                ? ParseVariableDeclarationStatement(false)
                : ParseForSimpleClause();
        }

        SyntaxToken firstSemicolonToken = Match(TokenKind.Semicolon);
        ExpressionSyntax? condition = Current.Kind == TokenKind.Semicolon
            ? null
            : ParseExpression();
        SyntaxToken secondSemicolonToken = Match(TokenKind.Semicolon);
        StatementSyntax? iterator = Current.Kind == TokenKind.CloseParenthesis
            ? null
            : ParseForSimpleClause();
        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);
        StatementSyntax body = ParseStatement();

        return new ForStatementSyntax(
            forKeyword,
            openParenthesisToken,
            initializer,
            firstSemicolonToken,
            condition,
            secondSemicolonToken,
            iterator,
            closeParenthesisToken,
            body
        );
    }

    private StatementSyntax ParseForSimpleClause()
    {
        StatementSyntax statement = ParseSimpleStatement(false);

        if (statement is RemovalStatementSyntax)
        {
            Report(
                DiagnosticCodes.InvalidForClause,
                statement.Span,
                "Property removal is not valid in a for clause."
            );
        }

        return statement;
    }

    private BreakStatementSyntax ParseBreakStatement()
    {
        SyntaxToken breakKeyword = Match(TokenKind.BreakKeyword);
        (SyntaxToken? levelSignToken, SyntaxToken? levelToken) =
            ParseOptionalIntegerLiteral();
        ReportDisabledLoopLevel(levelSignToken, levelToken);
        SyntaxToken semicolonToken = Match(TokenKind.Semicolon);

        return new BreakStatementSyntax(
            breakKeyword,
            levelSignToken,
            levelToken,
            semicolonToken
        );
    }

    private ContinueStatementSyntax ParseContinueStatement()
    {
        SyntaxToken continueKeyword = Match(TokenKind.ContinueKeyword);
        (SyntaxToken? levelSignToken, SyntaxToken? levelToken) =
            ParseOptionalIntegerLiteral();
        ReportDisabledLoopLevel(levelSignToken, levelToken);
        SyntaxToken semicolonToken = Match(TokenKind.Semicolon);

        return new ContinueStatementSyntax(
            continueKeyword,
            levelSignToken,
            levelToken,
            semicolonToken
        );
    }

    private (SyntaxToken? SignToken, SyntaxToken? LiteralToken)
        ParseOptionalIntegerLiteral()
    {
        if (Current.Kind == TokenKind.IntegerLiteral)
        {
            return (null, ParseToken());
        }

        if (
            Current.Kind is TokenKind.Plus or TokenKind.Minus &&
            Peek(1).Kind == TokenKind.IntegerLiteral
        )
        {
            return (ParseToken(), ParseToken());
        }

        return (null, null);
    }

    private ReturnStatementSyntax ParseReturnStatement()
    {
        SyntaxToken returnKeyword = Match(TokenKind.ReturnKeyword);
        ExpressionSyntax? expression = Current.Kind == TokenKind.Semicolon
            ? null
            : ParseExpression();
        SyntaxToken semicolonToken = Match(TokenKind.Semicolon);

        return new ReturnStatementSyntax(returnKeyword, expression, semicolonToken);
    }

    private ExpressionSyntax ParseExpression(int parentPrecedence = 0)
    {
        ExpressionSyntax left = ParsePrefixExpression();
        left = ParsePostfixExpression(left);
        ISet<int> usedNonAssociativePrecedences = new HashSet<int>();

        while (true)
        {
            int precedence = SyntaxFacts.GetBinaryPrecedence(Current.Kind);
            if (precedence == 0 || precedence <= parentPrecedence)
            {
                break;
            }

            SyntaxToken operatorToken = ParseToken();
            bool isNonAssociative = SyntaxFacts.IsNonAssociativeBinaryOperator(
                operatorToken.Kind
            );

            if (isNonAssociative && !usedNonAssociativePrecedences.Add(precedence))
            {
                Report(
                    DiagnosticCodes.NonAssociativeOperator,
                    operatorToken.Span,
                    $"Operator '{GetTokenText(operatorToken)}' is non-associative."
                );
            }

            switch (operatorToken.Kind)
            {
                case TokenKind.AsKeyword:
                {
                    TypeSyntax type = ParseType(true);
                    left = new ConversionExpressionSyntax(left, operatorToken, type);
                    break;
                }

                case TokenKind.IsKeyword:
                {
                    TypeSyntax type = ParseType(true);
                    left = new TypeTestExpressionSyntax(left, operatorToken, type);
                    break;
                }

                default:
                {
                    int rightPrecedence =
                        SyntaxFacts.IsRightAssociativeBinaryOperator(
                            operatorToken.Kind
                        )
                            ? precedence - 1
                            : precedence;
                    ExpressionSyntax right = ParseExpression(rightPrecedence);
                    left = operatorToken.Kind == TokenKind.HasKeyword
                        ? new PropertyTestExpressionSyntax(left, operatorToken, right)
                        : new BinaryExpressionSyntax(left, operatorToken, right);
                    break;
                }
            }
        }

        if (parentPrecedence < 1 && Current.Kind == TokenKind.Question)
        {
            SyntaxToken questionToken = ParseToken();
            ExpressionSyntax whenTrue = ParseExpression();
            SyntaxToken colonToken = Match(TokenKind.Colon);
            ExpressionSyntax whenFalse = ParseExpression();

            left = new ConditionalExpressionSyntax(
                left,
                questionToken,
                whenTrue,
                colonToken,
                whenFalse
            );
        }

        return left;
    }

    private ExpressionSyntax ParsePrefixExpression()
    {
        if (
            Current.Kind is TokenKind.Plus or TokenKind.Minus &&
            SyntaxFacts.IsNumericLiteral(Peek(1).Kind)
        )
        {
            SyntaxToken signToken = ParseToken();
            SyntaxToken literalToken = ParseToken();

            return new LiteralExpressionSyntax(signToken, literalToken);
        }

        int unaryPrecedence = SyntaxFacts.GetUnaryPrecedence(Current.Kind);
        if (unaryPrecedence != 0)
        {
            SyntaxToken operatorToken = ParseToken();
            ExpressionSyntax operand = ParseExpression(unaryPrecedence);

            return new UnaryExpressionSyntax(operatorToken, operand);
        }

        return ParsePrimaryExpression();
    }

    private ExpressionSyntax ParsePrimaryExpression()
    {
        return Current.Kind switch
        {
            TokenKind.IntegerLiteral or
                TokenKind.NumberLiteral or
                TokenKind.StringLiteral or
                TokenKind.TrueKeyword or
                TokenKind.FalseKeyword or
                TokenKind.NullKeyword =>
                new LiteralExpressionSyntax(null, ParseToken()),
            TokenKind.Identifier => new NameExpressionSyntax(ParseToken()),
            TokenKind.OpenParenthesis => ParseParenthesizedExpression(),
            TokenKind.OpenBracket => ParseArrayLiteralExpression(),
            TokenKind.OpenBrace or
                TokenKind.OpenObjectBrace
                => ParseObjectLiteralExpression(),
            _ => ParseMissingExpression(),
        };
    }

    private ParenthesizedExpressionSyntax ParseParenthesizedExpression()
    {
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        ExpressionSyntax expression = ParseExpression();
        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);

        return new ParenthesizedExpressionSyntax(
            openParenthesisToken,
            expression,
            closeParenthesisToken
        );
    }

    private ArrayLiteralExpressionSyntax ParseArrayLiteralExpression()
    {
        SyntaxToken openBracketToken = Match(TokenKind.OpenBracket);
        IList<ExpressionSyntax> elements = [ ];
        IList<SyntaxToken> commaTokens = [ ];

        while (Current.Kind is not TokenKind.CloseBracket and not TokenKind.EndOfFile)
        {
            elements.Add(ParseExpression());

            if (Current.Kind != TokenKind.Comma)
            {
                break;
            }

            commaTokens.Add(ParseToken());

            if (Current.Kind == TokenKind.CloseBracket)
            {
                ReportDisabledTrailingComma(commaTokens[^1]);
                break;
            }
        }

        SyntaxToken closeBracketToken = Match(TokenKind.CloseBracket);

        return new ArrayLiteralExpressionSyntax(
            openBracketToken,
            elements.AsReadOnly(),
            commaTokens.AsReadOnly(),
            closeBracketToken
        );
    }

    private ObjectLiteralExpressionSyntax ParseObjectLiteralExpression()
    {
        SyntaxToken openBraceToken = ParseToken();

        if (
            openBraceToken.Kind == TokenKind.OpenObjectBrace &&
            profile.OpenObjects != OpenObjectsFeature.Enabled
        )
        {
            Report(
                DiagnosticCodes.DisabledOpenObjects,
                openBraceToken.Span,
                "Open object literals are disabled by the language profile."
            );
        }

        IList<ObjectPropertyInitializerSyntax> properties = [ ];
        IList<SyntaxToken> commaTokens = [ ];

        while (Current.Kind is not TokenKind.CloseBrace and not TokenKind.EndOfFile)
        {
            SyntaxToken nameToken = Current.Kind is TokenKind.Identifier or TokenKind.StringLiteral
                ? ParseToken()
                : Match(TokenKind.Identifier);
            SyntaxToken separatorToken = Current.Kind == TokenKind.OptionalPropertyColon
                ? ParseToken()
                : Match(TokenKind.Colon);
            ExpressionSyntax value = ParseExpression();
            properties.Add(
                new ObjectPropertyInitializerSyntax(
                    nameToken,
                    separatorToken,
                    value
                )
            );

            if (Current.Kind != TokenKind.Comma)
            {
                break;
            }

            commaTokens.Add(ParseToken());

            if (Current.Kind == TokenKind.CloseBrace)
            {
                ReportDisabledTrailingComma(commaTokens[^1]);
                break;
            }
        }

        SyntaxToken closeBraceToken = Match(TokenKind.CloseBrace);

        return new ObjectLiteralExpressionSyntax(
            openBraceToken,
            properties.AsReadOnly(),
            commaTokens.AsReadOnly(),
            closeBraceToken
        );
    }

    private ExpressionSyntax ParsePostfixExpression(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (Current.Kind)
            {
                case TokenKind.OpenParenthesis:
                {
                    expression = ParseCallExpression(expression);
                    break;
                }

                case TokenKind.Dot:
                case TokenKind.OptionalDot:
                {
                    SyntaxToken operatorToken = ParseToken();
                    SyntaxToken nameToken = Match(TokenKind.Identifier);
                    expression = new MemberAccessExpressionSyntax(
                        expression,
                        operatorToken,
                        nameToken
                    );
                    break;
                }

                case TokenKind.OpenBracket:
                case TokenKind.OptionalOpenBracket:
                {
                    SyntaxToken openBracketToken = ParseToken();
                    ExpressionSyntax index = ParseExpression();
                    SyntaxToken closeBracketToken = Match(TokenKind.CloseBracket);
                    expression = new ElementAccessExpressionSyntax(
                        expression,
                        openBracketToken,
                        index,
                        closeBracketToken
                    );
                    break;
                }

                default:
                {
                    return expression;
                }
            }
        }
    }

    private CallExpressionSyntax ParseCallExpression(ExpressionSyntax target)
    {
        SyntaxToken openParenthesisToken = Match(TokenKind.OpenParenthesis);
        IList<ExpressionSyntax> arguments = [ ];
        IList<SyntaxToken> commaTokens = [ ];

        while (Current.Kind is not TokenKind.CloseParenthesis and not TokenKind.EndOfFile)
        {
            arguments.Add(ParseExpression());

            if (Current.Kind != TokenKind.Comma)
            {
                break;
            }

            commaTokens.Add(ParseToken());

            if (Current.Kind == TokenKind.CloseParenthesis)
            {
                Report(
                    DiagnosticCodes.TrailingSeparator,
                    commaTokens[^1].Span,
                    "A trailing comma is not permitted in an argument list."
                );
                break;
            }
        }

        SyntaxToken closeParenthesisToken = Match(TokenKind.CloseParenthesis);

        return new CallExpressionSyntax(
            target,
            openParenthesisToken,
            arguments.AsReadOnly(),
            commaTokens.AsReadOnly(),
            closeParenthesisToken
        );
    }

    private TypeSyntax ParseType(
        bool isOperatorType = false,
        bool allowVoid = false
    )
    {
        SyntaxToken nameToken =
            SyntaxFacts.IsTypeName(Current.Kind) ||
            allowVoid && Current.Kind == TokenKind.VoidKeyword
                ? ParseToken() : Match(TokenKind.Identifier);

        IList<SyntaxToken> suffixTokens = [ ];

        ParseNullableSuffix(suffixTokens, isOperatorType);

        while (Current.Kind == TokenKind.OpenBracket && Peek(1).Kind == TokenKind.CloseBracket)
        {
            suffixTokens.Add(ParseToken());
            suffixTokens.Add(ParseToken());
            ParseNullableSuffix(suffixTokens, isOperatorType);
        }

        return new TypeSyntax(nameToken, suffixTokens.AsReadOnly());
    }

    private ExpressionSyntax ParseMissingExpression()
    {
        SyntaxToken unexpectedToken = Current;
        Report(
            DiagnosticCodes.ExpectedExpression,
            unexpectedToken.Span,
            $"Expected an expression, but found '{GetTokenText(unexpectedToken)}'."
        );

        if (
            unexpectedToken.Kind is not
            TokenKind.EndOfFile and not
            TokenKind.CloseParenthesis and not
            TokenKind.CloseBracket and not
            TokenKind.CloseBrace and not
            TokenKind.Comma and not
            TokenKind.Colon and not
            TokenKind.Semicolon
        )
        {
            ParseToken();
        }

        SyntaxToken missingToken = new (
            TokenKind.Identifier,
            new TextSpan(unexpectedToken.Span.Start, 0),
            IsMissing: true
        );

        return new MissingExpressionSyntax(missingToken);
    }

    private void ParseNullableSuffix(
        ICollection<SyntaxToken> suffixTokens,
        bool isOperatorType
    )
    {
        if (!isOperatorType && Current.Kind == TokenKind.QuestionQuestion)
        {
            SyntaxToken combinedToken = ParseToken();
            SyntaxToken firstQuestion = new (
                TokenKind.Question,
                new TextSpan(combinedToken.Span.Start, 1)
            );
            SyntaxToken secondQuestion = new (
                TokenKind.Question,
                new TextSpan(combinedToken.Span.Start + 1, 1)
            );
            suffixTokens.Add(firstQuestion);
            suffixTokens.Add(secondQuestion);
            ReportRepeatedNullableAnnotation(secondQuestion);

            while (
                Current.Kind is
                TokenKind.Question or
                TokenKind.QuestionQuestion
            )
            {
                SyntaxToken repeatedToken = ParseToken();

                if (repeatedToken.Kind == TokenKind.Question)
                {
                    suffixTokens.Add(repeatedToken);
                    ReportRepeatedNullableAnnotation(repeatedToken);
                    continue;
                }

                SyntaxToken repeatedFirstQuestion = new (
                    TokenKind.Question,
                    new TextSpan(repeatedToken.Span.Start, 1)
                );
                SyntaxToken repeatedSecondQuestion = new (
                    TokenKind.Question,
                    new TextSpan(repeatedToken.Span.Start + 1, 1)
                );
                suffixTokens.Add(repeatedFirstQuestion);
                suffixTokens.Add(repeatedSecondQuestion);
                ReportRepeatedNullableAnnotation(repeatedFirstQuestion);
                ReportRepeatedNullableAnnotation(repeatedSecondQuestion);
            }

            return;
        }

        if (!ShouldConsumeNullableSuffix(isOperatorType))
        {
            return;
        }

        suffixTokens.Add(ParseToken());

        while (ShouldConsumeNullableSuffix(isOperatorType))
        {
            SyntaxToken questionToken = ParseToken();
            suffixTokens.Add(questionToken);
            ReportRepeatedNullableAnnotation(questionToken);
        }
    }

    private void ReportRepeatedNullableAnnotation(SyntaxToken questionToken)
    {
        Report(
            DiagnosticCodes.RepeatedNullableAnnotation,
            questionToken.Span,
            "A type construction cannot have more than one nullable annotation."
        );
    }

    private bool ShouldConsumeNullableSuffix(bool isOperatorType)
    {
        if (Current.Kind != TokenKind.Question)
        {
            return false;
        }

        if (!isOperatorType)
        {
            return true;
        }

        if (Peek(1).Kind == TokenKind.OpenBracket && Peek(2).Kind == TokenKind.CloseBracket)
        {
            return true;
        }

        return !SyntaxFacts.CanStartExpression(Peek(1).Kind);
    }

    private SyntaxToken Match(TokenKind kind)
    {
        if (Current.Kind == kind)
        {
            return ParseToken();
        }

        ReportUnexpectedToken(Current, kind);

        return new SyntaxToken(
            kind,
            new TextSpan(Current.Span.Start, 0),
            IsMissing: true
        );
    }

    private void ReportUnexpectedToken(
        SyntaxToken actualToken,
        params IEnumerable<TokenKind> expectedKinds
    )
    {
        string expected = string.Join(
            " or ",
            expectedKinds.Select(static kind => $"'{kind}'")
        );
        Report(
            DiagnosticCodes.UnexpectedToken,
            actualToken.Span,
            $"Expected {expected}, but found '{GetTokenText(actualToken)}'."
        );
    }

    private void Report(string code, TextSpan span, string message)
    {
        diagnostics.Add(
            new Diagnostic(
                code,
                DiagnosticSeverity.Error,
                DiagnosticCategory.Syntax,
                span,
                message
            )
        );
    }

    private void ReportDisabledUserDefinedFunctions(SyntaxToken funcKeyword)
    {
        if (profile.UserDefinedFunctions == UserDefinedFunctionsFeature.Enabled)
        {
            return;
        }

        Report(
            DiagnosticCodes.DisabledUserDefinedFunctions,
            funcKeyword.Span,
            "User-defined functions are disabled by the language profile."
        );
    }

    private void ReportDisabledLoopLevel(
        SyntaxToken? levelSignToken,
        SyntaxToken? levelToken
    )
    {
        if (
            levelToken is null ||
            profile.Loops == LoopFeatures.None ||
            profile.MultiLevelLoopControl == MultiLevelLoopControlFeature.Enabled
        )
        {
            return;
        }

        Report(
            DiagnosticCodes.DisabledMultiLevelLoopControl,
            (levelSignToken ?? levelToken).Span,
            "Explicit loop-control levels are disabled by the language profile."
        );
    }

    private void ReportDisabledTrailingComma(SyntaxToken commaToken)
    {
        if (profile.TrailingCommas == TrailingCommasFeature.Enabled)
        {
            return;
        }

        Report(
            DiagnosticCodes.DisabledTrailingCommas,
            commaToken.Span,
            "Trailing commas in literals are disabled by the language profile."
        );
    }

    private string GetTokenText(SyntaxToken token)
    {
        return token.Span.Length == 0
            ? token.Kind.ToString()
            : source.GetText(token.Span);
    }
}
