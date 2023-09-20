using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CatnipECS
{
    internal static class ComponentTypeInfo
    {
        private static Dictionary<Type, int> _typeIndexes = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetTypeIndex<T>() where T: IComponentData
        {
            return ComponentTypeIndexer<T>.TypeIndex;
        }

        public static int GetTypeIndex(Type type)
        {
            if (_typeIndexes.TryGetValue(type, out var index))
                return index;

            index = _typeIndexes.Count + 1;
            _typeIndexes.Add(type, index);
            return index;
        }
        
        private struct ComponentTypeIndexer<T> where T: IComponentData
        {
            // ReSharper disable once StaticMemberInGenericType
            // Index must be unique per component type.
            public static readonly int TypeIndex;
        
            static ComponentTypeIndexer()
            {
                TypeIndex = GetTypeIndex(typeof(T));
            }
        }
    }
}