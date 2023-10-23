using System;
using NUnit.Framework;
using CatnipECS.Sample;
using UnityEngine;

namespace SimpleECSTests
{
    using CatnipECS;
    
    public class ECSTest
    {
        [Test]
        public void TestMatcher()
        {
            var matcher1 = new Matcher()
                .All<PositionComponent, VelocityComponent>();
            
            var matcher2 = new Matcher()
                .All<PositionComponent>()
                .Exclude<VelocityComponent>();

            var components = new[] {1, 2};
            Assert.AreEqual(true, matcher1.Check(components, components.Length));
            Assert.AreEqual(false, matcher2.Check(components, components.Length));
        }

        [Test]
        public void TestFeature()
        {
            var feature = new SystemsGroup(new World());
            feature.CreateSystem<MoveSystem>();

            feature.Initialize();
            feature.Execute();
            feature.TearDown();
        }
    }
}