using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CatnipECS
{
    [Serializable]
    internal struct EntityData
    {
        public int Generation;
        public int ComponentsCount;
        public int[] ComponentTypes;
        public int[] ComponentValues;

        public int FindComponentIndex<T>() where T : IComponentData
        {
            var typeIndex = ComponentTypeInfo.GetTypeIndex<T>();
            for (var i = 0; i < ComponentsCount; ++i)
                if (ComponentTypes[i] == typeIndex)
                    return i;

            return -1;
        }

        public void AddComponent(int type, int value)
        {
            var index = ComponentsCount++;
            EnsureEnoughLength(ComponentsCount);
            ComponentTypes[index] = type;
            ComponentValues[index] = value;
        }

        public void RemoveComponentAt(int index)
        {
            ComponentsCount--;
            ComponentTypes[index] = ComponentTypes[ComponentsCount];
            ComponentValues[index] = ComponentValues[ComponentsCount];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureEnoughLength(int length)
        {
            if (ComponentTypes == null)
            {
                var size = Mathf.Max(4, length);
                ComponentTypes = new int[size];
                ComponentValues = new int[size];
                return;
            }
            
            if (length <= ComponentTypes.Length)
                return;

            var newLength = Mathf.Max(4, ComponentTypes.Length);
            while (length > newLength)
            {
                newLength *= 2;
            }
            
            Array.Resize(ref ComponentTypes, newLength);
            Array.Resize(ref ComponentValues, newLength);
        }

        public void Clear()
        {
            ComponentsCount = 0;
        }
    }
}