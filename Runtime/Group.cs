using System;
using System.Collections;
using System.Collections.Generic;

namespace CatnipECS
{
    public struct Group
    {
        private readonly GroupData _data;

        internal Group(GroupData data)
        {
            _data = data;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        public Entity GetSingleEntity()
        {
            return _data.GetSingleEntity();
        }

        public struct Enumerator : IEnumerator<Entity>
        {
            private HashSet<Entity>.Enumerator _enumerator;

            internal Enumerator(Group group)
            {
                _enumerator = group._data.GetEnumerator();
            }

            public bool MoveNext()
            {
                return _enumerator.MoveNext();
            }

            void IEnumerator.Reset()
            {
                ((IEnumerator)_enumerator).Reset();
            }

            public Entity Current => _enumerator.Current;

            object IEnumerator.Current => Current;

            void IDisposable.Dispose()
            {
            }
        }
    }
    
    internal class GroupData
    {
        private readonly HashSet<Entity> _entities = new HashSet<Entity>(1024);
        private readonly Matcher _matcher;
        private readonly World _world;
        private readonly List<IGroupEventListener> _listeners = new();

        public bool IsEmpty => _entities.Count == 0;

        public GroupData(World world, Matcher matcher)
        {
            _world = world;
            _matcher = matcher;
        }
        
        public bool Matches(ref EntityData entityData)
        {
            return _matcher.Check(entityData.ComponentTypes, entityData.ComponentsCount) && 
                   Array.IndexOf(entityData.ComponentTypes, ComponentTypeInfo.GetTypeIndex<Disabled>()) == -1;
        }

        public bool Add(Entity entity)
        {
            var result = _entities.Add(entity); 
            
            if (result)
            {
                foreach (var listener in _listeners)
                {
                    listener.OnEntityAdded(entity);
                }
            }

            return result;
        }

        public bool Remove(Entity entity)
        {
            var result = _entities.Remove(entity);
            if (result)
            {
                foreach (var listener in _listeners)
                {
                    listener.OnEntityRemoved(entity);
                }
            }

            return result;
        }
        
        public HashSet<Entity>.Enumerator GetEnumerator() => _entities.GetEnumerator();

        internal void Clear()
        {
            _entities.Clear();
        }

        public void AddListener(IGroupEventListener listener)
        {
            _listeners.Add(listener);
        }

        public Entity GetSingleEntity()
        {
            if (_entities.Count != 1)
                throw new InvalidOperationException("Component is not a singleton");

            using var enumerator = _entities.GetEnumerator();
            enumerator.MoveNext();
            return enumerator.Current;
        }
    }

    internal interface IGroupEventListener
    {
        void OnEntityAdded(Entity entity);
        void OnEntityRemoved(Entity entity);
    }
}