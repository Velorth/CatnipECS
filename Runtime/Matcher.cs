using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CatnipECS
{
    public struct Matcher : IEquatable<Matcher>
    {
        internal int IncludeAllId;
        internal int IncludeAnyId;
        internal int ExcludeAllId;

        public Matcher(Matcher a, Matcher b)
        {
            IncludeAllId = MergeComponentTypes(a.IncludeAllId, b.IncludeAllId);
            IncludeAnyId = MergeComponentTypes(a.IncludeAnyId, b.IncludeAnyId);
            ExcludeAllId = MergeComponentTypes(a.ExcludeAllId, b.ExcludeAllId);
        }

        public Matcher(int includeAll, int includeAny, int exclude)
        {
            IncludeAllId = includeAll;
            IncludeAnyId = includeAny;
            ExcludeAllId = exclude;
        }

        public readonly bool Check(int[] componentTypes, int length)
        {
            var excludeAll = TypesSet.GetTypes(ExcludeAllId);
            var includeAll = TypesSet.GetTypes(IncludeAllId);
            var includeAny = TypesSet.GetTypes(IncludeAnyId);
            
            var includeAllCount = includeAll.Length;
            var includeAnyCount = includeAny.Length > 0 ? 1 : 0;

            for (var index = 0; index < length; index++)
            {
                var componentType = componentTypes[index];
                if (Array.IndexOf(excludeAll, componentType) != -1)
                    return false;

                if (includeAllCount > 0 && Array.IndexOf(includeAll, componentType) != -1)
                    includeAllCount--;

                if (includeAnyCount > 0 && Array.IndexOf(includeAny, componentType) != -1)
                    includeAnyCount--;
            }

            return includeAllCount == 0 && includeAnyCount == 0;
        }

        public Matcher Any<T>() where T : IComponentData
        {
            return new Matcher(this, new Matcher(0, TypesSet<T>.Id, 0));
        }

        public Matcher Any<T1, T2>()
            where T1 : IComponentData
            where T2 : IComponentData
        {
            return new Matcher(this, new Matcher(0, TypesSet<T1, T2>.Id, 0));
        }
        
        public Matcher Any<T1, T2, T3>()
            where T1 : IComponentData
            where T2 : IComponentData
            where T3: IComponentData
        {
            return new Matcher(this, new Matcher(0, TypesSet<T1, T2, T3>.Id, 0));
        }
        
        public Matcher Any<T1, T2, T3, T4>()
            where T1 : IComponentData
            where T2 : IComponentData
            where T3: IComponentData
            where T4: IComponentData
        {
            return new Matcher(this, new Matcher(0, TypesSet<T1, T2, T3, T4>.Id, 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matcher Any(params int[] componentTypes)
        {
            return new Matcher(this, new Matcher(0, TypesSet.GetId(componentTypes), 0));
        }

        public Matcher All<T>()
            where T : IComponentData
        {
            return new Matcher(this, new Matcher(TypesSet<T>.Id, 0, 0));
        }

        public Matcher All<T1, T2>()
            where T1 : IComponentData
            where T2 : IComponentData
        {
            return new Matcher(this, new Matcher(TypesSet<T1, T2>.Id, 0, 0));
        }

        public Matcher All<T1, T2, T3>()
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
        {
            return new Matcher(this, new Matcher(TypesSet<T1, T2, T3>.Id, 0, 0));
        }

        public Matcher All<T1, T2, T3, T4>()
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData
        {
            return new Matcher(this, new Matcher(TypesSet<T1, T2, T3, T4>.Id, 0, 0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matcher All(params int[] componentTypes)
        {
            return new Matcher(this, new Matcher(TypesSet.GetId(componentTypes), 0, 0));
        }

        public Matcher Exclude<T>() where T : IComponentData
        {
            return new Matcher(this, new Matcher(0, 0, TypesSet<T>.Id));
        }
        
        public Matcher Exclude<T1, T2>() 
            where T1 : IComponentData
            where T2 : IComponentData
        {
            return new Matcher(this, new Matcher(0, 0, TypesSet<T1, T2>.Id));
        }
        
        public Matcher Exclude<T1, T2, T3>() 
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
        {
            return new Matcher(this, new Matcher(0, 0, TypesSet<T1, T2, T3>.Id));
        }
        
        public Matcher Exclude<T1, T2, T3, T4>() 
            where T1 : IComponentData
            where T2 : IComponentData
            where T3 : IComponentData
            where T4 : IComponentData
        {
            return new Matcher(this, new Matcher(0, 0, TypesSet<T1, T2, T3, T4>.Id));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matcher Exclude(params int[] componentTypes)
        {
            return new Matcher(this, new Matcher(0, 0, TypesSet.GetId(componentTypes)));
        }

        private static int MergeComponentTypes(int a, int b)
        {
            if (a == 0) return b;
            if (b == 0) return a;

            return TypesSet.GetId(MergeComponentTypes(TypesSet.GetTypes(a), TypesSet.GetTypes(b)));
        }

        private static int[] MergeComponentTypes(int[] a, int[] b)
        {
            if (a == null || a.Length == 0)
                return b;

            if (b == null || b.Length == 0)
                return a;

            var buff = ArrayPool<int>.Shared.Rent(Math.Max(64, a.Length + b.Length));
            
            var size = a.Length;
            Buffer.BlockCopy(a, 0, buff, 0, a.Length);

            for (var i = 0; i < b.Length; ++i)
            {
                if (Array.IndexOf(a, b[i]) != -1)
                    continue;

                buff[size++] = b[i];
            }

            var merged = new int[size];
            Buffer.BlockCopy(buff, 0, merged, 0, size);
            
            ArrayPool<int>.Shared.Return(buff);

            return merged;
        }

        public bool Equals(Matcher other)
        {
            return IncludeAllId == other.IncludeAllId && 
                   IncludeAnyId == other.IncludeAnyId &&
                   ExcludeAllId == other.ExcludeAllId;
        }

        public override bool Equals(object obj)
        {
            return obj is Matcher other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IncludeAllId, IncludeAnyId, ExcludeAllId);
        }

        public static bool operator ==(Matcher a, Matcher b) => a.Equals(b);

        public static bool operator !=(Matcher a, Matcher b) => !a.Equals(b);

        private static bool Equals(int[] a, int[] b)
        {
            if (a == b)
                return true;

            if (a == null || b == null)
                return false;

            if (a.Length != b.Length)
                return false;
            
            for (var i = 0; i < a.Length; ++i)
                if (a[i] != b[i])
                    return false;
            
            return true;
        }
    }

    internal static class TypesSet
    {
        private static Dictionary<int[], int> _keys = new(new ArrayComparer()) { [Array.Empty<int>()] = 0 };
        private static readonly List<int[]> _types = new() { Array.Empty<int>()};
        
        public static int GetId(params int[] types)
        {
            Array.Sort(types);

            if (_keys.TryGetValue(types, out var id))
                return id;

            id = _types.Count;
            types = (int[]) types.Clone();
            _types.Add(types);
            _keys[types] = id;
            return id;
        }

        public static int[] GetTypes(int id) => _types[id];
        
        private class ArrayComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[] x, int[] y)
            {
                if (ReferenceEquals(x, y))
                    return true;

                if (x.Length != y.Length)
                    return false;
                
                for (var i = 0; i < x.Length; ++i)
                    if (x[i] != y[i])
                        return false;
                
                return true;
            }

            public int GetHashCode(int[] value)
            {
                var result = 0;
                foreach (var item in value)
                {
                    result = HashCode.Combine(result, item);
                }

                return result;
            }
        }
    }

    internal struct TypesSet<T> where T : IComponentData
    {
        public static readonly int Id = TypesSet.GetId(ComponentTypeInfo.GetTypeIndex<T>());
    }

    internal struct TypesSet<T1, T2> 
        where T1 : IComponentData 
        where T2 : IComponentData
    {
        public static readonly int Id = TypesSet.GetId(
            ComponentTypeInfo.GetTypeIndex<T1>(),
            ComponentTypeInfo.GetTypeIndex<T2>());
    }
    
    internal struct TypesSet<T1, T2, T3> 
        where T1 : IComponentData 
        where T2 : IComponentData
        where T3 : IComponentData
    {
        public static readonly int Id = TypesSet.GetId(
            ComponentTypeInfo.GetTypeIndex<T1>(),
            ComponentTypeInfo.GetTypeIndex<T2>(),
            ComponentTypeInfo.GetTypeIndex<T3>());
    }
    
    internal struct TypesSet<T1, T2, T3, T4> 
        where T1 : IComponentData 
        where T2 : IComponentData
        where T3 : IComponentData
        where T4 : IComponentData
    {
        public static readonly int Id = TypesSet.GetId(
            ComponentTypeInfo.GetTypeIndex<T1>(),
            ComponentTypeInfo.GetTypeIndex<T2>(),
            ComponentTypeInfo.GetTypeIndex<T3>(),
            ComponentTypeInfo.GetTypeIndex<T4>());
    }
    
    internal struct TypesSet<T1, T2, T3, T4, T5> 
        where T1 : IComponentData 
        where T2 : IComponentData
        where T3 : IComponentData
        where T4 : IComponentData
        where T5 : IComponentData
    {
        public static readonly int Id = TypesSet.GetId(
            ComponentTypeInfo.GetTypeIndex<T1>(),
            ComponentTypeInfo.GetTypeIndex<T2>(),
            ComponentTypeInfo.GetTypeIndex<T3>(),
            ComponentTypeInfo.GetTypeIndex<T4>(),
            ComponentTypeInfo.GetTypeIndex<T5>());
    }
}