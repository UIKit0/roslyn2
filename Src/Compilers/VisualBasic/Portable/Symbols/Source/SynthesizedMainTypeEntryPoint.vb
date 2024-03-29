﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Generic
Imports System.Collections.Immutable
Imports System.Diagnostics
Imports System.Runtime.InteropServices
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax
Imports TypeKind = Microsoft.CodeAnalysis.TypeKind

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary>
    ''' Represents a compiler generated "Main" sub.
    ''' </summary>
    Friend Class SynthesizedMainTypeEntryPoint
        Inherits SynthesizedRegularMethodBase

        Public Sub New(syntaxNode As VisualBasicSyntaxNode, container As SourceNamedTypeSymbol)
            MyBase.New(syntaxNode, container, WellKnownMemberNames.EntryPointMethodName, IsShared:=True)
        End Sub

        Public Overrides ReadOnly Property DeclaredAccessibility As Accessibility
            Get
                Return Accessibility.Public
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                ' No reason to check Void type for errors because we require presence of constructor,
                ' which will complain.
                Return ContainingAssembly.GetSpecialType(SpecialType.System_Void)
            End Get
        End Property

        Friend Overrides Function GetBoundMethodBody(diagnostics As DiagnosticBag, <Out()> Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            methodBodyBinder = Nothing

            Dim syntaxNode As VisualBasicSyntaxNode = Me.Syntax
            Dim container = DirectCast(ContainingSymbol, SourceNamedTypeSymbol)
            Dim binder As Binder = BinderBuilder.CreateBinderForType(container.ContainingSourceModule, syntaxNode.SyntaxTree, container)

            Debug.Assert(binder.IsDefaultInstancePropertyAllowed)
            Dim instance As BoundExpression = binder.TryDefaultInstanceProperty(New BoundTypeExpression(syntaxNode, container),
                                                                                diagnosticsBagFor_ERR_CantReferToMyGroupInsideGroupType1:=Nothing)

            If instance Is Nothing Then
                ' Default instance is not available, create instance by invoking constructor.
                instance = binder.BindObjectCreationExpression(syntaxNode, container, ImmutableArray(Of BoundExpression).Empty, diagnostics)
            End If

            ' Call System.Windows.Forms.Application.Run(<instance>)
            Dim useSiteError As DiagnosticInfo = Nothing
            Dim runMethod = DirectCast(Binder.GetWellKnownTypeMember(container.DeclaringCompilation, WellKnownMember.System_Windows_Forms_Application__RunForm, useSiteError), MethodSymbol)
            Dim statement As BoundStatement

            If useSiteError Is Nothing Then
                statement = binder.BindInvocationExpression(syntaxNode, syntaxNode, TypeCharacter.None,
                                                            New BoundMethodGroup(syntaxNode, Nothing, ImmutableArray.Create(runMethod), LookupResultKind.Good, Nothing, QualificationKind.QualifiedViaTypeName),
                                                            ImmutableArray.Create(instance), Nothing, diagnostics,
                                                            callerInfoOpt:=Nothing).ToStatement()
            Else
                Binder.ReportDiagnostic(diagnostics, syntaxNode, useSiteError)
                statement = New BoundBadStatement(syntaxNode, ImmutableArray(Of BoundNode).Empty, hasErrors:=True)
            End If

            Return New BoundBlock(syntaxNode, Nothing, ImmutableArray(Of LocalSymbol).Empty, ImmutableArray.Create(statement, New BoundReturnStatement(syntaxNode, Nothing, Nothing, Nothing)))
        End Function

        Friend Overrides Sub AddSynthesizedAttributes(compilationState as ModuleCompilationState, ByRef attributes As ArrayBuilder(Of SynthesizedAttributeData))
            MyBase.AddSynthesizedAttributes(compilationState, attributes)

            AddSynthesizedAttribute(attributes,
                                    DeclaringCompilation.SynthesizeAttribute(WellKnownMember.System_STAThreadAttribute__ctor))
        End Sub

    End Class

End Namespace





