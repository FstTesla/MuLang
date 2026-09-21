using MuLang.Compiler.Diagnostics;
using MuLang.Compiler.Syntax;
using MuLang.Core;
using MuLang.Core.Text;

namespace MuLang.Compiler.Tests.Syntax;

public sealed class ParserTests
{
    [Test]
    public void AppliesBinaryOperatorPrecedence()
    {
        SyntaxTree tree = ParseExpression("1 + 2 * 3");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        BinaryExpressionSyntax addition = (BinaryExpressionSyntax)root.Expression;
        BinaryExpressionSyntax multiplication = (BinaryExpressionSyntax)addition.Right;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(addition.OperatorToken.Kind, Is.EqualTo(TokenKind.Plus));
            Assert.That(multiplication.OperatorToken.Kind, Is.EqualTo(TokenKind.Asterisk));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesNullCoalescingWithCSharpPrecedenceAndAssociativity()
    {
        SyntaxTree tree = ParseExpression("a || b ?? c ?? d ? e : f");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        ConditionalExpressionSyntax conditional =
            (ConditionalExpressionSyntax)root.Expression;
        BinaryExpressionSyntax firstCoalescing =
            (BinaryExpressionSyntax)conditional.Condition;
        BinaryExpressionSyntax conditionalOr =
            (BinaryExpressionSyntax)firstCoalescing.Left;
        BinaryExpressionSyntax secondCoalescing =
            (BinaryExpressionSyntax)firstCoalescing.Right;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                firstCoalescing.OperatorToken.Kind,
                Is.EqualTo(TokenKind.QuestionQuestion)
            );
            Assert.That(
                conditionalOr.OperatorToken.Kind,
                Is.EqualTo(TokenKind.PipePipe)
            );
            Assert.That(
                secondCoalescing.OperatorToken.Kind,
                Is.EqualTo(TokenKind.QuestionQuestion)
            );
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void DistinguishesNullableTypeSuffixFromNullCoalescing()
    {
        SyntaxTree tree = ParseExpression("value as int? ?? 0");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        BinaryExpressionSyntax coalescing =
            (BinaryExpressionSyntax)root.Expression;
        ConversionExpressionSyntax conversion =
            (ConversionExpressionSyntax)coalescing.Left;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                coalescing.OperatorToken.Kind,
                Is.EqualTo(TokenKind.QuestionQuestion)
            );
            Assert.That(conversion.Type.SuffixTokens, Has.Count.EqualTo(1));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void CombinesLeadingSignWithNumericLiteral()
    {
        SyntaxTree tree = ParseExpression("- 9223372036854775808");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        LiteralExpressionSyntax literal = (LiteralExpressionSyntax)root.Expression;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(literal.SignToken?.Kind, Is.EqualTo(TokenKind.Minus));
            Assert.That(literal.LiteralToken.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
            Assert.That(literal.Span, Is.EqualTo(new TextSpan(0, 21)));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesPostfixExpressionsLeftToRight()
    {
        SyntaxTree tree = ParseExpression("items?.[0]?.name");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        MemberAccessExpressionSyntax member = (MemberAccessExpressionSyntax)root.Expression;
        ElementAccessExpressionSyntax element = (ElementAccessExpressionSyntax)member.Target;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(element.IsOptional, Is.True);
            Assert.That(member.IsOptional, Is.True);
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesAsOperatorLeftAssociatively()
    {
        SyntaxTree tree = ParseExpression("value as int as number");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        ConversionExpressionSyntax outerConversion = (ConversionExpressionSyntax)root.Expression;
        ConversionExpressionSyntax innerConversion =
            (ConversionExpressionSyntax)outerConversion.Expression;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(innerConversion.Type.NameToken.Kind, Is.EqualTo(TokenKind.IntKeyword));
            Assert.That(outerConversion.Type.NameToken.Kind, Is.EqualTo(TokenKind.NumberKeyword));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsChainedNonAssociativeOperators()
    {
        SyntaxTree tree = ParseExpression("1 < 2 < 3");

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.NonAssociativeOperator)
        );
    }

    [Test]
    public void DistinguishesConditionalFromNullableTypeTest()
    {
        SyntaxTree conditionalTree = ParseExpression("value is int ? 1 : 0");
        SyntaxTree nullableTree = ParseExpression("value is int? == false");
        ExpressionRootSyntax conditionalRoot = (ExpressionRootSyntax)conditionalTree.Root;
        ExpressionRootSyntax nullableRoot = (ExpressionRootSyntax)nullableTree.Root;
        ConditionalExpressionSyntax conditional =
            (ConditionalExpressionSyntax)conditionalRoot.Expression;
        BinaryExpressionSyntax equality = (BinaryExpressionSyntax)nullableRoot.Expression;
        TypeTestExpressionSyntax nullableTypeTest =
            (TypeTestExpressionSyntax)equality.Left;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(conditional.Condition, Is.TypeOf<TypeTestExpressionSyntax>());
            Assert.That(nullableTypeTest.Type.SuffixTokens, Has.Count.EqualTo(1));
            Assert.That(conditionalTree.Diagnostics, Is.Empty);
            Assert.That(nullableTree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesOpenObjectLiteral()
    {
        SyntaxTree tree = ParseExpression("@{ name: \"MuLang\" }");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        ObjectLiteralExpressionSyntax objectLiteral =
            (ObjectLiteralExpressionSyntax)root.Expression;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(objectLiteral.IsOpen, Is.True);
            Assert.That(objectLiteral.Properties, Has.Count.EqualTo(1));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesOptionalObjectLiteralProperty()
    {
        SyntaxTree tree = ParseExpression("{ value?: 1 }");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;
        ObjectLiteralExpressionSyntax objectLiteral =
            (ObjectLiteralExpressionSyntax)root.Expression;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(objectLiteral.Properties[0].IsOptional, Is.True);
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void RejectsWhitespaceInsideOptionalPropertySeparator()
    {
        SyntaxTree tree = ParseExpression("{ value? : 1 }");

        Assert.That(tree.Diagnostics.HasErrors, Is.True);
    }

    [Test]
    public void ParsesProgramStatements()
    {
        const string source = """
                              var i: int;
                              for (i = 0; i < 10; i = i + 1) {
                                  if (i == 5)
                                      continue;
                                  log(i);
                              }
                              return;
                              """;

        SyntaxTree tree = Parser.Parse(SourceText.From(source), CompilationMode.Program);
        ProgramRootSyntax root = (ProgramRootSyntax)tree.Root;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Statements, Has.Count.EqualTo(3));
            Assert.That(root.Statements[0], Is.TypeOf<VariableDeclarationStatementSyntax>());
            Assert.That(root.Statements[1], Is.TypeOf<ForStatementSyntax>());
            Assert.That(root.Statements[2], Is.TypeOf<ReturnStatementSyntax>());
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ParsesLoopControlLevels()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("break +2; continue 3;"),
            CompilationMode.Program
        );
        ProgramRootSyntax root = (ProgramRootSyntax)tree.Root;
        BreakStatementSyntax breakStatement =
            (BreakStatementSyntax)root.Statements[0];
        ContinueStatementSyntax continueStatement =
            (ContinueStatementSyntax)root.Statements[1];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(breakStatement.LevelToken?.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
            Assert.That(breakStatement.LevelSignToken?.Kind, Is.EqualTo(TokenKind.Plus));
            Assert.That(continueStatement.LevelToken?.Kind, Is.EqualTo(TokenKind.IntegerLiteral));
            Assert.That(tree.Diagnostics, Is.Empty);
        }
    }

    [Test]
    public void ReportsPureExpressionUsedAsStatement()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("1 + 2;"),
            CompilationMode.Program
        );

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.InvalidExpressionStatement)
        );
    }

    [Test]
    public void ReportsInvalidAssignmentTarget()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("(a + b) = 1;"),
            CompilationMode.Program
        );

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.InvalidAssignmentTarget)
        );
    }

    [Test]
    public void ReportsOptionalAccessUsedAsAssignmentTarget()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("value?.property = 1;"),
            CompilationMode.Program
        );

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.InvalidAssignmentTarget)
        );
    }

    [TestCase("[1,]")]
    [TestCase("{ value: 1, }")]
    [TestCase("@{ value: 1, }")]
    public void AllowsTrailingSeparatorsInCollectionLiterals(string source)
    {
        SyntaxTree tree = ParseExpression(source);

        Assert.That(tree.Diagnostics, Is.Empty);
    }

    [Test]
    public void ReportsTrailingSeparatorInArgumentList()
    {
        SyntaxTree tree = ParseExpression("call(1,)");

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.TrailingSeparator)
        );
    }

    [Test]
    public void ReportsRepeatedNullableAnnotation()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("var value: int??;"),
            CompilationMode.Program
        );

        Assert.That(
            tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
            Does.Contain(DiagnosticCodes.RepeatedNullableAnnotation)
        );
    }

    [Test]
    public void DoesNotJoinSignedLiteralAcrossInvalidToken()
    {
        SyntaxTree tree = ParseExpression("-@1");
        ExpressionRootSyntax root = (ExpressionRootSyntax)tree.Root;

        Assert.That(root.Expression, Is.TypeOf<UnaryExpressionSyntax>());
    }

    [Test]
    public void RecoversAfterMissingSemicolon()
    {
        SyntaxTree tree = Parser.Parse(
            SourceText.From("var first = 1 var second = 2;"),
            CompilationMode.Program
        );
        ProgramRootSyntax root = (ProgramRootSyntax)tree.Root;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.Statements, Has.Count.EqualTo(2));
            Assert.That(
                tree.Diagnostics.Select(static diagnostic => diagnostic.Code),
                Does.Contain(DiagnosticCodes.UnexpectedToken)
            );
        }
    }

    private static SyntaxTree ParseExpression(string source)
    {
        return Parser.Parse(SourceText.From(source), CompilationMode.Expression);
    }
}
