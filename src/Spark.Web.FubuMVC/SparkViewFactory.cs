﻿using FubuMVC.Core.Registration.Nodes;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Spark.Compiler;
using Spark.FileSystem;
using Spark.Web.FubuMVC.Extensions;
using Spark.Web.FubuMVC.ViewCreation;
using Spark.Web.FubuMVC.ViewLocation;

namespace Spark.Web.FubuMVC
{
    public class SparkViewFactory : ISparkViewFactory
    {
        private readonly Dictionary<BuildDescriptorParams, ISparkViewEntry> _cache = new Dictionary<BuildDescriptorParams, ISparkViewEntry>();        
        private ICacheServiceProvider _cacheServiceProvider;
        private IDescriptorBuilder _descriptorBuilder;
        private ISparkViewEngine _engine;

        public SparkViewFactory(ISparkSettings settings)
        {
            Settings = settings ?? (ISparkSettings) ConfigurationManager.GetSection("spark") ?? new SparkSettings();
        }

        public IViewFolder ViewFolder
        {
            get { return Engine.ViewFolder; }
            set { Engine.ViewFolder = value; }
        }

        public IDescriptorBuilder DescriptorBuilder
        {
            get
            {
                return _descriptorBuilder ??
                       Interlocked.CompareExchange(ref _descriptorBuilder, new FubuDescriptorBuilder(Engine), null) ??
                       _descriptorBuilder;
            }
            set { _descriptorBuilder = value; }
        }

        public ISparkSettings Settings { get; set; }

        public ISparkViewEngine Engine
        {
            get
            {
                if (_engine == null)
                {
                    SetEngine(new SparkViewEngine(Settings));
                }
                return _engine;
            }
            set { SetEngine(value); }
        }

        public ICacheServiceProvider CacheServiceProvider
        {
            get
            {
                return _cacheServiceProvider ??
                       Interlocked.CompareExchange(ref _cacheServiceProvider, new DefaultCacheServiceProvider(), null) ??
                       _cacheServiceProvider;
            }

            set { _cacheServiceProvider = value; }
        }

        public void SetEngine(ISparkViewEngine engine)
        {
            _descriptorBuilder = null;
            _engine = engine;
            if (_engine != null)
            {
                _engine.DefaultPageBaseType = typeof (SparkView).FullName;
            }
        }

        public SparkViewDescriptor CreateDescriptor(ActionContext actionContext, string viewName, string masterName, bool findDefaultMaster, ICollection<string> searchedLocations)
        {
            return DescriptorBuilder.BuildDescriptor(
                new BuildDescriptorParams(
                    actionContext.ActionNamespace,
                    actionContext.ActionName,
                    viewName,
                    masterName,
                    findDefaultMaster,
                    DescriptorBuilder.GetExtraParameters(actionContext)),
                searchedLocations);
        }

        public Assembly Precompile(SparkBatchDescriptor batch, string viewLocatorName)
        {
            return Engine.BatchCompilation(batch.OutputAssembly, CreateDescriptors(batch, viewLocatorName));
        }

        public List<SparkViewDescriptor> CreateDescriptors(SparkBatchDescriptor batch, string viewLocatorName)
        {
            var descriptors = new List<SparkViewDescriptor>();
            foreach (var entry in batch.Entries)
            {
                descriptors.AddRange(CreateDescriptors(entry, viewLocatorName));
            } 

            return descriptors;
        }

        public IList<SparkViewDescriptor> CreateDescriptors(SparkBatchEntry entry, string viewLocatorName)
        {
            var actionName = viewLocatorName;
            var descriptors = new List<SparkViewDescriptor>();

            var viewNames = new List<string>();
            var includeViews = entry.IncludeViews;
            
            if (includeViews.Count == 0)
            {
                includeViews = new[] {"*"};
            }

            foreach (var include in includeViews)
            {
                if (include.EndsWith("*"))
                {
                    foreach (var fileName in ViewFolder.ListViews(actionName))
                    {
                        if (!string.Equals(Path.GetExtension(fileName), ".spark", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }

                        var potentialMatch = Path.GetFileNameWithoutExtension(fileName);
                        if (!potentialMatch.Matches(include))
                        {
                            continue;
                        }

                        var isExcluded = false;
                        foreach (var exclude in entry.ExcludeViews)
                        {
                            if (!potentialMatch.Matches(exclude.RemoveSuffix(".spark")))
                            {
                                continue;
                            }

                            isExcluded = true;
                            break;
                        }
                        if (!isExcluded)
                        {
                            viewNames.Add(potentialMatch);
                        }
                    }
                }
                else
                {
                    // explicitly included views don't test for exclusion
                    viewNames.Add(include.RemoveSuffix(".spark"));
                }
            }

            foreach (var viewName in viewNames)
            {
                if (entry.LayoutNames.Count == 0)
                {
                    descriptors.Add(CreateDescriptor(
                                        entry.ControllerType.Namespace,
                                        actionName,
                                        viewName,
                                        null /*masterName*/,
                                        true));
                }
                else
                {
                    foreach (var masterName in entry.LayoutNames)
                    {
                        descriptors.Add(CreateDescriptor(
                                            entry.ControllerType.Namespace,
                                            actionName,
                                            viewName,
                                            string.Join(" ", masterName.ToArray()),
                                            false));
                    }
                }
            }

            return descriptors;
        }

        public SparkViewDescriptor CreateDescriptor(string targetNamespace, string actionName, string viewName, string masterName, bool findDefaultMaster)
        {
            var searchedLocations = new List<string>();
            
            var descriptor = DescriptorBuilder.BuildDescriptor(
                new BuildDescriptorParams(
                    targetNamespace /*areaName*/,
                    actionName,
                    viewName,
                    masterName,
                    findDefaultMaster, null),
                searchedLocations);

            if (descriptor == null)
            {
                var errMsg = string.Format("Unable to find templates at {0}", string.Join(", ", searchedLocations.ToArray()));
                throw new CompilerException(errMsg);
            }

            return descriptor;
        }

        private ViewEngineResult buildResult(ISparkViewEntry entry)
        {
            var view = entry.CreateInstance();
            if (view is SparkView)
            {
                var sparkView = (SparkView) view;

                sparkView.ResourcePathManager = Engine.ResourcePathManager;
                sparkView.CacheService = CacheServiceProvider.GetCacheService();
            }

            return new ViewEngineResult(view, this);
        }

        public ViewEngineResult FindView(ActionContext actionContext, string viewName, string masterName)
        {
            return findViewInternal(actionContext, viewName, masterName, true, false);
        }

        public virtual ViewEngineResult FindPartialView(ActionContext actionContext, string partialViewName)
        {
            return findViewInternal(actionContext, partialViewName, null /*masterName*/, false, false);
        }

        public SparkViewToken GetViewToken(ActionCall call, string actionName, string viewName, LanguageType languageType)
        {
            var searchedLocations = new List<string>();

            var descriptorParams = new BuildDescriptorParams("", actionName, viewName, String.Empty, false, null);
            var descriptor = DescriptorBuilder.BuildDescriptor(descriptorParams, searchedLocations);
            if (descriptor == null)
            {
                var errMsg = string.Format("View '{0}' could not be found in any of the following locations: {1}", viewName, string.Join(", ", searchedLocations));
                throw new CompilerException(errMsg);
            }
            
            descriptor.Language = languageType;

            return new SparkViewToken(call, descriptor, call.Method.Name, viewName);
        }

        private ViewEngineResult findViewInternal(ActionContext actionContext, string viewName, string masterName, bool findDefaultMaster, bool useCache)
        {
            var searchedLocations = new List<string>();
            var targetNamespace = actionContext.ActionNamespace;

            var descriptorParams = new BuildDescriptorParams(
                targetNamespace,
                actionContext.ActionName,
                viewName,
                masterName,
                findDefaultMaster,
                DescriptorBuilder.GetExtraParameters(actionContext));

            ISparkViewEntry entry;
            if (useCache)
            {
                if (tryGetCacheValue(descriptorParams, out entry) && entry.IsCurrent())
                {
                    return buildResult(entry);
                }

                return new ViewEngineResult(new List<string> { "Cache" });
            }

            var descriptor = DescriptorBuilder.BuildDescriptor(descriptorParams, searchedLocations);

            if (descriptor == null)
            {
                return new ViewEngineResult(searchedLocations);
            }

            entry = Engine.CreateEntry(descriptor);
            setCacheValue(descriptorParams, entry);
            
            return buildResult(entry);
        }

        private bool tryGetCacheValue(BuildDescriptorParams descriptorParams, out ISparkViewEntry entry)
        {
            lock (_cache)
            {
                return _cache.TryGetValue(descriptorParams, out entry);
            }
        }

        private void setCacheValue(BuildDescriptorParams descriptorParams, ISparkViewEntry entry)
        {
            lock (_cache)
            {
                _cache[descriptorParams] = entry;
            }
        }
    }

    public class ViewEngineResult
    {
        public ViewEngineResult(ISparkView view, SparkViewFactory factory)
        {
            View = view;
            Factory = factory;
        }

        public ViewEngineResult(List<string> searchedLocations)
        {
            var locations = string.Empty;
            searchedLocations.ForEach(loc => locations += string.Format("{0} ", loc));
            throw new ConfigurationErrorsException(string.Format("The view could not be in any of the following locations: {0}", locations));
        }

        public ISparkView View { get; set; }
        public SparkViewFactory Factory { get; set; }
    }
}