﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    partial class LocalRewriter
    {
        internal static BoundStatement AddSequencePoint(VariableDeclaratorSyntax declaratorSyntax, BoundStatement rewrittenStatement)
        {
            SyntaxNode node;
            TextSpan? part;
            GetBreakpointSpan(declaratorSyntax, out node, out part);
            var result = BoundSequencePoint.Create(declaratorSyntax, part, rewrittenStatement);
            result.WasCompilerGenerated = rewrittenStatement.WasCompilerGenerated;
            return result;
        }

        internal static BoundStatement AddSequencePoint(PropertyDeclarationSyntax declarationSyntax, BoundStatement rewrittenStatement)
        {
            Debug.Assert(declarationSyntax.Initializer != null);
            int start = declarationSyntax.Initializer.Value.SpanStart;
            int end = declarationSyntax.Initializer.Span.End;
            TextSpan part = TextSpan.FromBounds(start, end);

            var result = BoundSequencePoint.Create(declarationSyntax, part, rewrittenStatement);
            result.WasCompilerGenerated = rewrittenStatement.WasCompilerGenerated;
            return result;
        }

        internal static BoundStatement AddSequencePoint(UsingStatementSyntax usingSyntax, BoundStatement rewrittenStatement)
        {
            int start = usingSyntax.Span.Start;
            int end = usingSyntax.CloseParenToken.Span.End;
            TextSpan span = TextSpan.FromBounds(start, end);
            return new BoundSequencePointWithSpan(usingSyntax, rewrittenStatement, span);
        }

        internal static BoundStatement AddSequencePoint(BlockSyntax blockSyntax, BoundStatement rewrittenStatement)
        {
            TextSpan span;

            var parent = blockSyntax.Parent as ConstructorDeclarationSyntax;
            if (parent != null)
            {
                span = CreateSpanForConstructorDeclaration(parent);
            }
            else
            {
                // This inserts a sequence points to any prologue code for method declarations.
                var start = blockSyntax.Parent.SpanStart;
                var end = blockSyntax.OpenBraceToken.GetPreviousToken().Span.End;
                span = TextSpan.FromBounds(start, end);
            }

            return new BoundSequencePointWithSpan(blockSyntax, rewrittenStatement, span);
        }

        private static TextSpan CreateSpanForConstructorDeclaration(ConstructorDeclarationSyntax constructorSyntax)
        {
            if (constructorSyntax.Initializer != null)
            {
                //  [SomeAttribute] public MyCtorName(params int[] values): [|base()|] { ... }
                var start = constructorSyntax.Initializer.ThisOrBaseKeyword.SpanStart;
                var end = constructorSyntax.Initializer.ArgumentList.CloseParenToken.Span.End;
                return TextSpan.FromBounds(start, end);
            }

            if (constructorSyntax.Modifiers.Any(SyntaxKind.StaticKeyword))
            {
                // [SomeAttribute] static MyCtorName(...) [|{|] ... }
                var start = constructorSyntax.Body.OpenBraceToken.SpanStart;
                var end = constructorSyntax.Body.OpenBraceToken.Span.End;
                return TextSpan.FromBounds(start, end);
            }

            //  [SomeAttribute] [|public MyCtorName(params int[] values)|] { ... }
            return CreateSpan(constructorSyntax.Modifiers, constructorSyntax.Identifier, constructorSyntax.ParameterList.CloseParenToken);
        }

        private static TextSpan CreateSpan(SyntaxTokenList startOpt, SyntaxNodeOrToken startFallbackOpt, SyntaxNodeOrToken endOpt)
        {
            Debug.Assert(startFallbackOpt != default(SyntaxNodeOrToken) || endOpt != default(SyntaxNodeOrToken));

            int startPos;
            if (startOpt.Count > 0)
            {
                startPos = startOpt.First().SpanStart;
            }
            else if (startFallbackOpt != default(SyntaxNodeOrToken))
            {
                startPos = startFallbackOpt.SpanStart;
            }
            else
            {
                startPos = endOpt.SpanStart;
            }

            int endPos;
            if (endOpt != default(SyntaxNodeOrToken))
            {
                endPos = GetEndPosition(endOpt);
            }
            else
            {
                endPos = GetEndPosition(startFallbackOpt);
            }

            return TextSpan.FromBounds(startPos, endPos);
        }

        private static int GetEndPosition(SyntaxNodeOrToken nodeOrToken)
        {
            if (nodeOrToken.IsToken)
            {
                return nodeOrToken.Span.End;
            }
            else
            {
                return nodeOrToken.AsNode().GetLastToken().Span.End;
            }
        }

        internal static void GetBreakpointSpan(VariableDeclaratorSyntax declaratorSyntax, out SyntaxNode node, out TextSpan? part)
        {
            var declarationSyntax = (VariableDeclarationSyntax)declaratorSyntax.Parent;

            if (declarationSyntax.Variables.First() == declaratorSyntax)
            {
                switch (declarationSyntax.Parent.Kind)
                {
                    case SyntaxKind.EventFieldDeclaration:
                    case SyntaxKind.FieldDeclaration:
                        var modifiers = ((BaseFieldDeclarationSyntax)declarationSyntax.Parent).Modifiers;
                        GetFirstLocalOrFieldBreakpointSpan(modifiers, declaratorSyntax, out node, out part);
                        break;

                    case SyntaxKind.LocalDeclarationStatement:
                        // only const locals have modifiers and those don't have a sequence point:
                        Debug.Assert(!((LocalDeclarationStatementSyntax)declarationSyntax.Parent).Modifiers.Any());
                        GetFirstLocalOrFieldBreakpointSpan(default(SyntaxTokenList), declaratorSyntax, out node, out part);
                        break;

                    case SyntaxKind.UsingStatement:
                    case SyntaxKind.FixedStatement:
                    case SyntaxKind.ForStatement:
                        // for ([|int i = 1|]; i < 10; i++)
                        // for ([|int i = 1|], j = 0; i < 10; i++)
                        node = declarationSyntax;
                        part = TextSpan.FromBounds(declarationSyntax.SpanStart, declaratorSyntax.Span.End);
                        break;

                    default:
                        throw ExceptionUtilities.Unreachable;
                }
            }
            else
            {
                // int x = 1, [|y = 2|];
                // public static int x = 1, [|y = 2|];
                // for (int i = 1, [|j = 0|]; i < 10; i++)
                node = declaratorSyntax;
                part = null;
            }
        }

        internal static void GetFirstLocalOrFieldBreakpointSpan(SyntaxTokenList modifiers, VariableDeclaratorSyntax declaratorSyntax, out SyntaxNode node, out TextSpan? part)
        {
            var declarationSyntax = (VariableDeclarationSyntax)declaratorSyntax.Parent;

            int start = modifiers.Any() ? modifiers[0].SpanStart : declarationSyntax.SpanStart;

            int end;
            if (declarationSyntax.Variables.Count == 1)
            {
                // [|int x = 1;|]
                // [|public static int x = 1;|]
                end = declarationSyntax.Parent.Span.End;
            }
            else
            {
                // [|int x = 1|], y = 2;
                // [|public static int x = 1|], y = 2;
                end = declaratorSyntax.Span.End;
            }

            part = TextSpan.FromBounds(start, end);
            node = declarationSyntax.Parent;
        }

        internal BoundExpression AddConditionSequencePoint(BoundExpression condition, BoundStatement containingStatement)
        {
            if (condition == null || !this.compilation.Options.EnableEditAndContinue || containingStatement.WasCompilerGenerated)
            {
                return condition;
            }

            // The local has to be associated with the syntax of the statement containing the condition since 
            // EnC source mapping only operates on statements.
            var local = factory.SynthesizedLocal(condition.Type, containingStatement.Syntax, kind: SynthesizedLocalKind.ConditionalBranchDiscriminator);

            var condConst = condition.ConstantValue;
            if (condConst == null)
            {
                return new BoundSequence(
                    condition.Syntax,
                    ImmutableArray.Create(local),
                    ImmutableArray.Create<BoundExpression>(factory.AssignmentExpression(factory.Local(local), condition)),
                    new BoundSequencePointExpression(syntax: null, expression: factory.Local(local), type: condition.Type),
                    condition.Type);
            }
            else
            {
                // const expression must stay a const to not invalidate results of control flow analysis
                return new BoundSequence(
                    condition.Syntax,
                    ImmutableArray.Create(local),
                    ImmutableArray.Create<BoundExpression>(factory.AssignmentExpression(factory.Local(local), condition)),
                    condition,
                    condition.Type);
            }
        }
    }
}
