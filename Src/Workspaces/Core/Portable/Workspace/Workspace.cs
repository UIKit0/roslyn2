// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// A workspace provides access to a active set of source code projects and documents and their
    /// associated syntax trees, compilations and semantic models. A workspace has a current solution
    /// that is an immutable snapshot of the projects and documents. This property may change over time 
    /// as the workspace is updated either from live interactions in the environment or via call to the
    /// workspace's <see cref="TryApplyChanges"/> method.
    /// </summary>
    public abstract partial class Workspace : IDisposable
    {
        private readonly string workspaceKind;
        private readonly HostWorkspaceServices services;
        private readonly BranchId primaryBranchId;

        // forces serialization of mutation calls from host (OnXXX methods). Must take this lock before taking stateLock.
        private readonly NonReentrantLock serializationLock = new NonReentrantLock(useThisInstanceForSynchronization: true);

        // this lock guards all the mutable fields (do not share lock with derived classes)
        private readonly NonReentrantLock stateLock = new NonReentrantLock(useThisInstanceForSynchronization: true);

        // Current solution.
        private Solution latestSolution;

        private readonly IWorkspaceTaskScheduler taskQueue;

        // test hooks.
        internal static bool TestHookStandaloneProjectsDoNotHoldReferences = false;

        internal bool TestHookPartialSolutionsDisabled { get; set; }

        /// <summary>
        /// Constructs a new workspace instance.
        /// </summary>
        /// <param name="host">The <see cref="HostServices"/> this workspace uses</param>
        /// <param name="workspaceKind">A string that can be used to identify the kind of workspace. Usually this matches the name of the class.</param>
        protected Workspace(HostServices host, string workspaceKind)
        {
            this.primaryBranchId = BranchId.GetNextId();
            this.workspaceKind = workspaceKind;

            this.services = host.CreateWorkspaceServices(this);

            // queue used for sending events
            var workspaceTaskSchedulerFactory = this.services.GetService<IWorkspaceTaskSchedulerFactory>();
            this.taskQueue = workspaceTaskSchedulerFactory.CreateTaskQueue();

            // initialize with empty solution
            this.latestSolution = CreateSolution(SolutionId.CreateNewId());
        }

        /// <summary>
        /// Services provider by the host for implementing workspace features.
        /// </summary>
        public HostWorkspaceServices Services
        {
            get { return this.services; }
        }

        /// <summary>
        /// primary branch id that current solution has
        /// </summary>
        internal BranchId PrimaryBranchId
        {
            get { return this.primaryBranchId; }
        }

        /// <summary>
        /// Override this property if the workspace supports partial semantics for documents.
        /// </summary>
        protected internal virtual bool PartialSemanticsEnabled
        {
            get { return false; }
        }

        /// <summary>
        /// The kind of the workspace. 
        /// This is generally <see cref="WorkspaceKind.Host"/> if originating from the host environment, but may be 
        /// any other name used for a specific kind of workspace.
        /// </summary>
        public string Kind
        {
            get { return this.workspaceKind; }
        }

        /// <summary>
        /// Create a new empty solution instance associated with this workspace.
        /// </summary>
        protected internal Solution CreateSolution(SolutionInfo solutionInfo)
        {
            return new Solution(this, solutionInfo);
        }

        /// <summary>
        /// Create a new empty solution instance associated with this workspace.
        /// </summary>
        protected internal Solution CreateSolution(SolutionId id)
        {
            return CreateSolution(SolutionInfo.Create(id, VersionStamp.Create()));
        }

        /// <summary>
        /// The current solution. 
        /// 
        /// The solution is an immutable model of the current set of projects and source documents.
        /// It provides access to source text, syntax trees and semantics.
        /// 
        /// This property may change as the workspace reacts to changes in the environment or
        /// after <see cref="TryApplyChanges"/> is called.
        /// </summary>
        public Solution CurrentSolution
        {
            get
            {
                return Volatile.Read(ref this.latestSolution);
            }
        }

        /// <summary>
        /// Sets the <see cref="CurrentSolution"/> of this workspace. This method does not raise a <see cref="WorkspaceChanged"/> event.
        /// </summary>
        protected Solution SetCurrentSolution(Solution solution)
        {
            var currentSolution = Volatile.Read(ref this.latestSolution);
            if (solution == currentSolution)
            {
                // No change
                return solution;
            }

            while (true)
            {
                var newSolution = solution.WithNewWorkspace(this, currentSolution.WorkspaceVersion + 1);
                var replacedSolution = Interlocked.CompareExchange(ref this.latestSolution, newSolution, currentSolution);
                if (replacedSolution == currentSolution)
                {
                    return newSolution;
                }

                currentSolution = replacedSolution;
            }
        }

        /// <summary>
        /// Gets or sets the set of all global options.
        /// </summary>
        public OptionSet Options
        {
            get
            {
                return this.services.GetService<IOptionService>().GetOptions();
            }

            set
            {
                this.services.GetService<IOptionService>().SetOptions(value);
            }
        }

        /// <summary>
        /// Executes an action as a background task, as part of a sequential queue of tasks.
        /// </summary>
        protected internal Task ScheduleTask(Action action, string taskName = "Workspace.Task")
        {
            return taskQueue.ScheduleTask(action, taskName);
        }

        /// <summary>
        /// Execute a function as a background task, as part of a sequential queue of tasks.
        /// </summary>
        protected internal Task<T> ScheduleTask<T>(Func<T> func, string taskName = "Workspace.Task")
        {
            return taskQueue.ScheduleTask(func, taskName);
        }

        /// <summary>
        /// Override this method to act immediately when the text of a document has changed, as opposed
        /// to waiting for the corresponding workspace changed event to fire asynchronously.
        /// </summary>
        protected virtual void OnDocumentTextChanged(Document document)
        {
        }

        /// <summary>
        /// Override this method to act immediately when a document is closing, as opposed
        /// to waiting for the corresponding workspace changed event to fire asynchronously.
        /// </summary>
        protected virtual void OnDocumentClosing(DocumentId documentId)
        {
        }

        /// <summary>
        /// Clears all solution data and empties the current solution.
        /// </summary>
        protected void ClearSolution()
        {
            var oldSolution = this.CurrentSolution;
            this.ClearSolutionData();

            this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.SolutionCleared, oldSolution, this.CurrentSolution);
        }

        /// <summary>
        /// This method is called when a solution is cleared.
        /// 
        /// Override this method if you want to do additional work when a solution is cleared. 
        /// Call the base method at the end of your method.
        /// </summary>
        protected virtual void ClearSolutionData()
        {
            // clear any open documents
            this.ClearOpenDocuments();

            this.SetCurrentSolution(this.CreateSolution(this.CurrentSolution.Id));
        }

        /// <summary>
        /// This method is called when an individual project is removed.
        /// 
        /// Override this method if you want to do additional work when a project is removed.
        /// Call the base method at the end of your method.
        /// </summary>
        protected virtual void ClearProjectData(ProjectId projectId)
        {
            this.ClearOpenDocuments(projectId);
        }

        /// <summary>
        /// This method is called to clear an individual document is removed.
        /// 
        /// Override this method if you want to do additional work when a document is removed.
        /// Call the base method at the end of your method.
        /// </summary>
        protected virtual void ClearDocumentData(DocumentId documentId)
        {
            this.ClearOpenDocument(documentId);
        }

        /// <summary>
        /// Disposes this workspace. The workspace can longer be used after it is disposed.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(finalize: false);
        }

        /// <summary>
        /// Call this method when the workspace is disposed.
        /// 
        /// Override this method to do addition work when the workspace is disposed.
        /// Call this method at the end of your method.
        /// </summary>
        protected virtual void Dispose(bool finalize)
        {
            if (!finalize)
            {
                this.ClearSolutionData();
            }
        }

        #region Host API
        /// <summary>
        /// Call this method to respond to a solution being opened in the host environment.
        /// </summary>
        protected internal void OnSolutionAdded(SolutionInfo solutionInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var oldSolution = this.CurrentSolution;
                var solutionId = solutionInfo.Id;

                CheckSolutionIsEmpty();
                this.SetCurrentSolution(this.CreateSolution(solutionInfo));

                solutionInfo.Projects.Do(p => OnProjectAdded_NoLock(p, silent: true));

                var newSolution = this.CurrentSolution;
                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.SolutionAdded, oldSolution, newSolution);
            }
        }

        /// <summary>
        /// Call this method to respond to a solution being reloaded in the host environment.
        /// </summary>
        protected internal void OnSolutionReloaded(SolutionInfo reloadedSolutionInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(this.CreateSolution(reloadedSolutionInfo));

                reloadedSolutionInfo.Projects.Do(pi => OnProjectAdded_NoLock(pi, silent: true));

                newSolution = this.AdjustReloadedSolution(oldSolution, this.CurrentSolution);
                newSolution = this.SetCurrentSolution(newSolution);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.SolutionReloaded, oldSolution, newSolution);
            }
        }

        /// <summary>
        /// This method is called when the solution is removed from the workspace.
        /// 
        /// Override this method if you want to do additional work when the solution is removed. 
        /// Call the base method at the end of your method.
        /// Call this method to respond to a solution being removed/cleared/closed in the host environment.
        /// </summary>
        protected internal void OnSolutionRemoved()
        {
            using (this.serializationLock.DisposableWait())
            {
                var oldSolution = this.CurrentSolution;

                this.ClearSolutionData();

                // reset to new empty solution
                this.SetCurrentSolution(this.CreateSolution(SolutionId.CreateNewId()));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.SolutionRemoved, oldSolution, this.CurrentSolution);

                // At this point, no one will need to pull any more items out of the old solution,
                // so clear all objects out of the caches. but this has to run after the workspace event
                // since the workspace event holds onto old solution which makes most of items in the cache alive
                // also, here we don't clear text cache since doing that right after clearing out other caches
                // make us to dump evicted texts unnecessarily to MMF. instead, we will let the text to be eventually
                // replaced with new texts and those texts belong to old solution will be removed without being written to MMF
                this.ScheduleTask(() =>
                {
                    this.services.GetService<ICompilationCacheService>().Clear();
                    this.services.GetService<ISyntaxTreeCacheService>().Clear();
                }, "Workspace.ClearCache");
            }
        }

        /// <summary>
        /// Call this method to respond to a project being added/opened in the host environment.
        /// </summary>
        protected internal void OnProjectAdded(ProjectInfo projectInfo)
        {
            this.OnProjectAdded(projectInfo, silent: false);
        }

        private void OnProjectAdded(ProjectInfo projectInfo, bool silent)
        {
            using (this.serializationLock.DisposableWait())
            {
                this.OnProjectAdded_NoLock(projectInfo, silent);
            }
        }

        private void OnProjectAdded_NoLock(ProjectInfo projectInfo, bool silent)
        {
            var projectId = projectInfo.Id;

            CheckProjectIsNotInCurrentSolution(projectId);

            var oldSolution = this.CurrentSolution;
            var newSolution = this.SetCurrentSolution(oldSolution.AddProject(projectInfo));

            if (!silent)
            {
                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectAdded, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method to respond to a project being reloaded in the host environment.
        /// </summary>
        protected internal virtual void OnProjectReloaded(ProjectInfo reloadedProjectInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var projectId = reloadedProjectInfo.Id;

                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = oldSolution.RemoveProject(projectId).AddProject(reloadedProjectInfo);

                newSolution = this.AdjustReloadedProject(oldSolution.GetProject(projectId), newSolution.GetProject(projectId)).Solution;
                newSolution = this.SetCurrentSolution(newSolution);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectReloaded, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method to respond to a project being removed from the host environment.
        /// </summary>
        protected internal virtual void OnProjectRemoved(ProjectId projectId)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                this.CheckProjectCanBeRemoved(projectId);

                var oldSolution = this.CurrentSolution;

                this.ClearProjectData(projectId);
                var newSolution = this.SetCurrentSolution(oldSolution.RemoveProject(projectId));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectRemoved, oldSolution, newSolution, projectId);
            }
        }

        protected virtual void CheckProjectCanBeRemoved(ProjectId projectId)
        {
            CheckProjectDoesNotContainOpenDocuments(projectId);
        }

        /// <summary>
        /// Call this method when a project's assembly name is changed in the host environment.
        /// </summary>
        protected internal void OnAssemblyNameChanged(ProjectId projectId, string assemblyName)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithProjectAssemblyName(projectId, assemblyName));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project's output file path is changed in the host environment.
        /// </summary>
        protected internal void OnOutputFilePathChanged(ProjectId projectId, string outputFilePath)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithProjectOutputFilePath(projectId, outputFilePath));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project's name is changed in the host environment.
        /// </summary>
        protected internal void OnProjectNameChanged(ProjectId projectId, string name, string filePath)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithProjectName(projectId, name).WithProjectFilePath(projectId, filePath));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project's compilation options are changed in the host environment.
        /// </summary>
        protected internal void OnCompilationOptionsChanged(ProjectId projectId, CompilationOptions options)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithProjectCompilationOptions(projectId, options));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project's parse options are changed in the host environment.
        /// </summary>
        protected internal void OnParseOptionsChanged(ProjectId projectId, ParseOptions options)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithProjectParseOptions(projectId, options));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project reference is added to a project in the host environment.
        /// </summary>
        protected internal void OnProjectReferenceAdded(ProjectId projectId, ProjectReference projectReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectIsInCurrentSolution(projectReference.ProjectId);
                CheckProjectDoesNotHaveProjectReference(projectId, projectReference);

                // Can only add this P2P reference if it would not cause a circularity.
                CheckProjectDoesNotHaveTransitiveProjectReference(projectId, projectReference.ProjectId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddProjectReference(projectId, projectReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a project reference is removed from a project in the host environment.
        /// </summary>
        protected internal void OnProjectReferenceRemoved(ProjectId projectId, ProjectReference projectReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectIsInCurrentSolution(projectReference.ProjectId);
                CheckProjectHasProjectReference(projectId, projectReference);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.RemoveProjectReference(projectId, projectReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a metadata reference is added to a project in the host environment.
        /// </summary>
        protected internal void OnMetadataReferenceAdded(ProjectId projectId, MetadataReference metadataReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectDoesNotHaveMetadataReference(projectId, metadataReference);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddMetadataReference(projectId, metadataReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a metadata reference is removed from a project in the host environment.
        /// </summary>
        protected internal void OnMetadataReferenceRemoved(ProjectId projectId, MetadataReference metadataReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectHasMetadataReference(projectId, metadataReference);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.RemoveMetadataReference(projectId, metadataReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when an analyzer reference is added to a project in the host environment.
        /// </summary>
        protected internal void OnAnalyzerReferenceAdded(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectDoesNotHaveAnalyzerReference(projectId, analyzerReference);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddAnalyzerReference(projectId, analyzerReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when an analyzer reference is removed from a project in the host environment.
        /// </summary>
        protected internal void OnAnalyzerReferenceRemoved(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckProjectIsInCurrentSolution(projectId);
                CheckProjectHasAnalyzerReference(projectId, analyzerReference);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.RemoveAnalyzerReference(projectId, analyzerReference));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.ProjectChanged, oldSolution, newSolution, projectId);
            }
        }

        /// <summary>
        /// Call this method when a document is added to a project in the host environment.
        /// </summary>
        protected internal void OnDocumentAdded(DocumentInfo documentInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var documentId = documentInfo.Id;

                CheckProjectIsInCurrentSolution(documentId.ProjectId);
                CheckDocumentIsNotInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddDocument(documentInfo));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentAdded, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when a document is reloaded in the host environment.
        /// </summary>
        protected internal void OnDocumentReloaded(DocumentInfo newDocumentInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var documentId = newDocumentInfo.Id;

                CheckProjectIsInCurrentSolution(documentId.ProjectId);
                CheckDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.RemoveDocument(documentId).AddDocument(newDocumentInfo));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentReloaded, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when a document is removed from a project in the host environment.
        /// </summary>
        protected internal void OnDocumentRemoved(DocumentId documentId)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckDocumentIsInCurrentSolution(documentId);

                this.CheckDocumentCanBeRemoved(documentId);

                var oldSolution = this.CurrentSolution;

                this.ClearDocumentData(documentId);

                var newSolution = this.SetCurrentSolution(oldSolution.RemoveDocument(documentId));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentRemoved, oldSolution, newSolution, documentId: documentId);
            }
        }

        protected virtual void CheckDocumentCanBeRemoved(DocumentId documentId)
        {
            CheckDocumentIsClosed(documentId);
        }

        /// <summary>
        /// Call this method when the text of a document is changed on disk.
        /// </summary>
        protected internal void OnDocumentTextLoaderChanged(DocumentId documentId, TextLoader loader)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;

                var newSolution = oldSolution.WithDocumentTextLoader(documentId, loader, PreservationMode.PreserveValue);
                newSolution = this.SetCurrentSolution(newSolution);

                var newDocument = newSolution.GetDocument(documentId);
                this.OnDocumentTextChanged(newDocument);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentChanged, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when the text of a document is changed on disk.
        /// </summary>
        protected internal void OnAdditionalDocumentTextLoaderChanged(DocumentId documentId, TextLoader loader)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckAdditionalDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;

                var newSolution = oldSolution.WithAdditionalDocumentTextLoader(documentId, loader, PreservationMode.PreserveValue);
                newSolution = this.SetCurrentSolution(newSolution);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.AdditionalDocumentChanged, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when the text of a document is updated in the host environment.
        /// </summary>
        protected internal void OnDocumentTextChanged(DocumentId documentId, SourceText newText, PreservationMode mode)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithDocumentText(documentId, newText, mode));

                var newDocument = newSolution.GetDocument(documentId);
                this.OnDocumentTextChanged(newDocument);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentChanged, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when the text of a document is updated in the host environment.
        /// </summary>
        protected internal void OnAdditionalDocumentTextChanged(DocumentId documentId, SourceText newText, PreservationMode mode)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckAdditionalDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithAdditionalDocumentText(documentId, newText, mode));

                var newDocument = newSolution.GetAdditionalDocument(documentId);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.AdditionalDocumentChanged, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when the SourceCodeKind of a document changes in the host environment.
        /// </summary>
        protected internal void OnDocumentSourceCodeKindChanged(DocumentId documentId, SourceCodeKind sourceCodeKind)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckDocumentIsInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.WithDocumentSourceCodeKind(documentId, sourceCodeKind));

                var newDocument = newSolution.GetDocument(documentId);
                this.OnDocumentTextChanged(newDocument);

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.DocumentChanged, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when an additional document is added to a project in the host environment.
        /// </summary>
        protected internal void OnAdditionalDocumentAdded(DocumentInfo documentInfo)
        {
            using (this.serializationLock.DisposableWait())
            {
                var documentId = documentInfo.Id;

                CheckProjectIsInCurrentSolution(documentId.ProjectId);
                CheckDocumentIsNotInCurrentSolution(documentId);

                var oldSolution = this.CurrentSolution;
                var newSolution = this.SetCurrentSolution(oldSolution.AddAdditionalDocument(documentInfo));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.AdditionalDocumentAdded, oldSolution, newSolution, documentId: documentId);
            }
        }

        /// <summary>
        /// Call this method when an additional document is removed from a project in the host environment.
        /// </summary>
        protected internal void OnAdditionalDocumentRemoved(DocumentId documentId)
        {
            using (this.serializationLock.DisposableWait())
            {
                CheckAdditionalDocumentIsInCurrentSolution(documentId);

                this.CheckDocumentCanBeRemoved(documentId);

                var oldSolution = this.CurrentSolution;

                this.ClearDocumentData(documentId);

                var newSolution = this.SetCurrentSolution(oldSolution.RemoveAdditionalDocument(documentId));

                this.RaiseWorkspaceChangedEventAsync(WorkspaceChangeKind.AdditionalDocumentRemoved, oldSolution, newSolution, documentId: documentId);
            }
        }

        #endregion

        #region Apply Changes

        /// <summary>
        /// Determines if the specific kind of change is supported by the <see cref="TryApplyChanges"/> method.
        /// </summary>
        public virtual bool CanApplyChange(ApplyChangesKind feature)
        {
            return false;
        }

        /// <summary>
        /// Apply changes made to a solution back to the workspace.
        /// 
        /// The specified solution must be one that originated from this workspace. If it is not, or the workspace
        /// has been updated since the solution was obtained from the workspace, then this method returns false.
        /// </summary>
        public virtual bool TryApplyChanges(Solution newSolution)
        {
            using (Logger.LogBlock(FunctionId.Workspace_ApplyChanges, CancellationToken.None))
            {
                // If solution did not originate from this workspace then fail
                if (newSolution.Workspace != this)
                {
                    return false;
                }

                Solution oldSolution = this.CurrentSolution;

                // If the workspace has already accepted an update, then fail
                if (newSolution.WorkspaceVersion != oldSolution.WorkspaceVersion)
                {
                    return false;
                }

                // make sure that newSolution is a branch of the current solution

                // the given solution must be a branched one.
                // otherwise, there should be no change to apply.
                if (oldSolution.BranchId == newSolution.BranchId)
                {
                    CheckNoChanges(oldSolution, newSolution);
                    return true;
                }

                var solutionChanges = newSolution.GetChanges(oldSolution);
                this.CheckAllowedSolutionChanges(solutionChanges);

                var solutionWithLinkedFileChangesMerged = newSolution.WithMergedLinkedFileChangesAsync(oldSolution, solutionChanges, CancellationToken.None).Result;
                solutionChanges = solutionWithLinkedFileChangesMerged.GetChanges(oldSolution);

                // process all project changes
                foreach (var projectChanges in solutionChanges.GetProjectChanges())
                {
                    this.ApplyProjectChanges(projectChanges);
                }

                return true;
            }
        }

        private void CheckAllowedSolutionChanges(SolutionChanges solutionChanges)
        {
            if (solutionChanges.GetRemovedProjects().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveProject))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingProjectsNotSupported);
            }

            if (solutionChanges.GetAddedProjects().Any() && !this.CanApplyChange(ApplyChangesKind.AddProject))
            {
                throw new NotSupportedException(WorkspacesResources.AddingProjectsNotSupported);
            }

            foreach (var projectChanges in solutionChanges.GetProjectChanges())
            {
                this.CheckAllowedProjectChanges(projectChanges);
            }
        }

        private void CheckAllowedProjectChanges(ProjectChanges projectChanges)
        {
            if (projectChanges.GetAddedDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.AddDocument))
            {
                throw new NotSupportedException(WorkspacesResources.AddingDocumentsNotSupported);
            }

            if (projectChanges.GetRemovedDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveDocument))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingDocumentsNotSupported);
            }

            if (projectChanges.GetChangedDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.ChangeDocument))
            {
                throw new NotSupportedException(WorkspacesResources.ChangingDocumentsNotSupported);
            }

            if (projectChanges.GetAddedAdditionalDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.AddAdditionalDocument))
            {
                throw new NotSupportedException(WorkspacesResources.AddingAdditionalDocumentsNotSupported);
            }

            if (projectChanges.GetRemovedAdditionalDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveAdditionalDocument))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingAdditionalDocumentsIsNotSupported);
            }

            if (projectChanges.GetChangedAdditionalDocuments().Any() && !this.CanApplyChange(ApplyChangesKind.ChangeAdditionalDocument))
            {
                throw new NotSupportedException(WorkspacesResources.ChangingAdditionalDocumentsIsNotSupported);
            }

            if (projectChanges.GetAddedProjectReferences().Any() && !this.CanApplyChange(ApplyChangesKind.AddProjectReference))
            {
                throw new NotSupportedException(WorkspacesResources.AddingProjectReferencesNotSupported);
            }

            if (projectChanges.GetRemovedProjectReferences().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveProjectReference))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingProjectReferencesNotSupported);
            }

            if (projectChanges.GetAddedMetadataReferences().Any() && !this.CanApplyChange(ApplyChangesKind.AddMetadataReference))
            {
                throw new NotSupportedException(WorkspacesResources.AddingProjectReferencesNotSupported);
            }

            if (projectChanges.GetRemovedMetadataReferences().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveMetadataReference))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingProjectReferencesNotSupported);
            }

            if (projectChanges.GetAddedAnalyzerReferences().Any() && !this.CanApplyChange(ApplyChangesKind.AddAnalyzerReference))
            {
                throw new NotSupportedException(WorkspacesResources.AddingAnalyzerReferencesNotSupported);
            }

            if (projectChanges.GetRemovedAnalyzerReferences().Any() && !this.CanApplyChange(ApplyChangesKind.RemoveAnalyzerReference))
            {
                throw new NotSupportedException(WorkspacesResources.RemovingAnalyzerReferencesNotSupported);
            }
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> for each project that has been added, removed or changed.
        /// 
        /// Override this method if you want to modify how project changes are applied.
        /// </summary>
        protected virtual void ApplyProjectChanges(ProjectChanges projectChanges)
        {
            // removed project references
            foreach (var removedProjectReference in projectChanges.GetRemovedProjectReferences())
            {
                this.RemoveProjectReference(projectChanges.ProjectId, removedProjectReference);
            }

            // added project references
            foreach (var addedProjectReference in projectChanges.GetAddedProjectReferences())
            {
                this.AddProjectReference(projectChanges.ProjectId, addedProjectReference);
            }

            // removed metadata references
            foreach (var metadata in projectChanges.GetRemovedMetadataReferences())
            {
                this.RemoveMetadataReference(projectChanges.ProjectId, metadata);
            }

            // added metadata references
            foreach (var metadata in projectChanges.GetAddedMetadataReferences())
            {
                this.AddMetadataReference(projectChanges.ProjectId, metadata);
            }

            // removed analyzer references
            foreach (var analyzerReference in projectChanges.GetRemovedAnalyzerReferences())
            {
                this.RemoveAnalyzerReference(projectChanges.ProjectId, analyzerReference);
            }

            // added analyzer references
            foreach (var analyzerReference in projectChanges.GetAddedAnalyzerReferences())
            {
                this.AddAnalyzerReference(projectChanges.ProjectId, analyzerReference);
            }

            // removed documents
            foreach (var documentId in projectChanges.GetRemovedDocuments())
            {
                this.RemoveDocument(documentId);
            }

            // removed documents
            foreach (var documentId in projectChanges.GetRemovedAdditionalDocuments())
            {
                this.RemoveAdditionalDocument(documentId);
            }

            // added documents
            foreach (var documentId in projectChanges.GetAddedDocuments())
            {
                var doc = projectChanges.NewProject.GetDocument(documentId);
                var text = doc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None); // needs wait
                var info = DocumentInfo.Create(
                    documentId, 
                    doc.Name, 
                    doc.Folders, 
                    doc.SourceCodeKind, 
                    defaultEncoding: text.Encoding);
                this.AddDocument(info, text);
            }

            // added documents
            foreach (var documentId in projectChanges.GetAddedAdditionalDocuments())
            {
                var doc = projectChanges.NewProject.GetAdditionalDocument(documentId);
                var text = doc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None); // needs wait
                var info = DocumentInfo.Create(
                    documentId, 
                    doc.Name, 
                    doc.Folders, 
                    defaultEncoding: text.Encoding);
                this.AddAdditionalDocument(info, text);
            }

            // changed documents
            foreach (var documentId in projectChanges.GetChangedDocuments())
            {
                var oldDoc = projectChanges.OldProject.GetDocument(documentId);
                var newDoc = projectChanges.NewProject.GetDocument(documentId);

                // see whether we can get oldText
                SourceText oldText;
                if (!oldDoc.TryGetText(out oldText))
                {
                    // we can't get old text, there is not much we can do except replacing whole text.
                    var currentText = newDoc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None); // needs wait
                    this.ChangedDocumentText(documentId, currentText);
                    continue;
                }

                // see whether we can get new text
                SourceText newText;
                if (!newDoc.TryGetText(out newText))
                {
                    // okay, we have old text, but no new text. let document determine text changes
                    var textChanges = newDoc.GetTextChangesAsync(oldDoc, CancellationToken.None).WaitAndGetResult(CancellationToken.None); // needs wait
                    this.ChangedDocumentText(documentId, oldText.WithChanges(textChanges));
                    continue;
                }

                // we have both old and new text, just update using the new text.
                this.ChangedDocumentText(documentId, newText);
            }

            // changed additional documents
            foreach (var documentId in projectChanges.GetChangedAdditionalDocuments())
            {
                var oldDoc = projectChanges.OldProject.GetAdditionalDocument(documentId);
                var newDoc = projectChanges.NewProject.GetAdditionalDocument(documentId);

                // We don't understand the text of additional documents and so we just replace the entire text.
                var currentText = newDoc.GetTextAsync(CancellationToken.None).WaitAndGetResult(CancellationToken.None); // needs wait
                this.ChangedAdditionalDocumentText(documentId, currentText);
            }
        }

        [Conditional("DEBUG")]
        private void CheckNoChanges(Solution oldSolution, Solution newSolution)
        {
            var changes = newSolution.GetChanges(oldSolution);
            Contract.ThrowIfTrue(changes.GetAddedProjects().Any());
            Contract.ThrowIfTrue(changes.GetRemovedProjects().Any());
            Contract.ThrowIfTrue(changes.GetProjectChanges().Any());
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to add a project reference to a project.
        /// 
        /// Override this method to implement the capability of adding project references.
        /// </summary>
        protected virtual void AddProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.AddProjectReference));
            this.OnProjectReferenceAdded(projectId, projectReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to remove a project reference from a project.
        /// 
        /// Override this method to implement the capability of removing project references.
        /// </summary>
        protected virtual void RemoveProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.RemoveProjectReference));
            this.OnProjectReferenceRemoved(projectId, projectReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to add a metadata reference to a project.
        /// 
        /// Override this method to implement the capability of adding metadata references.
        /// </summary>
        protected virtual void AddMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.AddMetadataReference));
            this.OnMetadataReferenceAdded(projectId, metadataReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to remove a metadata reference from a project.
        /// 
        /// Override this method to implement the capability of removing metadata references.
        /// </summary>
        protected virtual void RemoveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.RemoveMetadataReference));
            this.OnMetadataReferenceRemoved(projectId, metadataReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to add an analyzer reference to a project.
        /// 
        /// Override this method to implement the capability of adding analyzer references.
        /// </summary>
        protected virtual void AddAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.AddAnalyzerReference));
            this.OnAnalyzerReferenceAdded(projectId, analyzerReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to remove an analyzer reference from a project.
        /// 
        /// Override this method to implement the capability of removing analyzer references.
        /// </summary>
        protected virtual void RemoveAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.RemoveAnalyzerReference));
            this.OnAnalyzerReferenceRemoved(projectId, analyzerReference);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to add a new document to a project.
        /// 
        /// Override this method to implement the capability of adding documents.
        /// </summary>
        protected virtual void AddDocument(DocumentInfo info, SourceText text)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.AddDocument));
            this.OnDocumentAdded(info.WithTextLoader(TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create()))));
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to remove a document from a project.
        /// 
        /// Override this method to implement the capability of removing documents.
        /// </summary>
        protected virtual void RemoveDocument(DocumentId documentId)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.RemoveDocument));
            this.OnDocumentRemoved(documentId);
        }

        /// <summary>
        /// This method is called to change the text of a document.
        /// 
        /// Override this method to implement the capability of changing document text.
        /// </summary>
        protected virtual void ChangedDocumentText(DocumentId id, SourceText text)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.ChangeDocument));
            this.OnDocumentTextChanged(id, text, PreservationMode.PreserveValue);
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to add a new additional document to a project.
        /// 
        /// Override this method to implement the capability of adding additional documents.
        /// </summary>
        protected virtual void AddAdditionalDocument(DocumentInfo info, SourceText text)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.AddAdditionalDocument));
            this.OnAdditionalDocumentAdded(info.WithTextLoader(TextLoader.From(TextAndVersion.Create(text, VersionStamp.Create()))));
        }

        /// <summary>
        /// This method is called during <see cref="TryApplyChanges"/> to remove an additional document from a project.
        /// 
        /// Override this method to implement the capability of removing additional documents.
        /// </summary>
        protected virtual void RemoveAdditionalDocument(DocumentId documentId)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.RemoveAdditionalDocument));
            this.OnAdditionalDocumentRemoved(documentId);
        }

        /// <summary>
        /// This method is called to change the text of an additional document.
        /// 
        /// Override this method to implement the capability of changing additional document text.
        /// </summary>
        protected virtual void ChangedAdditionalDocumentText(DocumentId id, SourceText text)
        {
            Debug.Assert(CanApplyChange(ApplyChangesKind.ChangeAdditionalDocument));
            this.OnAdditionalDocumentTextChanged(id, text, PreservationMode.PreserveValue);
        }

        #endregion

        #region Checks and Asserts
        /// <summary>
        /// Throws an exception is the solution is not empty.
        /// </summary>
        protected void CheckSolutionIsEmpty()
        {
            if (this.CurrentSolution.ProjectIds.Any())
            {
                throw new ArgumentException(WorkspacesResources.WorkspaceIsNotEmpty);
            }
        }

        /// <summary>
        /// Throws an exception if the project is not part of the current solution.
        /// </summary>
        protected void CheckProjectIsInCurrentSolution(ProjectId projectId)
        {
            if (!this.CurrentSolution.ContainsProject(projectId))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentNotInWorkspace,
                    this.GetProjectName(projectId)));
            }
        }

        /// <summary>
        /// Throws an exception is the project is part of the current solution.
        /// </summary>
        protected void CheckProjectIsNotInCurrentSolution(ProjectId projectId)
        {
            if (this.CurrentSolution.ContainsProject(projectId))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentAlreadyInWorkspace,
                    this.GetProjectName(projectId)));
            }
        }

        /// <summary>
        /// Throws an exception if a project does not have a specific project reference.
        /// </summary>
        protected void CheckProjectHasProjectReference(ProjectId fromProjectId, ProjectReference projectReference)
        {
            if (!this.CurrentSolution.GetProject(fromProjectId).ProjectReferences.Contains(projectReference))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectNotReferenced,
                    this.GetProjectName(projectReference.ProjectId)));
            }
        }

        /// <summary>
        /// Throws an exception if a project already has a specific project reference.
        /// </summary>
        protected void CheckProjectDoesNotHaveProjectReference(ProjectId fromProjectId, ProjectReference projectReference)
        {
            if (this.CurrentSolution.GetProject(fromProjectId).ProjectReferences.Contains(projectReference))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectAlreadyReferenced,
                    this.GetProjectName(projectReference.ProjectId)));
            }
        }

        /// <summary>
        /// Throws an exception if project has a transitive reference to another project.
        /// </summary>
        protected void CheckProjectDoesNotHaveTransitiveProjectReference(ProjectId fromProjectId, ProjectId toProjectId)
        {
            var transitiveReferences = GetTransitiveProjectReferences(toProjectId);
            if (transitiveReferences.Contains(fromProjectId))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.CausesCircularProjectReference,
                    this.GetProjectName(fromProjectId), this.GetProjectName(toProjectId)));
            }
        }

        private ISet<ProjectId> GetTransitiveProjectReferences(ProjectId project, ISet<ProjectId> projects = null)
        {
            projects = projects ?? new HashSet<ProjectId>();
            if (projects.Add(project))
            {
                this.CurrentSolution.GetProject(project).ProjectReferences.Do(p => GetTransitiveProjectReferences(p.ProjectId, projects));
            }

            return projects;
        }

        /// <summary>
        /// Throws an exception if a project does not have a specific metadata reference.
        /// </summary>
        protected void CheckProjectHasMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            if (!this.CurrentSolution.GetProject(projectId).MetadataReferences.Contains(metadataReference))
            {
                throw new ArgumentException(WorkspacesResources.MetadataIsNotReferenced);
            }
        }

        /// <summary>
        /// Throws an exception if a project already has a specific metadata reference.
        /// </summary>
        protected void CheckProjectDoesNotHaveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            if (this.CurrentSolution.GetProject(projectId).MetadataReferences.Contains(metadataReference))
            {
                throw new ArgumentException(WorkspacesResources.MetadataIsAlreadyReferenced);
            }
        }

        /// <summary>
        /// Throws an exception if a project does not have a specific analyzer reference.
        /// </summary>
        protected void CheckProjectHasAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            if (!this.CurrentSolution.GetProject(projectId).AnalyzerReferences.Contains(analyzerReference))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.AnalyzerIsNotPresent, analyzerReference));
            }
        }

        /// <summary>
        /// Throws an exception if a project already has a specific analyzer reference.
        /// </summary>
        protected void CheckProjectDoesNotHaveAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            if (this.CurrentSolution.GetProject(projectId).AnalyzerReferences.Contains(analyzerReference))
            {
                throw new ArgumentException(string.Format(WorkspacesResources.AnalyzerIsAlreadyPresent, analyzerReference));
            }
        }

        /// <summary>
        /// Throws an exception if a document is not part of the current solution.
        /// </summary>
        protected void CheckDocumentIsInCurrentSolution(DocumentId documentId)
        {
            if (this.CurrentSolution.GetDocument(documentId) == null)
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentNotInWorkspace,
                    this.GetDocumentName(documentId)));
            }
        }

        /// <summary>
        /// Throws an exception if an additional document is not part of the current solution.
        /// </summary>
        protected void CheckAdditionalDocumentIsInCurrentSolution(DocumentId documentId)
        {
            if (this.CurrentSolution.GetAdditionalDocument(documentId) == null)
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentNotInWorkspace,
                    this.GetDocumentName(documentId)));
            }
        }

        /// <summary>
        /// Throws an exception if a document is already part of the current solution.
        /// </summary>
        protected void CheckDocumentIsNotInCurrentSolution(DocumentId documentId)
        {
            if (this.CurrentSolution.ContainsDocument(documentId))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentAlreadyInWorkspace,
                    this.GetDocumentName(documentId)));
            }
        }

        /// <summary>
        /// Throws an exception if an additional document is already part of the current solution.
        /// </summary>
        protected void CheckAdditionalDocumentIsNotInCurrentSolution(DocumentId documentId)
        {
            if (this.CurrentSolution.ContainsAdditionalDocument(documentId))
            {
                throw new ArgumentException(string.Format(
                    WorkspacesResources.ProjectOrDocumentAlreadyInWorkspace,
                    this.GetAdditionalDocumentName(documentId)));
            }
        }

        /// <summary>
        /// Gets the name to use for a project in an error message.
        /// </summary>
        protected virtual string GetProjectName(ProjectId projectId)
        {
            var project = this.CurrentSolution.GetProject(projectId);
            var name = project != null ? project.Name : "<Project" + projectId.Id + ">";
            return name;
        }

        /// <summary>
        /// Gets the name to use for a document in an error message.
        /// </summary>
        protected virtual string GetDocumentName(DocumentId documentId)
        {
            var document = this.CurrentSolution.GetDocument(documentId);
            var name = document != null ? document.Name : "<Document" + documentId.Id + ">";
            return name;
        }

        /// <summary>
        /// Gets the name to use for an additional document in an error message.
        /// </summary>
        protected virtual string GetAdditionalDocumentName(DocumentId documentId)
        {
            var document = this.CurrentSolution.GetAdditionalDocument(documentId);
            var name = document != null ? document.Name : "<Document" + documentId.Id + ">";
            return name;
        }

        #endregion
    }
}