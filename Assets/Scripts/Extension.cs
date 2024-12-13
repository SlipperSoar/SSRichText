/*
 * @author SlipperSoar
 * @Created: 2024-12-13 12:43 
 * @description 一些方法扩展
 */

using System;
using System.Collections.Generic;

namespace SS.UIComponent
{
    public static class Extension
    {
        /// <summary>
        /// 尝试向字典添加元素
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <returns></returns>
        public static bool TryAddToDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key,
            TValue value)
        {
#if UNITY_2021_2_OR_NEWER
            return dictionary.TryAdd(key, value);
#else
            if (dictionary.ContainsKey(key)) return false;
            dictionary.Add(key, value);
            return true;
#endif
        }

        /// <summary>
        /// 用指定元素值向数组填充满元素
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value"></param>
        /// <typeparam name="T"></typeparam>
        public static void FillArray<T>(this T[] array, T value)
        {
#if UNITY_2021_2_OR_NEWER
            Array.Fill(array, value);
#else
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
#endif
        }
    }
}