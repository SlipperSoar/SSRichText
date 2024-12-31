/*
 * @author SlipperSoar
 * @Created: 2024-12-13 12:43 
 * @description 一些方法扩展
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// 子字符串获取
        /// </summary>
        /// <param name="str"></param>
        /// <param name="startIndex"></param>
        /// <param name="endIndex"></param>
        /// <returns></returns>
        public static string GetSubString(this string str, int startIndex, int endIndex)
        {
#if UNITY_2021_2_OR_NEWER
            return str[startIndex..endIndex];
#else
            return str.Substring(startIndex, endIndex - startIndex);
#endif
        }
        
        /// <summary>
        /// 子字符串获取
        /// </summary>
        /// <param name="str"></param>
        /// <param name="startIndex"></param>
        /// <returns></returns>
        public static string GetSubString(this string str, int startIndex)
        {
#if UNITY_2021_2_OR_NEWER
            return str[startIndex..];
#else
            return str.Substring(startIndex);
#endif
        }

        /// <summary>
        /// 子数组获取
        /// </summary>
        /// <param name="array"></param>
        /// <param name="startIndex"></param>
        /// <param name="length"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static T[] GetSubArray<T>(this T[] array, int startIndex, int length)
        {
#if UNITY_2021_2_OR_NEWER
            return array[startIndex..(startIndex + length)];
#else
            var res = new T[length];

            for (int i = 0; i < length; i++)
            {
                res[i] = array[i + startIndex];
            }
            
            return res;
#endif
        }

        /// <summary>
        /// 从bit数组中获取指定长度转换的值（低位在前）
        /// </summary>
        /// <param name="bitArray"></param>
        /// <param name="index">起始索引</param>
        /// <param name="length">读取长度</param>
        /// <returns>转换成的值</returns>
        public static int GetValue(this BitArray bitArray, ref int index, int length)
        {
            // 不能超出索引范围，不能超出int范围
            if (index >= bitArray.Length || length > 32 || index + length > bitArray.Length)
            {
                return 0;
            }
            
            var res = 0;

            for (int i = 0; i < length; i++)
            {
                // if (index >= bitArray.Length)
                // {
                //     return res;
                // }

                var value = bitArray.Get(index++);
                res |= value ? (1 << i) : 0;
            }
            
            return res;
        }

        /// <summary>
        /// byte转二进制字符串
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToBinaryString(this byte value)
        {
            var chars = new char[8];

            for (int i = 0; i < 8; i++)
            {
                chars[8 - i - 1] = ((value >> i) & 1) == 1 ? '1' : '0';
            }
            
            var res = new string(chars);
            return res;
        }
    }
}