﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp
{
    public static class SyntaxExtensions
    {
        internal const string DefaultIndentation = "    ";

        /// <summary>
        /// Gets the expression-body syntax from an expression-bodied member. The
        /// given syntax must be for a member which could contain an expression-body.
        /// </summary>
        internal static ArrowExpressionClauseSyntax GetExpressionBodySyntax(this CSharpSyntaxNode node)
        {
            ArrowExpressionClauseSyntax arrowExpr = null;
            switch (node.Kind)
            {
                // The ArrowExpressionClause is the declaring syntax for the
                // 'get' SourcePropertyAccessorSymbol of properties and indexers.
                case SyntaxKind.ArrowExpressionClause:
                    arrowExpr = (ArrowExpressionClauseSyntax)node;
                    break;
                case SyntaxKind.MethodDeclaration:
                    arrowExpr = ((MethodDeclarationSyntax)node).ExpressionBody;
                    break;
                case SyntaxKind.OperatorDeclaration:
                    arrowExpr = ((OperatorDeclarationSyntax)node).ExpressionBody;
                    break;
                case SyntaxKind.ConversionOperatorDeclaration:
                    arrowExpr = ((ConversionOperatorDeclarationSyntax)node).ExpressionBody;
                    break;
                case SyntaxKind.PropertyDeclaration:
                    arrowExpr = ((PropertyDeclarationSyntax)node).ExpressionBody;
                    break;
                case SyntaxKind.IndexerDeclaration:
                    arrowExpr = ((IndexerDeclarationSyntax)node).ExpressionBody;
                    break;
                case SyntaxKind.ConstructorDeclaration:
                case SyntaxKind.DestructorDeclaration:
                    return null;
                default:
                    // Don't throw, just use for the assert in case this is used in the semantic model
                    ExceptionUtilities.UnexpectedValue(node.Kind);
                    break;
            }
            return arrowExpr;
        }

        /// <summary>
        /// Creates a new syntax token with all whitespace and end of line trivia replaced with
        /// regularly formatted trivia.
        /// </summary>
        /// <param name="token">The token to normalize.</param>
        /// <param name="indentation">An optional sequence of whitespace characters that defines a
        /// single level of indentation.</param>
        /// <param name="elasticTrivia">If true the replaced trivia is elastic trivia.</param>
        public static SyntaxToken NormalizeWhitespace(this SyntaxToken token, string indentation = DefaultIndentation, bool elasticTrivia = false)
        {
            return SyntaxFormatter.Format(token, indentation, elasticTrivia);
        }

        /// <summary>
        /// Creates a new syntax trivia list with all whitespace and end of line trivia replaced with
        /// regularly formatted trivia.
        /// </summary>
        /// <param name="list">The trivia list to normalize.</param>
        /// <param name="indentation">An optional sequence of whitespace characters that defines a
        /// single level of indentation.</param>
        /// <param name="elasticTrivia">If true the replaced trivia is elastic trivia.</param>
        public static SyntaxTriviaList NormalizeWhitespace(this SyntaxTriviaList list, string indentation = DefaultIndentation, bool elasticTrivia = false)
        {
            return SyntaxFormatter.Format(list, indentation, elasticTrivia);
        }

        public static SyntaxTriviaList ToSyntaxTriviaList(this IEnumerable<SyntaxTrivia> sequence)
        {
            return SyntaxFactory.TriviaList(sequence);
        }

        internal static XmlNameAttributeElementKind GetElementKind(this XmlNameAttributeSyntax attributeSyntax)
        {
            CSharpSyntaxNode parentSyntax = attributeSyntax.Parent;
            SyntaxKind parentKind = parentSyntax.Kind;

            string parentName;
            if (parentKind == SyntaxKind.XmlEmptyElement)
            {
                var parent = (XmlEmptyElementSyntax)parentSyntax;
                parentName = parent.Name.LocalName.ValueText;
                Debug.Assert((object)parent.Name.Prefix == null);
            }
            else if (parentKind == SyntaxKind.XmlElementStartTag)
            {
                var parent = (XmlElementStartTagSyntax)parentSyntax;
                parentName = parent.Name.LocalName.ValueText;
                Debug.Assert((object)parent.Name.Prefix == null);
            }
            else
            {
                throw ExceptionUtilities.UnexpectedValue(parentKind);
            }

            if (DocumentationCommentXmlNames.ElementEquals(parentName, DocumentationCommentXmlNames.ParameterElementName))
            {
                return XmlNameAttributeElementKind.Parameter;
            }
            else if (DocumentationCommentXmlNames.ElementEquals(parentName, DocumentationCommentXmlNames.ParameterReferenceElementName))
            {
                return XmlNameAttributeElementKind.ParameterReference;
            }
            else if (DocumentationCommentXmlNames.ElementEquals(parentName, DocumentationCommentXmlNames.TypeParameterElementName))
            {
                return XmlNameAttributeElementKind.TypeParameter;
            }
            else if (DocumentationCommentXmlNames.ElementEquals(parentName, DocumentationCommentXmlNames.TypeParameterReferenceElementName))
            {
                return XmlNameAttributeElementKind.TypeParameterReference;
            }
            else
            {
                throw ExceptionUtilities.UnexpectedValue(parentName);
            }
        }

        internal static bool ReportDocumentationCommentDiagnostics(this SyntaxTree tree)
        {
            return tree.Options.DocumentationMode >= DocumentationMode.Diagnose;
        }

        /// <summary>
        /// Updates the given SimpleNameSyntax node with the given identifier token.
        /// This function is a wrapper that calls WithIdentifier on derived syntax nodes.
        /// </summary>
        /// <param name="simpleName"></param>
        /// <param name="identifier"></param>
        /// <returns>The given simple name updated with the given identifier.</returns>
        public static SimpleNameSyntax WithIdentifier(this SimpleNameSyntax simpleName, SyntaxToken identifier)
        {
            return simpleName.Kind == SyntaxKind.IdentifierName
                ? (SimpleNameSyntax)((IdentifierNameSyntax)simpleName).WithIdentifier(identifier)
                : (SimpleNameSyntax)((GenericNameSyntax)simpleName).WithIdentifier(identifier);
        }

        internal static bool IsTypeInContextWhichNeedsDynamicAttribute(this IdentifierNameSyntax typeNode)
        {
            Debug.Assert(typeNode != null);
            Debug.Assert(SyntaxFacts.IsInTypeOnlyContext(typeNode));

            return IsInContextWhichNeedsDynamicAttribute(typeNode);
        }

        internal static CSharpSyntaxNode SkipParens(this CSharpSyntaxNode expression)
        {
            while (expression != null && expression.Kind == SyntaxKind.ParenthesizedExpression)
            {
                expression = ((ParenthesizedExpressionSyntax)expression).Expression;
            }

            return expression;
        }

        private static bool IsInContextWhichNeedsDynamicAttribute(CSharpSyntaxNode node)
        {
            Debug.Assert(node != null);

            switch (node.Kind)
            {
                case SyntaxKind.Parameter:
                case SyntaxKind.FieldDeclaration:
                case SyntaxKind.MethodDeclaration:
                case SyntaxKind.IndexerDeclaration:
                case SyntaxKind.OperatorDeclaration:
                case SyntaxKind.ConversionOperatorDeclaration:
                case SyntaxKind.PropertyDeclaration:
                case SyntaxKind.DelegateDeclaration:
                case SyntaxKind.EventDeclaration:
                case SyntaxKind.BaseList:
                case SyntaxKind.SimpleBaseType:
                    return true;

                case SyntaxKind.Block:
                case SyntaxKind.VariableDeclarator:
                case SyntaxKind.TypeParameterConstraintClause:
                case SyntaxKind.Attribute:
                case SyntaxKind.EqualsValueClause:
                    return false;

                default:
                    return node.Parent != null && IsInContextWhichNeedsDynamicAttribute(node.Parent);
            }
        }

        public static IndexerDeclarationSyntax Update(
            this IndexerDeclarationSyntax syntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            SyntaxTokenList modifiers,
            TypeSyntax type,
            ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifier,
            SyntaxToken thisKeyword,
            BracketedParameterListSyntax parameterList,
            AccessorListSyntax accessorList)
        {
            return syntax.Update(
                attributeLists,
                modifiers,
                type,
                explicitInterfaceSpecifier,
                thisKeyword,
                parameterList,
                accessorList,
                default(ArrowExpressionClauseSyntax),
                default(SyntaxToken));
        }

        public static OperatorDeclarationSyntax Update(
            this OperatorDeclarationSyntax syntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            SyntaxTokenList modifiers,
            TypeSyntax returnType,
            SyntaxToken operatorKeyword,
            SyntaxToken operatorToken,
            ParameterListSyntax parameterList,
            BlockSyntax block,
            SyntaxToken semicolonToken)
        {
            return syntax.Update(
                attributeLists,
                modifiers,
                returnType,
                operatorKeyword,
                operatorToken,
                parameterList,
                block,
                default(ArrowExpressionClauseSyntax),
                semicolonToken);
        }

        public static MethodDeclarationSyntax Update(
            this MethodDeclarationSyntax syntax,
            SyntaxList<AttributeListSyntax> attributeLists,
            SyntaxTokenList modifiers,
            TypeSyntax returnType,
            ExplicitInterfaceSpecifierSyntax explicitInterfaceSpecifier,
            SyntaxToken identifier,
            TypeParameterListSyntax typeParameterList,
            ParameterListSyntax parameterList,
            SyntaxList<TypeParameterConstraintClauseSyntax> constraintClauses,
            BlockSyntax block,
            SyntaxToken semicolonToken)
        {
            return syntax.Update(
                attributeLists,
                modifiers,
                returnType,
                explicitInterfaceSpecifier,
                identifier,
                typeParameterList,
                parameterList,
                constraintClauses,
                block,
                default(ArrowExpressionClauseSyntax),
                semicolonToken);
        }
    }
}
