﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports Microsoft.CodeAnalysis.VisualBasic.Extensions

Namespace Microsoft.CodeAnalysis.VisualBasic.Extensions
    Friend Module ParenthesizedExpressionSyntaxExtensions

        Private Function EndsQuery(token As SyntaxToken, semanticModel As SemanticModel, cancellationToken As CancellationToken) As Boolean
            Dim query = token.Parent.FirstAncestorOrSelf(Of QueryExpressionSyntax)()
            If query IsNot Nothing Then
                Return query.GetLastToken() = token
            Else
                Dim invocationAtLast = token.Parent.FirstAncestorOrSelf(Of InvocationExpressionSyntax)()
                Return invocationAtLast IsNot Nothing AndAlso
                   invocationAtLast.GetLastToken() = token AndAlso
                   invocationAtLast.CanRemoveEmptyArgumentList(semanticModel, cancellationToken) AndAlso
                   EndsQuery(invocationAtLast.Expression.GetLastToken(), semanticModel, cancellationToken)
            End If

            Return False
        End Function

        Private Function EndsVariableDeclarator(token As SyntaxToken) As Boolean
            Dim variableDeclarator = token.Parent.FirstAncestorOrSelf(Of VariableDeclaratorSyntax)()
            Return variableDeclarator IsNot Nothing AndAlso
                   variableDeclarator.GetLastToken() = token
        End Function

        Private Function EndsLambda(token As SyntaxToken) As Boolean
            Dim lambda = token.Parent.FirstAncestorOrSelf(Of SingleLineLambdaExpressionSyntax)()
            Return lambda IsNot Nothing AndAlso
                   lambda.GetLastToken() = token
        End Function

        <Extension>
        Public Function CanRemoveParentheses(
            node As ParenthesizedExpressionSyntax,
            semanticModel As SemanticModel,
            Optional cancellationToken As CancellationToken = Nothing
        ) As Boolean

            Dim expression = node.Expression

            ' Cases:
            '   ((Foo))
            If expression.IsKind(SyntaxKind.ParenthesizedExpression) Then
                Return True
            End If

            ' Cases:
            '   ("x"c)
            '   (#1/1/2001#)
            '   (False)
            '   (Nothing)
            '   (1)
            '   ("")
            '   (True)
            If expression.IsKind(SyntaxKind.CharacterLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.DateLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.FalseLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.NothingLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.NumericLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.StringLiteralExpression) OrElse
               expression.IsKind(SyntaxKind.TrueLiteralExpression) Then

                Return True
            End If

            ' Cases:
            '   (Me)
            '   (MyBase)
            '   (MyClass)
            If expression.IsKind(SyntaxKind.MeExpression) OrElse
               expression.IsKind(SyntaxKind.MyBaseExpression) OrElse
               expression.IsKind(SyntaxKind.MyClassExpression) Then

                Return True
            End If

            ' Cases:
            '   (DirectCast(Foo))
            '   (TryCast(Foo))
            '   (CType(Foo, Bar))
            '   (CInt(Foo))
            If expression.IsKind(SyntaxKind.DirectCastExpression) OrElse
               expression.IsKind(SyntaxKind.TryCastExpression) OrElse
               expression.IsKind(SyntaxKind.CTypeExpression) OrElse
               TypeOf expression Is PredefinedCastExpressionSyntax Then

                Return True
            End If

            ' Cases:
            '   (AddressOf Foo)
            '   (New With {.Foo = ""})
            '   (If(True, 1, 2))
            '   (If(Nothing, 1))
            If expression.IsKind(SyntaxKind.AddressOfExpression) OrElse
               expression.IsKind(SyntaxKind.AnonymousObjectCreationExpression) OrElse
               expression.IsKind(SyntaxKind.TernaryConditionalExpression) OrElse
               expression.IsKind(SyntaxKind.BinaryConditionalExpression) Then

                Return True
            End If

            ' Cases:
            '   List(Of Integer()) From {({1})} -to- {({1})}
            '   List(Of Integer()) From {{({1})}} -to- {{{1}}}
            '   {({1})} -to- {({1})}
            '   ({1}) -to- {1}
            If expression.IsKind(SyntaxKind.CollectionInitializer) Then
                If Not node.IsParentKind(SyntaxKind.CollectionInitializer) Then
                    Return True
                End If

                Dim expressionTypeInfo = semanticModel.GetTypeInfo(expression, cancellationToken)
                If expressionTypeInfo.Type Is Nothing Then
                    Return True
                End If

                Dim parentTypeInfo = semanticModel.GetTypeInfo(DirectCast(node.Parent, CollectionInitializerSyntax), cancellationToken)
                If parentTypeInfo.Type Is Nothing AndAlso
                   Not node.Parent.IsParentKind(SyntaxKind.ObjectCollectionInitializer) Then

                    Return True
                End If

                Dim conversion = semanticModel.GetConversion(expression, cancellationToken)
                If conversion.IsIdentity Then
                    Return False
                End If

                Return True
            End If

            Dim firstToken = expression.GetFirstToken()
            Dim previousToken = node.OpenParenToken.GetPreviousToken()

            ' Case:
            '   0 > <x/>.Value
            '   0 < <x/>.Value
            If firstToken.IsKind(SyntaxKind.LessThanToken) AndAlso
               previousToken.IsKind(SyntaxKind.LessThanToken, SyntaxKind.GreaterThanToken) Then

                Return False
            End If

            ' Cases:
            '   (<x/>.@a)
            '   (<x/>...<b>)
            '   (<x/>.<a>)
            If expression.IsKind(SyntaxKind.XmlAttributeAccessExpression) OrElse
               expression.IsKind(SyntaxKind.XmlDescendantAccessExpression) OrElse
               expression.IsKind(SyntaxKind.XmlElementAccessExpression) Then
                Return True
            End If

            Dim lastToken = expression.GetLastToken()
            Dim nextToken = node.CloseParenToken.GetNextToken()

            ' Cases:
            '   Dim x = (Foo)
            If node.IsParentKind(SyntaxKind.EqualsValue) AndAlso
               Not EndsQuery(lastToken, semanticModel, cancellationToken) AndAlso
               Not EndsLambda(lastToken) AndAlso
               Not nextToken.IsKindOrHasMatchingText(SyntaxKind.CommaToken) Then
                Return True
            End If

            ' Cases:
            '   (New Foo)
            '   (New Foo())
            If expression.IsKind(SyntaxKind.ObjectCreationExpression) Then
                Dim objectCreation = DirectCast(expression, ObjectCreationExpressionSyntax)

                If nextToken.IsKindOrHasMatchingText(SyntaxKind.DotToken) Then
                    If objectCreation.ArgumentList Is Nothing Then
                        ' Note we can remove the parentheses when the next token is dot only
                        ' if the type of the ObjectCreationExpression is a predefined type.
                        ' So, we can remove parentheses in this case...
                        '
                        '     Call (New Integer).ToString
                        '
                        ' But not this one...
                        '
                        '     Call (New Int32).ToString

                        Return TypeOf objectCreation.Type Is PredefinedTypeSyntax
                    End If
                End If

                If nextToken.IsKindOrHasMatchingText(SyntaxKind.OpenParenToken) Then
                    Return False
                End If

                Return True
            End If

            ' Cases:
            ' 1.   (Foo)
            ' 2.   (Foo())
            ' 3.   <x/>.GetHashCode()
            ' 4.   1 < (<x/>.GetHashCode()) Or 1 > (<x/>.GetHashCode())
            If expression.IsKind(SyntaxKind.InvocationExpression) Then
                Dim invocationExpression = DirectCast(expression, InvocationExpressionSyntax)

                If invocationExpression.Expression.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                    Dim memberAccess = DirectCast(invocationExpression.Expression, MemberAccessExpressionSyntax)
                    If (TypeOf memberAccess.Expression Is XmlNodeSyntax AndAlso
                        (previousToken.IsKindOrHasMatchingText(SyntaxKind.LessThanToken) OrElse
                        previousToken.IsKindOrHasMatchingText(SyntaxKind.GreaterThanToken))) Then

                        Return False
                    End If
                End If

                If invocationExpression.ArgumentList Is Nothing Then
                    Return Not nextToken.IsKindOrHasMatchingText(SyntaxKind.OpenParenToken)
                End If

                Return True
            End If

            ' Cases:
            '   (Foo.Bar)
            '   (Foo)
            If expression.IsKind(SyntaxKind.IdentifierName) OrElse
               expression.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then

                ' If this is a local, field or property is passed to a ByRef parameter, we should
                ' keep the parentheses to ensure that we don't change copy-back semantics.
                If TypeOf node.Parent Is ArgumentSyntax Then
                    Dim symbol = semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol
                    If symbol IsNot Nothing Then
                        If symbol.MatchesKind(SymbolKind.Local, SymbolKind.Field, SymbolKind.Property) Then
                            Dim argument = DirectCast(node.Parent, ArgumentSyntax)
                            Dim parameter = argument.DetermineParameter(semanticModel, cancellationToken:=cancellationToken)

                            If parameter IsNot Nothing AndAlso
                               parameter.RefKind <> RefKind.None Then

                                Return False
                            End If
                        End If
                    End If
                End If

                ' If the next token is an open paren, we need to be careful to ensure
                ' that it is the opening of the argument list of a parenting invocation
                ' for which this is the expression.
                If nextToken.IsKindOrHasMatchingText(SyntaxKind.OpenParenToken) Then
                    If node.IsParentKind(SyntaxKind.InvocationExpression) Then
                        Dim parentInvocation = DirectCast(node.Parent, InvocationExpressionSyntax)
                        If parentInvocation.Expression Is node AndAlso
                           parentInvocation.ArgumentList IsNot Nothing AndAlso
                           parentInvocation.ArgumentList.OpenParenToken = nextToken Then

                            Return True
                        End If
                    End If

                    Return False
                End If

                Return True
            End If

            ' Case:
            '   (Foo(Of Bar))
            If expression.IsKind(SyntaxKind.GenericName) Then
                If Not nextToken.IsKindOrHasMatchingText(SyntaxKind.OpenParenToken) Then
                    Return True
                End If
            End If

            Dim isNodeCloseParenLastTokenOfStatement = node.CloseParenToken.IsLastTokenOfStatement(checkColonTrivia:=True)
            Dim nextNextToken = nextToken.GetNextToken()

            ' Dim z = Function() (From x In "") ' OK
            ' Select 1
            ' End Select
            ' Select is the only keyword in LINQ which has dual usage 1. Case selection 2. Query Select Clause
            If isNodeCloseParenLastTokenOfStatement AndAlso
                EndsQuery(lastToken, semanticModel, cancellationToken) AndAlso
                nextToken.VBKind = SyntaxKind.SelectKeyword AndAlso
                nextNextToken.VBKind <> SyntaxKind.CaseKeyword Then
                Return False
            End If

            ' (Await Task.Run(Function() i)) <EOL>
            ' (Await Task.Run(Function() i)),
            If node.Expression.IsKind(SyntaxKind.AwaitExpression) AndAlso
                (isNodeCloseParenLastTokenOfStatement OrElse
                nextToken.VBKind = SyntaxKind.CommaToken) Then
                Return True
            End If

            ' Cases:
            '   (1 + 1) * 8
            '   (1 + 1).ToString
            If TypeOf expression Is BinaryExpressionSyntax OrElse
               TypeOf expression Is UnaryExpressionSyntax Then

                Dim parentExpression = TryCast(node.Parent, ExpressionSyntax)
                If parentExpression IsNot Nothing Then
                    If parentExpression.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                        Return False
                    End If

                    Dim precedence = expression.GetOperatorPrecedence()
                    Dim parentPrecedence = parentExpression.GetOperatorPrecedence()

                    ' Only remove if the expression's precedence is higher than its parent.
                    If parentPrecedence <> OperatorPrecedence.PrecedenceNone AndAlso
                       precedence < parentPrecedence Then

                        Return False
                    End If

                    ' If the expression's precedence is the same as its parent, and both are binary expressions,
                    ' check for associativity and commutability.
                    If precedence <> OperatorPrecedence.PrecedenceNone AndAlso precedence = parentPrecedence Then
                        Dim binaryExpression = TryCast(expression, BinaryExpressionSyntax)
                        Dim parentBinaryExpression = TryCast(parentExpression, BinaryExpressionSyntax)

                        If binaryExpression IsNot Nothing AndAlso parentBinaryExpression IsNot Nothing Then
                            ' All binary expressions are left associative, so if the expression
                            ' is on the left side of a binary expression the parentheses can be removed.
                            If parentBinaryExpression.Left Is node Then
                                Return True
                            End If

                            ' If both the expression and it's parent are binary expressions and their kinds
                            ' are the same, check to see if they are commutative (e.g. + or *).
                            If parentBinaryExpression.IsKind(SyntaxKind.AddExpression, SyntaxKind.MultiplyExpression) AndAlso
                               expression.VBKind = parentExpression.VBKind Then

                                Return True
                            End If
                        End If

                        Return False
                    End If
                End If

                Return True
            End If

            ' Cases:
            '   (Sub() From x in y), Foo
            '   Dim a = (Sub() If True Then Dim x), b = a
            '   Dim y = (Function() Console.ReadLine)()
            '   Call (Sub() Exit Sub)
            '   Dim x = <x <%= (Sub() If True Then Else) %>/>
            If TypeOf expression Is SingleLineLambdaExpressionSyntax Then
                If node.CloseParenToken.IsLastTokenOfStatementWithEndOfLine() AndAlso
                    lastToken.VBKind = SyntaxKind.ThenKeyword Then
                    Return False
                End If

                If nextToken.IsKindOrHasMatchingText(SyntaxKind.CommaToken) Then
                    Dim lastStatement = lastToken.Parent.GetFirstEnclosingStatement()
                    If EndsQuery(lastToken, semanticModel, cancellationToken) OrElse EndsVariableDeclarator(lastToken) OrElse
                            (EndsLambda(lastToken) AndAlso
                            Not previousToken.IsKindOrHasMatchingText(SyntaxKind.OpenParenToken) AndAlso
                            lastStatement IsNot Nothing AndAlso lastStatement.VBKind = SyntaxKind.ReDimStatement) Then
                        Return False
                    End If

                    Return True
                End If

                ' case:
                ' (Sub() If True Then Dim y = Sub(z As Integer)
                '                             End Sub).Invoke()
                If nextToken.IsKindOrHasMatchingText(SyntaxKind.DotToken) AndAlso
                       nextToken.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) Then
                    Return False
                End If

                ' case:
                ' 1. Call (Sub() If True Then Dim y = Sub(z As Integer)
                '                             End Sub)
                ' 2. (Sub() If True Then Dim y = Sub()
                '                                End Sub) Is Nothing
                ' 3. TypeOf (Sub() If True Then Dim y = Sub()
                '                                End Sub) Is Object
                ' 4. TypeOf (Sub() If True Then Dim y = Sub()
                '                                End Sub) IsNot Object
                If (node.Parent.VBKind = SyntaxKind.InvocationExpression OrElse
                        node.Parent.VBKind = SyntaxKind.IsExpression OrElse
                        node.Parent.VBKind = SyntaxKind.TypeOfIsExpression OrElse
                        node.Parent.VBKind = SyntaxKind.TypeOfIsNotExpression) Then
                    Return False
                End If

                ' case:
                ' 1. (Sub() If True Then) Implements I.A
                ' 2. If True Then : Dim x As Action = (Sub() If True Then) : Else : Return : End If
                If nextToken.IsKindOrHasMatchingText(SyntaxKind.CloseParenToken) OrElse
                       nextToken.IsKindOrHasMatchingText(SyntaxKind.CloseBraceToken) OrElse
                       lastToken.IsLastTokenOfStatement(checkColonTrivia:=True) OrElse
                       node.Parent.VBKind = SyntaxKind.XmlEmbeddedExpression Then
                    Return True
                End If

                ' case:
                ' 1 .
                ' Dim a = Sub() If False Then Console.WriteLine() Else Dim q = From x In ""
                ' [Take]()
                If isNodeCloseParenLastTokenOfStatement AndAlso
                    EndsQuery(lastToken, semanticModel, cancellationToken) AndAlso
                  nextToken.IsKeyword Then
                    Return True
                End If

                If isNodeCloseParenLastTokenOfStatement AndAlso
                    Not EndsQuery(lastToken, semanticModel, cancellationToken) Then
                    Return True
                End If

                Return False
            End If

            If TypeOf expression Is MultiLineLambdaExpressionSyntax Then
                Return True
            End If

            ' Cases:
            '   {(From x in y), From x in y}
            '
            '   Dim q = (From x in "")
            '   Select 1
            '   End Select
            '
            '   With New StringBuilder
            '       Dim q = (From x in "")
            '       .Length = 0
            '   End With
            '
            ' Dim y = (From c In "" Distinct)
            '    !A = !B
            If EndsQuery(lastToken, semanticModel, cancellationToken) Then
                If nextToken.IsKindOrHasMatchingText(SyntaxKind.CloseParenToken) OrElse
                   nextToken.IsKindOrHasMatchingText(SyntaxKind.CloseBraceToken) OrElse
                   node.CloseParenToken.IsLastTokenOfStatement() Then

                    If Not (nextToken.IsKindOrHasMatchingText(SyntaxKind.DotToken) AndAlso
                            nextToken.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression)) AndAlso
                       Not (nextToken.IsKindOrHasMatchingText(SyntaxKind.SelectKeyword) AndAlso
                            nextToken.Parent.IsKind(SyntaxKind.SelectStatement)) AndAlso
                        Not (nextToken.IsKindOrHasMatchingText(SyntaxKind.ExclamationToken) AndAlso
                             lastToken.IsKeyword AndAlso
                             nextToken.Parent.IsKind(SyntaxKind.DictionaryAccessExpression)) Then

                        Return True
                    End If
                End If

                Return False
            End If

            ' case:
            ' (GetType(String)) => GetType(String)
            If expression.IsKind(SyntaxKind.GetTypeExpression) Then
                Return True
            End If

            ' case:
            ' 1. (!b) => !b
            If expression.VBKind = SyntaxKind.DictionaryAccessExpression AndAlso
                node.CloseParenToken.IsLastTokenOfStatement() Then
                Return True
            End If

            Return False
        End Function

    End Module
End Namespace
