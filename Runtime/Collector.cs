using System.Collections.Generic;
using Unity.Collections;

namespace CatnipECS
{
    public class Collector : IGroupEventListener
    {
        private readonly World _world;
        private readonly Trigger _trigger;
        private readonly HashSet<Entity> _data = new();

        public NativeArray<Entity> Get()
        {
            var buffer = new NativeArray<Entity>(_data.Count, Allocator.Temp);
            var index = 0;
            foreach (var entity in _data)
            {
                buffer[index++] = entity;
            }

            return buffer;
        }

        public bool HasEntities() => _data.Count > 0;

        public Collector(World world, Trigger trigger)
        {
            _world = world;
            _trigger = trigger;
        }

        public void OnEntityAdded(Entity entity)
        {
            HandleEntity(entity, GroupEvent.Add);
        }

        public void OnEntityRemoved(Entity entity)
        {
            HandleEntity(entity, GroupEvent.Remove);
        }

        private void HandleEntity(Entity entity, GroupEvent eventFilter)
        {
            if (!_trigger.Event.HasFlag(eventFilter))
            {
                _data.Remove(entity);
                return;
            }

            _data.Add(entity);
        }

        public void Clear()
        {
            _data.Clear();
        }
    }
}