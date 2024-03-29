﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Threading
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols
    ''' <summary>
    ''' Represents a Lambda parameter for an UnboundLambda.
    ''' </summary>
    Friend Class UnboundLambdaParameterSymbol
        Inherits LambdaParameterSymbol

        Private ReadOnly m_IdentifierSyntax As ModifiedIdentifierSyntax
        Private ReadOnly m_TypeSyntax As SyntaxNodeOrToken

        Private Sub New(
            name As String,
            ordinal As Integer,
            type As TypeSymbol,
            location As Location,
            flags As SourceParameterFlags,
            identifierSyntax As ModifiedIdentifierSyntax,
            typeSyntax As SyntaxNodeOrToken
        )
            MyBase.New(name, ordinal, type, ((flags And SourceParameterFlags.ByRef) <> 0), location)

            m_IdentifierSyntax = identifierSyntax
            m_TypeSyntax = typeSyntax
        End Sub

        Public ReadOnly Property IdentifierSyntax As SyntaxToken
            Get
                Return m_IdentifierSyntax.Identifier
            End Get
        End Property

        Public ReadOnly Property Syntax As ModifiedIdentifierSyntax
            Get
                Return m_IdentifierSyntax
            End Get
        End Property

        Public ReadOnly Property TypeSyntax As SyntaxNodeOrToken
            Get
                Return m_TypeSyntax
            End Get
        End Property

        Public Overrides ReadOnly Property ContainingSymbol As Symbol
            Get
                Return Nothing
            End Get
        End Property

        ' Create a parameter from syntax.
        Friend Shared Function CreateFromSyntax(syntax As ParameterSyntax,
                                                name As String,
                                                flags As SourceParameterFlags,
                                                ordinal As Integer,
                                                binder As Binder,
                                                diagBag As DiagnosticBag) As ParameterSymbol
            If (flags And SourceParameterFlags.ParamArray) <> 0 Then
                ' 'Lambda' parameters cannot be declared 'ParamArray'.
                Binder.ReportDiagnostic(diagBag, GetModifierToken(syntax.Modifiers, SyntaxKind.ParamArrayKeyword), ERRID.ERR_ParamArrayIllegal1, StringConstants.Lambda)
            End If

            If (flags And SourceParameterFlags.Optional) <> 0 Then
                ' 'Lambda' parameters cannot be declared 'Optional'.
                Binder.ReportDiagnostic(diagBag, GetModifierToken(syntax.Modifiers, SyntaxKind.OptionalKeyword), ERRID.ERR_OptionalIllegal1, StringConstants.Lambda)
            End If

            If syntax.AttributeLists.Node IsNot Nothing Then
                Binder.ReportDiagnostic(diagBag, syntax.AttributeLists.Node, ERRID.ERR_LambdasCannotHaveAttributes)
            End If

            'TODO: Should report the following error for Option Strict, but not here, after inference.
            '      BC36642: Option Strict On requires each lambda expression parameter to be declared with an 'As' clause if its type cannot be inferred.
            Dim getErrorInfo As Func(Of DiagnosticInfo) = Nothing

            Dim paramType As TypeSymbol = binder.DecodeModifiedIdentifierType(syntax.Identifier, syntax.AsClause, Nothing, getErrorInfo, diagBag, Binder.ModifiedIdentifierTypeDecoderContext.LambdaParameterType)

            ' Preserve the fact that parameter doesn't have explicitly provided type.
            ' DecodeModifiedIdentifierType returns System.Object if there was no type character and there was no an 'As' clause.
            ' Type character can never produce System.Object, so System.Object clearly indicates that type character is not present. 
            If paramType.IsObjectType() AndAlso syntax.AsClause Is Nothing Then
                paramType = Nothing
            End If

            Return New UnboundLambdaParameterSymbol(name, ordinal, paramType, syntax.Identifier.Identifier.GetLocation(), flags,
                                                    syntax.Identifier,
                                                    If(syntax.AsClause Is Nothing, CType(syntax.Identifier, SyntaxNodeOrToken), syntax.AsClause.Type))
        End Function

        Private Shared Function GetModifierToken(modifiers As SyntaxTokenList, tokenKind As SyntaxKind) As SyntaxToken
            For Each keywordSyntax In modifiers
                If keywordSyntax.VBKind = tokenKind Then
                    Return keywordSyntax
                End If
            Next

            Throw ExceptionUtilities.Unreachable
        End Function

    End Class

End Namespace