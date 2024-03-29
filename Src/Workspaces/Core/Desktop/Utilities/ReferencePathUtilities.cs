﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Roslyn.Utilities
{
    internal static class ReferencePathUtilities
    {
        public static bool TryGetReferenceFilePath(string filePath, out string referenceFilePath)
        {
            // TODO(DustinCa): This is a workaround and we'll need to update this to handle getting the
            // correct reference assembly for different framework versions and profiles. We can use
            // the handy ToolLocationHelper from Microsoft.Build.Utilities.v4.5.dll

            var assemblyName = Path.GetFileName(filePath);

            // NOTE: Don't use the Path.HasExtension() and Path.ChangeExtension() helpers because
            // an assembly might have a dotted name like 'System.Core'.
            var extension = Path.GetExtension(assemblyName);
            if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                assemblyName += ".dll";
            }

            foreach (var referenceAssemblyPath in GetReferencePaths())
            {
                var referenceAssembly = Path.Combine(referenceAssemblyPath, assemblyName);
                if (File.Exists(referenceAssembly))
                {
                    referenceFilePath = referenceAssembly;
                    return true;
                }
            }

            referenceFilePath = null;
            return false;
        }

        public static bool TryFindXmlDocumentationFile(string assemblyFilePath, out string xmlDocumentationFilePath)
        {
            // TODO(DustinCa): This is a workaround  and we'll need to update this to handle getting the correct
            // reference assembly for different framework versions and profiles.

            string xmlFilePath = string.Empty;

            // 1. Look in subdirectories based on the current culture
            // TODO: This logic is somewhat duplicated between here and 
            // Roslyn.Scripting.MetadataShadowCopyProvider
            string xmlFileName = Path.ChangeExtension(Path.GetFileName(assemblyFilePath), ".xml");
            string originalDirectory = Path.GetDirectoryName(assemblyFilePath);

            var culture = CultureInfo.CurrentCulture;
            while (culture != CultureInfo.InvariantCulture)
            {
                xmlFilePath = Path.Combine(originalDirectory, culture.Name, xmlFileName);
                if (File.Exists(xmlFilePath))
                {
                    break;
                }

                culture = culture.Parent;
            }

            if (File.Exists(xmlFilePath))
            {
                xmlDocumentationFilePath = xmlFilePath;
                return true;
            }

            // 2. Look in the same directory as the assembly itself

            var extension = Path.GetExtension(assemblyFilePath);
            if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                xmlFilePath = Path.ChangeExtension(assemblyFilePath, ".xml");
            }
            else
            {
                xmlFilePath = assemblyFilePath + ".xml";
            }

            if (File.Exists(xmlFilePath))
            {
                xmlDocumentationFilePath = xmlFilePath;
                return true;
            }

            // 3. Look for reference assemblies

            string referenceAssemblyFilePath;
            if (!TryGetReferenceFilePath(assemblyFilePath, out referenceAssemblyFilePath))
            {
                xmlDocumentationFilePath = null;
                return false;
            }

            xmlFilePath = Path.ChangeExtension(referenceAssemblyFilePath, ".xml");

            if (File.Exists(xmlFilePath))
            {
                xmlDocumentationFilePath = xmlFilePath;
                return true;
            }

            xmlDocumentationFilePath = null;
            return false;
        }

        private static IEnumerable<string> GetFrameworkPaths()
        {
            ////            Concat(Path.GetDirectoryName(typeof(Microsoft.CSharp.RuntimeHelpers.SessionHelpers).Assembly.Location)).
            return GlobalAssemblyCache.RootLocations.Concat(RuntimeEnvironment.GetRuntimeDirectory());
        }

        public static IEnumerable<string> GetReferencePaths()
        {
            // TODO:
            // WORKAROUND: properly enumerate them
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0");
        }

        public static bool PartOfFrameworkOrReferencePaths(string filePath)
        {
            if (!PathUtilities.IsAbsolute(filePath))
            {
                return false;
            }

            var directory = Path.GetDirectoryName(filePath);

            var frameworkOrReferencePaths = GetReferencePaths().Concat(GetFrameworkPaths()).Select(FileUtilities.NormalizeDirectoryPath);
            return frameworkOrReferencePaths.Any(dir => directory.StartsWith(dir, StringComparison.OrdinalIgnoreCase));
        }
    }
}
