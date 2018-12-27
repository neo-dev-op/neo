﻿using Neo.Ledger;
using System;
using System.IO;

namespace Neo.Network.P2P.Payloads
{
    // <summary>
    // 区块头
    // </summary>
    /// <summary>
    /// The block header
    /// </summary>
    public class Header : BlockBase, IEquatable<Header>
    {

        // <summary>
        // 存储大小
        // </summary>
        /// <summary>
        /// The storage size for this block header object
        /// </summary>
        public override int Size => base.Size + 1;

        // <summary>
        // 反序列化
        // </summary>
        // <param name="reader">二进制输入流</param>
        // <exception cref="System.FormatException">二进制数据格式与Header序列化后格式不符时抛出</exception>
        /// <summary>
        /// The deserialization
        /// </summary>
        /// <param name="reader">The binary output stream</param>
        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            if (reader.ReadByte() != 0) throw new FormatException();
        }

        // <summary>
        // 比较区块头
        // </summary>
        // <param name="other">待比较区块头</param>
        // <returns>若待比较区块头为null，返回false。否则按哈希值比较</returns>
        /// <summary>
        /// Compare the block header
        /// </summary>
        /// <param name="other">The other block header</param>
        /// <returns>If the other block header is null, return false, otherwise compare the hashCode</returns>
        public bool Equals(Header other)
        {
            if (other is null) return false;
            if (ReferenceEquals(other, this)) return true;
            return Hash.Equals(other.Hash);
        }

        // <summary>
        // 比较区块头是否等于某对象
        // </summary>
        // <param name="obj">待比较对象</param>
        // <returns>等于返回true，不等于返回false</returns>
        /// <summary>
        /// Compare the block header equals to other object
        /// </summary>
        /// <param name="obj">Compare the object and the block header</param>
        /// <returns>If the object equals to block header returns true, otherwise return false</returns>
        public override bool Equals(object obj)
        {
            return Equals(obj as Header);
        }

        // <summary>
        // 获取hash code
        // </summary>
        // <returns>区块哈希的hashcode</returns>
        /// <summary>
        /// Get the hash code
        /// </summary>
        /// <returns>The hash code of block</returns>
        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        // <summary>
        // 序列化，尾部写入固定值0
        // <list type="bullet">
        // <item>
        // <term>Version</term>
        // <description>状态版本号</description>
        // </item>
        // <item>
        // <term>PrevHash</term>
        // <description>上一个区块hash</description>
        // </item>
        // <item>
        // <term>MerkleRoot</term>
        // <description>梅克尔树</description>
        // </item>
        // <item>
        // <term>Timestamp</term>
        // <description>时间戳</description>
        // </item>
        // <item>
        // <term>Index</term>
        // <description>区块高度</description>
        // </item>
        // <item>
        // <term>ConsensusData</term>
        // <description>共识数据，默认为block nonce</description>
        // </item>
        // <item>
        // <term>NextConsensus</term>
        // <description>下一个区块共识地址</description>
        // </item>
        // <item>
        // <term>0</term>
        // <description>固定值0</description>
        // </item>
        // </list>
        // </summary>
        // <param name="writer">二进制输出流</param>

        /// <summary>
        /// Serialization, with a fixed 0 at trim
        /// <list type="bullet">
        /// <item>
        /// <term>Version</term>
        /// <description>The version of the state</description>
        /// </item>
        /// <item>
        /// <term>PrevHash</term>
        /// <description>The hash of previous block</description>
        /// </item>
        /// <item>
        /// <term>MerkleRoot</term>
        /// <description>The root of Merkle tree</description>
        /// </item>
        /// <item>
        /// <term>Timestamp</term>
        /// <description>The timestamp</description>
        /// </item>
        /// <item>
        /// <term>Index</term>
        /// <description>The height of block</description>
        /// </item>
        /// <item>
        /// <term>ConsensusData</term>
        /// <description>The consensusData, default if block nonce</description>
        /// </item>
        /// <item>
        /// <term>NextConsensus</term>
        /// <description>The next consensus node address</description>
        /// </item>
        /// <item>
        /// <term>0</term>
        /// <description>Fixed 0 </description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="writer">The binary output avalue</param>
        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write((byte)0);
        }

        // <summary>
        // 转成简化版block。从区块头简化的TrimmedBlock实例不包含交易队列的哈希值。
        // </summary>
        // <returns>简化版block对象</returns>
        /// <summary>
        /// Transfer to trimmed block. The trimmed block instance Transferd from header does not include the hash value of transactions
        /// </summary>
        /// <returns>The trimmed block</returns>
        public TrimmedBlock Trim()
        {
            return new TrimmedBlock
            {
                Version = Version,
                PrevHash = PrevHash,
                MerkleRoot = MerkleRoot,
                Timestamp = Timestamp,
                Index = Index,
                ConsensusData = ConsensusData,
                NextConsensus = NextConsensus,
                Witness = Witness,
                Hashes = new UInt256[0]
            };
        }
    }
}
