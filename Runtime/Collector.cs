namespace CatnipECS
{
    public class Collector : IGroupEventListener
    {
        private readonly World _world;
        private readonly Trigger _trigger;
        private Group _group;
        private GroupData _groupData;

        public Group Get() => _group;

        public bool HasEntities() => !_groupData.IsEmpty;

        public Collector(World world, Trigger trigger)
        {
            _world = world;
            _trigger = trigger;
            _groupData = new GroupData(world, trigger.Matcher);
            _group = new Group(_groupData);
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
                _groupData.Remove(entity);
                return;
            }

            _groupData.Add(entity);
        }

        public void Clear()
        {
            _groupData.Clear();
        }
    }
}