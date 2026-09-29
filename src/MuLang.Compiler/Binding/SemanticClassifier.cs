using MuLang.Compiler.Syntax;
using MuLang.Core.Text;

namespace MuLang.Compiler.Binding;

internal static class SemanticClassifier
{
    public static IReadOnlyList<SemanticClassification> Classify(
        BindingResult binding
    )
    {
        IList<SemanticClassification> classifications =
            new List<SemanticClassification>();

        void Add(
            SemanticClassificationKind kind,
            TextSpan span,
            SemanticClassificationModifiers modifiers =
                SemanticClassificationModifiers.None
        )
        {
            classifications.Add(new SemanticClassification(kind, modifiers, span));
        }

        void VisitType(TypeSyntax? syntax)
        {
            if (syntax?.NameToken.Kind == TokenKind.Identifier)
            {
                Add(SemanticClassificationKind.Type, syntax.NameToken.Span);
            }
        }

        void VisitStatement(BoundStatement statement)
        {
            switch (statement)
            {
                case BoundStatement.Block block:
                {
                    foreach (BoundStatement child in block.Statements)
                    {
                        VisitStatement(child);
                    }

                    break;
                }

                case BoundStatement.VariableDeclaration declaration:
                {
                    VariableDeclarationStatementSyntax syntax =
                        (VariableDeclarationStatementSyntax)declaration.Syntax;
                    Add(
                        SemanticClassificationKind.Variable,
                        syntax.IdentifierToken.Span,
                        SemanticClassificationModifiers.Declaration
                    );
                    VisitType(syntax.Type);

                    if (declaration.Initializer is not null)
                    {
                        VisitExpression(declaration.Initializer);
                    }

                    break;
                }

                case BoundStatement.Assignment assignment:
                {
                    VisitExpression(assignment.Target);
                    VisitExpression(assignment.Value);
                    break;
                }

                case BoundStatement.Removal removal:
                {
                    VisitExpression(removal.Target);
                    break;
                }

                case BoundStatement.ExpressionStatement expression:
                {
                    VisitExpression(expression.Value);
                    break;
                }

                case BoundStatement.If conditional:
                {
                    VisitExpression(conditional.Condition);
                    VisitStatement(conditional.Then);

                    if (conditional.Else is not null)
                    {
                        VisitStatement(conditional.Else);
                    }

                    break;
                }

                case BoundStatement.While loop:
                {
                    VisitExpression(loop.Condition);
                    VisitStatement(loop.Body);
                    break;
                }

                case BoundStatement.For loop:
                {
                    if (loop.Initializer is not null)
                    {
                        VisitStatement(loop.Initializer);
                    }

                    if (loop.Condition is not null)
                    {
                        VisitExpression(loop.Condition);
                    }

                    if (loop.Iterator is not null)
                    {
                        VisitStatement(loop.Iterator);
                    }

                    VisitStatement(loop.Body);
                    break;
                }

                case BoundStatement.Return { Value: not null } result:
                {
                    VisitExpression(result.Value);
                    break;
                }
            }
        }

        void VisitExpression(BoundExpression expression)
        {
            switch (expression)
            {
                case BoundExpression.Local local:
                {
                    Add(SemanticClassificationKind.Variable, local.Span);
                    break;
                }

                case BoundExpression.Parameter parameter:
                {
                    Add(SemanticClassificationKind.Parameter, parameter.Span);
                    break;
                }

                case BoundExpression.Global global:
                {
                    Add(
                        SemanticClassificationKind.Variable,
                        global.Span,
                        SemanticClassificationModifiers.ReadOnly |
                        SemanticClassificationModifiers.DefaultLibrary
                    );
                    break;
                }

                case BoundExpression.Array array:
                {
                    foreach (BoundExpression element in array.Elements)
                    {
                        VisitExpression(element);
                    }

                    break;
                }

                case BoundExpression.Object value:
                {
                    ObjectLiteralExpressionSyntax syntax =
                        (ObjectLiteralExpressionSyntax)value.Syntax;

                    for (int index = 0; index < value.Properties.Count; index++)
                    {
                        Add(
                            SemanticClassificationKind.Property,
                            syntax.Properties[index].NameToken.Span,
                            SemanticClassificationModifiers.Declaration
                        );
                        VisitExpression(value.Properties[index].Value);
                    }

                    break;
                }

                case BoundExpression.Unary unary:
                {
                    VisitExpression(unary.Operand);
                    break;
                }

                case BoundExpression.Binary binary:
                {
                    VisitExpression(binary.Left);
                    VisitExpression(binary.Right);
                    break;
                }

                case BoundExpression.Coalescing coalescing:
                {
                    VisitExpression(coalescing.Left);
                    VisitExpression(coalescing.Right);
                    break;
                }

                case BoundExpression.Conversion conversion:
                {
                    VisitExpression(conversion.Expression);

                    if (conversion.Syntax is ConversionExpressionSyntax syntax)
                    {
                        VisitType(syntax.Type);
                    }

                    break;
                }

                case BoundExpression.Truthiness truthiness:
                {
                    VisitExpression(truthiness.Expression);
                    break;
                }

                case BoundExpression.TypeTest typeTest:
                {
                    VisitExpression(typeTest.Expression);
                    VisitType(((TypeTestExpressionSyntax)typeTest.Syntax).Type);
                    break;
                }

                case BoundExpression.PropertyTest propertyTest:
                {
                    VisitExpression(propertyTest.Target);
                    VisitExpression(propertyTest.Key);
                    break;
                }

                case BoundExpression.Conditional conditional:
                {
                    VisitExpression(conditional.Condition);
                    VisitExpression(conditional.WhenTrue);
                    VisitExpression(conditional.WhenFalse);
                    break;
                }

                case BoundExpression.ProviderCall call:
                {
                    CallExpressionSyntax syntax = (CallExpressionSyntax)call.Syntax;
                    Add(
                        SemanticClassificationKind.Function,
                        syntax.Target.Span,
                        SemanticClassificationModifiers.DefaultLibrary
                    );

                    foreach (BoundExpression argument in call.Arguments)
                    {
                        VisitExpression(argument);
                    }

                    break;
                }

                case BoundExpression.UserCall call:
                {
                    CallExpressionSyntax syntax = (CallExpressionSyntax)call.Syntax;
                    Add(SemanticClassificationKind.Function, syntax.Target.Span);

                    foreach (BoundExpression argument in call.Arguments)
                    {
                        VisitExpression(argument);
                    }

                    break;
                }

                case BoundExpression.MemberAccess member:
                {
                    VisitExpression(member.Target);
                    Add(
                        SemanticClassificationKind.Property,
                        ((MemberAccessExpressionSyntax)member.Syntax).NameToken.Span
                    );
                    break;
                }

                case BoundExpression.ElementAccess element:
                {
                    VisitExpression(element.Target);
                    VisitExpression(element.Index);
                    break;
                }
            }
        }

        switch (binding.Root)
        {
            case BoundRoot.Expression expression:
            {
                VisitExpression(expression.Value);
                break;
            }

            case BoundRoot.Program program:
            {
                foreach (BoundFunction function in program.Functions)
                {
                    FunctionDeclarationSyntax declaration =
                        function.Symbol.Declaration;
                    Add(
                        SemanticClassificationKind.Function,
                        declaration.IdentifierToken.Span,
                        SemanticClassificationModifiers.Declaration
                    );
                    VisitType(declaration.ReturnType);

                    for (
                        int index = 0;
                        index < function.Symbol.Parameters.Count;
                        index++
                    )
                    {
                        ParameterSyntax parameter = declaration.Parameters[index];
                        Add(
                            SemanticClassificationKind.Parameter,
                            parameter.IdentifierToken.Span,
                            SemanticClassificationModifiers.Declaration
                        );
                        VisitType(parameter.Type);
                    }

                    foreach (BoundStatement statement in function.Statements)
                    {
                        VisitStatement(statement);
                    }
                }

                foreach (BoundStatement statement in program.Statements)
                {
                    VisitStatement(statement);
                }

                break;
            }
        }

        return
        [
            .. classifications
                .OrderBy(static classification => classification.Span.Start)
                .ThenBy(static classification => classification.Span.Length),
        ];
    }
}
