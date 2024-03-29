﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.Syntax.InternalSyntax
{
    internal abstract partial class SyntaxParser : IDisposable
    {
        protected readonly Lexer lexer;
        private readonly bool isIncremental;
        private readonly bool allowModeReset;
        protected readonly CancellationToken cancellationToken;

        private LexerMode mode;
        private Blender firstBlender;
        private BlendedNode currentNode;
        private SyntaxToken currentToken;
        private ArrayElement<SyntaxToken>[] lexedTokens;
        private CSharpSyntaxNode prevTokenTrailingTrivia;
        private int firstToken;
        private int tokenOffset;
        private int tokenCount;
        private int resetCount;
        private int resetStart;

        private static readonly ObjectPool<BlendedNode[]> blendedNodesPool = new ObjectPool<BlendedNode[]>(() => new BlendedNode[32], 2);

        private BlendedNode[] blendedTokens;

        protected SyntaxParser(
            Lexer lexer,
            LexerMode mode,
            CSharp.CSharpSyntaxNode oldTree,
            IEnumerable<TextChangeRange> changes,
            bool allowModeReset,
            bool preLexIfNotIncremental = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.lexer = lexer;
            this.mode = mode;
            this.allowModeReset = allowModeReset;
            this.cancellationToken = cancellationToken;
            this.currentNode = default(BlendedNode);
            this.isIncremental = oldTree != null;

            if (this.IsIncremental || allowModeReset)
            {
                this.firstBlender = new Blender(lexer, oldTree, changes);
                this.blendedTokens = blendedNodesPool.Allocate();
            }
            else
            {
                this.firstBlender = default(Blender);
                this.lexedTokens = new ArrayElement<SyntaxToken>[32];
            }

            // PreLex is not cancellable. 
            //      If we may cancell why would we aggressively lex ahead?
            //      Cancellations in a constructor make disposing complicated
            //
            // So, if we have a real cancellation token, do not do prelexing.
            if (preLexIfNotIncremental && !this.IsIncremental && !cancellationToken.CanBeCanceled)
            {
                this.PreLex();
            }
        }

        public void Dispose()
        {
            var blendedTokens = this.blendedTokens;
            if (blendedTokens != null)
            {
                Array.Clear(this.blendedTokens, 0, this.blendedTokens.Length);
                blendedNodesPool.Free(this.blendedTokens);
                this.blendedTokens = null;
            }
        }

        protected void ReInitialize()
        {
            this.firstToken = 0;
            this.tokenOffset = 0;
            this.tokenCount = 0;
            this.resetCount = 0;
            this.resetStart = 0;
            this.currentToken = null;
            this.prevTokenTrailingTrivia = null;
            if (this.IsIncremental || allowModeReset)
            {
                this.firstBlender = new Blender(this.lexer, null, null);
            }
        }

        protected bool IsIncremental
        {
            get
            {
                return this.isIncremental;
            }
        }

        private void PreLex()
        {
            // NOTE: Do not cancel in this method. It is called from the constructor.
            var size = Math.Min(4096, Math.Max(32, this.lexer.TextWindow.Text.Length / 2));
            this.lexedTokens = new ArrayElement<SyntaxToken>[size];
            var lexer = this.lexer;
            var mode = this.mode;

            for (int i = 0; i < size; i++)
            {
                var token = lexer.Lex(mode);
                this.AddLexedToken(token);
                if (token.Kind == SyntaxKind.EndOfFileToken)
                {
                    break;
                }
            }
        }

        protected ResetPoint GetResetPoint()
        {
            var pos = this.firstToken + this.tokenOffset;
            if (this.resetCount == 0)
            {
                this.resetStart = pos; // low water mark
            }

            this.resetCount++;
            return new ResetPoint(this.resetCount, this.mode, pos, prevTokenTrailingTrivia);
        }

        protected void Reset(ref ResetPoint point)
        {
            this.mode = point.Mode;
            var offset = point.Position - this.firstToken;
            Debug.Assert(offset >= 0 && offset < this.tokenCount);
            this.tokenOffset = offset;
            this.currentToken = default(SyntaxToken);
            this.currentNode = default(BlendedNode);
            this.prevTokenTrailingTrivia = point.PrevTokenTrailingTrivia;
            if (this.blendedTokens != null)
            {
                // look forward for slots not holding a token
                for (int i = this.tokenOffset; i < this.tokenCount; i++)
                {
                    if (this.blendedTokens[i].Token == null)
                    {
                        // forget anything after and including any slot not holding a token
                        this.tokenCount = i;
                        break;
                    }
                }
            }
        }

        protected void Release(ref ResetPoint point)
        {
            Debug.Assert(this.resetCount == point.ResetCount);
            this.resetCount--;
            if (this.resetCount == 0)
            {
                this.resetStart = -1;
            }
        }

        public CSharpParseOptions Options
        {
            get { return this.lexer.Options; }
        }

        /// <summary>
        /// Interactive code - global statements, member declarations and expressions allowed.
        /// </summary>
        public bool IsInteractive
        {
            get { return Options.Kind == SourceCodeKind.Interactive; }
        }

        /// <summary>
        /// Script - global statements and member declarations allowed, but not expressions.
        /// </summary>
        public bool IsScript
        {
            get { return Options.Kind != SourceCodeKind.Regular; }
        }

        protected LexerMode Mode
        {
            get
            {
                return this.mode;
            }

            set
            {
                if (this.mode != value)
                {
                    Debug.Assert(this.allowModeReset);

                    this.mode = value;
                    this.currentToken = default(SyntaxToken);
                    this.currentNode = default(BlendedNode);
                    this.tokenCount = this.tokenOffset;
                }
            }
        }

        protected CSharp.CSharpSyntaxNode CurrentNode
        {
            get
            {
                // we will fail anyways. Assert is just to catch that earlier.
                Debug.Assert(this.blendedTokens != null);

                //PERF: currentNode is a BlendedNode, which is a fairly large struct.
                // the following code tries not to pull the whole struct into a local
                // we only need .Node
                var node = this.currentNode.Node;
                if (node != null)
                {
                    return node;
                }

                this.ReadCurrentNode();
                return this.currentNode.Node;
            }
        }

        protected SyntaxKind CurrentNodeKind
        {
            get
            {
                var cn = this.CurrentNode;
                return cn != null ? cn.Kind : SyntaxKind.None;
            }
        }

        private void ReadCurrentNode()
        {
            if (this.tokenOffset == 0)
            {
                this.currentNode = this.firstBlender.ReadNode(this.mode);
            }
            else
            {
                this.currentNode = this.blendedTokens[this.tokenOffset - 1].Blender.ReadNode(this.mode);
            }
        }

        protected GreenNode EatNode()
        {
            // we will fail anyways. Assert is just to catch that earlier.
            Debug.Assert(this.blendedTokens != null);

            // remember result
            var result = CurrentNode.Green;

            // store possible non-token in token sequence 
            if (this.tokenOffset >= this.blendedTokens.Length)
            {
                this.AddTokenSlot();
            }

            this.blendedTokens[this.tokenOffset++] = this.currentNode;
            this.tokenCount = this.tokenOffset; // forget anything after this slot

            // erase current state
            this.currentNode = default(BlendedNode);
            this.currentToken = default(SyntaxToken);

            return result;
        }

        protected SyntaxToken CurrentToken
        {
            get
            {
                return this.currentToken ?? (this.currentToken = this.FetchCurrentToken());
            }
        }

        private SyntaxToken FetchCurrentToken()
        {
            if (this.tokenOffset >= this.tokenCount)
            {
                this.AddNewToken();
            }

            if (this.blendedTokens != null)
            {
                return this.blendedTokens[this.tokenOffset].Token;
            }
            else
            {
                return this.lexedTokens[this.tokenOffset];
            }
        }

        private void AddNewToken()
        {
            if (this.blendedTokens != null)
            {
                if (this.tokenCount > 0)
                {
                    this.AddToken(this.blendedTokens[this.tokenCount - 1].Blender.ReadToken(this.mode));
                }
                else
                {
                    if (this.currentNode.Token != null)
                    {
                        this.AddToken(this.currentNode);
                    }
                    else
                    {
                        this.AddToken(this.firstBlender.ReadToken(this.mode));
                    }
                }
            }
            else
            {
                this.AddLexedToken(this.lexer.Lex(this.mode));
            }
        }

        // adds token to end of current token array
        private void AddToken(BlendedNode tokenResult)
        {
            Debug.Assert(tokenResult.Token != null);
            if (this.tokenCount >= this.blendedTokens.Length)
            {
                this.AddTokenSlot();
            }

            this.blendedTokens[this.tokenCount] = tokenResult;
            this.tokenCount++;
        }

        private void AddLexedToken(SyntaxToken token)
        {
            Debug.Assert(token != null);
            if (this.tokenCount >= this.lexedTokens.Length)
            {
                this.AddLexedTokenSlot();
            }

            this.lexedTokens[this.tokenCount].Value = token;
            this.tokenCount++;
        }

        private void AddTokenSlot()
        {
            // shift tokens to left if we are far to the right
            // don't shift if reset points have fixed locked tge starting point at the token in the window
            if (this.tokenOffset > (this.blendedTokens.Length >> 1)
                && (this.resetStart == -1 || this.resetStart > this.firstToken))
            {
                int shiftOffset = (this.resetStart == -1) ? this.tokenOffset : this.resetStart - this.firstToken;
                int shiftCount = this.tokenCount - shiftOffset;
                Debug.Assert(shiftOffset > 0);
                this.firstBlender = this.blendedTokens[shiftOffset - 1].Blender;
                if (shiftCount > 0)
                {
                    Array.Copy(this.blendedTokens, shiftOffset, this.blendedTokens, 0, shiftCount);
                }

                this.firstToken += shiftOffset;
                this.tokenCount -= shiftOffset;
                this.tokenOffset -= shiftOffset;
            }
            else
            {
                var old = this.blendedTokens;
                Array.Resize(ref this.blendedTokens, this.blendedTokens.Length * 2);
                blendedNodesPool.ForgetTrackedObject(old, replacement: this.blendedTokens);
            }
        }

        private void AddLexedTokenSlot()
        {
            // shift tokens to left if we are far to the right
            // don't shift if reset points have fixed locked tge starting point at the token in the window
            if (this.tokenOffset > (this.lexedTokens.Length >> 1)
                && (this.resetStart == -1 || this.resetStart > this.firstToken))
            {
                int shiftOffset = (this.resetStart == -1) ? this.tokenOffset : this.resetStart - this.firstToken;
                int shiftCount = this.tokenCount - shiftOffset;
                Debug.Assert(shiftOffset > 0);
                if (shiftCount > 0)
                {
                    Array.Copy(this.lexedTokens, shiftOffset, this.lexedTokens, 0, shiftCount);
                }

                this.firstToken += shiftOffset;
                this.tokenCount -= shiftOffset;
                this.tokenOffset -= shiftOffset;
            }
            else
            {
                var tmp = new ArrayElement<SyntaxToken>[this.lexedTokens.Length * 2];
                Array.Copy(this.lexedTokens, tmp, this.lexedTokens.Length);
                this.lexedTokens = tmp;
            }
        }

        protected SyntaxToken PeekToken(int n)
        {
            Debug.Assert(n >= 0);
            while (this.tokenOffset + n >= this.tokenCount)
            {
                this.AddNewToken();
            }

            if (this.blendedTokens != null)
            {
                return this.blendedTokens[this.tokenOffset + n].Token;
            }
            else
            {
                return this.lexedTokens[this.tokenOffset + n];
            }
        }

        //this method is called very frequently
        //we should keep it simple so that it can be inlined.
        protected SyntaxToken EatToken()
        {
            var ct = this.CurrentToken;
            MoveToNextToken();
            return ct;
        }

        private void MoveToNextToken()
        {
            this.prevTokenTrailingTrivia = this.currentToken.GetTrailingTrivia();

            this.currentToken = default(SyntaxToken);

            if (this.blendedTokens != null)
            {
                this.currentNode = default(BlendedNode);
            }

            this.tokenOffset++;
        }

        //this method is called very frequently
        //we should keep it simple so that it can be inlined.
        protected SyntaxToken EatToken(SyntaxKind kind)
        {
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));

            var ct = this.CurrentToken;
            if (ct.Kind == kind)
            {
                MoveToNextToken();
                return ct;
            }

            //slow part of EatToken(SyntaxKind kind)
            return CreateMissingToken(kind, this.CurrentToken.Kind, reportError: true);
        }

        private SyntaxToken CreateMissingToken(SyntaxKind expected, SyntaxKind actual, bool reportError)
        {
            // should we eat the current ParseToken's leading trivia?
            var token = SyntaxFactory.MissingToken(expected);
            if (reportError)
            {
                token = WithAdditionalDiagnostics(token, this.GetExpectedTokenError(expected, actual));
            }

            return token;
        }

        private SyntaxToken CreateMissingToken(SyntaxKind expected, ErrorCode code, bool reportError)
        {
            // should we eat the current ParseToken's leading trivia?
            var token = SyntaxFactory.MissingToken(expected);
            if (reportError)
            {
                token = AddError(token, code);
            }

            return token;
        }

        protected SyntaxToken EatToken(SyntaxKind kind, bool reportError)
        {
            if (reportError)
            {
                return EatToken(kind);
            }

            Debug.Assert(SyntaxFacts.IsAnyToken(kind));
            if (this.CurrentToken.Kind != kind)
            {
                // should we eat the current ParseToken's leading trivia?
                return SyntaxFactory.MissingToken(kind);
            }
            else
            {
                return this.EatToken();
            }
        }

        protected SyntaxToken EatToken(SyntaxKind kind, ErrorCode code, bool reportError = true)
        {
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));
            if (this.CurrentToken.Kind != kind)
            {
                return CreateMissingToken(kind, code, reportError);
            }
            else
            {
                return this.EatToken();
            }
        }

        protected SyntaxToken EatTokenWithPrejudice(SyntaxKind kind)
        {
            var token = this.CurrentToken;
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));
            if (token.Kind != kind)
            {
                token = WithAdditionalDiagnostics(token, this.GetExpectedTokenError(kind, token.Kind));
            }

            this.MoveToNextToken();
            return token;
        }

        protected SyntaxToken EatTokenWithPrejudice(ErrorCode errorCode, params object[] args)
        {
            var token = this.EatToken();
            token = WithAdditionalDiagnostics(token, MakeError(token.GetLeadingTriviaWidth(), token.Width, errorCode, args));
            return token;
        }

        protected SyntaxToken EatContextualToken(SyntaxKind kind, ErrorCode code, bool reportError = true)
        {
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));

            if (this.CurrentToken.ContextualKind != kind)
            {
                return CreateMissingToken(kind, code, reportError);
            }
            else
            {
                return ConvertToKeyword(this.EatToken());
            }
        }

        protected SyntaxToken EatContextualToken(SyntaxKind kind, bool reportError = true)
        {
            Debug.Assert(SyntaxFacts.IsAnyToken(kind));

            var contextualKind = this.CurrentToken.ContextualKind;
            if (contextualKind != kind)
            {
                return CreateMissingToken(kind, contextualKind, reportError);
            }
            else
            {
                return ConvertToKeyword(this.EatToken());
            }
        }

        protected virtual SyntaxDiagnosticInfo GetExpectedTokenError(SyntaxKind expected, SyntaxKind actual, int offset, int width)
        {
            var code = GetExpectedTokenErrorCode(expected, actual);
            if (code == ErrorCode.ERR_SyntaxError || code == ErrorCode.ERR_IdentifierExpectedKW)
            {
                return new SyntaxDiagnosticInfo(offset, width, code, SyntaxFacts.GetText(expected), SyntaxFacts.GetText(actual));
            }
            else
            {
                return new SyntaxDiagnosticInfo(offset, width, code);
            }
        }

        protected virtual SyntaxDiagnosticInfo GetExpectedTokenError(SyntaxKind expected, SyntaxKind actual)
        {
            int offset, width;
            this.GetDiagnosticSpanForMissingToken(out offset, out width);

            return this.GetExpectedTokenError(expected, actual, offset, width);
        }

        private static ErrorCode GetExpectedTokenErrorCode(SyntaxKind expected, SyntaxKind actual)
        {
            switch (expected)
            {
                case SyntaxKind.IdentifierToken:
                    if (SyntaxFacts.IsReservedKeyword(actual))
                    {
                        return ErrorCode.ERR_IdentifierExpectedKW;   // A keyword -- use special message.
                    }
                    else
                    {
                        return ErrorCode.ERR_IdentifierExpected;
                    }

                case SyntaxKind.SemicolonToken:
                    return ErrorCode.ERR_SemicolonExpected;

                // case TokenKind::Colon:         iError = ERR_ColonExpected;          break;
                // case TokenKind::OpenParen:     iError = ERR_LparenExpected;         break;
                case SyntaxKind.CloseParenToken:
                    return ErrorCode.ERR_CloseParenExpected;
                case SyntaxKind.OpenBraceToken:
                    return ErrorCode.ERR_LbraceExpected;
                case SyntaxKind.CloseBraceToken:
                    return ErrorCode.ERR_RbraceExpected;

                // case TokenKind::CloseSquare:   iError = ERR_CloseSquareExpected;    break;
                default:
                    return ErrorCode.ERR_SyntaxError;
            }
        }

        protected void GetDiagnosticSpanForMissingToken(out int offset, out int width)
        {
            // If the previous token has a trailing EndOfLineTrivia,
            // the missing token diagnostic position is moved to the
            // end of line containing the previous token and
            // its width is set to zero.
            // Otherwise the diagnostic offset and width is set
            // to the corresponding values of the current token

            var trivia = this.prevTokenTrailingTrivia;
            if (trivia != null)
            {
                SyntaxList<CSharpSyntaxNode> triviaList = new SyntaxList<CSharpSyntaxNode>(trivia);
                bool prevTokenHasEndOfLineTrivia = triviaList.Any(SyntaxKind.EndOfLineTrivia);
                if (prevTokenHasEndOfLineTrivia)
                {
                    offset = -trivia.FullWidth;
                    width = 0;
                    return;
                }
            }

            SyntaxToken ct = this.CurrentToken;
            offset = ct.GetLeadingTriviaWidth();
            width = ct.Width;
        }

        protected virtual TNode WithAdditionalDiagnostics<TNode>(TNode node, params DiagnosticInfo[] diagnostics) where TNode : CSharpSyntaxNode
        {
            DiagnosticInfo[] existingDiags = node.GetDiagnostics();

            //EDMAURER avoid the creation of a bunch of temporary objects that aggravate the memory situation
            //when the parser gets profoundly confused and produces many missing token diagnostics.

            int existingLength = existingDiags.Length;
            if (existingLength == 0)
                return node.WithDiagnosticsGreen(diagnostics);
            else
            {
                DiagnosticInfo[] result = new DiagnosticInfo[existingDiags.Length + diagnostics.Length];
                existingDiags.CopyTo(result, 0);
                diagnostics.CopyTo(result, existingLength);
                return node.WithDiagnosticsGreen(result);
            }
        }

        protected TNode AddError<TNode>(TNode node, ErrorCode code) where TNode : CSharpSyntaxNode
        {
            return AddError(node, code, SpecializedCollections.EmptyObjects);
        }

        protected TNode AddError<TNode>(TNode node, ErrorCode code, params object[] args) where TNode : CSharpSyntaxNode
        {
            if (!node.IsMissing)
            {
                return WithAdditionalDiagnostics(node, MakeError(node, code, args));
            }

            int offset, width;

            SyntaxToken token = node as SyntaxToken;
            if (token != null && token.ContainsSkippedText)
            {
                // This code exists to clean up an anti-pattern:
                //   1) an undesirable token is parsed,
                //   2) a desirable missing token is created and the parsed token is appended as skipped text,
                //   3) an error is attached to the missing token describing the problem.
                // If this occurs, then this.previousTokenTrailingTrivia is still populated with the trivia 
                // of the undesirable token (now skipped text).  Since the trivia no longer precedes the
                // node to which the error is to be attached, the computed offset will be incorrect.

                offset = token.GetLeadingTriviaWidth(); // Should always be zero, but at least we'll do something sensible if it's not.
                Debug.Assert(offset == 0, "Why are we producing a missing token that has both skipped text and leading trivia?");

                width = 0;
                bool seenSkipped = false;
                foreach (var trivia in token.TrailingTrivia)
                {
                    if (trivia.Kind == SyntaxKind.SkippedTokensTrivia)
                    {
                        seenSkipped = true;
                        width += trivia.Width;
                    }
                    else if (seenSkipped)
                    {
                        break;
                    }
                    else
                    {
                        offset += trivia.Width;
                    }
                }
            }
            else
            {
                this.GetDiagnosticSpanForMissingToken(out offset, out width);
            }

            return WithAdditionalDiagnostics(node, MakeError(offset, width, code, args));
        }

        protected TNode AddError<TNode>(TNode node, int offset, int length, ErrorCode code, params object[] args) where TNode : CSharpSyntaxNode
        {
            return WithAdditionalDiagnostics(node, MakeError(offset, length, code, args));
        }

        protected TNode AddError<TNode>(TNode node, CSharpSyntaxNode location, ErrorCode code, params object[] args) where TNode : CSharpSyntaxNode
        {
            // assumes non-terminals will at most appear once in sub-tree
            int offset;
            FindOffset(node, location, out offset);
            return WithAdditionalDiagnostics(node, MakeError(offset, location.Width, code, args));
        }

        protected TNode AddErrorToFirstToken<TNode>(TNode node, ErrorCode code) where TNode : CSharpSyntaxNode
        {
            var firstToken = node.GetFirstToken();
            return WithAdditionalDiagnostics(node, MakeError(firstToken.GetLeadingTriviaWidth(), firstToken.Width, code));
        }

        protected TNode AddErrorToFirstToken<TNode>(TNode node, ErrorCode code, params object[] args) where TNode : CSharpSyntaxNode
        {
            var firstToken = node.GetFirstToken();
            return WithAdditionalDiagnostics(node, MakeError(firstToken.GetLeadingTriviaWidth(), firstToken.Width, code, args));
        }

        protected TNode AddErrorToLastToken<TNode>(TNode node, ErrorCode code) where TNode : CSharpSyntaxNode
        {
            int offset;
            int width;
            GetOffsetAndWidthForLastToken(node, out offset, out width);
            return WithAdditionalDiagnostics(node, MakeError(offset, width, code));
        }

        protected TNode AddErrorToLastToken<TNode>(TNode node, ErrorCode code, params object[] args) where TNode : CSharpSyntaxNode
        {
            int offset;
            int width;
            GetOffsetAndWidthForLastToken(node, out offset, out width);
            return WithAdditionalDiagnostics(node, MakeError(offset, width, code, args));
        }

        private static void GetOffsetAndWidthForLastToken<TNode>(TNode node, out int offset, out int width) where TNode : CSharpSyntaxNode
        {
            var lastToken = node.GetLastNonmissingToken();
            offset = node.FullWidth; //advance to end of entire node
            width = 0;
            if (lastToken != null) //will be null if all tokens are missing
            {
                offset -= lastToken.FullWidth; //rewind past last token
                offset += lastToken.GetLeadingTriviaWidth(); //advance past last token leading trivia - now at start of last token
                width += lastToken.Width;
            }
        }

        protected static SyntaxDiagnosticInfo MakeError(int offset, int width, ErrorCode code)
        {
            return new SyntaxDiagnosticInfo(offset, width, code);
        }

        protected static SyntaxDiagnosticInfo MakeError(int offset, int width, ErrorCode code, params object[] args)
        {
            return new SyntaxDiagnosticInfo(offset, width, code, args);
        }

        protected static SyntaxDiagnosticInfo MakeError(CSharpSyntaxNode node, ErrorCode code, params object[] args)
        {
            return new SyntaxDiagnosticInfo(node.GetLeadingTriviaWidth(), node.Width, code, args);
        }

        protected static SyntaxDiagnosticInfo MakeError(ErrorCode code, params object[] args)
        {
            return new SyntaxDiagnosticInfo(code, args);
        }

        protected TNode AddLeadingSkippedSyntax<TNode>(TNode node, CSharpSyntaxNode skippedSyntax) where TNode : CSharpSyntaxNode
        {
            var oldToken = node as SyntaxToken ?? node.GetFirstToken();
            var newToken = AddSkippedSyntax(oldToken, skippedSyntax, trailing: false);
            return SyntaxFirstTokenReplacer.Replace(node, oldToken, newToken, skippedSyntax.FullWidth);
        }

        protected TNode AddTrailingSkippedSyntax<TNode>(TNode node, CSharpSyntaxNode skippedSyntax) where TNode : CSharpSyntaxNode
        {
            var token = node as SyntaxToken;
            if (token != null)
            {
                return (TNode)(object)AddSkippedSyntax(token, skippedSyntax, trailing: true);
            }
            else
            {
                var lastToken = node.GetLastToken();
                var newToken = AddSkippedSyntax(lastToken, skippedSyntax, trailing: true);
                return SyntaxLastTokenReplacer.Replace(node, newToken);
            }
        }

        /// <summary>
        /// Converts skippedSyntax node into tokens and adds these as trivia on the target token.
        /// Also adds the first error (in depth-first preorder) found in the skipped syntax tree to the target token.
        /// </summary>
        internal SyntaxToken AddSkippedSyntax(SyntaxToken target, CSharpSyntaxNode skippedSyntax, bool trailing)
        {
            var builder = new SyntaxListBuilder(4);

            // the error in we'll attach to the node
            SyntaxDiagnosticInfo diagnostic = null;

            // the position of the error within the skipedSyntax node full tree
            int diagnosticOffset = 0;

            int currentOffset = 0;
            foreach (var node in skippedSyntax.EnumerateNodes())
            {
                SyntaxToken token = node as SyntaxToken;
                if (token != null)
                {
                    builder.Add(token.GetLeadingTrivia());

                    if (token.Width > 0)
                    {
                        // separate trivia from the tokens
                        SyntaxToken tk = token.WithLeadingTrivia(null).WithTrailingTrivia(null);

                        // adjust relative offsets of diagnostics attached to the token:
                        int leadingWidth = token.GetLeadingTriviaWidth();
                        if (leadingWidth > 0)
                        {
                            var tokenDiagnostics = tk.GetDiagnostics();
                            for (int i = 0; i < tokenDiagnostics.Length; i++)
                            {
                                var d = (SyntaxDiagnosticInfo)tokenDiagnostics[i];
                                tokenDiagnostics[i] = new SyntaxDiagnosticInfo(d.Offset - leadingWidth, d.Width, (ErrorCode)d.Code, d.Arguments);
                            }
                        }

                        builder.Add(SyntaxFactory.SkippedTokensTrivia(tk));
                    }
                    else
                    {
                        // do not create zero-width structured trivia, GetStructure doesn't work well for them
                        var existing = (SyntaxDiagnosticInfo)token.GetDiagnostics().FirstOrDefault();
                        if (existing != null)
                        {
                            diagnostic = existing;
                            diagnosticOffset = currentOffset;
                        }
                    }
                    builder.Add(token.GetTrailingTrivia());

                    currentOffset += token.FullWidth;
                }
                else if (node.ContainsDiagnostics && diagnostic == null)
                {
                    // only propagate the first error to reduce noise:
                    var existing = (SyntaxDiagnosticInfo)node.GetDiagnostics().FirstOrDefault();
                    if (existing != null)
                    {
                        diagnostic = existing;
                        diagnosticOffset = currentOffset;
                    }
                }
            }

            int triviaWidth = currentOffset;
            var trivia = builder.ToListNode();

            // total width of everything preceding the added trivia
            int triviaOffset;
            if (trailing)
            {
                var trailingTrivia = target.GetTrailingTrivia();
                triviaOffset = target.FullWidth; //added trivia is full width (before addition)
                target = target.WithTrailingTrivia(SyntaxList.Concat(trailingTrivia, trivia));
            }
            else
            {
                // Since we're adding triviaWidth before the token, we have to add that much to
                // the offset of each of its diagnostics.
                if (triviaWidth > 0)
                {
                    var targetDiagnostics = target.GetDiagnostics();
                    for (int i = 0; i < targetDiagnostics.Length; i++)
                    {
                        var d = (SyntaxDiagnosticInfo)targetDiagnostics[i];
                        targetDiagnostics[i] = new SyntaxDiagnosticInfo(d.Offset + triviaWidth, d.Width, (ErrorCode)d.Code, d.Arguments);
                    }
                }

                var leadingTrivia = target.GetLeadingTrivia();
                target = target.WithLeadingTrivia(SyntaxList.Concat(trivia, leadingTrivia));
                triviaOffset = 0; //added trivia is first, so offset is zero
            }

            if (diagnostic != null)
            {
                int newOffset = triviaOffset + diagnosticOffset + diagnostic.Offset;

                target = WithAdditionalDiagnostics(target,
                    new SyntaxDiagnosticInfo(newOffset, diagnostic.Width, (ErrorCode)diagnostic.Code, diagnostic.Arguments)
                );
            }

            return target;
        }

        /// <summary>
        /// This function searches for the given location node within the subtree rooted at root node. 
        /// If it finds it, the function computes the offset span of that child node within the root and returns true, 
        /// otherwise it returns false.
        /// </summary>
        /// <param name="root">Root node</param>
        /// <param name="location">Node to search in the subtree rooted at root node</param>
        /// <param name="offset">Offset of the location node within the subtree rooted at child</param>
        /// <returns></returns>
        private bool FindOffset(GreenNode root, CSharpSyntaxNode location, out int offset)
        {
            int currentOffset = 0;
            offset = 0;
            if (root != null)
            {
                for (int i = 0, n = root.SlotCount; i < n; i++)
                {
                    var child = root.GetSlot(i);
                    if (child == null)
                    {
                        // ignore null slots
                        continue;
                    }

                    // check if the child node is the location node
                    if (child == location)
                    {
                        // Found the location node in the subtree
                        // Initialize offset with the offset of the location node within its parent
                        // and walk up the stack of recursive calls adding the offset of each node
                        // within its parent
                        offset = currentOffset;
                        return true;
                    }

                    // search for the location node in the subtree rooted at child node
                    if (this.FindOffset(child, location, out offset))
                    {
                        // Found the location node in child's subtree
                        // Add the offset of child node within its parent to offset
                        // and continue walking up the stack
                        offset += child.GetLeadingTriviaWidth() + currentOffset;
                        return true;
                    }

                    // We didn't find the location node in the subtree rooted at child
                    // Move on to the next child
                    currentOffset += child.FullWidth;
                }
            }

            // We didn't find the location node within the subtree rooted at root node
            return false;
        }

        protected static SyntaxToken ConvertToKeyword(SyntaxToken token)
        {
            if (token.Kind != token.ContextualKind)
            {
                var kw = token.IsMissing
                        ? SyntaxFactory.MissingToken(token.LeadingTrivia.Node, token.ContextualKind, token.TrailingTrivia.Node)
                        : SyntaxFactory.Token(token.LeadingTrivia.Node, token.ContextualKind, token.TrailingTrivia.Node);
                var d = token.GetDiagnostics();
                if (d != null && d.Length > 0)
                {
                    kw = kw.WithDiagnosticsGreen(d);
                }

                return kw;
            }

            return token;
        }

        internal DirectiveStack Directives
        {
            get { return lexer.Directives; }
        }

        /// <remarks>
        /// NOTE: we are specifically diverging from dev11 to improve the user experience.
        /// Since treating the "async" keyword as an identifier in older language
        /// versions can never result in a correct program, we instead accept it as a
        /// keyword regardless of the language version and produce an error if the version
        /// is insufficient.
        /// </remarks>
        protected TNode CheckFeatureAvailability<TNode>(TNode node, MessageID feature, bool forceWarning = false)
            where TNode : CSharpSyntaxNode
        {
            LanguageVersion availableVersion = this.Options.LanguageVersion;

            if (feature == MessageID.IDS_FeatureModuleAttrLoc)
            {
                // There's a special error code for this feature, so handle it separately.
                return availableVersion >= LanguageVersion.CSharp2
                    ? node
                    : this.AddError(node, ErrorCode.WRN_NonECMAFeature, feature.Localize());
            }

            if (IsFeatureEnabled(feature))
            {
                return node;
            }

            var featureName = feature.Localize();
            var requiredVersion = feature.RequiredVersion();

            if (forceWarning)
            {
                SyntaxDiagnosticInfo rawInfo = new SyntaxDiagnosticInfo(availableVersion.GetErrorCode(), featureName, requiredVersion.Localize());
                return this.AddError(node, ErrorCode.WRN_ErrorOverride, rawInfo, rawInfo.Code);
            }
            
            if (requiredVersion == LanguageVersion.Experimental)
            {
                return this.AddError(node, ErrorCode.ERR_FeatureIsExperimental, featureName);
            }

            return this.AddError(node, availableVersion.GetErrorCode(), featureName, requiredVersion.Localize());
        }

        protected bool IsFeatureEnabled(MessageID feature)
        {
            LanguageVersion availableVersion = this.Options.LanguageVersion;
            LanguageVersion requiredVersion = feature.RequiredVersion();
            return availableVersion >= requiredVersion;
        }
    }
}
