﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions.ContextQuery;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Extensions
{
    internal static partial class SyntaxTreeExtensions
    {
        public static bool IsInteractiveOrScript(this SyntaxTree syntaxTree)
        {
            return syntaxTree.Options.Kind != SourceCodeKind.Regular;
        }

        public static ISet<SyntaxKind> GetPrecedingModifiers(
            this SyntaxTree syntaxTree, int position, SyntaxToken tokenOnLeftOfPosition, CancellationToken cancellationToken)
        {
            var token = tokenOnLeftOfPosition;
            token = token.GetPreviousTokenIfTouchingWord(position);

            var result = new HashSet<SyntaxKind>(SyntaxFacts.EqualityComparer);
            while (true)
            {
                switch (token.CSharpKind())
                {
                    case SyntaxKind.PublicKeyword:
                    case SyntaxKind.InternalKeyword:
                    case SyntaxKind.ProtectedKeyword:
                    case SyntaxKind.PrivateKeyword:
                    case SyntaxKind.SealedKeyword:
                    case SyntaxKind.AbstractKeyword:
                    case SyntaxKind.StaticKeyword:
                    case SyntaxKind.VirtualKeyword:
                    case SyntaxKind.ExternKeyword:
                    case SyntaxKind.NewKeyword:
                    case SyntaxKind.OverrideKeyword:
                    case SyntaxKind.ReadOnlyKeyword:
                    case SyntaxKind.VolatileKeyword:
                    case SyntaxKind.UnsafeKeyword:
                    case SyntaxKind.AsyncKeyword:
                        result.Add(token.CSharpKind());
                        token = token.GetPreviousToken(includeSkipped: true);
                        continue;
                    case SyntaxKind.IdentifierToken:
                        if (token.HasMatchingText(SyntaxKind.AsyncKeyword))
                        {
                            result.Add(SyntaxKind.AsyncKeyword);
                            token = token.GetPreviousToken(includeSkipped: true);
                            continue;
                        }

                        break;
                }

                break;
            }

            return result;
        }

        public static TypeDeclarationSyntax GetContainingTypeDeclaration(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return syntaxTree.GetContainingTypeDeclarations(position, cancellationToken).FirstOrDefault();
        }

        public static BaseTypeDeclarationSyntax GetContainingTypeOrEnumDeclaration(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return syntaxTree.GetContainingTypeOrEnumDeclarations(position, cancellationToken).FirstOrDefault();
        }

        public static IEnumerable<TypeDeclarationSyntax> GetContainingTypeDeclarations(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);

            return token.GetAncestors<TypeDeclarationSyntax>().Where(t =>
            {
                return BaseTypeDeclarationContainsPosition(t, position);
            });
        }

        private static bool BaseTypeDeclarationContainsPosition(BaseTypeDeclarationSyntax declaration, int position)
        {
            if (position <= declaration.OpenBraceToken.SpanStart)
            {
                return false;
            }

            if (declaration.CloseBraceToken.IsMissing)
            {
                return true;
            }

            return position <= declaration.CloseBraceToken.SpanStart;
        }

        public static IEnumerable<BaseTypeDeclarationSyntax> GetContainingTypeOrEnumDeclarations(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);

            return token.GetAncestors<BaseTypeDeclarationSyntax>().Where(t => BaseTypeDeclarationContainsPosition(t, position));
        }

        /// <summary>
        /// If the position is inside of token, return that token; otherwise, return the token to the right.
        /// </summary>
        public static SyntaxToken FindTokenOnRightOfPosition(
            this SyntaxTree syntaxTree,
            int position,
            CancellationToken cancellationToken,
            bool includeSkipped = true,
            bool includeDirectives = false,
            bool includeDocumentationComments = false)
        {
            return syntaxTree.GetRoot(cancellationToken).FindTokenOnRightOfPosition(
                position, includeSkipped, includeDirectives, includeDocumentationComments);
        }

        /// <summary>
        /// If the position is inside of token, return that token; otherwise, return the token to the left.
        /// </summary>
        public static SyntaxToken FindTokenOnLeftOfPosition(
            this SyntaxTree syntaxTree,
            int position,
            CancellationToken cancellationToken,
            bool includeSkipped = true,
            bool includeDirectives = false,
            bool includeDocumentationComments = false)
        {
            return syntaxTree.GetRoot(cancellationToken).FindTokenOnLeftOfPosition(
                position, includeSkipped, includeDirectives, includeDocumentationComments);
        }

        private static readonly Func<SyntaxKind, bool> IsDotOrArrow = k => k == SyntaxKind.DotToken || k == SyntaxKind.MinusGreaterThanToken;
        private static readonly Func<SyntaxKind, bool> IsDotOrArrowOrColonColon =
            k => k == SyntaxKind.DotToken || k == SyntaxKind.MinusGreaterThanToken || k == SyntaxKind.ColonColonToken;

        public static bool IsNamespaceDeclarationNameContext(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);
            var namespaceName = token.GetAncestor<NamespaceDeclarationSyntax>();
            if (namespaceName == null)
            {
                return false;
            }

            return namespaceName.Name.Span.IntersectsWith(position);
        }

        public static bool IsRightOfDotOrArrowOrColonColon(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return syntaxTree.IsRightOf(position, IsDotOrArrowOrColonColon, cancellationToken);
        }

        public static bool IsRightOfDotOrArrow(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return syntaxTree.IsRightOf(position, IsDotOrArrow, cancellationToken);
        }

        private static bool IsRightOf(
            this SyntaxTree syntaxTree, int position, Func<SyntaxKind, bool> predicate, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);
            token = token.GetPreviousTokenIfTouchingWord(position);

            if (token.CSharpKind() == SyntaxKind.None)
            {
                return false;
            }

            return predicate(token.CSharpKind());
        }

        public static bool IsRightOfNumericLiteral(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);
            return token.CSharpKind() == SyntaxKind.NumericLiteralToken;
        }

        public static bool IsPrimaryFunctionExpressionContext(this SyntaxTree syntaxTree, int position, SyntaxToken tokenOnLeftOfPosition, CancellationToken cancellationToken)
        {
            return
                syntaxTree.IsTypeOfExpressionContext(position, tokenOnLeftOfPosition, cancellationToken) ||
                syntaxTree.IsDefaultExpressionContext(position, tokenOnLeftOfPosition, cancellationToken) ||
                syntaxTree.IsSizeOfExpressionContext(position, tokenOnLeftOfPosition, cancellationToken);
        }

        public static bool IsAfterKeyword(this SyntaxTree syntaxTree, int position, SyntaxKind kind, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);
            token = token.GetPreviousTokenIfTouchingWord(position);

            return token.CSharpKind() == kind;
        }

        public static bool IsInNonUserCode(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return
                syntaxTree.IsEntirelyWithinNonUserCodeComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinStringOrCharLiteral(position, cancellationToken) ||
                syntaxTree.IsInInactiveRegion(position, cancellationToken);
        }

        public static bool IsEntirelyWithinNonUserCodeComment(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var inNonUserSingleLineDocComment =
                syntaxTree.IsEntirelyWithinSingleLineDocComment(position, cancellationToken) && !syntaxTree.IsEntirelyWithinCrefSyntax(position, cancellationToken);
            return
                syntaxTree.IsEntirelyWithinTopLevelSingleLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinPreProcessorSingleLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinMultiLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinMultiLineDocComment(position, cancellationToken) ||
                inNonUserSingleLineDocComment;
        }

        public static bool IsEntirelyWithinComment(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return
                syntaxTree.IsEntirelyWithinTopLevelSingleLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinPreProcessorSingleLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinMultiLineComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinMultiLineDocComment(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinSingleLineDocComment(position, cancellationToken);
        }

        public static bool IsCrefContext(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken, includeDocumentationComments: true);
            token = token.GetPreviousTokenIfTouchingWord(position);

            if (token.Parent is XmlCrefAttributeSyntax)
            {
                var attribute = (XmlCrefAttributeSyntax)token.Parent;
                return token == attribute.StartQuoteToken;
            }

            return false;
        }

        public static bool IsEntirelyWithinCrefSyntax(this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            if (syntaxTree.IsCrefContext(position, cancellationToken))
            {
                return true;
            }

            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken, includeDocumentationComments: true);
            return token.GetAncestor<CrefSyntax>() != null;
        }

        public static bool IsEntirelyWithinSingleLineDocComment(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var root = syntaxTree.GetRoot(cancellationToken) as CompilationUnitSyntax;
            var trivia = root.FindTrivia(position);

            // If we ask right at the end of the file, we'll get back nothing.
            // So move back in that case and ask again.
            var eofPosition = root.FullWidth();
            if (position == eofPosition)
            {
                var eof = root.EndOfFileToken;
                if (eof.HasLeadingTrivia)
                {
                    trivia = eof.LeadingTrivia.Last();
                }
            }

            if (trivia.IsSingleLineDocComment())
            {
                var span = trivia.Span;
                var fullSpan = trivia.FullSpan;
                var endsWithNewLine = trivia.GetStructure().GetLastToken(includeSkipped: true).CSharpKind() == SyntaxKind.XmlTextLiteralNewLineToken;

                if (endsWithNewLine)
                {
                    if (position > fullSpan.Start && position < fullSpan.End)
                    {
                        return true;
                    }
                }
                else
                {
                    if (position > fullSpan.Start && position <= fullSpan.End)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsEntirelyWithinMultiLineDocComment(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var trivia = syntaxTree.GetRoot(cancellationToken).FindTrivia(position);

            if (trivia.IsMultiLineDocComment())
            {
                var span = trivia.FullSpan;

                if (position > span.Start && position < span.End)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEntirelyWithinMultiLineComment(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var trivia = FindTriviaAndAdjustForEndOfFile(syntaxTree, position, cancellationToken);

            if (trivia.IsMultiLineComment())
            {
                var span = trivia.FullSpan;

                return trivia.IsCompleteMultiLineComment()
                    ? position > span.Start && position < span.End
                    : position > span.Start && position <= span.End;
            }

            return false;
        }

        public static bool IsEntirelyWithinTopLevelSingleLineComment(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var trivia = FindTriviaAndAdjustForEndOfFile(syntaxTree, position, cancellationToken);

            if (trivia.CSharpKind() == SyntaxKind.EndOfLineTrivia)
            {
                // Check if we're on the newline right at the end of a comment
                trivia = trivia.GetPreviousTrivia(syntaxTree, cancellationToken);
            }

            if (trivia.IsSingleLineComment())
            {
                var span = trivia.FullSpan;

                if (position > span.Start && position <= span.End)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsEntirelyWithinPreProcessorSingleLineComment(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            // Search inside trivia for directives to ensure that we recognize
            // single-line comments at the end of preprocessor directives.
            var trivia = FindTriviaAndAdjustForEndOfFile(syntaxTree, position, cancellationToken, findInsideTrivia: true);

            if (trivia.CSharpKind() == SyntaxKind.EndOfLineTrivia)
            {
                // Check if we're on the newline right at the end of a comment
                trivia = trivia.GetPreviousTrivia(syntaxTree, cancellationToken, findInsideTrivia: true);
            }

            if (trivia.IsSingleLineComment())
            {
                var span = trivia.FullSpan;

                if (position > span.Start && position <= span.End)
                {
                    return true;
                }
            }

            return false;
        }

        private static SyntaxTrivia FindTriviaAndAdjustForEndOfFile(
            SyntaxTree syntaxTree, int position, CancellationToken cancellationToken, bool findInsideTrivia = false)
        {
            var root = syntaxTree.GetRoot(cancellationToken) as CompilationUnitSyntax;
            var trivia = root.FindTrivia(position, findInsideTrivia);

            // If we ask right at the end of the file, we'll get back nothing.
            // We handle that case specially for now, though SyntaxTree.FindTrivia should
            // work at the end of a file.
            if (position == root.FullWidth())
            {
                var endOfFileToken = root.EndOfFileToken;
                if (endOfFileToken.HasLeadingTrivia)
                {
                    trivia = endOfFileToken.LeadingTrivia.Last();
                }
                else
                {
                    var token = endOfFileToken.GetPreviousToken(includeSkipped: true);
                    if (token.HasTrailingTrivia)
                    {
                        trivia = token.TrailingTrivia.Last();
                    }
                }
            }

            return trivia;
        }

        private static bool AtEndOfIncompleteStringOrCharLiteral(SyntaxToken token, int position, char lastChar)
        {
            if (!token.IsKind(SyntaxKind.StringLiteralToken, SyntaxKind.CharacterLiteralToken))
            {
                throw new ArgumentException(CSharpWorkspaceResources.ExpectedStringOrCharLitera, "token");
            }

            int startLength = 1;
            if (token.IsVerbatimStringLiteral())
            {
                startLength = 2;
            }

            return position == token.Span.End &&
                (token.Span.Length == startLength || (token.Span.Length > startLength && token.ToString().Cast<char>().LastOrDefault() != lastChar));
        }

        public static bool IsEntirelyWithinStringOrCharLiteral(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            return
                syntaxTree.IsEntirelyWithinStringLiteral(position, cancellationToken) ||
                syntaxTree.IsEntirelyWithinCharLiteral(position, cancellationToken);
        }

        public static bool IsEntirelyWithinStringLiteral(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var token = syntaxTree.GetRoot(cancellationToken).FindToken(position, findInsideTrivia: true);

            // If we ask right at the end of the file, we'll get back nothing. We handle that case
            // specially for now, though SyntaxTree.FindToken should work at the end of a file.
            if (token.IsKind(SyntaxKind.EndOfDirectiveToken, SyntaxKind.EndOfFileToken))
            {
                token = token.GetPreviousToken(includeSkipped: true, includeDirectives: true);
            }

            if (token.IsKind(SyntaxKind.StringLiteralToken))
            {
                var span = token.Span;

                // cases:
                // "|"
                // "|  (e.g. incomplete string literal)
                return (position > span.Start && position < span.End)
                    || AtEndOfIncompleteStringOrCharLiteral(token, position, '"');
            }

            return false;
        }

        public static bool IsEntirelyWithinCharLiteral(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var root = syntaxTree.GetRoot(cancellationToken) as CompilationUnitSyntax;
            var token = root.FindToken(position, findInsideTrivia: true);

            // If we ask right at the end of the file, we'll get back nothing.
            // We handle that case specially for now, though SyntaxTree.FindToken should
            // work at the end of a file.
            if (position == root.FullWidth())
            {
                token = root.EndOfFileToken.GetPreviousToken(includeSkipped: true, includeDirectives: true);
            }

            if (token.CSharpKind() == SyntaxKind.CharacterLiteralToken)
            {
                var span = token.Span;

                // cases:
                // '|'
                // '|  (e.g. incomplete char literal)
                return (position > span.Start && position < span.End)
                    || AtEndOfIncompleteStringOrCharLiteral(token, position, '\'');
            }

            return false;
        }

        public static bool IsInInactiveRegion(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(syntaxTree);

            // cases:
            // $ is EOF

            // #if false
            //    |

            // #if false
            //    |$

            // #if false
            // |

            // #if false
            // |$

            if (syntaxTree.IsPreProcessorKeywordContext(position, cancellationToken))
            {
                return false;
            }

            // The latter two are the hard cases we don't actually have an 
            // DisabledTextTrivia yet. 
            var trivia = syntaxTree.GetRoot(cancellationToken).FindTrivia(position, findInsideTrivia: false);
            if (trivia.CSharpKind() == SyntaxKind.DisabledTextTrivia)
            {
                return true;
            }

            var token = syntaxTree.FindTokenOrEndToken(position, cancellationToken);
            var text = syntaxTree.GetText(cancellationToken);
            var lineContainingPosition = text.Lines.IndexOf(position);
            if (token.CSharpKind() == SyntaxKind.EndOfFileToken)
            {
                var triviaList = token.LeadingTrivia;
                foreach (var triviaTok in triviaList.Reverse())
                {
                    if (triviaTok.HasStructure)
                    {
                        var structure = triviaTok.GetStructure();
                        if (structure is BranchingDirectiveTriviaSyntax)
                        {
                            var triviaLine = text.Lines.IndexOf(triviaTok.SpanStart);
                            if (triviaLine < lineContainingPosition)
                            {
                                var branch = (BranchingDirectiveTriviaSyntax)structure;
                                return !branch.IsActive || !branch.BranchTaken;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool IsBeforeFirstToken(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            var firstToken = syntaxTree.GetRoot(cancellationToken).GetFirstToken(includeZeroWidth: true, includeSkipped: true);

            return position <= firstToken.SpanStart;
        }

        public static SyntaxToken FindTokenOrEndToken(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            Contract.ThrowIfNull(syntaxTree);

            var root = syntaxTree.GetRoot(cancellationToken) as CompilationUnitSyntax;
            var result = root.FindToken(position, findInsideTrivia: true);
            if (result.CSharpKind() != SyntaxKind.None)
            {
                return result;
            }

            // Special cases.  See if we're actually at the end of a:
            // a) doc comment
            // b) pp directive
            // c) file

            var triviaList = root.EndOfFileToken.LeadingTrivia;
            foreach (var trivia in triviaList.Reverse())
            {
                if (trivia.HasStructure)
                {
                    var token = trivia.GetStructure().GetLastToken(includeZeroWidth: true);
                    if (token.Span.End == position)
                    {
                        return token;
                    }
                }
            }

            if (position == root.FullSpan.End)
            {
                return root.EndOfFileToken;
            }

            return default(SyntaxToken);
        }

        public static IList<MemberDeclarationSyntax> GetMembersInSpan(
            this SyntaxTree syntaxTree,
            TextSpan textSpan,
            CancellationToken cancellationToken)
        {
            var token = syntaxTree.GetRoot(cancellationToken).FindToken(textSpan.Start);
            var firstMember = token.GetAncestors<MemberDeclarationSyntax>().FirstOrDefault();
            if (firstMember != null)
            {
                var containingType = firstMember.Parent as TypeDeclarationSyntax;
                if (containingType != null)
                {
                    var members = GetMembersInSpan(textSpan, containingType, firstMember);
                    if (members != null)
                    {
                        return members;
                    }
                }
            }

            return SpecializedCollections.EmptyList<MemberDeclarationSyntax>();
        }

        private static List<MemberDeclarationSyntax> GetMembersInSpan(
            TextSpan textSpan,
            TypeDeclarationSyntax containingType,
            MemberDeclarationSyntax firstMember)
        {
            List<MemberDeclarationSyntax> selectedMembers = null;

            var members = containingType.Members;
            var fieldIndex = members.IndexOf(firstMember);
            if (fieldIndex < 0)
            {
                return null;
            }

            for (var i = fieldIndex; i < members.Count; i++)
            {
                var member = members[i];
                if (textSpan.Contains(member.Span))
                {
                    selectedMembers = selectedMembers ?? new List<MemberDeclarationSyntax>();
                    selectedMembers.Add(member);
                }
                else if (textSpan.OverlapsWith(member.Span))
                {
                    return null;
                }
                else
                {
                    break;
                }
            }

            return selectedMembers;
        }

        public static bool IsInPartiallyWrittenGeneric(
            this SyntaxTree syntaxTree, int position, CancellationToken cancellationToken)
        {
            SyntaxToken genericIdentifier;
            SyntaxToken lessThanToken;
            return syntaxTree.IsInPartiallyWrittenGeneric(position, cancellationToken, out genericIdentifier, out lessThanToken);
        }

        public static bool IsInPartiallyWrittenGeneric(
            this SyntaxTree syntaxTree,
            int position,
            CancellationToken cancellationToken,
            out SyntaxToken genericIdentifier)
        {
            SyntaxToken lessThanToken;
            return syntaxTree.IsInPartiallyWrittenGeneric(position, cancellationToken, out genericIdentifier, out lessThanToken);
        }

        public static bool IsInPartiallyWrittenGeneric(
            this SyntaxTree syntaxTree,
            int position,
            CancellationToken cancellationToken,
            out SyntaxToken genericIdentifier,
            out SyntaxToken lessThanToken)
        {
            genericIdentifier = default(SyntaxToken);
            lessThanToken = default(SyntaxToken);
            int index = 0;

            var token = syntaxTree.FindTokenOnLeftOfPosition(position, cancellationToken);
            if (token.CSharpKind() == SyntaxKind.None)
            {
                return false;
            }

            // check whether we are under type or member decl
            if (token.GetAncestor<TypeParameterListSyntax>() != null)
            {
                return false;
            }

            int stack = 0;
            while (true)
            {
                switch (token.CSharpKind())
                {
                    case SyntaxKind.LessThanToken:
                        if (stack == 0)
                        {
                            // got here so we read successfully up to a < now we have to read the
                            // name before that and we're done!
                            lessThanToken = token;
                            token = token.GetPreviousToken(includeSkipped: true);
                            if (token.CSharpKind() == SyntaxKind.None)
                            {
                                return false;
                            }

                            // ok
                            // so we've read something like:
                            // ~~~~~~~~~<a,b,...
                            // but we need to know the simple name that precedes the <
                            // it could be
                            // ~~~~~~foo<a,b,...
                            if (token.CSharpKind() == SyntaxKind.IdentifierToken)
                            {
                                // okay now check whether it is actually partially written
                                if (IsFullyWrittenGeneric(token, lessThanToken))
                                {
                                    return false;
                                }

                                genericIdentifier = token;
                                return true;
                            }

                            return false;
                        }
                        else
                        {
                            stack--;
                            break;
                        }

                    case SyntaxKind.GreaterThanGreaterThanToken:
                        stack++;
                        goto case SyntaxKind.GreaterThanToken;

                    // fall through
                    case SyntaxKind.GreaterThanToken:
                        stack++;
                        break;

                    case SyntaxKind.AsteriskToken:      // for int*
                    case SyntaxKind.QuestionToken:      // for int?
                    case SyntaxKind.ColonToken:         // for global::  (so we don't dismiss help as you type the first :)
                    case SyntaxKind.ColonColonToken:    // for global::
                    case SyntaxKind.CloseBracketToken:
                    case SyntaxKind.OpenBracketToken:
                    case SyntaxKind.DotToken:
                    case SyntaxKind.IdentifierToken:
                        break;

                    case SyntaxKind.CommaToken:
                        if (stack == 0)
                        {
                            index++;
                        }

                        break;

                    default:
                        // user might have typed "in" on the way to typing "int"
                        // don't want to disregard this genericname because of that
                        if (SyntaxFacts.IsKeywordKind(token.CSharpKind()))
                        {
                            break;
                        }

                        // anything else and we're sunk.
                        return false;
                }

                // look backward one token, include skipped tokens, because the parser frequently
                // does skip them in cases like: "Func<A, B", which get parsed as: expression
                // statement "Func<A" with missing semicolon, expression statement "B" with missing
                // semicolon, and the "," is skipped.
                token = token.GetPreviousToken(includeSkipped: true);
                if (token.CSharpKind() == SyntaxKind.None)
                {
                    return false;
                }
            }
        }

        private static bool IsFullyWrittenGeneric(SyntaxToken token, SyntaxToken lessThanToken)
        {
            var genericName = token.Parent as GenericNameSyntax;

            return genericName != null && genericName.TypeArgumentList != null &&
                   genericName.TypeArgumentList.LessThanToken == lessThanToken && !genericName.TypeArgumentList.GreaterThanToken.IsMissing;
        }
    }
}