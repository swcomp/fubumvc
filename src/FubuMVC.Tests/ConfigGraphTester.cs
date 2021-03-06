﻿using System;
using System.Reflection;
using Bottles.PackageLoaders.Assemblies;
using FubuMVC.Core;
using FubuMVC.Core.Configuration;
using FubuMVC.Core.Registration;
using NUnit.Framework;
using System.Linq;
using FubuTestingSupport;
using System.Collections.Generic;
using Rhino.Mocks;

namespace FubuMVC.Tests
{
    [TestFixture]
    public class ConfigGraphTester
    {

        [Test]
        public void add_configuration_action_with_indeterminate_ConfigurationType()
        {
            Exception<ArgumentOutOfRangeException>.ShouldBeThrownBy(() => {
                new ConfigGraph(Assembly.GetExecutingAssembly()).Add(new IndeterminateAction(), null);
            });
        }

        [Test]
        public void add_configuration_action_with_default_configuration_type()
        {
            var graph = new ConfigGraph(Assembly.GetExecutingAssembly());

            var action = new IndeterminateAction();

            graph.Add(action, ConfigurationType.Explicit);

            graph.ActionsFor(ConfigurationType.Explicit).Single()
                .ShouldBeTheSameAs(action);
        }

        [Test]
        public void add_configuration_action_that_is_marked_with_attribute()
        {
            var graph = new ConfigGraph(Assembly.GetExecutingAssembly());

            var action = new DeterminateAciton();

            graph.Add(action);

            graph.ActionsFor(ConfigurationType.Explicit).Single()
                .ShouldBeTheSameAs(action);
        }


    }
    
    [ConfigurationType(ConfigurationType.Explicit)]
    public class DeterminateAciton : IConfigurationAction
    {
        public void Configure(BehaviorGraph graph)
        {
            throw new NotImplementedException();
        }
    }

    public class IndeterminateAction : IConfigurationAction
    {
        public void Configure(BehaviorGraph graph)
        {
            throw new NotImplementedException();
        }
    }

    public class SomeFubuRegistry : FubuRegistry
    {
        
    }

    public class FakeRegistryExtension : IFubuRegistryExtension
    {
        public void Configure(FubuRegistry registry)
        {
            throw new NotImplementedException();
        }
    }
}