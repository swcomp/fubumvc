using System;
using System.Collections.Generic;
using FubuMVC.Core;
using FubuMVC.Core.Registration;
using FubuMVC.Core.Registration.Nodes;
using HtmlTags;
using NUnit.Framework;
using FubuTestingSupport;

namespace FubuMVC.Tests.Registration
{
    [TestFixture]
    public class default_action_discovery
    {
        [Test]
        public void with_no_other_explicit_action_discovery()
        {
            var graph = new FubuRegistry(x =>
            {

            }).BuildGraph();


            graph.BehaviorFor<MyEndpoint>(x => x.Go()).ShouldNotBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go()).ShouldNotBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go2()).ShouldNotBeNull();
        }

        [Test]
        public void does_not_find_endpoints_with_an_explicit_action_discovery()
        {
            var graph = new FubuRegistry(x =>
            {
                x.Actions.IncludeClassesSuffixedWithController();
            }).BuildGraph();

            graph.BehaviorFor<MyEndpoint>(x => x.Go()).ShouldBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go()).ShouldBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go2()).ShouldBeNull();
        }

        [Test]
        public void does_not_find_endpoints_with_an_explicit_action_discovery_policy()
        {
            var graph = new FubuRegistry(x =>
            {
                x.Actions.FindWith(new FakeActionSource());
            }).BuildGraph();

            graph.BehaviorFor<MyEndpoint>(x => x.Go()).ShouldBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go()).ShouldBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go2()).ShouldBeNull();
        }

        [Test]
        public void will_find_endpoints_if_endpoint_is_explictly_specified_too()
        {
            var graph = new FubuRegistry(x =>
            {
                x.Actions.FindWith(new FakeActionSource());
                x.Actions.IncludeClassesSuffixedWithEndpoint();
            }).BuildGraph();

            graph.BehaviorFor<MyEndpoint>(x => x.Go()).ShouldNotBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go()).ShouldNotBeNull();
            graph.BehaviorFor<MyEndpoints>(x => x.Go2()).ShouldNotBeNull();
        }
    }

    public class FakeActionSource : IActionSource
    {
        public IEnumerable<ActionCall> FindActions(TypePool types)
        {
            yield break;
        }
    }

    public class MyEndpoint
    {
        public HtmlTag Go()
        {
            return new HtmlTag("div");
        }
    }

    public class MyEndpoints
    {
        public HtmlTag Go()
        {
            return new HtmlTag("div");
        }

        public HtmlTag Go2()
        {
            return new HtmlTag("div");
        }
    }
}