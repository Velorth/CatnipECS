using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using UnityEngine;
using UnityEngine.Pool;

namespace CatnipECS
{
    [Serializable]
    [DebuggerTypeProxy(typeof(WorldDebugProxy))]
    public class World : IDisposable, ISerializable
    {
        private IComponentDataContainer[] _componentDataContainers = new IComponentDataContainer[64];
        private EntityData[] _entitiesData = new EntityData[1024];
        private int _entitiesCount;
        private readonly HashSet<Entity> _entities = new (1024);
        private Entity[] _recycledEntities = new Entity[1024];
        private int _recycledEntitiesCount;
        private readonly Dictionary<Matcher, GroupData> _groups = new (1024);
        private readonly Dictionary<int, List<GroupData>> _groupsByComponent = new(1024);
        private readonly Dictionary<Trigger, Collector> _collectors = new(1024);
        private bool _isDisposed;

        public World()
        {
        }

        /// <summary>
        /// Creates a new entity.
        /// </summary>
        /// <returns>Entity ready to use.</returns>
        public Entity CreateEntity()
        {
            if (_recycledEntitiesCount > 0)
            {
                var recycledEntity = _recycledEntities[--_recycledEntitiesCount];
                ref var entityData = ref _entitiesData[recycledEntity.Index];
                var entity = new Entity(recycledEntity.Index, entityData.Generation);
                _entities.Add(entity);
                return entity;
            }
            else
            {
                var id = _entitiesCount++;
                ArrayUtility.EnsureSize(ref _entitiesData, _entitiesCount);
                var entity = new Entity(id, 0);
                _entities.Add(entity);
                return entity;
            }
        }

        /// <summary>
        /// Destroys an entity.
        /// </summary>
        /// <param name="entity">Entity to destroy.</param>
        public void DestroyEntity(Entity entity)
        {
            RemoveAllComponents(entity);
            
            ref var entityData = ref _entitiesData[entity.Index];
            entityData.Generation++;
            
            ArrayUtility.EnsureSize(ref _recycledEntities, _recycledEntitiesCount + 1);
            _recycledEntities[_recycledEntitiesCount++] = entity;
            _entities.Remove(entity);

            foreach (var keyValue in _groups)
            {
                keyValue.Value.Remove(entity);
            }
        }

        /// <summary>
        /// Verifies that entity is not destroyed.
        /// </summary>
        /// <param name="entity">Entity to check.</param>
        /// <returns></returns>
        public bool IsEntityAlive(Entity entity)
        {
            ref var entityData = ref _entitiesData[entity.Index];
            return entityData.Generation == entity.Generation;
        }

        /// <summary>
        /// Checks if entity has a given component attached.
        /// </summary>
        /// <param name="entity">Entity.</param>
        /// <typeparam name="T">Component to check.</typeparam>
        /// <returns>True is component is attached, False otherwise.</returns>
        /// <exception cref="EntityDestroyedException">In case entity is destroyed.</exception>
        public bool HasComponent<T>(Entity entity) where T : IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];

            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            return entityData.FindComponentIndex<T>() != -1;
        }

        /// <summary>
        /// Gets an attached component.
        /// </summary>
        /// <param name="entity">Entity.</param>
        /// <typeparam name="T">Component type.</typeparam>
        /// <returns>Reference to the component data.</returns>
        /// <exception cref="EntityDestroyedException">In case entity is destroyed.</exception>
        public ref T GetComponent<T>(Entity entity) where T : IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];
            
            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentIndex = entityData.FindComponentIndex<T>();
            if (componentIndex == -1)
                throw new ComponentNotFoundException(entity);
            
            var dataContainer = GetDataContainer<T>();
            return ref dataContainer.Get(entityData.ComponentValues[componentIndex]);
        }

        public void ReplaceComponent<T>(Entity entity, T component) where T : IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];
            
            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentIndex = entityData.FindComponentIndex<T>();
            if (componentIndex == -1)
                throw new MissingComponentException("There is no component to replace");

            var dataContainer = GetDataContainer<T>();
            var dataIndex = entityData.ComponentValues[componentIndex];
            dataContainer.Set(dataIndex, ref component);
        }

        public ref T AddComponent<T>(Entity entity) where T : IComponentData => ref AddComponent<T>(entity, default);

        public ref T AddComponent<T>(Entity entity, T data) where T : IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];
            
            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentTypeIndex = ComponentTypeInfo.GetTypeIndex<T>();
            var componentIndex = entityData.FindComponentIndex<T>();
            if (componentIndex != -1)
                throw new InvalidOperationException("Component already exists");
            
            var dataContainer = GetDataContainer<T>();
            var dataIndex = dataContainer.Create();
            dataContainer.Set(dataIndex, ref data);
            
            entityData.AddComponent(componentTypeIndex, dataIndex);
            
            OnEntityChanged(entity, ref entityData, componentTypeIndex);

            return ref dataContainer.Get(dataIndex);
        }

        public void RemoveComponent<T>(Entity entity) where T : IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];

            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentIndex = entityData.FindComponentIndex<T>();
            if (componentIndex == -1)
                return;

            var dataIndex = entityData.ComponentValues[componentIndex];
            var dataContainer = GetDataContainer<T>();
            dataContainer.Destroy(dataIndex);

            entityData.RemoveComponentAt(componentIndex);

            OnEntityChanged(entity, ref entityData, ComponentTypeInfo.GetTypeIndex<T>());
        }

        public void RemoveAllComponents(Entity entity)
        {
            ref var entityData = ref _entitiesData[entity.Index];

            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentIndex = entityData.ComponentsCount - 1;
            while (componentIndex >= 0)
            {
                var componentTypeIndex = entityData.ComponentTypes[componentIndex];
                entityData.RemoveComponentAt(componentIndex);
                
                OnEntityChanged(entity, ref entityData, componentTypeIndex);
                componentIndex--;
            }
        }

        public bool HasBuffer<T>(Entity entity) where T : IBufferElementData =>
            HasComponent<DynamicBuffer<T>>(entity);

        public ref DynamicBuffer<T> GetBuffer<T>(Entity entity) where T : IBufferElementData
        {
            return ref GetComponent<DynamicBuffer<T>>(entity);
        }

        public ref DynamicBuffer<T> GetOrCreateBuffer<T>(Entity entity) where T : IBufferElementData
        {
            if (HasBuffer<T>(entity))
                return ref GetBuffer<T>(entity);
            return ref AddBuffer<T>(entity);
        }

        public ref DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : IBufferElementData
        {
            var buffer = new DynamicBuffer<T>();
            buffer.Initialize();
            return ref AddComponent(entity, buffer);
        }

        public void SetEnabled(Entity entity)
        {
            if (HasComponent<Disabled>(entity))
            {
                RemoveComponent<Disabled>(entity);
            }
        }

        public void SetDisabled(Entity entity)
        {
            if (!HasComponent<Disabled>(entity))
            {
                AddComponent<Disabled>(entity);
            }
        }

        public Entity CreateSingleton<T>() where T : IComponentData
        {
            var entity = CreateEntity();
            AddComponent<T>(entity, default);
            return entity;
        }

        public T GetSingleton<T>() where T : IComponentData
        {
            var group = GetGroup(new Matcher(TypesSet<T>.Id, 0, 0));
            var entity = group.GetSingleEntity();
            return GetComponent<T>(entity);
        }

        private void OnEntityChanged(Entity entity, ref EntityData entityData, int componentTypeIndex)
        {
            if (!_groupsByComponent.TryGetValue(componentTypeIndex, out var groups)) 
                return;
            
            foreach (var group in groups)
            {
                if (group.Matches(ref entityData))
                {
                    group.Add(entity);
                }
                else
                {
                    group.Remove(entity);
                }
            }
        }

        public Group GetGroup(Matcher matcher)
        {
            return new Group(GetGroupData(matcher));
        }

        public void GetGroup(Matcher matcher, List<Entity> buf)
        {
            foreach (var entity in GetGroup(matcher))
            {
                buf.Add(entity);
            }
        }
        
        private IComponentDataContainer<T> GetDataContainer<T>() where T : IComponentData
        {
            var index = ComponentTypeInfo.GetTypeIndex<T>();
            if (index >= _componentDataContainers.Length)
            {
                var newLength = _componentDataContainers.Length;
                while (index >= newLength)
                {
                    newLength *= 2;
                }
                
                Array.Resize(ref _componentDataContainers, newLength);
            }

            _componentDataContainers[index] ??= new StructComponentDataContainer<T>();
            return (IComponentDataContainer<T>) _componentDataContainers[index];
        }
        
        private ref T CreateComponent<T>(Entity entity, ref EntityData data) where T : IComponentData
        {
            var dataContainer = GetDataContainer<T>();
            var dataIndex = dataContainer.Create();
            var componentIndex = ComponentTypeInfo.GetTypeIndex<T>();
            data.AddComponent(componentIndex, dataIndex);
            
            OnEntityChanged(entity, ref data, componentIndex);
            return ref dataContainer.Get(dataIndex);
        }

        private GroupData GetGroupData(Matcher matcher)
        {
            if (_groups.TryGetValue(matcher, out var groupData))
                return groupData;

            groupData = new GroupData(this, matcher);
            _groups[matcher] = groupData;
            
            GetOrCreateGroupsForComponent(ComponentTypeInfo.GetTypeIndex<Disabled>()).Add(groupData);

            foreach (var componentIndex in TypesSet.GetTypes(matcher.ExcludeAllId))
            {
                GetOrCreateGroupsForComponent(componentIndex).Add(groupData);
            }

            foreach (var componentIndex in TypesSet.GetTypes(matcher.IncludeAllId))
            {
                GetOrCreateGroupsForComponent(componentIndex).Add(groupData);
            }

            foreach (var componentIndex in TypesSet.GetTypes(matcher.IncludeAnyId))
            {
                GetOrCreateGroupsForComponent(componentIndex).Add(groupData);
            }

            foreach (var entity in _entities)
            {
                ref var entityData = ref _entitiesData[entity.Index];
                if (groupData.Matches(ref entityData))
                {
                    groupData.Add(entity);
                }
            }

            return groupData;
        }

        private List<GroupData> GetOrCreateGroupsForComponent(int componentIndex)
        {
            if (!_groupsByComponent.TryGetValue(componentIndex, out var groupsList))
            {
                groupsList = new List<GroupData>();
                _groupsByComponent.Add(componentIndex, groupsList);
            }

            return groupsList;
        }

        public Collector GetCollector(Trigger trigger)
        {
            if (!_collectors.TryGetValue(trigger, out var collector))
            {
                collector = CreateCollector(trigger);
            }

            return collector;
        }

        private Collector CreateCollector(Trigger trigger)
        {
            var trackedGroup = GetGroupData(trigger.Matcher);
            var collector = new Collector(this, trigger);

            _collectors[trigger] = collector;
            trackedGroup.AddListener(collector);

            return collector;
        }

        public bool TryGetComponent<T>(Entity entity, out T data) where T: IComponentData
        {
            ref var entityData = ref _entitiesData[entity.Index];
            
            if (entityData.Generation != entity.Generation)
                throw new EntityDestroyedException(entity);
            
            var componentIndex = entityData.FindComponentIndex<T>();
            if (componentIndex == -1)
            {
                data = default;
                return false;
            }
            
            var dataContainer = GetDataContainer<T>();
            data = dataContainer.Get(entityData.ComponentValues[componentIndex]);
            return true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
            }
            
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~World()
        {
            Dispose(false);
        }

        private World(SerializationInfo info, StreamingContext context)
        {
            _entitiesCount = info.GetInt32(nameof(_entitiesCount));
            _recycledEntitiesCount = info.GetInt32(nameof(_recycledEntitiesCount));
            _recycledEntities = (Entity[])info.GetValue(nameof(_recycledEntities), typeof(Entity[]));
            _entitiesData = (EntityData[]) info.GetValue(nameof(_entitiesData), typeof(EntityData[]));
            
            var dataContainers = (IComponentDataContainer[])info.GetValue(nameof(_componentDataContainers), typeof(IComponentDataContainer[]));
            // TODO: Register data containers and remap type indexes
            var typeIndexMap = new int[dataContainers.Length];
            var maxTypeIndex = 0;
            for (var i = 0; i < dataContainers.Length; ++i)
            {
                typeIndexMap[i] = dataContainers[i] == null
                    ? -1
                    : ComponentTypeInfo.GetTypeIndex(dataContainers[i].ComponentType);
                maxTypeIndex = Math.Max(typeIndexMap[i], maxTypeIndex);
            }

            _entities = new HashSet<Entity>();
            for (var entityIndex = 0; entityIndex < _entitiesCount; ++entityIndex)
            {
                ref var entityData = ref _entitiesData[entityIndex];
                var componentTypes = entityData.ComponentTypes;
                
                for (var componentIndex = 0; componentIndex < entityData.ComponentsCount; ++componentIndex)
                {
                    componentTypes[componentIndex] = typeIndexMap[componentTypes[componentIndex]];
                }

                _entities.Add(new Entity(entityIndex, entityData.Generation));
            }

            // Remove recycled entities from the list
            for (var index = 0; index < _recycledEntitiesCount; index++)
            {
                var entity = _recycledEntities[index];
                var entityData = _entitiesData[entity.Index];
                _entities.Remove(new Entity(entity.Index, entityData.Generation));
            }

            _componentDataContainers = new IComponentDataContainer[Math.Max(maxTypeIndex + 1, dataContainers.Length)];
            for (var i = 0; i < dataContainers.Length; ++i)
            {
                var componentTypeIndex = typeIndexMap[i];
                if (componentTypeIndex == -1)
                    continue;
                
                _componentDataContainers[componentTypeIndex] = dataContainers[i];
            }
            
            // Initialize non-serialized fields
            _groups = new Dictionary<Matcher, GroupData>(1024);
            _groupsByComponent = new Dictionary<int, List<GroupData>>(1024);
            _collectors = new Dictionary<Trigger, Collector>(1024);
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            var containers = new IComponentDataContainer[_componentDataContainers.Length];
            for (var i = 0; i < _componentDataContainers.Length; ++i)
            {
                var container = _componentDataContainers[i];
                if (container == null || !container.ComponentType.IsSerializable)
                    continue;

                containers[i] = container;
            }

            var entitiesBuf = ListPool<EntityData>.Get();
            var typesBuf = ListPool<int>.Get();
            var valuesBuf = ListPool<int>.Get();

            for (var entityIndex = 0; entityIndex < _entitiesCount; ++entityIndex)
            {
                var entityData = _entitiesData[entityIndex];
                typesBuf.Clear();
                valuesBuf.Clear();
                for (var componentIndex = 0; componentIndex < entityData.ComponentsCount; ++componentIndex)
                {
                    var componentTypeIndex = entityData.ComponentTypes[componentIndex];
                    
                    // Skip non-serialized components
                    if (containers[componentTypeIndex] == null)
                        continue;
                    
                    typesBuf.Add(componentTypeIndex);
                    valuesBuf.Add(entityData.ComponentValues[componentIndex]);
                }

                var componentsCount = typesBuf.Count;
                entitiesBuf.Add(new EntityData
                {
                    Generation = entityData.Generation,
                    ComponentsCount = componentsCount,
                    ComponentTypes = componentsCount == 0 ? null : typesBuf.ToArray(),
                    ComponentValues = componentsCount == 0 ? null : valuesBuf.ToArray()
                });
            }

            info.AddValue(nameof(_componentDataContainers), containers);
            info.AddValue(nameof(_entitiesCount), _entitiesCount);
            info.AddValue(nameof(_entitiesData), entitiesBuf.ToArray());
            info.AddValue(nameof(_recycledEntitiesCount), _recycledEntitiesCount);
            info.AddValue(nameof(_recycledEntities), _recycledEntities);
            
            ListPool<EntityData>.Release(entitiesBuf);
            ListPool<int>.Release(typesBuf);
            ListPool<int>.Release(valuesBuf);
        }
        
        internal struct WorldDebugProxy
        {
            public EntityDebugData[] Entities;
            private readonly World _world;

            public WorldDebugProxy(World world)
            {
                _world = world;
                Entities = world._entitiesData.Take(world._entitiesCount)
                    .Select(data => new EntityDebugData
                    {
                        Components = GetBoxedComponents(world, data)
                    }).ToArray();
            }

            private static object[] GetBoxedComponents(World world, EntityData entityData)
            {
                var result = new object[entityData.ComponentsCount];
                for (var componentIndex = 0; componentIndex < entityData.ComponentsCount; ++componentIndex)
                {
                    var typeIndex = entityData.ComponentTypes[componentIndex];
                    var dataContainer = world._componentDataContainers[typeIndex];
                    var dataIndex = entityData.ComponentValues[componentIndex];
                    result[componentIndex] = dataContainer.GetBoxedComponent(dataIndex);
                }

                return result;
            }
        }
        
        internal struct EntityDebugData
        {
            public object[] Components;
        }
    }

    internal interface IInitializeSystemHandler
    {
        void Initialize(ref SystemState state);
    }

    internal readonly struct InitializeSystemHandler : IInitializeSystemHandler
    {
        private readonly IInitializeSystem _system;

        public InitializeSystemHandler(IInitializeSystem system)
        {
            _system = system;
        }
        
        public void Initialize(ref SystemState state)
        {
            _system.OnInitialize(ref state);
        }
    }

    internal interface IExecuteSystemHandler
    {
        void Execute(ref SystemState state);
    }

    internal readonly struct ExecuteSystemHandler : IExecuteSystemHandler
    {
        private readonly IExecuteSystem _system;

        public ExecuteSystemHandler(IExecuteSystem system)
        {
            _system = system;
        }
        
        public void Execute(ref SystemState state)
        {
            _system.OnExecute(ref state);
        }
    }

    internal class ReactiveSystemHandler : IInitializeSystemHandler, IExecuteSystemHandler
    {
        private readonly IReactiveSystem _system;
        private Trigger _trigger;
        private Collector _collector;

        public ReactiveSystemHandler(IReactiveSystem reactiveSystem)
        {
            _system = reactiveSystem;
        }

        public void CreateCollector(World world)
        {
            _trigger = _system.Trigger;
            _collector = world.GetCollector(_trigger);
        }

        public void Initialize(ref SystemState state)
        {
            if (_collector.HasEntities())
            {
                using var affectedEntities = _collector.Get();
                _collector.Clear();
                _system.OnExecute(ref state, affectedEntities);
            }
        }

        public void Execute(ref SystemState state)
        {
            if (_collector.HasEntities())
            {
                using var affectedEntities = _collector.Get();
                _collector.Clear();
                _system.OnExecute(ref state, affectedEntities);
            }
        }
    }

    internal interface ITearDownSystemHandler
    {
        void TearDown(ref SystemState state);
    }

    internal readonly struct TearDownSystemHandler : ITearDownSystemHandler
    {
        private readonly ITearDownSystem _system;

        public TearDownSystemHandler(ITearDownSystem system)
        {
            _system = system;
        }
        
        public void TearDown(ref SystemState state)
        {
            _system.TearDown(ref state);
        }
    }

    public static class WorldQueries
    {
        public static Group Query<T>(this World world) where T : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T>.Id, 0, 0));

        public static Group Query<T1, T2>(this World world)
            where T1 : IComponentData
            where T2 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2>.Id, 0, 0));
        
        public static Group Query<T1, T2, T3>(this World world)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3>.Id, 0, 0));
        
        public static Group Query<T1, T2, T3, T4>(this World world)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3, T4>.Id, 0, 0));
        
        public static Group Query<T1, T2, T3, T4, T5>(this World world)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData 
            where T5 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3, T4, T5>.Id, 0, 0));
        
        

        public static void Query<T>(this World world, List<Entity> buf) where T : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T>.Id, 0, 0), buf);

        public static void Query<T1, T2>(this World world, List<Entity> buf)
            where T1 : IComponentData
            where T2 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2>.Id, 0, 0), buf);
        
        public static void Query<T1, T2, T3>(this World world, List<Entity> buf)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3>.Id, 0, 0), buf);
        
        public static void Query<T1, T2, T3, T4>(this World world, List<Entity> buf)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3, T4>.Id, 0, 0), buf);
        
        public static void Query<T1, T2, T3, T4, T5>(this World world, List<Entity> buf)
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData 
            where T5 : IComponentData =>
            world.GetGroup(new Matcher(TypesSet<T1, T2, T3, T4, T5>.Id, 0, 0), buf);
    }
}