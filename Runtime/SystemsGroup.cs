using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatnipECS
{
    public class SystemsGroup
    {
        private readonly World _world;
        private readonly List<IInitializeSystemHandler> _initializeSystems = new();
        private readonly List<IExecuteSystemHandler> _executeSystems = new();
        private readonly List<ITearDownSystemHandler> _teardownSystems = new();

        public SystemsGroup(World world)
        {
            _world = world;
        }

        public T CreateSystem<T>() where T : ISystem, new()
        {
            var system = new T();
            var boxedSystem = (ISystem) system;
            
            if (boxedSystem is IInitializeSystem initializeSystem)
            {
                _initializeSystems.Add(new InitializeSystemHandler(initializeSystem));
            }

            if (boxedSystem is IExecuteSystem executeSystem)
            {
                _executeSystems.Add(new ExecuteSystemHandler(executeSystem));
            }

            if (boxedSystem is IReactiveSystem reactiveSystem)
            {
                var reactiveSystemHandler = new ReactiveSystemHandler(reactiveSystem);
                reactiveSystemHandler.CreateCollector(_world);
                
                _initializeSystems.Add(reactiveSystemHandler);
                _executeSystems.Add(reactiveSystemHandler);
            }

            if (boxedSystem is ITearDownSystem tearDownSystem)
            {
                _teardownSystems.Add(new TearDownSystemHandler(tearDownSystem));
            }
            
            return system;
        }

        public void Initialize()
        {
            var state = new SystemState(_world);
            
            foreach (var system in _initializeSystems)
            {
                system.Initialize(ref state);
            }
        }

        public void Execute()
        {
            var state = new SystemState(_world);

            foreach (var system in _executeSystems)
            {
                try
                {
                    system.Execute(ref state);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void TearDown()
        {
            var state = new SystemState(_world);
            
            foreach (var system in _teardownSystems)
            {
                system.TearDown(ref state);
            }
        }
    }
}