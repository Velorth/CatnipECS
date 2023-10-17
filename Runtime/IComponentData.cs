using System;
using System.Collections.Generic;

namespace CatnipECS
{
    /// <summary>
    /// Interface for components. Every component must implement it.
    /// </summary>
    public interface IComponentData
    {
    }

    [Serializable]
    internal struct Disabled : IComponentData
    {
    }

    public interface IBufferElementData
    {
        
    }

    [Serializable]
    public struct DynamicBuffer<T>: IComponentData where T : IBufferElementData
    {
        private List<T> _data;

        public int Count => _data.Count;

        public int Capacity
        {
            get => _data.Capacity;
            set => _data.Capacity = value;
        }

        public T this[int index]
        {
            get => _data[index];
            set => _data[index] = value;
        }

        public List<T>.Enumerator GetEnumerator() => _data.GetEnumerator();

        public void Add(T element) => _data.Add(element);

        public void Clear() => _data.Clear();

        public void RemoveAt(int index) => _data.RemoveAt(index);

        public void Initialize()
        {
            _data = new List<T>();
        }
    }

    public static class DynamicBufferExt
    {
        public static bool Contains<T>(this DynamicBuffer<T> buffer, T value) where T : IBufferElementData, IEquatable<T>
        {
            foreach (var item in buffer)
            {
                if (item.Equals(value))
                    return true;
            }

            return false;
        } 
    }
}