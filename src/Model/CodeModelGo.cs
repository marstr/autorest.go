// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using AutoRest.Core;
using AutoRest.Core.Model;
using AutoRest.Core.Utilities;
using AutoRest.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AutoRest.Go.Model
{
    public class CodeModelGo : CodeModel
    {
        private static readonly Regex semVerPattern = new Regex(@"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-(?<tag>\S+))?$", RegexOptions.Compiled);

        public CodeModelGo()
        {
            Version = FormatVersion(Settings.Instance.PackageVersion);
            SpecifiedUserAgent = Settings.Instance.Host?.GetValue<string>("user-agent").Result;
        }

        public string Version { get; }

        public string UserAgent
        {
            get => SpecifiedUserAgent ?? DefaultUserAgent;
            set => SpecifiedUserAgent = value;
        }

        private string DefaultUserAgent
        {
            get
            {
                return $"Azure-SDK-For-Go/{Version} arm-{Namespace}/{ApiVersion}";
            }
        }

        private string SpecifiedUserAgent
        {
            get;
            set;
        }

        public string ServiceName => CodeNamerGo.Instance.PascalCase(Namespace ?? string.Empty);

        public string BaseClient => "BaseClient";
        public bool IsCustomBaseUri => Extensions.ContainsKey(SwaggerExtensions.ParameterizedHostExtension);

        public string APIType => Settings.Instance.Host?.GetValue<string>("openapi-type").Result;

        public IEnumerable<string> ClientImports
        {
            get
            {
                var imports = new HashSet<string>();
                imports.UnionWith(CodeNamerGo.Instance.AutorestImports);
                var clientMg = MethodGroups.FirstOrDefault(mg => string.IsNullOrEmpty(mg.Name));
                if (clientMg != null)
                {
                    imports.UnionWith(clientMg.Imports);
                }
                foreach (var p in Properties)
                {
                    p.ModelType.AddImports(imports);
                }
                return imports.OrderBy(i => i);
            }
        }

        public string ClientDocumentation => string.Format("{0} is the base client for {1}.", BaseClient, ServiceName);

        public IEnumerable<string> ModelImports
        {
            get
            {
                // Create an ordered union of the imports each model requires
                var imports = new HashSet<string>();
                if (ModelTypes != null && ModelTypes.Cast<CompositeTypeGo>().Any(mtm => mtm.IsResponseType || mtm.IsWrapperType))
                {
                    imports.Add(PrimaryTypeGo.GetImportLine("github.com/Azure/go-autorest/autorest"));
                }
                if (ModelTypes.Any(mt => mt is FutureTypeGo))
                {
                    imports.Add(PrimaryTypeGo.GetImportLine("github.com/Azure/go-autorest/autorest/azure"));
                    imports.Add(PrimaryTypeGo.GetImportLine("net/http"));
                }
                ModelTypes.Cast<CompositeTypeGo>()
                    .ForEach(mt =>
                    {
                        mt.AddImports(imports);
                    });
                // if any paged types need a preparer created add the pageable imports
                if (ModelTypes.Any(mt => mt is PageTypeGo && mt.Cast<PageTypeGo>().PreparerNeeded))
                {
                    imports.UnionWith(CodeNamerGo.Instance.PageableImports);
                }
                return imports.OrderBy(i => i);
            }
        }

        public virtual IEnumerable<MethodGroupGo> MethodGroups => Operations.Cast<MethodGroupGo>();

        public bool ShouldValidate => (bool)AutoRest.Core.Settings.Instance.Host?.GetValue<bool?>("client-side-validation").Result;

        public string GlobalParameters
        {
            get
            {
                var declarations = new List<string>();
                foreach (var p in Properties)
                {
                    if (!p.SerializedName.IsApiVersion() && p.DefaultValue.FixedValue.IsNullOrEmpty())
                    {
                        declarations.Add(
                                string.Format(
                                        (p.IsRequired || p.ModelType.CanBeEmpty() ? "{0} {1}" : "{0} *{1}"),
                                         p.Name.Value.ToSentence(), p.ModelType.Name));
                    }
                }
                return string.Join(", ", declarations);
            }
        }

        public string HelperGlobalParameters
        {
            get
            {
                var invocationParams = new List<string>();
                foreach (var p in Properties)
                {
                    if (!p.SerializedName.IsApiVersion() && p.DefaultValue.FixedValue.IsNullOrEmpty())
                    {
                        invocationParams.Add(p.Name.Value.ToSentence());
                    }
                }
                return string.Join(", ", invocationParams);
            }
        }

        public string GlobalDefaultParameters
        {
            get
            {
                var declarations = new List<string>();
                foreach (var p in Properties)
                {
                    if (!p.SerializedName.IsApiVersion() && !p.DefaultValue.FixedValue.IsNullOrEmpty())
                    {
                        declarations.Add(
                                string.Format(
                                        (p.IsRequired || p.ModelType.CanBeEmpty() ? "{0} {1}" : "{0} *{1}"),
                                         p.Name.Value.ToSentence(), p.ModelType.Name.Value.ToSentence()));
                    }
                }
                return string.Join(", ", declarations);
            }
        }

        public string HelperGlobalDefaultParameters
        {
            get
            {
                var invocationParams = new List<string>();
                foreach (var p in Properties)
                {
                    if (!p.SerializedName.IsApiVersion() && !p.DefaultValue.FixedValue.IsNullOrEmpty())
                    {
                        invocationParams.Add("Default" + p.Name.Value);
                    }
                }
                return string.Join(", ", invocationParams);
            }
        }

        public string ConstGlobalDefaultParameters
        {
            get
            {
                var constDeclaration = new List<string>();
                if (!IsCustomBaseUri)
                {
                    constDeclaration.Add($"// DefaultBaseURI is the default URI used for the service {ServiceName}\nDefaultBaseURI = \"{BaseUrl}\"");
                }
                foreach (var p in Properties)
                {
                    if (!p.SerializedName.IsApiVersion() && !p.DefaultValue.FixedValue.IsNullOrEmpty())
                    {
                        constDeclaration.Add(string.Format("// Default{0} is the default value for {1}\nDefault{0} = {2}",
                            p.Name.Value,
                            p.Name.Value.ToPhrase(),
                            p.DefaultValue.Value));
                    }
                }
                return string.Join("\n", constDeclaration);
            }
        }

        public string AllGlobalParameters
        {
            get
            {
                if (GlobalParameters.IsNullOrEmpty())
                {
                    return GlobalDefaultParameters;
                }
                if (GlobalDefaultParameters.IsNullOrEmpty())
                {
                    return GlobalParameters;
                }
                return string.Join(", ", GlobalParameters, GlobalDefaultParameters);
            }
        }

        public string HelperAllGlobalParameters
        {
            get
            {
                if (HelperGlobalParameters.IsNullOrEmpty())
                {
                    return HelperGlobalDefaultParameters;
                }
                if (HelperGlobalDefaultParameters.IsNullOrEmpty())
                {
                    return HelperGlobalParameters;
                }
                return string.Join(", ", HelperGlobalParameters, HelperGlobalDefaultParameters);
            }
        }

        // client methods are the ones with no method group
        public IEnumerable<MethodGo> ClientMethods => Methods.Cast<MethodGo>().Where(m => string.IsNullOrEmpty(m.MethodGroup.Name));

        public override string Namespace
        {
            get => string.IsNullOrEmpty(base.Namespace) ? base.Namespace : base.Namespace.ToLowerInvariant();
            set => base.Namespace = value;
        }

        public string GetDocumentation => $"Package {Namespace} implements the Azure ARM {ServiceName} service API version {ApiVersion}.\n\n{(base.Documentation ?? string.Empty).UnwrapAnchorTags()}";

        /// FormatVersion normalizes a version string into a SemVer if it resembles one. Otherwise,
        /// it returns the original string unmodified. If version is empty or only comprised of
        /// whitespace, 
        public static string FormatVersion(string version)
        {

            if (string.IsNullOrWhiteSpace(version))
            {
                return "0.0.0";
            }

            var semVerMatch = semVerPattern.Match(version);

            if (!semVerMatch.Success)
            {
                return version;
            }

            var builder = new StringBuilder("v");
            builder.Append(semVerMatch.Groups["major"].Value);
            builder.Append('.');
            builder.Append(semVerMatch.Groups["minor"].Value);
            builder.Append('.');
            builder.Append(semVerMatch.Groups["patch"].Value);
            if (semVerMatch.Groups["tag"].Success)
            {
                builder.Append('-');
                builder.Append(semVerMatch.Groups["tag"].Value);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Creates a pageable type for the specified method and updates its return type.
        /// </summary>
        /// <param name="method">The method to be modified.</param>
        internal void CreatePageableTypeForMethod(MethodGo method)
        {
            if (!method.IsPageable)
            {
                throw new InvalidOperationException("CreatePageableTypeForMethod requires method to be a pageable operation");
            }

            var page = new PageTypeGo(method);
            if (ModelTypes.Contains(page))
            {
                page = ModelTypes.First(mt => mt.Equals(page)).Cast<PageTypeGo>();
            }
            else
            {
                Add(page);
                Add(page.IteratorType);
            }

            method.ReturnType = new Response(page, method.ReturnType.Headers);
        }

        /// <summary>
        /// Creates a future for the specified method and updates its return type.
        /// </summary>
        /// <param name="method">The method to be modified.</param>
        internal void CreateFutureTypeForMethod(MethodGo method)
        {
            if (!method.IsLongRunningOperation())
            {
                throw new InvalidOperationException("CreateFutureTypeForMethod requires method to be a long-running operation");
            }

            // this is the future to return from the method
            var future = GetOrAddFuture(new FutureTypeGo(method));

            // if this is a pageable method create a future type for the
            // "list all" method wrapped in our custom response type
            if (method.IsPageable)
            {
                var listAllFuture = GetOrAddFuture(new FutureTypeGo(CodeNamerGo.Instance.GetFutureTypeName($"{method.Group}{method.Name}All"), method));
                method.ReturnType = new LroPagedResponseGo(future, listAllFuture, method.ReturnType.Headers);
            }
            else
            {
                method.ReturnType = new Response(future, method.ReturnType.Headers);
            }
        }

        /// <summary>
        /// Checks if the specified future type already exists, if it does return that one instead.
        /// If it does not exist it is added to the collection of model types and returned.
        /// </summary>
        /// <param name="futureType">The future type to check for and possibly add.</param>
        /// <returns>The existing or added object.</returns>
        private FutureTypeGo GetOrAddFuture(FutureTypeGo futureType)
        {
            // don't create duplicate future types
            if (ModelTypes.Contains(futureType))
            {
                futureType = ModelTypes.First(mt => mt.Equals(futureType)).Cast<FutureTypeGo>();
            }
            else
            {
                Add(futureType);
            }
            return futureType;
        }
    }
}
