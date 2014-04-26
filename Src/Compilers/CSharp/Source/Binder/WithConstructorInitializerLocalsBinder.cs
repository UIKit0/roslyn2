// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.CSharp
{
    internal sealed class WithConstructorInitializerLocalsBinder : LocalScopeBinder
    {
        private readonly ConstructorDeclarationSyntax declaration;

        public WithConstructorInitializerLocalsBinder(MethodSymbol owner, Binder enclosing, ConstructorDeclarationSyntax declaration)
            : base(owner, enclosing, enclosing.Flags)
        {
            Debug.Assert(declaration.Initializer != null);
            this.declaration = declaration;
        }

        protected override ImmutableArray<LocalSymbol> BuildLocals()
        {
            var walker = new BuildLocalsFromDeclarationsWalker(this);

            walker.Visit(declaration.Initializer);

            if (walker.Locals != null)
            {
                return walker.Locals.ToImmutableAndFree();
            }

            return ImmutableArray<LocalSymbol>.Empty;
        }

        internal override ImmutableArray<LocalSymbol> GetDeclaredLocalsForScope(CSharpSyntaxNode node)
        {
            if (node == declaration)
            {
                return this.Locals;
            }

            return base.GetDeclaredLocalsForScope(node);
        }
    }
}
