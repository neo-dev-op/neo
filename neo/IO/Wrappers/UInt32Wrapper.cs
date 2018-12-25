﻿using System;
using System.IO;

namespace Neo.IO.Wrappers
{
    // <summary>
    // SerializableWrapper的子类
    // 用于封装uint类型的数据对象
    // </summary>
    /// <summary>
    ///a subclass of SerializableWrapper,
    /// used to encapsulate data objects of type uint
    /// </summary>
    public sealed class UInt32Wrapper : SerializableWrapper<uint>, IEquatable<UInt32Wrapper>
    {
        // <summary>
        // 大小，默认是内部封装的uint类型的数据对象的大小
        // </summary>
        /// <summary>
        /// Size, the default is the size of the internally encapsulated uint type data object
        /// </summary>
        public override int Size => sizeof(uint);
        // <summary>
        // 无参构造方法
        // </summary>
        /// <summary>
        /// Constructor
        /// </summary>
        public UInt32Wrapper()
        {
        }
        // <summary>
        // 有参构造方法
        // </summary>
        // <param name="value">需要封装的uint类型的数据</param>
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">uint type data that needs to be encapsulated</param>
        private UInt32Wrapper(uint value)
        {
            this.value = value;
        }
        // <summary>
        // 反序列化方法
        // </summary>
        // <param name="reader">2进制读取器</param>
        /// <summary>
        /// Deserialize method
        /// </summary>
        /// <param name="reader">BinaryReader</param>
        public override void Deserialize(BinaryReader reader)
        {
            value = reader.ReadUInt32();
        }
        // <summary>
        // 判断当前对象是否与另一个UInt32Wrapper对象相等
        // </summary>
        // <param name="other">待比较的UInt32Wrapper对象</param>
        // <returns>判断结果，相等返回true,否则返回true</returns>
        /// <summary>
        /// Determine if the current object is equal to another UInt32Wrapper object
        /// </summary>
        /// <param name="other">another UInt32Wrapper object</param>
        /// <returns>Compare results, equality returns true, otherwise returns false</returns>
        public bool Equals(UInt32Wrapper other)
        {
            return value == other.value;
        }
        // <summary>
        // 序列化方法
        // </summary>
        // <param name="writer">2进制输出器</param>
        /// <summary>
        /// Serialize method
        /// </summary>
        /// <param name="writer">BinaryWriter</param>
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(value);
        }
        // <summary>
        // 从uint 默认转换为UInt32Wrapper对象
        // </summary>
        // <param name="value">待封装的对象</param>
        /// <summary>
        /// Convert a uint type data to a UInt32Wrapper object
        /// </summary>
        /// <param name="value">a uint type data</param>
        public static implicit operator UInt32Wrapper(uint value)
        {
            return new UInt32Wrapper(value);
        }
        // <summary>
        // 从UInt32Wrapper对象默认转换为 uint
        // </summary>
        // <param name="wrapper">UInt32Wrapper对象</param>
        /// <summary>
        /// Convert a UInt32Wrapper object to a uint type data
        /// </summary>
        /// <param name="wrapper">UInt32Wrapper object</param>
        public static implicit operator uint(UInt32Wrapper wrapper)
        {
            return wrapper.value;
        }
    }
}
