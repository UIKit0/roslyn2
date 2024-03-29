﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis.RuntimeMembers
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    Friend MustInherit Class SynthesizedEventAccessorSymbol
        Inherits SynthesizedAccessor(Of SourceEventSymbol)

        Private m_lazyReturnType As TypeSymbol
        Private m_lazyParameters As ImmutableArray(Of ParameterSymbol)
        Private m_lazyExplicitImplementations As ImmutableArray(Of MethodSymbol) ' lazily populated with explicit implementations

        Protected Sub New(container As SourceMemberContainerTypeSymbol,
                          [event] As SourceEventSymbol)
            MyBase.New(container, [event])
            ' TODO: custom modifiers
        End Sub

        Private ReadOnly Property SourceEvent As SourceEventSymbol
            Get
                Return m_propertyOrEvent
            End Get
        End Property

        Public Overrides ReadOnly Property ExplicitInterfaceImplementations As ImmutableArray(Of MethodSymbol)
            Get
                If m_lazyExplicitImplementations.IsDefault Then
                    ImmutableInterlocked.InterlockedInitialize(
                        m_lazyExplicitImplementations,
                        SourceEvent.GetAccessorImplementations(Me.MethodKind))
                End If

                Return m_lazyExplicitImplementations
            End Get
        End Property

        Public Overrides ReadOnly Property Parameters As ImmutableArray(Of ParameterSymbol)
            Get
                If m_lazyParameters.IsDefault Then
                    Dim diagnostics = DiagnosticBag.GetInstance()

                    Dim parameterType As TypeSymbol
                    If Me.MethodKind = MethodKind.EventRemove AndAlso m_propertyOrEvent.IsWindowsRuntimeEvent Then
                        parameterType = Me.DeclaringCompilation.GetWellKnownType(WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken)
                        Dim useSite = Binder.GetUseSiteErrorForWellKnownType(parameterType)
                        If useSite IsNot Nothing Then
                            diagnostics.Add(useSite, Me.Locations(0))
                        End If
                    Else
                        parameterType = SourceEvent.Type
                    End If

                    Dim parameter = New SynthesizedParameterSymbol(Me, parameterType, 0, False, "obj")
                    Dim parameterList = ImmutableArray.Create(Of ParameterSymbol)(parameter)

                    DirectCast(Me.ContainingModule, SourceModuleSymbol).AtomicStoreArrayAndDiagnostics(m_lazyParameters, parameterList, diagnostics, CompilationStage.Declare)

                    diagnostics.Free()
                End If

                Return m_lazyParameters
            End Get
        End Property

        Public Overrides ReadOnly Property ReturnType As TypeSymbol
            Get
                If m_lazyReturnType Is Nothing Then
                    Dim diagnostics = DiagnosticBag.GetInstance()

                    Dim compilation = Me.DeclaringCompilation
                    Dim type As TypeSymbol
                    Dim useSite As DiagnosticInfo
                    If Me.IsSub Then
                        type = compilation.GetSpecialType(SpecialType.System_Void)
                        ' Don't report on add, because it will be the same for remove.
                        useSite = If(Me.MethodKind = MethodKind.EventRemove, Binder.GetUseSiteErrorForSpecialType(type), Nothing)
                    Else
                        type = compilation.GetWellKnownType(WellKnownType.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationToken)
                        useSite = Binder.GetUseSiteErrorForWellKnownType(type)
                    End If
                    If useSite IsNot Nothing Then
                        diagnostics.Add(useSite, Me.Locations(0))
                    End If

                    DirectCast(Me.ContainingModule, SourceModuleSymbol).AtomicStoreReferenceAndDiagnostics(m_lazyReturnType, type, diagnostics, CompilationStage.Declare)

                    diagnostics.Free()
                End If

                Debug.Assert(m_lazyReturnType IsNot Nothing)
                Return m_lazyReturnType
            End Get
        End Property

        Public Overrides ReadOnly Property IsSub As Boolean
            Get
                Return Not (Me.MethodKind = MethodKind.EventAdd AndAlso m_propertyOrEvent.IsWindowsRuntimeEvent)
            End Get
        End Property

        Friend Overrides Function GetBoundMethodBody(diagnostics As DiagnosticBag, Optional ByRef methodBodyBinder As Binder = Nothing) As BoundBlock
            Dim compilation = Me.DeclaringCompilation
            Return ConstructFieldLikeEventAccessorBody(Me.m_propertyOrEvent, Me.MethodKind = MethodKind.EventAdd, compilation, diagnostics)
        End Function

        Protected Shared Function ConstructFieldLikeEventAccessorBody(eventSymbol As SourceEventSymbol,
                                                           isAddMethod As Boolean,
                                                           compilation As VisualBasicCompilation,
                                                           diagnostics As DiagnosticBag) As BoundBlock
            Debug.Assert(eventSymbol.HasAssociatedField)
            Dim result As BoundBlock = If(eventSymbol.IsWindowsRuntimeEvent,
                       ConstructFieldLikeEventAccessorBody_WinRT(eventSymbol, isAddMethod, compilation, diagnostics),
                       ConstructFieldLikeEventAccessorBody_Regular(eventSymbol, isAddMethod, compilation, diagnostics))

            ' Contract guarantees non-nothing return.
            Return If(result,
                      New BoundBlock(
                        DirectCast(eventSymbol.SyntaxReference.GetSyntax(), VisualBasicSyntaxNode),
                        Nothing,
                        ImmutableArray(Of LocalSymbol).Empty,
                        ImmutableArray(Of BoundStatement).Empty,
                        hasErrors:=True))
        End Function

        Private Shared Function ConstructFieldLikeEventAccessorBody_WinRT(eventSymbol As SourceEventSymbol,
                                                           isAddMethod As Boolean,
                                                           compilation As VisualBasicCompilation,
                                                           diagnostics As DiagnosticBag) As BoundBlock
            Dim syntax = eventSymbol.SyntaxReference.GetVisualBasicSyntax()

            Dim accessor As MethodSymbol = If(isAddMethod, eventSymbol.AddMethod, eventSymbol.RemoveMethod)
            Debug.Assert(accessor IsNot Nothing)

            Dim field As FieldSymbol = eventSymbol.AssociatedField
            Debug.Assert(field IsNot Nothing)

            Dim fieldType As NamedTypeSymbol = DirectCast(field.Type, NamedTypeSymbol)
            Debug.Assert(fieldType.Name = "EventRegistrationTokenTable")

            ' Don't cascade.
            If fieldType.IsErrorType Then
                Return Nothing
            End If

            Dim useSiteErrorInfo As DiagnosticInfo = Nothing

            Dim getOrCreateMethod As MethodSymbol = DirectCast(Binder.GetWellKnownTypeMember(
                compilation,
                WellKnownMember.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__GetOrCreateEventRegistrationTokenTable,
                useSiteErrorInfo), MethodSymbol)

            If useSiteErrorInfo IsNot Nothing Then
                diagnostics.Add(useSiteErrorInfo, syntax.GetLocation())
            Else
                Debug.Assert(getOrCreateMethod IsNot Nothing)
            End If

            If getOrCreateMethod Is Nothing Then
                Return Nothing
            End If

            getOrCreateMethod = getOrCreateMethod.AsMember(fieldType)

            Dim processHandlerMember As WellKnownMember = If(isAddMethod,
                WellKnownMember.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__AddEventHandler,
                WellKnownMember.System_Runtime_InteropServices_WindowsRuntime_EventRegistrationTokenTable_T__RemoveEventHandler)

            Dim processHandlerMethod As MethodSymbol = DirectCast(Binder.GetWellKnownTypeMember(
                compilation,
                processHandlerMember,
                useSiteErrorInfo), MethodSymbol)

            If useSiteErrorInfo IsNot Nothing Then
                diagnostics.Add(useSiteErrorInfo, syntax.GetLocation())
            Else
                Debug.Assert(processHandlerMethod IsNot Nothing)
            End If

            If processHandlerMethod Is Nothing Then
                Return Nothing
            End If

            processHandlerMethod = processHandlerMethod.AsMember(fieldType)

            ' _tokenTable
            Dim fieldAccess = New BoundFieldAccess(
                syntax,
                If(field.IsShared, Nothing, New BoundMeReference(syntax, accessor.MeParameter.Type)),
                field,
                isLValue:=True,
                type:=field.Type).MakeCompilerGenerated()

            ' EventRegistrationTokenTable(Of Event).GetOrCreateEventRegistrationTokenTable(_tokenTable)
            Dim getOrCreateCall = New BoundCall(
                syntax:=syntax,
                method:=getOrCreateMethod,
                methodGroup:=Nothing,
                receiver:=Nothing,
                arguments:=ImmutableArray.Create(Of BoundExpression)(fieldAccess),
                constantValueOpt:=Nothing,
                type:=getOrCreateMethod.ReturnType).MakeCompilerGenerated()

            Dim parameterSymbol As ParameterSymbol = accessor.Parameters.Single()

            ' value
            Dim parameterAccess = New BoundParameter(
                syntax,
                parameterSymbol,
                isLValue:=False,
                type:=parameterSymbol.Type).MakeCompilerGenerated()

            ' EventRegistrationTokenTable(Of Event).GetOrCreateEventRegistrationTokenTable(_tokenTable).AddHandler(value) ' or RemoveHandler
            Dim processHandlerCall = New BoundCall(
                syntax:=syntax,
                method:=processHandlerMethod,
                methodGroup:=Nothing,
                receiver:=getOrCreateCall,
                arguments:=ImmutableArray.Create(Of BoundExpression)(parameterAccess),
                constantValueOpt:=Nothing,
                type:=processHandlerMethod.ReturnType).MakeCompilerGenerated()

            If isAddMethod Then
                ' {
                '     return EventRegistrationTokenTable(Of Event).GetOrCreateEventRegistrationTokenTable(_tokenTable).AddHandler(value)
                ' }   
                Dim returnStatement = New BoundReturnStatement(syntax, processHandlerCall, functionLocalOpt:=Nothing, exitLabelOpt:=Nothing)
                Return New BoundBlock(
                    syntax,
                    statementListSyntax:=Nothing,
                    locals:=ImmutableArray(Of LocalSymbol).Empty,
                    statements:=ImmutableArray.Create(Of BoundStatement)(returnStatement)).MakeCompilerGenerated()
            Else
                ' {
                '     EventRegistrationTokenTable(Of Event).GetOrCreateEventRegistrationTokenTable(_tokenTable).RemoveHandler(value)
                '     return
                ' }  
                Dim callStatement = New BoundExpressionStatement(syntax, processHandlerCall).MakeCompilerGenerated()
                Dim returnStatement = New BoundReturnStatement(syntax, expressionOpt:=Nothing, functionLocalOpt:=Nothing, exitLabelOpt:=Nothing)
                Return New BoundBlock(
                    syntax,
                    statementListSyntax:=Nothing,
                    locals:=ImmutableArray(Of LocalSymbol).Empty,
                    statements:=ImmutableArray.Create(Of BoundStatement)(callStatement, returnStatement)).MakeCompilerGenerated()
            End If

        End Function

        ''' <summary>
        ''' Generate a thread-safe accessor for a field-like event.
        ''' 
        ''' DelegateType tmp0 = _event; //backing field
        ''' DelegateType tmp1;
        ''' DelegateType tmp2;
        ''' do {
        '''     tmp1 = tmp0;
        '''     tmp2 = (DelegateType)Delegate.Combine(tmp1, value); //Remove for -=
        '''     tmp0 = Interlocked.CompareExchange&lt; DelegateType&gt; (ref _event, tmp2, tmp1);
        ''' } while ((object)tmp0 != (object)tmp1);
        ''' </summary>
        Private Shared Function ConstructFieldLikeEventAccessorBody_Regular(eventSymbol As SourceEventSymbol,
                                                                   isAddMethod As Boolean,
                                                                   compilation As VisualBasicCompilation,
                                                                   diagnostics As DiagnosticBag) As BoundBlock


            If Not eventSymbol.Type.IsDelegateType() Then
                Return Nothing
            End If

            Dim syntax = eventSymbol.SyntaxReference.GetVisualBasicSyntax
            Dim delegateType As TypeSymbol = eventSymbol.Type
            Dim accessor As MethodSymbol = If(isAddMethod, eventSymbol.AddMethod, eventSymbol.RemoveMethod)
            Dim meParameter As ParameterSymbol = accessor.MeParameter
            Dim boolType As TypeSymbol = compilation.GetSpecialType(SpecialType.System_Boolean)
            Dim updateMethod As MethodSymbol = DirectCast(compilation.Assembly.GetSpecialTypeMember(If(isAddMethod, SpecialMember.System_Delegate__Combine, SpecialMember.System_Delegate__Remove)), MethodSymbol)
            Dim compareExchangeMethod As MethodSymbol = GetConstructedCompareExchangeMethod(delegateType, compilation, accessor.Locations(0), diagnostics)

            If compareExchangeMethod Is Nothing Then
                Return New BoundBlock(syntax,
                                      Nothing,
                                      ImmutableArray(Of LocalSymbol).Empty,
                                      ImmutableArray.Create(Of BoundStatement)(New BoundReturnStatement(syntax,
                                                                                                          Nothing,
                                                                                                          Nothing,
                                                                                                          Nothing).MakeCompilerGenerated)
                                                                                                   ).MakeCompilerGenerated
            End If

            Dim loopLabel As GeneratedLabelSymbol = New GeneratedLabelSymbol("LOOP")
            Const numTemps As Integer = 3
            Dim tmps As LocalSymbol() = New LocalSymbol(numTemps - 1) {}
            Dim boundTmps As BoundLocal() = New BoundLocal(numTemps - 1) {}

            Dim i As Integer = 0
            While i < tmps.Length
                tmps(i) = New SynthesizedLocal(accessor, delegateType, SynthesizedLocalKind.LoweringTemp)
                boundTmps(i) = New BoundLocal(syntax, tmps(i), delegateType)
                i = i + 1
            End While

            Dim fieldReceiver As BoundMeReference = If(eventSymbol.IsShared,
                                                       Nothing,
                                                       New BoundMeReference(syntax, meParameter.Type).MakeCompilerGenerated)

            Dim fieldSymbol = eventSymbol.AssociatedField
            Dim boundBackingField As BoundFieldAccess = New BoundFieldAccess(syntax,
                                                                             fieldReceiver,
                                                                             fieldSymbol,
                                                                             True,
                                                                             fieldSymbol.Type).MakeCompilerGenerated

            Dim parameterSymbol = accessor.Parameters(0)
            Dim boundParameter As BoundParameter = New BoundParameter(syntax,
                                                                      parameterSymbol,
                                                                      isLValue:=False,
                                                                      type:=parameterSymbol.Type).MakeCompilerGenerated

            ' tmp0 = _event;
            Dim tmp0Init As BoundStatement = New BoundExpressionStatement(syntax,
                                                                          New BoundAssignmentOperator(syntax,
                                                                                                      boundTmps(0),
                                                                                                      boundBackingField.MakeRValue(),
                                                                                                      True,
                                                                                                      delegateType).MakeCompilerGenerated
                                                                                                  ).MakeCompilerGenerated

            ' LOOP:
            Dim loopStart As BoundStatement = New BoundLabelStatement(syntax, loopLabel).MakeCompilerGenerated

            ' tmp1 = tmp0;
            Dim tmp1Update As BoundStatement = New BoundExpressionStatement(syntax,
                                                                            New BoundAssignmentOperator(syntax,
                                                                                                        boundTmps(1),
                                                                                                        boundTmps(0).MakeRValue(),
                                                                                                        True,
                                                                                                        delegateType).MakeCompilerGenerated
                                                                                                    ).MakeCompilerGenerated

            ' (DelegateType)Delegate.Combine(tmp1, value)
            Debug.Assert(Conversions.ClassifyDirectCastConversion(boundTmps(1).Type, updateMethod.Parameters(0).Type, Nothing) = ConversionKind.WideningReference)
            Debug.Assert(Conversions.ClassifyDirectCastConversion(boundParameter.Type, updateMethod.Parameters(1).Type, Nothing) = ConversionKind.WideningReference)
            Dim delegateUpdate As BoundExpression = New BoundDirectCast(syntax,
                                                                        New BoundCall(syntax,
                                                                                      updateMethod,
                                                                                      Nothing,
                                                                                      Nothing,
                                                                                      ImmutableArray.Create(Of BoundExpression)(
                                                                                          New BoundDirectCast(syntax, boundTmps(1).MakeRValue(), ConversionKind.WideningReference, updateMethod.Parameters(0).Type),
                                                                                          New BoundDirectCast(syntax, boundParameter, ConversionKind.WideningReference, updateMethod.Parameters(1).Type)),
                                                                                      Nothing,
                                                                                      updateMethod.ReturnType),
                                                                                  ConversionKind.NarrowingReference,
                                                                                  delegateType,
                                                                                  delegateType.IsErrorType).MakeCompilerGenerated

            ' tmp2 = (DelegateType)Delegate.Combine(tmp1, value);
            Dim tmp2Update As BoundStatement = New BoundExpressionStatement(syntax,
                                                                            New BoundAssignmentOperator(syntax,
                                                                                                        boundTmps(2),
                                                                                                        delegateUpdate,
                                                                                                        True,
                                                                                                        delegateType).MakeCompilerGenerated
                                                                                                    ).MakeCompilerGenerated

            ' Interlocked.CompareExchange<DelegateType>(ref _event, tmp2, tmp1)
            Dim compareExchange As BoundExpression = New BoundCall(syntax,
                                                                   compareExchangeMethod,
                                                                   Nothing,
                                                                   Nothing,
                                                                   ImmutableArray.Create(Of BoundExpression)(boundBackingField, boundTmps(2).MakeRValue(), boundTmps(1).MakeRValue()),
                                                                   Nothing,
                                                                   compareExchangeMethod.ReturnType)

            ' tmp0 = Interlocked.CompareExchange<DelegateType>(ref _event, tmp2, tmp1);
            Dim tmp0Update As BoundStatement = New BoundExpressionStatement(syntax,
                                                                            New BoundAssignmentOperator(syntax,
                                                                                                        boundTmps(0),
                                                                                                        compareExchange,
                                                                                                        True,
                                                                                                        delegateType).MakeCompilerGenerated
                                                                                                    ).MakeCompilerGenerated

            ' tmp[0] == tmp[1] // i.e. exit when they are equal, jump to start otherwise
            Dim loopExitCondition As BoundExpression = New BoundBinaryOperator(syntax,
                                                                               BinaryOperatorKind.Is,
                                                                               boundTmps(0).MakeRValue(),
                                                                               boundTmps(1).MakeRValue(),
                                                                               False,
                                                                               boolType).MakeCompilerGenerated

            ' branchfalse (tmp[0] == tmp[1]) LOOP
            Dim loopEnd As BoundStatement = New BoundConditionalGoto(syntax,
                                                                     loopExitCondition,
                                                                     False,
                                                                     loopLabel).MakeCompilerGenerated

            Dim [return] As BoundStatement = New BoundReturnStatement(syntax,
                                                                      Nothing,
                                                                      Nothing,
                                                                      Nothing).MakeCompilerGenerated

            Return New BoundBlock(syntax,
                                  Nothing,
                                  tmps.AsImmutable(),
                                  ImmutableArray.Create(Of BoundStatement)(
                                      tmp0Init,
                                      loopStart,
                                      tmp1Update,
                                      tmp2Update,
                                      tmp0Update,
                                      loopEnd,
                                      [return])
                                  ).MakeCompilerGenerated
        End Function

        ''' <summary>
        ''' Get the MethodSymbol for System.Threading.Interlocked.CompareExchange&lt; T&gt;  for a given T.
        ''' </summary>
        Private Shared Function GetConstructedCompareExchangeMethod(typeArg As TypeSymbol, compilation As VisualBasicCompilation, errorLocation As Location, diagnostics As DiagnosticBag) As MethodSymbol
            Dim compareExchangeDefinition As MethodSymbol = DirectCast(compilation.GetWellKnownTypeMember(WellKnownMember.System_Threading_Interlocked__CompareExchange_T), MethodSymbol)
            If compareExchangeDefinition Is Nothing Then
                Dim memberDescriptor As MemberDescriptor = WellKnownMembers.GetDescriptor(WellKnownMember.System_Threading_Interlocked__CompareExchange_T)
                Dim containingType As WellKnownType = CType(memberDescriptor.DeclaringTypeId, WellKnownType)

                diagnostics.Add(ERRID.ERR_RuntimeMemberNotFound2, errorLocation, memberDescriptor.Name, containingType.GetMetadataName())
                Return Nothing
            End If

            Return compareExchangeDefinition.Construct(ImmutableArray.Create(Of TypeSymbol)(typeArg))
        End Function

        Friend Overrides Sub AddSynthesizedAttributes(compilationState as ModuleCompilationState, ByRef attributes As ArrayBuilder(Of SynthesizedAttributeData))
            MyBase.AddSynthesizedAttributes(compilationState, attributes)

            Debug.Assert(Not ContainingType.IsImplicitlyDeclared)
            Dim compilation = Me.DeclaringCompilation
            AddSynthesizedAttribute(attributes,
                                    compilation.SynthesizeAttribute(WellKnownMember.System_Runtime_CompilerServices_CompilerGeneratedAttribute__ctor))

            ' Dev11 adds DebuggerNonUserCode; there is no reason to do so since:
            ' - we emit no debug info for the body
            ' - the code doesn't call any user code that could inspect the stack and find the accessor's frame
            ' - the code doesn't throw exceptions whose stack frames we would need to hide
            ' 
            ' C# also doesn't add DebuggerHidden nor DebuggerNonUserCode attributes.
        End Sub

        Friend Overrides Sub GenerateDeclarationErrors(cancellationToken As CancellationToken)
            MyBase.GenerateDeclarationErrors(cancellationToken)

            cancellationToken.ThrowIfCancellationRequested()
            Dim unusedParameters = Me.Parameters
            Dim unusedReturnType = Me.ReturnType
        End Sub
    End Class

    Friend NotInheritable Class SynthesizedAddAccessorSymbol
        Inherits SynthesizedEventAccessorSymbol

        Public Sub New(container As SourceMemberContainerTypeSymbol,
                          [event] As SourceEventSymbol)
            MyBase.New(container, [event])
        End Sub

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return MethodKind.EventAdd
            End Get
        End Property
    End Class

    Friend NotInheritable Class SynthesizedRemoveAccessorSymbol
        Inherits SynthesizedEventAccessorSymbol

        Public Sub New(container As SourceMemberContainerTypeSymbol,
                          [event] As SourceEventSymbol)
            MyBase.New(container, [event])
        End Sub

        Public Overrides ReadOnly Property MethodKind As MethodKind
            Get
                Return MethodKind.EventRemove
            End Get
        End Property
    End Class

End Namespace