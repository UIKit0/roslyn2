﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Roslyn.Collections.Immutable;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Represents a set of projects and their source code documents. 
    /// </summary>
    public partial class Solution
    {
        // branch id for this solution
        private readonly BranchId branchId;

        // the version of the workspace this solution is from
        private readonly int workspaceVersion;

        private readonly SolutionServices solutionServices;
        private readonly SolutionId id;
        private readonly string filePath;
        private readonly IReadOnlyList<ProjectId> projectIds;
        private readonly ImmutableDictionary<ProjectId, ProjectState> projectIdToProjectStateMap;
        private readonly ImmutableDictionary<string, ImmutableArray<DocumentId>> linkedFilesMap;
        private readonly VersionStamp version;
        private readonly Lazy<VersionStamp> lazyLatestProjectVersion;
        private readonly ProjectDependencyGraph dependencyGraph;

        // Values for all these are created on demand.
        private ImmutableHashMap<ProjectId, Project> projectIdToProjectMap;
        private ImmutableDictionary<ProjectId, CompilationTracker> projectIdToTrackerMap;

        private Solution(
            BranchId branchId,
            int workspaceVersion,
            SolutionServices solutionServices,
            SolutionId id,
            string filePath,
            IEnumerable<ProjectId> projectIds,
            ImmutableDictionary<ProjectId, ProjectState> idToProjectStateMap,
            ImmutableDictionary<ProjectId, CompilationTracker> projectIdToTrackerMap,
            ImmutableDictionary<string, ImmutableArray<DocumentId>> linkedFilesMap,
            ProjectDependencyGraph dependencyGraph,
            VersionStamp version,
            Lazy<VersionStamp> lazyLatestProjectVersion)
        {
            this.branchId = branchId;
            this.workspaceVersion = workspaceVersion;
            this.id = id;
            this.filePath = filePath;
            this.solutionServices = solutionServices;
            this.projectIds = projectIds.ToImmutableReadOnlyListOrEmpty();
            this.projectIdToProjectStateMap = idToProjectStateMap;
            this.projectIdToTrackerMap = projectIdToTrackerMap;
            this.linkedFilesMap = linkedFilesMap;
            this.dependencyGraph = dependencyGraph;
            this.projectIdToProjectMap = ImmutableHashMap<ProjectId, Project>.Empty;
            this.version = version;
            this.lazyLatestProjectVersion = lazyLatestProjectVersion;

            CheckInvariants();
        }

        internal Solution(
            Workspace workspace,
            SolutionInfo info)
            : this(
                workspace.PrimaryBranchId,
                workspaceVersion: 0,
                solutionServices: new SolutionServices(workspace),
                id: info.Id,
                filePath: info.FilePath,
                version: info.Version,
                projectIds: null,
                idToProjectStateMap: ImmutableDictionary<ProjectId, ProjectState>.Empty,
                projectIdToTrackerMap: ImmutableDictionary<ProjectId, CompilationTracker>.Empty,
                linkedFilesMap: ImmutableDictionary.Create<string, ImmutableArray<DocumentId>>(StringComparer.OrdinalIgnoreCase),
                dependencyGraph: ProjectDependencyGraph.Empty,
                lazyLatestProjectVersion: null)
        {
            this.lazyLatestProjectVersion = new Lazy<VersionStamp>(() => ComputeLatestProjectVersion());
        }

        internal Solution WithNewWorkspace(Workspace workspace, int workspaceVersion)
        {
            var services = workspace != this.solutionServices.Workspace
                ? new SolutionServices(workspace)
                : this.solutionServices;

            // Note: this will potentially have problems if the workspace services are different, as some services
            // get locked-in by document states and project states when first constructed.
            return CreatePrimarySolution(branchId: workspace.PrimaryBranchId, workspaceVersion: workspaceVersion, services: services);
        }

        private VersionStamp ComputeLatestProjectVersion()
        {
            // this may produce a version that is out of sync with the actual Document versions.
            var latestVersion = VersionStamp.Default;
            foreach (var projectId in this.ProjectIds)
            {
                var project = this.GetProject(projectId);
                latestVersion = project.Version.GetNewerVersion(latestVersion);
            }

            return latestVersion;
        }

        internal int WorkspaceVersion
        {
            get { return this.workspaceVersion; }
        }

        internal SolutionServices Services
        {
            get { return this.solutionServices; }
        }

        /// <summary>
        /// branch id of this solution
        /// 
        /// currently, it only supports one level of branching. there is a primary branch of a workspace and all other
        /// branches that are branched from the primary branch.
        /// 
        /// one still can create multiple forked solutions from an already branched solution, but versions among those
        /// can't be reliably used and compared. 
        /// 
        /// version only has a meaning between primary solution and branched one or between solutions from same branch.
        /// </summary>
        internal BranchId BranchId
        {
            get { return this.branchId; }
        }

        /// <summary>
        /// The Workspace this solution is associated with.
        /// </summary>
        public Workspace Workspace
        {
            get { return this.solutionServices.Workspace; }
        }

        /// <summary>
        /// The Id of the solution. Multiple solution instances may share the same Id.
        /// </summary>
        public SolutionId Id
        {
            get { return this.id; }
        }

        /// <summary>
        /// The path to the solution file or null if there is no solution file.
        /// </summary>
        public string FilePath
        {
            get { return this.filePath; }
        }

        /// <summary>
        /// The solution version. This equates to the solution file's version.
        /// </summary>
        public VersionStamp Version
        {
            get { return this.version; }
        }

        /// <summary>
        /// A list of all the ids for all the projects contained by the solution.
        /// </summary>
        public IReadOnlyList<ProjectId> ProjectIds
        {
            get { return this.projectIds; }
        }

        /// <summary>
        /// A list of all the projects contained by the solution.
        /// </summary>
        public IEnumerable<Project> Projects
        {
            get { return this.projectIds.Select(id => GetProject(id)); }
        }

        // [Conditional("DEBUG")]
        private void CheckInvariants()
        {
            Contract.ThrowIfTrue(projectIds.Count != projectIdToProjectStateMap.Count);

            // An id shouldn't point at a tracker for a different project.
            Contract.ThrowIfTrue(projectIdToTrackerMap.Any(kvp => kvp.Key != kvp.Value.ProjectState.Id));
        }

        private Solution Branch(
            IEnumerable<ProjectId> projectIds = null,
            ImmutableDictionary<ProjectId, ProjectState> idToProjectStateMap = null,
            ImmutableDictionary<ProjectId, CompilationTracker> projectIdToTrackerMap = null,
            ImmutableDictionary<string, ImmutableArray<DocumentId>> linkedFilesMap = null,
            ProjectDependencyGraph dependencyGraph = null,
            VersionStamp? version = default(VersionStamp?),
            Lazy<VersionStamp> lazyLatestProjectVersion = null)
        {
            var branchId = GetBranchId();

            projectIds = projectIds ?? this.projectIds;
            idToProjectStateMap = idToProjectStateMap ?? this.projectIdToProjectStateMap;
            projectIdToTrackerMap = projectIdToTrackerMap ?? this.projectIdToTrackerMap;
            linkedFilesMap = linkedFilesMap ?? this.linkedFilesMap;
            dependencyGraph = dependencyGraph ?? this.dependencyGraph;
            version = version.HasValue ? version.Value : this.version;
            lazyLatestProjectVersion = lazyLatestProjectVersion ?? this.lazyLatestProjectVersion;

            if (branchId == this.branchId &&
                projectIds == this.projectIds &&
                idToProjectStateMap == this.projectIdToProjectStateMap &&
                projectIdToTrackerMap == this.projectIdToTrackerMap &&
                linkedFilesMap == this.linkedFilesMap &&
                dependencyGraph == this.dependencyGraph &&
                version == this.version &&
                lazyLatestProjectVersion == this.lazyLatestProjectVersion)
            {
                return this;
            }

            return new Solution(
                branchId,
                this.workspaceVersion,
                this.solutionServices,
                this.id,
                this.filePath,
                projectIds,
                idToProjectStateMap,
                projectIdToTrackerMap,
                linkedFilesMap,
                dependencyGraph,
                version.Value,
                lazyLatestProjectVersion);
        }

        private Solution CreatePrimarySolution(
            BranchId branchId,
            int workspaceVersion,
            SolutionServices services)
        {
            if (branchId == this.branchId &&
                workspaceVersion == this.workspaceVersion &&
                services == this.solutionServices)
            {
                return this;
            }

            return new Solution(
                branchId,
                workspaceVersion,
                services,
                this.id,
                this.filePath,
                this.projectIds,
                this.projectIdToProjectStateMap,
                this.projectIdToTrackerMap,
                this.linkedFilesMap,
                this.dependencyGraph,
                this.version,
                this.lazyLatestProjectVersion);
        }

        private BranchId GetBranchId()
        {
            // currently we only support one level branching. 
            // my reasonings are
            // 1. it seems there is no one who needs sub branches.
            // 2. this lets us to branch without explicit branch API
            return this.branchId == Workspace.PrimaryBranchId ? BranchId.GetNextId() : this.branchId;
        }

        /// <summary>
        /// The version of the most recently modified project.
        /// </summary>
        public VersionStamp GetLatestProjectVersion()
        {
            return this.lazyLatestProjectVersion.Value;
        }

        /// <summary>
        /// True if the solution contains a project with the specified project ID.
        /// </summary>
        public bool ContainsProject(ProjectId projectId)
        {
            return projectId != null && this.projectIdToProjectStateMap.ContainsKey(projectId);
        }

        /// <summary>
        /// Gets the project in this solution with the specified project ID. 
        /// 
        /// If the id is not an id of a project that is part of this solution the method returns null.
        /// </summary>
        public Project GetProject(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (this.ContainsProject(projectId))
            {
                return ImmutableHashMapExtensions.GetOrAdd(ref projectIdToProjectMap, projectId, createProjectFunction, this);
            }

            return null;
        }

        private static readonly Func<ProjectId, Solution, Project> createProjectFunction = CreateProject;
        private static Project CreateProject(ProjectId projectId, Solution solution)
        {
            return new Project(solution, solution.GetProjectState(projectId));
        }

        /// <summary>
        /// Gets the <see cref="Project"/> associated with an assembly symbol.
        /// </summary>
        public Project GetProject(IAssemblySymbol assemblySymbol, CancellationToken cancellationToken = default(CancellationToken))
        {
            Compilation compilation;
            ProjectId id;

            // The symbol must be from one of the compilations already built.
            // if the symbol is a source symbol then one of the compilations must be its source
            // if the symbol is metadata then one of the compilations must have a reference to it's metadata assembly.
            foreach (var state in this.projectIdToProjectStateMap.Values)
            {
                if (this.TryGetCompilation(state.Id, out compilation))
                {
                    // if the symbol is the compilation's assembly symbol, we are done
                    if (compilation.Assembly == assemblySymbol)
                    {
                        return this.GetProject(state.Id);
                    }

                    // otherwise check to see if this compilation has a metadata reference for this assembly symbol
                    // and if we know what project that metadata reference is associated with.
                    var mdref = compilation.GetMetadataReference(assemblySymbol);
                    if (mdref != null && metadataReferenceToProjectMap.TryGetValue(mdref, out id))
                    {
                        return this.GetProject(id);
                    }
                }
            }

            // no project was found in this solution to be associated with this assembly symbol
            return null;
        }

        /// <summary>
        /// True if the solution contains the document in one of its projects
        /// </summary>
        public bool ContainsDocument(DocumentId documentId)
        {
            return
                documentId != null &&
                this.ContainsProject(documentId.ProjectId) &&
                this.GetProjectState(documentId.ProjectId).ContainsDocument(documentId);
        }

        /// <summary>
        /// True if the solution contains the additional document in one of its projects
        /// </summary>
        public bool ContainsAdditionalDocument(DocumentId documentId)
        {
            return
                documentId != null &&
                this.ContainsProject(documentId.ProjectId) &&
                this.GetProjectState(documentId.ProjectId).ContainsAdditionalDocument(documentId);
        }

        /// <summary>
        /// Gets the documentId in this solution with the specified syntax tree.
        /// </summary>
        public DocumentId GetDocumentId(SyntaxTree syntaxTree)
        {
            return this.GetDocumentId(syntaxTree, null);
        }

        /// <summary>
        /// Gets the documentId in this solution with the specified syntax tree.
        /// </summary>
        public DocumentId GetDocumentId(SyntaxTree syntaxTree, ProjectId projectId)
        {
            if (syntaxTree != null)
            {
                // is this tree known to be associated with a document?
                var documentId = DocumentState.GetDocumentIdForTree(syntaxTree);
                if (documentId != null && (projectId == null || documentId.ProjectId == projectId))
                {
                    // does this solution even have the document?
                    if (this.ContainsDocument(documentId))
                    {
                        return documentId;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the document in this solution with the specified document ID.
        /// </summary>
        public Document GetDocument(DocumentId documentId)
        {
            if (documentId != null && this.ContainsDocument(documentId))
            {
                return this.GetProject(documentId.ProjectId).GetDocument(documentId);
            }

            return null;
        }

        /// <summary>
        /// Gets the additional document in this solution with the specified document ID.
        /// </summary>
        public TextDocument GetAdditionalDocument(DocumentId documentId)
        {
            if (documentId != null && this.ContainsAdditionalDocument(documentId))
            {
                return this.GetProject(documentId.ProjectId).GetAdditionalDocument(documentId);
            }

            return null;
        }

        private DocumentState GetDocumentState(DocumentId documentId)
        {
            if (documentId != null)
            {
                var projectState = this.GetProjectState(documentId.ProjectId);
                if (projectState != null)
                {
                    return projectState.GetDocumentState(documentId);
                }
            }

            return null;
        }

        private TextDocumentState GetAdditionalDocumentState(DocumentId documentId)
        {
            if (documentId != null)
            {
                var projectState = this.GetProjectState(documentId.ProjectId);
                if (projectState != null)
                {
                    return projectState.GetAdditionalDocumentState(documentId);
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the document in this solution with the specified syntax tree.
        /// </summary>
        public Document GetDocument(SyntaxTree syntaxTree)
        {
            return this.GetDocument(syntaxTree, null);
        }

        internal Document GetDocument(SyntaxTree syntaxTree, ProjectId projectId)
        {
            if (syntaxTree != null)
            {
                // is this tree known to be associated with a document?
                var docId = DocumentState.GetDocumentIdForTree(syntaxTree);
                if (docId != null && (projectId == null || docId.ProjectId == projectId))
                {
                    // does this solution even have the document?
                    var document = this.GetDocument(docId);
                    if (document != null)
                    {
                        // does this document really have the syntax tree?
                        SyntaxTree documentTree;
                        if (document.TryGetSyntaxTree(out documentTree) && documentTree == syntaxTree)
                        {
                            return document;
                        }
                    }
                }
            }

            return null;
        }

        internal Task<VersionStamp> GetDependentVersionAsync(ProjectId projectId, CancellationToken cancellationToken)
        {
            return this.GetCompilationTracker(projectId).GetDependentVersionAsync(this, cancellationToken);
        }

        internal Task<VersionStamp> GetDependentSemanticVersionAsync(ProjectId projectId, CancellationToken cancellationToken)
        {
            return this.GetCompilationTracker(projectId).GetDependentSemanticVersionAsync(this, cancellationToken);
        }

        internal ProjectState GetProjectState(ProjectId projectId)
        {
            ProjectState state;
            this.projectIdToProjectStateMap.TryGetValue(projectId, out state);
            return state;
        }

        private bool TryGetCompilationTracker(ProjectId projectId, out CompilationTracker tracker)
        {
            return this.projectIdToTrackerMap.TryGetValue(projectId, out tracker);
        }

        private static readonly Func<ProjectId, Solution, CompilationTracker> createCompilationTrackerFunction = CreateCompilationTracker;
        private static CompilationTracker CreateCompilationTracker(ProjectId projectId, Solution solution)
        {
            return new CompilationTracker(solution.GetProjectState(projectId));
        }

        private CompilationTracker GetCompilationTracker(ProjectId projectId)
        {
            CompilationTracker tracker;
            if (!this.projectIdToTrackerMap.TryGetValue(projectId, out tracker))
            {
                tracker = ImmutableInterlocked.GetOrAdd(ref this.projectIdToTrackerMap, projectId, createCompilationTrackerFunction, this);
            }

            return tracker;
        }

        private Solution AddProject(ProjectId projectId, ProjectState projectState)
        {
            var newProjectIds = this.projectIds.ToImmutableArray().Add(projectId);
            var newStateMap = this.projectIdToProjectStateMap.Add(projectId, projectState);
            var newDependencyGraph = CreateDependencyGraph(newProjectIds, newStateMap);
            var newTrackerMap = CreateCompilationTrackerMap(projectId, newDependencyGraph);
            var newLinkedFilesMap = CreateLinkedFilesMapWithAddedProject(newStateMap[projectId]);

            return this.Branch(
                projectIds: newProjectIds,
                idToProjectStateMap: newStateMap,
                projectIdToTrackerMap: newTrackerMap,
                linkedFilesMap: newLinkedFilesMap,
                dependencyGraph: newDependencyGraph,
                version: this.Version.GetNewerVersion(),  // changed project list so, increment version.
                lazyLatestProjectVersion: new Lazy<VersionStamp>(() => projectState.Version)); // this is the newest!
        }

        /// <summary>
        /// Creates a new solution instance that includes a project with the specified language and names.
        /// Returns the new project.
        /// </summary>
        public Project AddProject(string name, string assemblyName, string language)
        {
            var id = ProjectId.CreateNewId(debugName: name);
            return this.AddProject(id, name, assemblyName, language).GetProject(id);
        }

        /// <summary>
        /// Creates a new solution instance that includes a project with the specified language and names.
        /// </summary>
        public Solution AddProject(ProjectId projectId, string name, string assemblyName, string language)
        {
            return this.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), name, assemblyName, language));
        }

        /// <summary>
        /// Create a new solution instance that includes a project with the specified project information.
        /// </summary>
        public Solution AddProject(ProjectInfo projectInfo)
        {
            if (projectInfo == null)
            {
                throw new ArgumentNullException("projectInfo");
            }

            var projectId = projectInfo.Id;

            var language = projectInfo.Language;
            if (language == null)
            {
                throw new ArgumentNullException("language");
            }

            var displayName = projectInfo.Name;
            if (displayName == null)
            {
                throw new ArgumentNullException("name");
            }

            CheckNotContainsProject(projectId);

            var languageServices = this.Workspace.Services.GetLanguageServices(language);
            if (languageServices == null)
            {
                throw new ArgumentException(string.Format(WorkspacesResources.UnsupportedLanguage, language));
            }

            var newProject = new ProjectState(projectInfo, languageServices, this.solutionServices);

            return this.AddProject(newProject.Id, newProject);
        }

        private ImmutableDictionary<string, ImmutableArray<DocumentId>> CreateLinkedFilesMapWithAddedProject(ProjectState projectState)
        {
            return CreateLinkedFilesMapWithAddedDocuments(projectState, projectState.DocumentIds);
        }

        private ImmutableDictionary<string, ImmutableArray<DocumentId>> CreateLinkedFilesMapWithAddedDocuments(ProjectState projectState, IReadOnlyList<DocumentId> documentIds)
        {
            var builder = this.linkedFilesMap.ToBuilder();

            foreach (var documentId in documentIds)
            {
                var filePath = projectState.GetDocumentState(documentId).FilePath;

                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                ImmutableArray<DocumentId> documentIdsWithPath;
                builder[filePath] = builder.TryGetValue(filePath, out documentIdsWithPath)
                    ? documentIdsWithPath.Add(documentId)
                    : ImmutableArray.Create(documentId);
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Create a new solution instance without the project specified.
        /// </summary>
        public Solution RemoveProject(ProjectId projectId)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            CheckContainsProject(projectId);

            var newProjectIds = this.projectIds.ToImmutableArray().Remove(projectId);
            var newStateMap = this.projectIdToProjectStateMap.Remove(projectId);
            var newDependencyGraph = CreateDependencyGraph(newProjectIds, newStateMap);
            var newTrackerMap = CreateCompilationTrackerMap(projectId, newDependencyGraph);
            var newLinkedFilesMap = CreateLinkedFilesMapWithRemovedProject(this.projectIdToProjectStateMap[projectId]);

            return this.Branch(
                projectIds: newProjectIds,
                idToProjectStateMap: newStateMap,
                projectIdToTrackerMap: newTrackerMap.Remove(projectId),
                linkedFilesMap: newLinkedFilesMap,
                dependencyGraph: newDependencyGraph,
                version: this.Version.GetNewerVersion()); // changed project list, so increment version
        }

        private ImmutableDictionary<string, ImmutableArray<DocumentId>> CreateLinkedFilesMapWithRemovedProject(ProjectState projectState)
        {
            return CreateLinkedFilesMapWithRemovedDocuments(projectState, projectState.DocumentIds);
        }

        private ImmutableDictionary<string, ImmutableArray<DocumentId>> CreateLinkedFilesMapWithRemovedDocuments(
            ProjectState projectState,
            IReadOnlyList<DocumentId> documentIds)
        {
            var builder = this.linkedFilesMap.ToBuilder();

            foreach (var documentId in documentIds)
            {
                var filePath = projectState.GetDocumentState(documentId).FilePath;

                if (string.IsNullOrEmpty(filePath))
                {
                    continue;
                }

                ImmutableArray<DocumentId> documentIdsWithPath;
                if (!builder.TryGetValue(filePath, out documentIdsWithPath) || !documentIdsWithPath.Contains(documentId))
                {
                    throw new ArgumentException("The given documentId was not found in the linkedFilesMap.");
                }

                if (documentIdsWithPath.Length == 1)
                {
                    builder.Remove(filePath);
                }
                else
                {
                    builder[filePath] = documentIdsWithPath.Remove(documentId);
                }
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Creates a new solution instance with the project specified updated to have the new
        /// assembly name.
        /// </summary>
        public Solution WithProjectAssemblyName(ProjectId projectId, string assemblyName)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (assemblyName == null)
            {
                throw new ArgumentNullException("assemblyName");
            }

            CheckContainsProject(projectId);

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.UpdateAssemblyName(assemblyName);

            if (oldProject == newProject)
            {
                return this;
            }

            return this.ForkProject(newProject, CompilationTranslationAction.ProjectAssemblyName(assemblyName));
        }

        /// <summary>
        /// Creates a new solution instance with the project specified updated to have the output file path.
        /// </summary>
        public Solution WithProjectOutputFilePath(ProjectId projectId, string outputFilePath)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            CheckContainsProject(projectId);

            return this.ForkProject(this.GetProjectState(projectId).UpdateOutputPath(outputFilePath));
        }

        /// <summary>
        /// Creates a new solution instance with the project specified updated to have the name.
        /// </summary>
        public Solution WithProjectName(ProjectId projectId, string name)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            CheckContainsProject(projectId);

            return this.ForkProject(this.GetProjectState(projectId).UpdateName(name));
        }

        /// <summary>
        /// Creates a new solution instance with the project specified updated to have the project file path.
        /// </summary>
        public Solution WithProjectFilePath(ProjectId projectId, string filePath)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            CheckContainsProject(projectId);

            return this.ForkProject(this.GetProjectState(projectId).UpdateFilePath(filePath));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to have
        /// the specified compilation options.
        /// </summary>
        public Solution WithProjectCompilationOptions(ProjectId projectId, CompilationOptions options)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (options == null)
            {
                throw new ArgumentNullException("OptionSet");
            }

            CheckContainsProject(projectId);

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.UpdateCompilationOptions(options);

            if (oldProject == newProject)
            {
                return this;
            }

            return this.ForkProject(newProject, CompilationTranslationAction.ProjectCompilationOptions(options));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to have
        /// the specified parse options.
        /// </summary>
        public Solution WithProjectParseOptions(ProjectId projectId, ParseOptions options)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            Contract.Requires(this.ContainsProject(projectId));

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.UpdateParseOptions(options);

            if (oldProject == newProject)
            {
                return this;
            }

            return this.ForkProject(newProject, CompilationTranslationAction.ProjectParseOptions(newProject));
        }

        private static async Task<Compilation> ReplaceSyntaxTreesWithTreesFromNewProjectStateAsync(Compilation compilation, ProjectState projectState, CancellationToken cancellationToken)
        {
            var syntaxTrees = new List<SyntaxTree>(capacity: projectState.DocumentIds.Count);

            foreach (var documentState in projectState.OrderedDocumentStates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                syntaxTrees.Add(await documentState.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false));
            }

            return compilation.RemoveAllSyntaxTrees().AddSyntaxTrees(syntaxTrees);
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include
        /// the specified project reference.
        /// </summary>
        public Solution AddProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (projectReference == null)
            {
                throw new ArgumentNullException("projectReference");
            }

            CheckContainsProject(projectId);
            CheckContainsProject(projectReference.ProjectId);
            CheckNotContainsProjectReference(projectId, projectReference);
            CheckNotContainsTransitiveReference(projectReference.ProjectId, projectId);

            CheckNotSecondSubmissionReference(projectId, projectReference.ProjectId);

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.AddProjectReference(projectReference);

            return this.ForkProject(newProject, withProjectReferenceChange: true);
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include
        /// the specified project references.
        /// </summary>
        public Solution AddProjectReferences(ProjectId projectId, IEnumerable<ProjectReference> projectReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException("projectReferences");
            }

            CheckContainsProject(projectId);

            foreach (var referencedProject in projectReferences)
            {
                CheckContainsProject(referencedProject.ProjectId);
                CheckNotContainsProjectReference(projectId, referencedProject);
                CheckNotContainsTransitiveReference(referencedProject.ProjectId, projectId);
                CheckNotSecondSubmissionReference(projectId, referencedProject.ProjectId);
            }

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.AddProjectReferences(projectReferences);

            return this.ForkProject(newProject, withProjectReferenceChange: true);
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to no longer
        /// include the specified project reference.
        /// </summary>
        public Solution RemoveProjectReference(ProjectId projectId, ProjectReference projectReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (projectReference == null)
            {
                throw new ArgumentNullException("projectReference");
            }

            CheckContainsProject(projectId);
            CheckContainsProject(projectReference.ProjectId);

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.RemoveProjectReference(projectReference);

            return this.ForkProject(newProject, withProjectReferenceChange: true);
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to contain
        /// the specified list of project references.
        /// </summary>
        public Solution WithProjectReferences(ProjectId projectId, IEnumerable<ProjectReference> projectReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (projectReferences == null)
            {
                throw new ArgumentNullException("projectReferences");
            }

            CheckContainsProject(projectId);

            var oldProject = this.GetProjectState(projectId);
            var newProject = oldProject.WithProjectReferences(projectReferences);

            return this.ForkProject(newProject, withProjectReferenceChange: true);
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include the 
        /// specified metadata reference.
        /// </summary>
        public Solution AddMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (metadataReference == null)
            {
                throw new ArgumentNullException("metadataReference");
            }

            CheckContainsProject(projectId);

            return this.ForkProject(
                this.GetProjectState(projectId).AddMetadataReference(metadataReference));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include the
        /// specified metadata references.
        /// </summary>
        public Solution AddMetadataReferences(ProjectId projectId, IEnumerable<MetadataReference> metadataReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (metadataReferences == null)
            {
                throw new ArgumentNullException("metadataReferences");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).AddMetadataReferences(metadataReferences));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to no longer include
        /// the specified metadata reference.
        /// </summary>
        public Solution RemoveMetadataReference(ProjectId projectId, MetadataReference metadataReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (metadataReference == null)
            {
                throw new ArgumentNullException("metadataReference");
            }

            CheckContainsProject(projectId);

            return this.ForkProject(
                this.GetProjectState(projectId).RemoveMetadataReference(metadataReference));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include only the
        /// specified metadata references.
        /// </summary>
        public Solution WithProjectMetadataReferences(ProjectId projectId, IEnumerable<MetadataReference> metadataReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (metadataReferences == null)
            {
                throw new ArgumentNullException("metadataReferences");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).WithMetadataReferences(metadataReferences));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include the 
        /// specified analyzer reference.
        /// </summary>
        public Solution AddAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (analyzerReference == null)
            {
                throw new ArgumentNullException("analyzerReference");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).AddAnalyzerReference(analyzerReference));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include the
        /// specified analyzer references.
        /// </summary>
        public Solution AddAnalyzerReferences(ProjectId projectId, IEnumerable<AnalyzerReference> analyzerReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (analyzerReferences == null)
            {
                throw new ArgumentNullException("analyzerReferences");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).AddAnalyzerReferences(analyzerReferences));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to no longer include
        /// the specified analyzer reference.
        /// </summary>
        public Solution RemoveAnalyzerReference(ProjectId projectId, AnalyzerReference analyzerReference)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (analyzerReference == null)
            {
                throw new ArgumentNullException("analyzerReference");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).RemoveAnalyzerReference(analyzerReference));
        }

        /// <summary>
        /// Create a new solution instance with the project specified updated to include only the
        /// specified analyzer references.
        /// </summary>
        public Solution WithProjectAnalyzerReferences(ProjectId projectId, IEnumerable<AnalyzerReference> analyzerReferences)
        {
            if (projectId == null)
            {
                throw new ArgumentNullException("projectId");
            }

            if (analyzerReferences == null)
            {
                throw new ArgumentNullException("analyzerReferences");
            }

            CheckContainsProject(projectId);
            return this.ForkProject(this.GetProjectState(projectId).WithAnalyzerReferences(analyzerReferences));
        }

        private Solution AddDocument(DocumentState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("document");
            }

            CheckContainsProject(state.Id.ProjectId);

            var oldProject = this.GetProjectState(state.Id.ProjectId);
            var newProject = oldProject.AddDocument(state);

            return this.ForkProject(newProject, CompilationTranslationAction.AddDocument(state), withDocumentListChange: true);
        }

        /// <summary>
        /// Creates a new solution instance with the corresponding project updated to include a new
        /// document instance defined by its name and text.
        /// </summary>
        public Solution AddDocument(DocumentId documentId, string name, string text, IEnumerable<string> folders = null, string filePath = null)
        {
            return this.AddDocument(documentId, name, SourceText.From(text), folders, filePath);
        }

        /// <summary>
        /// Creates a new solution instance with the corresponding project updated to include a new
        /// document instance defined by its name and text.
        /// </summary>
        public Solution AddDocument(DocumentId documentId, string name, SourceText text, IEnumerable<string> folders = null, string filePath = null, bool isGenerated = false)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            CheckContainsProject(documentId.ProjectId);
            CheckNotContainsDocument(documentId);

            if (name == null)
            {
                throw new ArgumentNullException("displayName");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            var project = this.GetProjectState(documentId.ProjectId);

            var version = VersionStamp.Create();
            var loader = TextLoader.From(TextAndVersion.Create(text, version, name));

            var info = DocumentInfo.Create(
                documentId,
                name: name,
                folders: folders,
                sourceCodeKind: project.ParseOptions.Kind,
                loader: loader,
                filePath: filePath,
                isGenerated: isGenerated);

            var doc = DocumentState.Create(
                info,
                project.ParseOptions,
                project.LanguageServices,
                this.solutionServices);

            return this.AddDocument(doc);
        }

        /// <summary>
        /// Creates a new solution instance with the project updated to include a new document with
        /// the arguments specified.
        /// </summary>
        public Solution AddDocument(DocumentId documentId, string name, TextLoader loader, IEnumerable<string> folders = null)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            CheckContainsProject(documentId.ProjectId);
            CheckNotContainsDocument(documentId);

            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            if (loader == null)
            {
                throw new ArgumentNullException("loader");
            }

            var project = this.GetProjectState(documentId.ProjectId);

            var info = DocumentInfo.Create(
                documentId,
                name: name,
                folders: folders,
                sourceCodeKind: project.ParseOptions.Kind,
                loader: loader);

            return this.AddDocument(info);
        }

        /// <summary>
        /// Create a new solution instance with the corresponding project updated to include a new 
        /// document instanced defined by the document info.
        /// </summary>
        public Solution AddDocument(DocumentInfo documentInfo)
        {
            if (documentInfo == null)
            {
                throw new ArgumentNullException("documentInfo");
            }

            CheckContainsProject(documentInfo.Id.ProjectId);
            CheckNotContainsDocument(documentInfo.Id);

            var project = this.GetProjectState(documentInfo.Id.ProjectId);

            var doc = DocumentState.Create(
                documentInfo,
                project.ParseOptions,
                project.LanguageServices,
                this.solutionServices).UpdateSourceCodeKind(documentInfo.SourceCodeKind);

            return this.AddDocument(doc);
        }

        private Solution AddDocument(Document document)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            CheckNotContainsDocument(document.Id);
            CheckContainsProject(document.Id.ProjectId);

            return this.AddDocument(document.State);
        }

        /// <summary>
        /// Creates a new solution instance with the corresponding project updated to include a new
        /// additional document instance defined by its name and text.
        /// </summary>
        public Solution AddAdditionalDocument(DocumentId documentId, string name, SourceText text, IEnumerable<string> folders = null, string filePath = null)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            CheckContainsProject(documentId.ProjectId);
            CheckNotContainsAdditionalDocument(documentId);

            if (name == null)
            {
                throw new ArgumentNullException("displayName");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            var project = this.GetProjectState(documentId.ProjectId);

            var version = VersionStamp.Create();
            var loader = TextLoader.From(TextAndVersion.Create(text, version, name));

            var info = DocumentInfo.Create(
                documentId,
                name: name,
                folders: folders,
                sourceCodeKind: project.ParseOptions.Kind,
                loader: loader,
                filePath: filePath);

            var doc = TextDocumentState.Create(
                info,
                this.solutionServices);

            return this.AddAdditionalDocument(doc);
        }

        public Solution AddAdditionalDocument(DocumentInfo documentInfo)
        {
            if (documentInfo == null)
            {
                throw new ArgumentNullException("documentInfo");
            }

            CheckContainsProject(documentInfo.Id.ProjectId);
            CheckNotContainsAdditionalDocument(documentInfo.Id);

            var project = this.GetProjectState(documentInfo.Id.ProjectId);

            var doc = TextDocumentState.Create(
                documentInfo,
                this.solutionServices);

            return this.AddAdditionalDocument(doc);
        }

        private Solution AddAdditionalDocument(TextDocumentState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("document");
            }

            CheckContainsProject(state.Id.ProjectId);

            var oldProject = this.GetProjectState(state.Id.ProjectId);
            var newProject = oldProject.AddAdditionalDocument(state);

            return this.ForkProject(newProject);
        }

        /// <summary>
        /// Creates a new solution instance that no longer includes the specified document.
        /// </summary>
        [SuppressMessage("Microsoft.StyleCop.CSharp.SpacingRules", "SA1008:OpeningParenthesisMustBeSpacedCorrectly", Justification = "Working around StyleCop bug 7080")]
        public Solution RemoveDocument(DocumentId documentId)
        {
            CheckContainsDocument(documentId);

            var oldProject = this.GetProjectState(documentId.ProjectId);
            var oldDocument = oldProject.GetDocumentState(documentId);
            var newProject = oldProject.RemoveDocument(documentId);

            return this.ForkProject(newProject, CompilationTranslationAction.RemoveDocument(oldDocument), withDocumentListChange: true);
        }

        /// <summary>
        /// Creates a new solution instance that no longer includes the specified additional document.
        /// </summary>
        public Solution RemoveAdditionalDocument(DocumentId documentId)
        {
            CheckContainsAdditionalDocument(documentId);

            var oldProject = this.GetProjectState(documentId.ProjectId);
            var newProject = oldProject.RemoveAdditionalDocument(documentId);

            return this.ForkProject(newProject);
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to be contained in
        /// the sequence of logical folders.
        /// </summary>
        public Solution WithDocumentFolders(DocumentId documentId, IEnumerable<string> folders)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (folders == null)
            {
                throw new ArgumentNullException("folders");
            }

            folders = folders != null ? folders.WhereNotNull().ToReadOnlyCollection() : null;

            var oldDocument = this.GetDocumentState(documentId);
            var newDocument = oldDocument.UpdateFolders(folders.WhereNotNull().ToReadOnlyCollection());

            return this.WithDocumentState(newDocument);
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to have the text
        /// specified.
        /// </summary>
        public Solution WithDocumentText(DocumentId documentId, SourceText text, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            CheckContainsDocument(documentId);

            var oldDocument = this.GetDocumentState(documentId);

            SourceText oldText;
            if (oldDocument.TryGetText(out oldText) && text == oldText)
            {
                return this;
            }

            // check to see if this solution has already been branched before with the same doc & text changes.
            // this helps reduce duplicate parsing when typing, and separate services generating duplicate symbols.
            if (mode == PreservationMode.PreserveIdentity)
            {
                var branch = this.firstBranch;
                if (branch != null && branch.Id == documentId && branch.Text == text)
                {
                    return branch.Solution;
                }
            }

            var newSolution = this.WithDocumentState(oldDocument.UpdateText(text, mode), textChanged: true);

            if (mode == PreservationMode.PreserveIdentity && this.firstBranch == null)
            {
                Interlocked.CompareExchange(ref this.firstBranch, new SolutionBranch(documentId, text, newSolution), null);
            }

            return newSolution;
        }

        /// <summary>
        /// Creates a new solution instance with the additional document specified updated to have the text
        /// specified.
        /// </summary>
        public Solution WithAdditionalDocumentText(DocumentId documentId, SourceText text, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            CheckContainsAdditionalDocument(documentId);

            var oldDocument = this.GetAdditionalDocumentState(documentId);

            SourceText oldText;
            if (oldDocument.TryGetText(out oldText) && text == oldText)
            {
                return this;
            }

            var newSolution = this.WithTextDocumentState(oldDocument.UpdateText(text, mode), textChanged: true);
            return newSolution;
        }

        internal async Task<Solution> WithMergedLinkedFileChangesAsync(Solution oldSolution, SolutionChanges? solutionChanges = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            // we only log sessioninfo for actual changes committed to workspace which should exclude ones from preview
            var session = new LinkedFileDiffMergingSession(oldSolution, this, solutionChanges ?? this.GetChanges(oldSolution), logSessionInfo: solutionChanges != null);
            return await session.MergeDiffsAsync(cancellationToken).ConfigureAwait(false);
        }

        private SolutionBranch firstBranch;

        private class SolutionBranch
        {
            public readonly DocumentId Id;
            public readonly SourceText Text;
            public readonly Solution Solution;

            public SolutionBranch(DocumentId id, SourceText text, Solution solution)
            {
                this.Id = id;
                this.Text = text;
                this.Solution = solution;
            }
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to have the text
        /// and version specified.
        /// </summary>
        public Solution WithDocumentText(DocumentId documentId, TextAndVersion textAndVersion, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (textAndVersion == null)
            {
                throw new ArgumentNullException("textAndVersion");
            }

            CheckContainsDocument(documentId);

            var oldDocument = this.GetDocumentState(documentId);

            return WithDocumentState(oldDocument.UpdateText(textAndVersion, mode), textChanged: true);
        }

        /// <summary>
        /// Creates a new solution instance with the additional document specified updated to have the text
        /// and version specified.
        /// </summary>
        public Solution WithAdditionalDocumentText(DocumentId documentId, TextAndVersion textAndVersion, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (textAndVersion == null)
            {
                throw new ArgumentNullException("textAndVersion");
            }

            CheckContainsAdditionalDocument(documentId);

            var oldDocument = this.GetAdditionalDocumentState(documentId);

            return WithTextDocumentState(oldDocument.UpdateText(textAndVersion, mode), textChanged: true);
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to have a syntax tree
        /// rooted by the specified syntax node.
        /// </summary>
        public Solution WithDocumentSyntaxRoot(DocumentId documentId, SyntaxNode root, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentId == null)
            {
                throw new ArgumentNullException("documentId");
            }

            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            CheckContainsDocument(documentId);

            var oldDocument = this.GetDocumentState(documentId);

            SyntaxTree oldTree;
            SyntaxNode oldRoot;
            if (oldDocument.TryGetSyntaxTree(out oldTree) &&
                oldTree.TryGetRoot(out oldRoot) &&
                oldRoot == root)
            {
                return this;
            }

            return WithDocumentState(oldDocument.UpdateTree(root, mode), textChanged: true);
        }

        private static async Task<Compilation> UpdateDocumentInCompilationAsync(
            Compilation compilation,
            DocumentState oldDocument,
            DocumentState newDocument,
            CancellationToken cancellationToken)
        {
            return compilation.ReplaceSyntaxTree(
                await oldDocument.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false),
                await newDocument.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false));
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to have the source
        /// code kind specified.
        /// </summary>
        public Solution WithDocumentSourceCodeKind(DocumentId documentId, SourceCodeKind sourceCodeKind)
        {
            if (!Enum.IsDefined(typeof(SourceCodeKind), sourceCodeKind))
            {
                throw new ArgumentNullException("sourceCodeKind");
            }

            CheckContainsDocument(documentId);

            var oldDocument = this.GetDocumentState(documentId);

            if (oldDocument.SourceCodeKind == sourceCodeKind)
            {
                return this;
            }

            return WithDocumentState(oldDocument.UpdateSourceCodeKind(sourceCodeKind), textChanged: true);
        }

        /// <summary>
        /// Creates a new solution instance with the document specified updated to have the text
        /// supplied by the text loader.
        /// </summary>
        public Solution WithDocumentTextLoader(DocumentId documentId, TextLoader loader, PreservationMode mode)
        {
            CheckContainsDocument(documentId);

            var oldDocument = this.GetDocumentState(documentId);

            // assumes that text has changed. user could have closed a doc without saving and we are loading text from closed file with
            // old content. also this should make sure we don't re-use latest doc version with data associated with opened document.
            return this.WithDocumentState(oldDocument.UpdateText(loader, mode), textChanged: true, recalculateDependentVersions: true);
        }

        /// <summary>
        /// Creates a new solution instance with the additional document specified updated to have the text
        /// supplied by the text loader.
        /// </summary>
        public Solution WithAdditionalDocumentTextLoader(DocumentId documentId, TextLoader loader, PreservationMode mode)
        {
            CheckContainsAdditionalDocument(documentId);

            var oldDocument = this.GetAdditionalDocumentState(documentId);

            // assumes that text has changed. user could have closed a doc without saving and we are loading text from closed file with
            // old content. also this should make sure we don't re-use latest doc version with data associated with opened document.
            return this.WithTextDocumentState(oldDocument.UpdateText(loader, mode), textChanged: true, recalculateDependentVersions: true);
        }

        private Solution WithDocumentState(DocumentState newDocument, bool textChanged = false, bool recalculateDependentVersions = false)
        {
            if (newDocument == null)
            {
                throw new ArgumentNullException("newDocument");
            }

            CheckContainsDocument(newDocument.Id);

            if (newDocument == this.GetDocumentState(newDocument.Id))
            {
                // old and new documents are the same instance
                return this;
            }

            return this.TouchDocument(newDocument.Id, p => p.UpdateDocument(newDocument, textChanged, recalculateDependentVersions));
        }

        private Solution TouchDocument(DocumentId documentId, Func<ProjectState, ProjectState> touchProject)
        {
            Contract.Requires(this.ContainsDocument(documentId));

            var oldProject = this.GetProjectState(documentId.ProjectId);
            var newProject = touchProject(oldProject);

            if (oldProject == newProject)
            {
                // old and new projects are the same instance
                return this;
            }

            var oldDocument = oldProject.GetDocumentState(documentId);
            var newDocument = newProject.GetDocumentState(documentId);

            return this.ForkProject(newProject, CompilationTranslationAction.TouchDocument(oldDocument, newDocument));
        }

        private Solution WithTextDocumentState(TextDocumentState newDocument, bool textChanged = false, bool recalculateDependentVersions = false)
        {
            if (newDocument == null)
            {
                throw new ArgumentNullException("newDocument");
            }

            CheckContainsAdditionalDocument(newDocument.Id);

            if (newDocument == this.GetAdditionalDocumentState(newDocument.Id))
            {
                // old and new documents are the same instance
                return this;
            }

            var oldProject = this.GetProjectState(newDocument.Id.ProjectId);
            var newProject = oldProject.UpdateAdditionalDocument(newDocument, textChanged, recalculateDependentVersions);

            if (oldProject == newProject)
            {
                // old and new projects are the same instance
                return this;
            }

            return this.ForkProject(newProject);
        }


        /// <summary>
        /// Creates a new snapshot with an updated project and an action that will produce a new
        /// compilation matching the new project out of an old compilation. All dependent projects
        /// are fixed-up if the change to the new project affects its public metadata, and old
        /// dependent compilations are forgotten.
        /// </summary>
        private Solution ForkProject(
            ProjectState newProjectState,
            CompilationTranslationAction translate = null,
            bool withProjectReferenceChange = false,
            bool withDocumentListChange = false)
        {
            // make sure we are getting only known translate actions
            CompilationTranslationAction.CheckKnownActions(translate);

            var projectId = newProjectState.Id;

            var newStateMap = this.projectIdToProjectStateMap.SetItem(projectId, newProjectState);
            var newDependencyGraph = withProjectReferenceChange ? CreateDependencyGraph(this.projectIds, newStateMap) : this.dependencyGraph;
            var newTrackerMap = CreateCompilationTrackerMap(projectId, newDependencyGraph);
            var newLinkedFilesMap = withDocumentListChange
                ? CreateLinkedFilesMapWithChangedProject(projectIdToProjectStateMap[projectId], newProjectState)
                : this.linkedFilesMap;

            // If we have a tracker for this project, then fork it as well (along with the
            // translation action and store it in the tracker map.
            CompilationTracker state;
            if (newTrackerMap.TryGetValue(projectId, out state))
            {
                newTrackerMap = newTrackerMap.Remove(projectId).Add(projectId, state.Fork(newProjectState, translate));
            }

            var modifiedDocumentOnly = translate is CompilationTranslationAction.TouchDocumentAction;
            var newLatestProjectVersion = modifiedDocumentOnly ? this.lazyLatestProjectVersion : new Lazy<VersionStamp>(() => newProjectState.Version);

            return this.Branch(
                idToProjectStateMap: newStateMap,
                projectIdToTrackerMap: newTrackerMap,
                dependencyGraph: newDependencyGraph,
                linkedFilesMap: newLinkedFilesMap,
                lazyLatestProjectVersion: newLatestProjectVersion);
        }

        private ImmutableDictionary<string, ImmutableArray<DocumentId>> CreateLinkedFilesMapWithChangedProject(ProjectState oldProjectState, ProjectState newProjectState)
        {
            var oldDocumentIds = oldProjectState.DocumentIds;
            var newDocumentIds = newProjectState.DocumentIds;
            var addedDocumentIds = newDocumentIds.ToImmutableArray().RemoveRange(oldDocumentIds);
            var removedDocumentIds = oldDocumentIds.ToImmutableArray().RemoveRange(newDocumentIds);

            Debug.Assert(addedDocumentIds.Any() || removedDocumentIds.Any(), "The solution's linkedFilesMap should only be recalculated if its files changed.");

            var linkedFilesMap = this.linkedFilesMap;
            linkedFilesMap = addedDocumentIds.Any() ? CreateLinkedFilesMapWithAddedDocuments(newProjectState, addedDocumentIds) : linkedFilesMap;
            linkedFilesMap = removedDocumentIds.Any() ? CreateLinkedFilesMapWithRemovedDocuments(oldProjectState, removedDocumentIds) : linkedFilesMap;
            return linkedFilesMap;
        }

        /// <summary>
        /// Gets the set of <see cref="DocumentId"/>s in this <see cref="Solution"/> with a
        /// <see cref="TextDocument.FilePath"/> that matches the given file path.
        /// </summary>
        public ImmutableArray<DocumentId> GetDocumentIdsWithFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return ImmutableArray.Create<DocumentId>();
            }

            ImmutableArray<DocumentId> documentIds;
            return this.linkedFilesMap.TryGetValue(filePath, out documentIds)
                ? documentIds
                : ImmutableArray.Create<DocumentId>();
        }

        private static ProjectDependencyGraph CreateDependencyGraph(
            IReadOnlyList<ProjectId> projectIds,
            ImmutableDictionary<ProjectId, ProjectState> projectStates)
        {
            var map = projectStates.Values.Select(state => new KeyValuePair<ProjectId, ImmutableHashSet<ProjectId>>(
                    state.Id,
                    state.ProjectReferences.Where(pr => projectStates.ContainsKey(pr.ProjectId)).Select(pr => pr.ProjectId).ToImmutableHashSet()))
                    .ToImmutableDictionary();

            return new ProjectDependencyGraph(projectIds.ToImmutableArray(), map);
        }

        private ImmutableDictionary<ProjectId, CompilationTracker> CreateCompilationTrackerMap(ProjectId projectId, ProjectDependencyGraph dependencyGraph)
        {
            var builder = ImmutableDictionary.CreateBuilder<ProjectId, CompilationTracker>();
            var dependencies = dependencyGraph.GetProjectsThatTransitivelyDependOnThisProject(projectId);

            foreach (var projectIdAndTracker in this.projectIdToTrackerMap)
            {
                var id = projectIdAndTracker.Key;
                var tracker = projectIdAndTracker.Value;

                if (!tracker.HasCompilation)
                {
                    continue;
                }

                var canReuse = id == projectId || !dependencies.Contains(id);
                builder.Add(id, canReuse ? tracker : tracker.Fork(tracker.ProjectState));
            }

            return builder.ToImmutable();
        }

        /// <summary>
        /// Gets a copy of the solution isolated from the original so that they do not share computed state.
        /// 
        /// Use isolated solutions when doing operations that are likely to access a lot of text,
        /// syntax trees or compilations that are unlikely to be needed again after the operation is done. 
        /// When the isolated solution is reclaimed so will the computed state.
        /// </summary>
        public Solution GetIsolatedSolution()
        {
            var forkedMap = ImmutableDictionary.CreateRange<ProjectId, CompilationTracker>(
                projectIdToTrackerMap.Where(kvp => kvp.Value.HasCompilation)
                                     .Select(kvp => new KeyValuePair<ProjectId, CompilationTracker>(kvp.Key, kvp.Value.Clone())));

            return this.Branch(projectIdToTrackerMap: forkedMap);
        }

        // this lock guards all the mutable fields (do not share lock with derived classes)
        private NonReentrantLock stateLockBackingField;
        private NonReentrantLock StateLock
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref stateLockBackingField, NonReentrantLock.Factory);
            }
        }

        private WeakReference<Solution> latestSolutionWithPartialCompilation;
        private DateTime timeOfLatestSolutionWithPartialCompilation;
        private DocumentId documentIdOfLatestSolutionWithPartialCompilation;

        /// <summary>
        /// Creates a branch of the solution that has its compilations frozen in whatever state they are in at the time, assuming a background compiler is
        /// busy building this compilations.
        /// 
        /// A compilation for the project containing the specified document id will be guaranteed to exist with at least the syntax tree for the document.
        /// 
        /// This not intended to be the public API, use Document.WithFrozenPartialSemantics() instead.
        /// </summary>
        internal async Task<Solution> WithFrozenPartialCompilationIncludingSpecificDocumentAsync(DocumentId documentId, CancellationToken cancellationToken)
        {
            try
            {
                var doc = this.GetDocument(documentId);
                var tree = await doc.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);

                using (this.StateLock.DisposableWait())
                {
                    // in progress solutions are disabled for some testing
                    Workspace ws = this.Workspace as Workspace;
                    if (ws != null && ws.TestHookPartialSolutionsDisabled)
                    {
                        return this;
                    }

                    Solution currentPartialSolution = null;
                    if (this.latestSolutionWithPartialCompilation != null)
                    {
                        this.latestSolutionWithPartialCompilation.TryGetTarget(out currentPartialSolution);
                    }

                    // if we don't have one or it is stale, create a new partial solution
                    if (currentPartialSolution == null
                        || (DateTime.UtcNow - this.timeOfLatestSolutionWithPartialCompilation).TotalSeconds >= 0.1
                        || this.documentIdOfLatestSolutionWithPartialCompilation != documentId)
                    {
                        var tracker = this.GetCompilationTracker(documentId.ProjectId);
                        var newTracker = tracker.FreezePartialStateWithTree(this, doc.State, tree, cancellationToken);

                        var newIdToProjectStateMap = this.projectIdToProjectStateMap.SetItem(documentId.ProjectId, newTracker.ProjectState);
                        var newIdToTrackerMap = this.projectIdToTrackerMap.SetItem(documentId.ProjectId, newTracker);

                        currentPartialSolution = this.Branch(
                            idToProjectStateMap: newIdToProjectStateMap,
                            projectIdToTrackerMap: newIdToTrackerMap,
                            dependencyGraph: CreateDependencyGraph(this.projectIds, newIdToProjectStateMap));

                        this.latestSolutionWithPartialCompilation = new WeakReference<Solution>(currentPartialSolution);
                        this.timeOfLatestSolutionWithPartialCompilation = DateTime.UtcNow;
                        this.documentIdOfLatestSolutionWithPartialCompilation = documentId;
                    }

                    return currentPartialSolution;
                }
            }
            catch (Exception e) if (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        /// <summary>
        /// Creates a new solution instance with all the documents specified updated to have the same specified text.
        /// </summary>
        public Solution WithDocumentText(IEnumerable<DocumentId> documentIds, SourceText text, PreservationMode mode = PreservationMode.PreserveValue)
        {
            if (documentIds == null)
            {
                throw new ArgumentNullException("documentIds");
            }

            if (text == null)
            {
                throw new ArgumentNullException("text");
            }

            var solution = this;

            foreach (var documentId in documentIds)
            {
                var doc = solution.GetDocument(documentId);
                if (doc != null)
                {
                    SourceText existingText;
                    if (!doc.TryGetText(out existingText) || existingText != text)
                    {
                        solution = solution.WithDocumentText(documentId, text, mode);
                    }
                }
            }

            return solution;
        }

        internal bool TryGetCompilation(ProjectId projectId, out Compilation compilation)
        {
            CheckContainsProject(projectId);

            CompilationTracker tracker;
            compilation = null;

            return this.TryGetCompilationTracker(projectId, out tracker)
                && tracker.TryGetCompilation(out compilation);
        }

        internal Task<Compilation> GetCompilationAsync(ProjectId projectId, CancellationToken cancellationToken)
        {
            return this.GetCompilationTracker(projectId).GetCompilationAsync(this, cancellationToken);
        }

        private static readonly ConditionalWeakTable<MetadataReference, ProjectId> metadataReferenceToProjectMap =
            new ConditionalWeakTable<MetadataReference, ProjectId>();

        private void RecordReferencedProject(MetadataReference reference, ProjectId projectId)
        {
            // remember which project is associated with this reference
            ProjectId tmp;
            if (!metadataReferenceToProjectMap.TryGetValue(reference, out tmp))
            {
                // use GetValue to avoid race condition exceptions from Add.
                // the first one to set the value wins.
                metadataReferenceToProjectMap.GetValue(reference, _ => projectId);
            }
            else
            {
                // sanity check: this should always be true, no matter how many times
                // we attempt to record the association.
                System.Diagnostics.Debug.Assert(tmp == projectId);
            }
        }

        internal ProjectId GetProjectId(MetadataReference reference)
        {
            ProjectId id = null;
            metadataReferenceToProjectMap.TryGetValue(reference, out id);
            return id;
        }

        /// <summary>
        /// Get a metadata reference for the project's compilation
        /// </summary>
        internal async Task<MetadataReference> GetMetadataReferenceAsync(ProjectReference projectReference, ProjectState fromProject, CancellationToken cancellationToken)
        {
            try
            {
                // Get the compilation state for this project.  If it's not already created, then this
                // will create it.  Then force that state to completion and get a metadata reference to it.
                var tracker = this.GetCompilationTracker(projectReference.ProjectId);
                var mdref = await tracker.GetMetadataReferenceAsync(this, fromProject, projectReference, cancellationToken).ConfigureAwait(false);

                if (mdref != null)
                {
                    RecordReferencedProject(mdref, projectReference.ProjectId);
                }

                return mdref;
            }
            catch (Exception e) if (FatalError.ReportUnlessCanceled(e))
            {
                throw ExceptionUtilities.Unreachable;
            }
        }

        /// <summary>
        /// Attempt to get the best readily available compilation for the project. It may be a
        /// partially built compilation.
        /// </summary>
        internal MetadataReference GetPartialMetadataReference(
            ProjectReference projectReference,
            ProjectState fromProject,
            CancellationToken cancellationToken)
        {
            // Try to get the compilation state for this project.  If it doesn't exist, don't do any
            // more work.  
            CompilationTracker state;
            if (!this.projectIdToTrackerMap.TryGetValue(projectReference.ProjectId, out state))
            {
                return null;
            }

            var mdref = state.GetPartialMetadataReference(this, fromProject, projectReference, cancellationToken);

            if (mdref != null)
            {
                RecordReferencedProject(mdref, projectReference.ProjectId);
            }

            return mdref;
        }

        /// <summary>
        /// Gets an objects that lists the added, changed and removed projects between
        /// this solution and the specified solution.
        /// </summary>
        public SolutionChanges GetChanges(Solution oldSolution)
        {
            if (oldSolution == null)
            {
                throw new ArgumentNullException("oldSolution");
            }

            return new SolutionChanges(this, oldSolution);
        }

        /// <summary>
        /// Gets an ProjectDependencyGraph that details the dependencies between projects for this solution.
        /// </summary>
        public ProjectDependencyGraph GetProjectDependencyGraph()
        {
            return this.dependencyGraph;
        }

        private void CheckNotContainsProject(ProjectId projectId)
        {
            if (this.ContainsProject(projectId))
            {
                throw new InvalidOperationException(WorkspacesResources.ProjectAlreadyInSolution);
            }
        }

        private void CheckContainsProject(ProjectId projectId)
        {
            if (!this.ContainsProject(projectId))
            {
                throw new InvalidOperationException(WorkspacesResources.ProjectNotInSolution);
            }
        }

        private void CheckNotContainsProjectReference(ProjectId projectId, ProjectReference referencedProject)
        {
            if (this.GetProjectState(projectId).ProjectReferences.Contains(referencedProject))
            {
                throw new InvalidOperationException(WorkspacesResources.ProjectDirectlyReferencesTargetProject);
            }
        }

        private void CheckNotContainsTransitiveReference(ProjectId fromProjectId, ProjectId toProjectId)
        {
            var dependents = this.dependencyGraph.GetProjectsThatThisProjectTransitivelyDependsOn(fromProjectId);
            if (dependents.Contains(toProjectId))
            {
                throw new InvalidOperationException(WorkspacesResources.ProjectTransitivelyReferencesTargetProject);
            }
        }

        private void CheckNotSecondSubmissionReference(ProjectId projectId, ProjectId toProjectId)
        {
            var projectState = GetProjectState(projectId);

            if (projectState.IsSubmission && GetProjectState(toProjectId).IsSubmission)
            {
                if (projectState.ProjectReferences.Any(p => GetProjectState(p.ProjectId).IsSubmission))
                {
                    throw new InvalidOperationException(WorkspacesResources.InvalidSubmissionReference);
                }
            }
        }

        private void CheckNotContainsDocument(DocumentId documentId)
        {
            Contract.Requires(!this.ContainsDocument(documentId));

            if (this.ContainsDocument(documentId))
            {
                throw new InvalidOperationException(WorkspacesResources.DocumentAlreadyInSolution);
            }
        }

        private void CheckNotContainsAdditionalDocument(DocumentId documentId)
        {
            Contract.Requires(!this.ContainsAdditionalDocument(documentId));

            if (this.ContainsAdditionalDocument(documentId))
            {
                throw new InvalidOperationException(WorkspacesResources.DocumentAlreadyInSolution);
            }
        }

        private void CheckContainsDocument(DocumentId documentId)
        {
            Contract.Requires(this.ContainsDocument(documentId));

            if (!this.ContainsDocument(documentId))
            {
                throw new InvalidOperationException(WorkspacesResources.DocumentNotInSolution);
            }
        }

        private void CheckContainsAdditionalDocument(DocumentId documentId)
        {
            Contract.Requires(this.ContainsAdditionalDocument(documentId));

            if (!this.ContainsAdditionalDocument(documentId))
            {
                throw new InvalidOperationException(WorkspacesResources.DocumentNotInSolution);
            }
        }
    }
}
