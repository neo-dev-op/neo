﻿using Neo.Cryptography;
using Neo.IO;
using System;
using System.IO;

namespace Neo.Network.P2P
{
    // <summary>
    // 描述NEO p2p网络传输数据所用的报文结构的实体类
    // </summary>
    /// <summary>
    /// an entity class describing the message structure used for data transmission in NEO p2p network
    /// </summary>
    public class Message : ISerializable
    {
        // <summary>
        // 报文头部大小
        // </summary>
        /// <summary>
        /// message header size
        /// </summary>
        public const int HeaderSize = sizeof(uint) + 12 + sizeof(int) + sizeof(uint);
        // <summary>
        // 报文正文大小限制
        // </summary>
        /// <summary>
        /// message body size limitation
        /// </summary>
        public const int PayloadMaxSize = 0x02000000;
        // <summary>
        // 魔法字符串，用来避免网络冲突
        // </summary>
        /// <summary>
        /// magic string，avoid network conflicts
        /// </summary>
        public static readonly uint Magic = Settings.Default.Magic;
        // <summary>
        // 指令的名称。最长12个字节。不足时补0x00
        // </summary>
        /// <summary>
        /// command name。Fixed 12 bytes.When the length is insufficient, append 0x00
        /// </summary>
        public string Command;
        // <summary>
        // Payload校验，避免篡改和传输错误
        // </summary>
        /// <summary>
        /// Payload verification to avoid tampering and transmission errors
        /// </summary>
        public uint Checksum;
        // <summary>
        // 报文的正文内容，根据报文种类不同而不同
        // </summary>
        /// <summary>
        /// The body content of the message varies according to the type of message
        /// </summary>
        public byte[] Payload;
        // <summary>
        // 报文大小，等于头部大小加上正文大小
        // </summary>
        /// <summary>
        /// Message size, equal to the size of the header + the size of the body
        /// </summary>
        public int Size => HeaderSize + Payload.Length;
        // <summary>
        // 创建一个报文
        // </summary>
        // <param name="command">指令字符串</param>
        // <param name="payload">正文内容（对象形式</param>
        // <returns>创建的报文对象</returns>
        /// <summary>
        /// create a message object
        /// </summary>
        /// <param name="command">command</param>
        /// <param name="payload">the body content of the message（object type data）</param>
        /// <returns>message object</returns>
        public static Message Create(string command, ISerializable payload = null)
        {
            return Create(command, payload == null ? new byte[0] : payload.ToArray());
        }
        // <summary>
        // 创建一个报文
        // </summary>
        // <param name="command">指令字符串</param>
        // <param name="payload">正文内容（2进制形式）</param>
        // <returns>创建的报文对象</returns>
        /// <summary>
        /// create a message object
        /// </summary>
        /// <param name="command">command</param>
        /// <param name="payload">the body content of the message（byte array type）</param>
        /// <returns>message object</returns>
        public static Message Create(string command, byte[] payload)
        {
            return new Message
            {
                Command = command,
                Checksum = GetChecksum(payload),
                Payload = payload
            };
        }
        // <summary>
        // 反序列化方法
        // </summary>
        // <param name="reader">2进制读取器</param>
        /// <summary>
        /// Deserialize method
        /// </summary>
        /// <param name="reader">BinaryReader</param>
        void ISerializable.Deserialize(BinaryReader reader)
        {
            if (reader.ReadUInt32() != Magic)
                throw new FormatException();
            this.Command = reader.ReadFixedString(12);
            uint length = reader.ReadUInt32();
            if (length > PayloadMaxSize)
                throw new FormatException();
            this.Checksum = reader.ReadUInt32();
            this.Payload = reader.ReadBytes((int)length);
            if (GetChecksum(Payload) != Checksum)
                throw new FormatException();
        }

        private static uint GetChecksum(byte[] value)
        {
            return Crypto.Default.Hash256(value).ToUInt32(0);
        }
        // <summary>
        // 序列化方法
        // </summary>
        // <param name="writer">2进制输出器</param>
        /// <summary>
        /// Serialize method
        /// </summary>
        /// <param name="writer">BinaryWriter</param>
        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write(Magic);
            writer.WriteFixedString(Command, 12);
            writer.Write(Payload.Length);
            writer.Write(Checksum);
            writer.Write(Payload);
        }
    }
}
