﻿using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Neo.VM
{
    /// <summary>
    /// 脚本生成器类，用于构建脚本
    /// </summary>
    public class ScriptBuilder : IDisposable
    {
        private readonly MemoryStream ms = new MemoryStream();
        private readonly BinaryWriter writer;
        /// <summary>
        /// MemoryStream数据读取的偏移位
        /// </summary>
        public int Offset => (int)ms.Position;
        /// <summary>
        /// 脚本生成器的构造函数，由MemoryStream新建一个BinaryWriter
        /// </summary>
        public ScriptBuilder()
        {
            this.writer = new BinaryWriter(ms);
        }
        /// <summary>
        /// 释放脚本生成器资源，包括MemoryStream和BinaryWriter
        /// </summary>
        public void Dispose()
        {
            writer.Dispose();
            ms.Dispose();
        }
        /// <summary>
        /// 向脚本生成器中写入操作码和参数
        /// </summary>
        /// <param name="op">操作码</param>
        /// <param name="arg">参数，可选，默认为空</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder Emit(OpCode op, byte[] arg = null)
        {
            writer.Write((byte)op);
            if (arg != null)
                writer.Write(arg);
            return this;
        }
        /// <summary>
        /// 向脚本生成器中写入函数调用，函数由脚本哈希指定
        /// </summary>
        /// <param name="scriptHash">调用函数的脚本哈希</param>
        /// <param name="useTailCall">是否使用尾调用形式，默认为false</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitAppCall(byte[] scriptHash, bool useTailCall = false)
        {
            if (scriptHash.Length != 20)
                throw new ArgumentException();
            return Emit(useTailCall ? OpCode.TAILCALL : OpCode.APPCALL, scriptHash);
        }
        /// <summary>
        /// 向脚本生成器中写入跳转指令
        /// </summary>
        /// <param name="op">指定的跳转指令</param>
        /// <param name="offset">跳转指令偏移量</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitJump(OpCode op, short offset)
        {
            if (op != OpCode.JMP && op != OpCode.JMPIF && op != OpCode.JMPIFNOT && op != OpCode.CALL)
                throw new ArgumentException();
            return Emit(op, BitConverter.GetBytes(offset));
        }
        /// <summary>
        /// 向脚本生成器中写入一个整数，包括整数对应的压栈指令和整数本身
        /// </summary>
        /// <param name="number">带符号整数</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitPush(BigInteger number)
        {
            if (number == -1) return Emit(OpCode.PUSHM1);
            if (number == 0) return Emit(OpCode.PUSH0);
            if (number > 0 && number <= 16) return Emit(OpCode.PUSH1 - 1 + (byte)number);
            return EmitPush(number.ToByteArray());
        }
        /// <summary>
        ///  向脚本生成器中写入一个布尔类型的值
        /// </summary>
        /// <param name="data">写入的布尔值</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitPush(bool data)
        {
            return Emit(data ? OpCode.PUSHT : OpCode.PUSHF);
        }
        /// <summary>
        /// 向脚本生成器中写入一个字节数组，首先判断字节数组的长度，对不同的长度使用不同的压栈指令
        /// </summary>
        /// <param name="data">需要写入的字节数组</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitPush(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException();
            if (data.Length <= (int)OpCode.PUSHBYTES75)
            {
                writer.Write((byte)data.Length);
                writer.Write(data);
            }
            else if (data.Length < 0x100)
            {
                Emit(OpCode.PUSHDATA1);
                writer.Write((byte)data.Length);
                writer.Write(data);
            }
            else if (data.Length < 0x10000)
            {
                Emit(OpCode.PUSHDATA2);
                writer.Write((ushort)data.Length);
                writer.Write(data);
            }
            else// if (data.Length < 0x100000000L)
            {
                Emit(OpCode.PUSHDATA4);
                writer.Write(data.Length);
                writer.Write(data);
            }
            return this;
        }
        /// <summary>
        /// 向脚本生成器中写入一个字符串
        /// </summary>
        /// <param name="data">需要写入的字符串</param>
        /// <returns>写入完成后的脚本生成器</returns>
        public ScriptBuilder EmitPush(string data)
        {
            return EmitPush(Encoding.UTF8.GetBytes(data));
        }
        /// <summary>
        /// 向脚本生成器中写入指定的系统互操作服务调用
        /// </summary>
        /// <param name="api">系统互操作服务api字符串</param>
        /// <returns>写入完成后的脚本生成器</returns>
        /// <exception cref="System.ArgumentException">输入的api字符串为空或转换成字节后长度等于0或大于252时抛出</exception>
        public ScriptBuilder EmitSysCall(string api)
        {
            if (api == null)
                throw new ArgumentNullException();
            byte[] api_bytes = Encoding.ASCII.GetBytes(api);
            if (api_bytes.Length == 0 || api_bytes.Length > 252)
                throw new ArgumentException();
            byte[] arg = new byte[api_bytes.Length + 1];
            arg[0] = (byte)api_bytes.Length;
            Buffer.BlockCopy(api_bytes, 0, arg, 1, api_bytes.Length);
            return Emit(OpCode.SYSCALL, arg);
        }
        /// <summary>
        /// 获取脚本生成器的内容
        /// </summary>
        /// <returns>脚本生成器的内容</returns>
        public byte[] ToArray()
        {
            writer.Flush();
            return ms.ToArray();
        }
    }
}
