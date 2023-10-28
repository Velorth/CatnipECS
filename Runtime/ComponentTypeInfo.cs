using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CatnipECS
{
    internal static class ComponentTypeInfo
    {
        private static readonly Dictionary<Type, int> TypeToIndexMap = new();
        private static Dictionary<int, Type> IndexToTypeMap = new();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetTypeIndex<T>() where T: IComponentData
        {
            return ComponentTypeIndexer<T>.TypeIndex;
        }

        public static int GetTypeIndex(Type type)
        {
            if (TypeToIndexMap.TryGetValue(type, out var index))
                return index;

            index = TypeToIndexMap.Count + 1;
            TypeToIndexMap.Add(type, index);
            IndexToTypeMap.Add(index, type);
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

        public static Type GetTypeByIndex(int typeIndex)
        {
            return IndexToTypeMap[typeIndex];
        }
    }
}