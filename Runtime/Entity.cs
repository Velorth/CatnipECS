using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Collections;
using UnityEngine;

namespace CatnipECS
{
    [Serializable]
    public readonly struct Entity : IEquatable<Entity>, IComparable<Entity>
    {
        public static readonly Entity None = new();
        
        internal readonly int Index;
        internal readonly int Generation;

        internal Entity(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }
        
        public bool Equals(Entity other) => 
            Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => 
            obj is Entity other && Equals(other);

        public override int GetHashCode() => Index;

        public static bool operator ==(Entity a, Entity b) => a.Equals(b);

        public static bool operator !=(Entity a, Entity b) => !a.Equals(b);

        int IComparable<Entity>.CompareTo(Entity other) => Index.CompareTo(other.Index);

        public override string ToString() => $"{Index}:{Generation}";
    }

    public class EntityDestroyedException : Exception
    {
        public EntityDestroyedException(Entity entity) : base($"Entity {entity} is destroyed.")
        {
        }
    }

    public ref struct SystemState
    {
        public readonly World World;

        internal SystemState(World world)
        {
            World = world;
        }
    }

    public interface ISystem
    {
        
    }

    public interface IInitializeSystem : ISystem
    {
        void OnInitialize(ref SystemState state);
    }

    public interface IExecuteSystem : ISystem
    {
        void OnExecute(ref SystemState state);
    }

    public interface IReactiveSystem : ISystem
    {
        Trigger Trigger { get; }

        void OnExecute(ref SystemState state, NativeArray<Entity> affectedEntities);
    }

    public interface ITearDownSystem : ISystem
    {
        void TearDown(ref SystemState state);
    }

    internal interface IComponentDataContainer
    {
        Type ComponentType { get; }
        
        /// <summary>
        /// Creates a new value and returns an index of created data.
        /// </summary>
        int Create();

        /// <summary>
        /// Destroys data at given index
        /// </summary>
        /// <param name="index">Index of data to destroy</param>
        void Destroy(int index);

        /// <summary>
        /// Gets the value of the component.
        /// This method is designed for debug purposes and causes boxing of value type components.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        object GetBoxedComponent(int index);
    }

    internal interface IComponentDataContainer<T> : IComponentDataContainer where T : IComponentData
    {
        ref T Get(int index);

        void Set(int index, ref T value);
    }
    
    [Serializable]
    internal class StructComponentDataContainer<T> : IComponentDataContainer<T> where T : IComponentData
    {
        private T[] _data = new T[1024];
        private int[] _thrash = new int[64];
        private int _count;
        private int _thrashedCount;

        public Type ComponentType => typeof(T);

        public int Create()
        {
            int index;
            if (_thrashedCount > 0)
            {
                index = _thrash[--_thrashedCount];
                _data[index] = default;
                return index;
            }

            index = _count;
            ArrayUtility.EnsureSize(ref _data, ++_count);
            return index;
        }

        public void Destroy(int index)
        {
            _thrashedCount++;
            ArrayUtility.EnsureSize(ref _thrash, _thrashedCount);
            _thrash[_thrashedCount - 1] = index;
        }

        object IComponentDataContainer.GetBoxedComponent(int index) => Get(index);

        public ref T Get(int index)
        {
            ArrayUtility.EnsureSize(ref _data, index + 1);

            return ref _data[index];
        }

        public void Set(int index, ref T value)
        {
            _data[index] = value;
        }
    }
    
    internal static class ArrayUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureSize<T>(ref T[] array, int size)
        {
            if (size <= array.Length) 
                return;
            
            var newSize = array.Length;
            while (size >= newSize)
            {
                newSize *= 2;
            }
            Array.Resize(ref array, newSize);
        }
    }
}