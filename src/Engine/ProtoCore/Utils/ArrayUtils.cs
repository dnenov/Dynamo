﻿using System;
using System.Collections.Generic;
using System.Linq;
using ProtoCore.DSASM;
using ProtoCore.Exceptions;
using ProtoCore.Runtime;

namespace ProtoCore.Utils
{
    public static class ArrayUtils
    {
        private static int RECURSION_LIMIT = 1024;

        internal static ClassNode GetGreatestCommonSubclassForArrayInternal(Dictionary<ClassNode, int> typeStats, RuntimeCore runtimeCore)
        {
            //@PERF: This could be improved with a 
            List<List<int>> chains = new List<List<int>>();
            HashSet<int> commonTypeIDs = new HashSet<int>();

            foreach (ClassNode cn in typeStats.Keys)
            {
                List<int> chain = ClassUtils.GetClassUpcastChain(cn, runtimeCore);

                //Now add in the other conversions - as we don't have a common superclass yet
                //@TODO(Jun): Remove this hack when we have a proper casting structure
                foreach (int id in cn.CoerceTypes.Keys)
                    if (!chain.Contains(id))
                        chain.Add((id));

                chains.Add(chain);

                foreach (int nodeId in chain)
                    commonTypeIDs.Add(nodeId);

            }

            //Remove nulls if they exist
            {

                if (commonTypeIDs.Contains((int)PrimitiveType.Null))
                    commonTypeIDs.Remove((int)PrimitiveType.Null);

                List<List<int>> nonNullChains = new List<List<int>>();

                foreach (List<int> chain in chains)
                {
                    if (chain.Contains((int)PrimitiveType.Null))
                        chain.Remove((int)PrimitiveType.Null);

                    if (chain.Count > 0)
                        nonNullChains.Add(chain);
                }
                chains = nonNullChains;

            }


            //Contract the hashset so that it contains only the nodes present in all chains
            //@PERF: this is very inefficent
            {
                foreach (List<int> chain in chains)
                {
                    commonTypeIDs.IntersectWith(chain);
                }
            }

            //No common subtypes
            if (commonTypeIDs.Count == 0)
                return null;

            if (commonTypeIDs.Count == 1)
                return runtimeCore.DSExecutable.classTable.ClassNodes[commonTypeIDs.First()];


            List<int> lookupChain = chains[0];


            //Insertion sort the IDs, we may only have a partial ordering on them.
            List<int> orderedTypes = new List<int>();

            foreach (int typeToInsert in commonTypeIDs)
            {
                bool inserted = false;

                for (int i = 0; i < orderedTypes.Count; i++)
                {
                    int orderedType = orderedTypes[i];

                    if (lookupChain.IndexOf(typeToInsert) < lookupChain.IndexOf(orderedType))
                    {
                        inserted = true;
                        orderedTypes.Insert(i, typeToInsert);
                        break;
                    }
                }

                if (!inserted)
                    orderedTypes.Add(typeToInsert);
            }

            return runtimeCore.DSExecutable.classTable.ClassNodes[orderedTypes.First()];
        }

        /// <summary>
        /// If an empty array is passed, the result will be null
        /// if there are instances, but they share no common supertype the result will be var
        /// </summary>
        public static ClassNode GetGreatestCommonSubclassForArray(StackValue array, RuntimeCore runtimeCore)
        {
            if (!array.IsArray)
                throw new ArgumentException("The stack value provided was not an array");

            Dictionary<ClassNode, int> typeStats = GetTypeStatisticsForArray(array, runtimeCore);

            return GetGreatestCommonSubclassForArrayInternal(typeStats, runtimeCore);
            
        }

        /// <summary>
        /// This method returns the distinct(by metadata type) reduced params for all the elements inside the
        /// paramStackValue, if it is an array. If it is not an array, it just returns the paramStackValue.
        /// </summary>
        /// <param name="paramStackValue"></param>
        /// <param name="runtimeCore"></param>
        /// <returns> A dictionary where the value is the current ReducedParam and the key is its metaData type</returns>
        public static Dictionary<int, StackValue> GetTypeExamplesForLayer(StackValue paramStackValue, RuntimeCore runtimeCore)
        {
            Dictionary<int, StackValue> usageFreq = new Dictionary<int, StackValue>();

            if (!paramStackValue.IsArray)
            {
                usageFreq.Add(paramStackValue.metaData.type, paramStackValue);
                return usageFreq;
            }

            //This is the element on the heap that manages the data structure
            var dsArray = runtimeCore.Heap.ToHeapObject<DSArray>(paramStackValue);
            foreach (var sv in dsArray.Values)
            {
                if (!usageFreq.ContainsKey(sv.metaData.type))
                {
                    usageFreq.Add(sv.metaData.type, sv);
                }
            }

            return usageFreq;
        }

        /// <summary>
        /// Similar to GetTypeExamplesForLayer but it returns all non-empty arrays.
        /// Its purpose is to support inspecting heterogeneous arrays in replication scenarios.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        internal static List<StackValue> GetTypeExamplesForLayerWithoutArraySampling(StackValue array, RuntimeCore runtimeCore)
        {
            var result = new List<StackValue>();
            var alreadyFoundTypes = new HashSet<int>();

            if (!array.IsArray)
            {
                result.Add(array);
                return result;
            }

            var dsArray = runtimeCore.Heap.ToHeapObject<DSArray>(array);
            foreach (var sv in dsArray.Values)
            {
                if (sv.IsArray)
                {
                    if (!IsEmpty(sv, runtimeCore))
                    {
                        result.Add(sv);
                    }
                }
                else
                {
                    if (!alreadyFoundTypes.Contains(sv.metaData.type))
                    {
                        alreadyFoundTypes.Add(sv.metaData.type);
                        result.Add(sv);
                    }
                }
            }

            return result;
        }


        /// <summary>
        /// Generate type statistics for given layer of an array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        public static Dictionary<ClassNode, int> GetTypeStatisticsForLayer(StackValue array, RuntimeCore runtimeCore)
        {
            if (!array.IsArray)
            {
                Dictionary<ClassNode, int> ret = new Dictionary<ClassNode, int>();
                ret.Add(runtimeCore.DSExecutable.classTable.ClassNodes[array.metaData.type], 1);
                return ret;
            }

            Dictionary<ClassNode, int> usageFreq = new Dictionary<ClassNode, int>();

            //This is the element on the heap that manages the data structure
            var dsArray = runtimeCore.Heap.ToHeapObject<DSArray>(array);
            foreach (var sv in dsArray.Values)
            {
                ClassNode cn = runtimeCore.DSExecutable.classTable.ClassNodes[sv.metaData.type];
                if (!usageFreq.ContainsKey(cn))
                    usageFreq.Add(cn, 0);

                usageFreq[cn] = usageFreq[cn] + 1;
            }

            return usageFreq;
        }

        /// <summary>
        /// Generate type statistics for the whole array
        /// </summary>
        /// <param name="array"></param>
        /// <param name="runtimeCore"></param>
        /// <returns>usage frequency by type</returns>
        public static Dictionary<ClassNode, int> GetTypeStatisticsForArray(StackValue array, RuntimeCore runtimeCore)
        {
            if (!array.IsArray)
            {
                Dictionary<ClassNode, int> ret = new Dictionary<ClassNode, int>();
                ret.Add(runtimeCore.DSExecutable.classTable.ClassNodes[array.metaData.type], 1);
                return ret;
            }

            Dictionary<ClassNode, int> usageFreq = new Dictionary<ClassNode, int>();

            //This is the element on the heap that manages the data structure
            var dsArray = runtimeCore.Heap.ToHeapObject<DSArray>(array);
            foreach (var sv in dsArray.Values)
            {
                if (sv.IsArray)
                {
                    //Recurse
                    Dictionary<ClassNode, int> subLayer = GetTypeStatisticsForArray(sv, runtimeCore);
                    foreach (ClassNode cn in subLayer.Keys)
                    {
                        if (!usageFreq.ContainsKey(cn))
                            usageFreq.Add(cn, 0);

                        usageFreq[cn] = usageFreq[cn] + subLayer[cn];

                    }
                }
                else
                {

                    ClassNode cn = runtimeCore.DSExecutable.classTable.ClassNodes[sv.metaData.type];
                    if (!usageFreq.ContainsKey(cn))
                        usageFreq.Add(cn, 0);

                    usageFreq[cn] += 1;
                }
            }

            return usageFreq;
        }

        private static int GetMaxRankForArray(StackValue array, RuntimeCore runtimeCore, int tracer)
        {
            if (tracer > RECURSION_LIMIT)
                throw new CompilerInternalException("Internal Recursion limit exceeded in Rank Check - Possible heap corruption {3317D4F6-4758-4C19-9680-75B68DA0436D}");

            if (!array.IsArray)
                return 0;
            //throw new ArgumentException("The stack value provided was not an array");

            int ret = 1;

            int largestSub = 0;

            //This is the element on the heap that manages the data structure
            foreach (var sv in runtimeCore.Heap.ToHeapObject<DSArray>(array).Values)
            {
                if (sv.IsArray)
                {
                    int subArrayRank = GetMaxRankForArray(sv, runtimeCore, tracer + 1);
                    largestSub = Math.Max(subArrayRank, largestSub);
                }
            }

            return largestSub + ret;
        }

        public static int GetMaxRankForArray(StackValue array, RuntimeCore runtimeCore)
        {
            return GetMaxRankForArray(array, runtimeCore, 0);

        }

        /// <summary>
        /// Whether sv is double or arrays contains double value.
        /// </summary>
        /// <param name="sv"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        public static bool ContainsDoubleElement(StackValue sv, RuntimeCore runtimeCore)
        {
            Executable exe = runtimeCore.DSExecutable;
            if (!sv.IsArray)
                return exe.TypeSystem.GetType(sv) == (int)PrimitiveType.Double;

            DSArray array = runtimeCore.Heap.ToHeapObject<DSArray>(sv);
            return array.Values.Any(
                        v => (v.IsArray && ContainsDoubleElement(v, runtimeCore)) ||
                             (exe.TypeSystem.GetType(v) == (int)PrimitiveType.Double));
        }

        /// <summary>
        /// If the passed in value is not an array or an empty array or an array which contains only empty arrays, return false.
        /// Otherwise, return true;
        /// </summary>
        /// <param name="sv"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        public static bool ContainsNonArrayElement(StackValue sv, RuntimeCore runtimeCore)
        {
            if (!sv.IsArray)
                return true;

            var array = runtimeCore.Heap.ToHeapObject<DSArray>(sv);
            return array.Values.Any(v => ContainsNonArrayElement(v, runtimeCore)); 
        }

        /// <summary>
        /// Retrieve the first non-array element in an array 
        /// </summary>
        /// <param name="svArray"></param>
        /// <param name="sv"></param>
        /// <param name="runtimeCore"></param>
        /// <returns> true if the element was found </returns>
        public static bool GetFirstNonArrayStackValue(StackValue svArray, ref StackValue sv, RuntimeCore runtimeCore)
        {
            if (!svArray.IsArray)
            {
                return false;
            }

            var svFound = GetFirstNonArrayStackValueRecursive(svArray, runtimeCore);
            if (svFound.HasValue)
            {
                sv = svFound.Value.ShallowClone();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Recursively searches a stack value that is not an array in the given array.
        /// </summary>
        /// <param name="svArray">Stack value representing an array</param>
        /// <param name="runtimeCore">Runtime core</param>
        /// <returns>The first stack value found that is not an array. Null if none is found.</returns>
        private static StackValue? GetFirstNonArrayStackValueRecursive(StackValue svArray, RuntimeCore runtimeCore)
        {
            RuntimeMemory rmem = runtimeCore.RuntimeMemory;
            var array = rmem.Heap.ToHeapObject<DSArray>(svArray);
            if (!array.Values.Any())
            {
                return null;
            }

            foreach (var svItem in array.Values)
            {
                if (svItem.IsArray)
                {
                    var svFound = GetFirstNonArrayStackValueRecursive(svItem, runtimeCore);
                    // If we found a non-array sv value, return it. Otherwise, keep looking in the array.
                    if (svFound.HasValue)
                    {
                        return svFound;
                    }
                }
                else
                {
                    return svItem;
                }
            }

            return null;
        }

        private static StackValue[] GetFlattenValue(StackValue array, RuntimeCore runtimeCore)
        {
            Queue<StackValue> workingSet = new Queue<StackValue>();
            List<StackValue> flattenValues = new List<StackValue>();

            if (!array.IsArray)
            {
                return null;
            }

            workingSet.Enqueue(array);
            while (workingSet.Count > 0)
            {
                array = workingSet.Dequeue();
                foreach (var value in runtimeCore.Heap.ToHeapObject<DSArray>(array).Values)
                {
                    if (value.IsArray)
                    {
                        workingSet.Enqueue(value);
                    }
                    else
                    {
                        flattenValues.Add(value);
                    }
                }
            }

            return flattenValues.ToArray();
        }

        /// <summary>
        /// For an array we supporting zipped replicaiton for array indexing as 
        /// well. I.e., for the following expression:
        /// 
        ///     a[1..3][2..4] = x;
        /// 
        /// It will be expanded to 
        /// 
        ///     a[1][2] = x;
        ///     a[2][3] = x;
        ///     a[3][4] = x;
        ///     
        /// So here we need to calculate zipped indices. The length of returned 
        /// indices is decided by the shortest length of index that used in 
        /// array indexing. E.g.,
        /// 
        /// For array indexing
        /// 
        ///     [{1, 2, 3}][{"x", "y"}][{6, 7, 8}], i.e., 
        ///     
        ///     1 -> "x" -> 6
        ///     2 -> "y" -> 7
        ///     3 ->     -> 8
        /// 
        /// The shortest length of index is 2 ({"x", "y"}), so function will 
        /// returns:
        /// 
        ///     {{1, "x", 6}, {2, "y", 7}}
        ///     
        /// </summary>
        /// <param name="indices"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        public static StackValue[][] GetZippedIndices(List<StackValue> indices, RuntimeCore runtimeCore)
        {
            List<StackValue[]> allFlattenValues = new List<StackValue[]>();

            int zipLength = Int32.MaxValue;
            foreach (var index in indices)
            {
                int length = 1;
                if (index.IsArray)
                {
                    StackValue[] flattenValues = GetFlattenValue(index, runtimeCore);
                    allFlattenValues.Add(flattenValues);
                    length = flattenValues.Count();
                }
                else
                {
                    allFlattenValues.Add(null);
                }

                if (zipLength > length)
                {
                    zipLength = length;
                }
            }

            if (zipLength == 0)
            {
                return null;
            }
            else
            {
                int dims = indices.Count;
                StackValue[][] zippedIndices = new StackValue[zipLength][];
                for (int i = 0; i < zipLength; ++i)
                {
                    zippedIndices[i] = new StackValue[dims];
                }

                for (int i = 0; i < dims; ++i)
                {
                    StackValue index = indices[i];
                    StackValue[] values = null;
                    if (index.IsArray)
                    {
                        values = allFlattenValues[i];
                    }

                    if (1 == zipLength)
                    {
                        if (index.IsArray)
                        {
                            zippedIndices[0][i] = values[0];
                        }
                        else
                        {
                            zippedIndices[0][i] = index;
                        }
                    }
                    else
                    {
                        for (int j = 0; j < zipLength; ++j)
                        {
                            zippedIndices[j][i] = values[j];
                        }
                    }
                }

                return zippedIndices;
            }
        }

        /// <summary>
        /// Returns true if an array is an empty list or all its elements are empty lists.
        /// </summary>
        /// <param name="arrayPointer"></param>
        /// <param name="runtimeCore"></param>
        /// <returns></returns>
        public static bool IsEmpty(StackValue arrayPointer, RuntimeCore runtimeCore)
        {
            if (!arrayPointer.IsArray)
                return false;

            var array = runtimeCore.Heap.ToHeapObject<DSArray>(arrayPointer);
            return array.Values.All(v => IsEmpty(v, runtimeCore));
        }

        /// <summary>
        /// Returns the list of common items from a given collection of generic lists 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="lists"></param>
        /// <returns>list of common items from multiple lists</returns>
        public static IEnumerable<T> GetCommonItems<T>(IEnumerable<T>[] lists)
        {
            HashSet<T> hs = new HashSet<T>(lists.First());
            for (int i = 1; i < lists.Length; i++)
                hs.IntersectWith(lists[i]);
            return hs;
        }
    }
}
