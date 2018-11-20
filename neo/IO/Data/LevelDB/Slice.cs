﻿using Neo.Cryptography;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Neo.IO.Data.LevelDB
{
    /// <summary>
    /// 封装leveldb的切片，可以存放任何基础类型的值，并支持逻辑运算。与leveldb原生切片不同点是这里存放的是真实值而非引用或指针
    /// </summary>
    public struct Slice : IComparable<Slice>, IEquatable<Slice>
    {
        internal byte[] buffer;

        internal Slice(IntPtr data, UIntPtr length)
        {
            buffer = new byte[(int)length];
            Marshal.Copy(data, buffer, 0, (int)length);
        }

        /// <summary>
        /// 比较大小，按照单个字节进行对比（注，当时前缀关系时，长度最长的大）
        /// </summary>
        /// <param name="other">待对比切片</param>
        /// <returns></returns>
        public int CompareTo(Slice other)
        {
            for (int i = 0; i < buffer.Length && i < other.buffer.Length; i++)
            {
                int r = buffer[i].CompareTo(other.buffer[i]);
                if (r != 0) return r;
            }
            return buffer.Length.CompareTo(other.buffer.Length);
        }

        /// <summary>
        /// 是否等于该切片
        /// </summary>
        /// <param name="other">待比较切片</param>
        /// <returns></returns>
        public bool Equals(Slice other)
        {
            if (buffer.Length != other.buffer.Length) return false;
            return buffer.SequenceEqual(other.buffer);
        }

        /// <summary>
        /// 与某对象是否相等
        /// </summary>
        /// <param name="obj">待对比的对象</param>
        /// <returns>注，若obj为null 或不是slice时，返回false</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (!(obj is Slice)) return false;
            return Equals((Slice)obj);
        }

        /// <summary>
        /// 获取hash code
        /// </summary>
        /// <returns>murmur32 hash code</returns>
        public override int GetHashCode()
        {
            return (int)buffer.Murmur32(0);
        }

        /// <summary>
        /// 转成byte数组
        /// </summary>
        /// <returns>若切片为空，返回空的byte数组</returns>
        public byte[] ToArray()
        {
            return buffer ?? new byte[0];
        }

        /// <summary>
        /// 转bool
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于bool存储大小时，抛出该异常</exception>
        unsafe public bool ToBoolean()
        {
            if (buffer.Length != sizeof(bool))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((bool*)pbyte);
            }
        }

        /// <summary>
        /// 转byte
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于byte存储大小时，抛出该异常</exception>
        public byte ToByte()
        {
            if (buffer.Length != sizeof(byte))
                throw new InvalidCastException();
            return buffer[0];
        }

        /// <summary>
        /// 转double
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于double存储大小时，抛出该异常</exception>
        unsafe public double ToDouble()
        {
            if (buffer.Length != sizeof(double))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((double*)pbyte);
            }
        }

        /// <summary>
        /// 转int16
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于short存储大小时，抛出该异常</exception>
        unsafe public short ToInt16()
        {
            if (buffer.Length != sizeof(short))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((short*)pbyte);
            }
        }

        /// <summary>
        /// 转int32
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于int存储大小时，抛出该异常</exception>
        unsafe public int ToInt32()
        {
            if (buffer.Length != sizeof(int))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((int*)pbyte);
            }
        }

        /// <summary>
        /// 转int64
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于long存储大小时，抛出该异常</exception>
        unsafe public long ToInt64()
        {
            if (buffer.Length != sizeof(long))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((long*)pbyte);
            }
        }

        /// <summary>
        /// 转float
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于float存储大小时，抛出该异常</exception>
        unsafe public float ToSingle()
        {
            if (buffer.Length != sizeof(float))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((float*)pbyte);
            }
        }

        /// <summary>
        /// 转utf8 string
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// 转uint16
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于ushort存储大小时，抛出该异常</exception>
        unsafe public ushort ToUInt16()
        {
            if (buffer.Length != sizeof(ushort))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((ushort*)pbyte);
            }
        }

        /// <summary>
        /// 转uint32
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于uint存储大小时，抛出该异常</exception>
        unsafe public uint ToUInt32(int index = 0)
        {
            if (buffer.Length != sizeof(uint) + index)
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[index])
            {
                return *((uint*)pbyte);
            }
        }

        /// <summary>
        /// 转UInt64
        /// </summary>
        /// <returns></returns>
        /// <exception cref="System.InvalidCastException">若切片长度不等于ulong存储大小时，抛出该异常</exception>
        unsafe public ulong ToUInt64()
        {
            if (buffer.Length != sizeof(ulong))
                throw new InvalidCastException();
            fixed (byte* pbyte = &buffer[0])
            {
                return *((ulong*)pbyte);
            }
        }

        /// <summary>
        /// 创建byte数组切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(byte[] data)
        {
            return new Slice { buffer = data };
        }

        /// <summary>
        /// 创建bool数组切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(bool data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }


        /// <summary>
        /// 创建byte切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(byte data)
        {
            return new Slice { buffer = new[] { data } };
        }

        /// <summary>
        /// 创建double切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(double data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建short切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(short data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建int切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(int data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建long切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(long data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建float切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(float data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建string切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(string data)
        {
            return new Slice { buffer = Encoding.UTF8.GetBytes(data) };
        }

        /// <summary>
        /// 创建ushort切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(ushort data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建uint切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(uint data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }

        /// <summary>
        /// 创建ulong切片
        /// </summary>
        /// <param name="data"></param>
        public static implicit operator Slice(ulong data)
        {
            return new Slice { buffer = BitConverter.GetBytes(data) };
        }


        /// <summary>
        /// 切片操作，小于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator <(Slice x, Slice y)
        {
            return x.CompareTo(y) < 0;
        }

        /// <summary>
        /// 切片操作，小于等于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator <=(Slice x, Slice y)
        {
            return x.CompareTo(y) <= 0;
        }
        /// <summary>
        /// 切片操作，大于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator >(Slice x, Slice y)
        {
            return x.CompareTo(y) > 0;
        }
        /// <summary>
        /// 切片操作，大于等于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator >=(Slice x, Slice y)
        {
            return x.CompareTo(y) >= 0;
        }

        /// <summary>
        /// 切片操作，等于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator ==(Slice x, Slice y)
        {
            return x.Equals(y);
        }


        /// <summary>
        /// 切片操作，不等于
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public static bool operator !=(Slice x, Slice y)
        {
            return !x.Equals(y);
        }
    }
}
