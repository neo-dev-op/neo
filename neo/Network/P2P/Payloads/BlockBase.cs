﻿using Neo.Cryptography;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.IO;

namespace Neo.Network.P2P.Payloads
{
    // <summary>
    // 区块基类
    // </summary>
    /// <summary>
    /// Block base class
    /// </summary>
    public abstract class BlockBase : IVerifiable
    {
        // <summary>
        // 区块版本号
        // </summary>
        /// <summary>
        /// Block version
        /// </summary>
        public uint Version;

        // <summary>
        // 上一个区块hash
        // </summary>
        /// <summary>
        /// Previous block hash
        /// </summary>
        public UInt256 PrevHash;

        // <summary>
        // 交易的梅克尔根
        // </summary>
        /// <summary>
        /// Merkle root
        /// </summary>
        public UInt256 MerkleRoot;

        // <summary>
        // 区块时间戳
        // </summary>
        /// <summary>
        /// Timestamp
        /// </summary>
        public uint Timestamp;

        // <summary>
        // 区块高度
        // </summary>
        /// <summary>
        /// Block height
        /// </summary>
        public uint Index;

        // <summary>
        // 共识附加数据，默认为block nonce。议长出块时生成的一个伪随机数
        // </summary>
        /// <summary>
        /// Consensus additional data, the default is block nonce.
        /// a pseudo-random number generated by the speaker when the block is released
        /// </summary>
        public ulong ConsensusData;

        // <summary>
        // 下一个区块共识地址，为共识节点三分之二多方签名合约地址
        // </summary>
        /// <summary>
        /// Next block consensus address，two-thirds multiparty signed contract address of consensus nodes
        /// </summary>
        public UInt160 NextConsensus;

        // <summary>
        // 见证人
        // </summary>
        /// <summary>
        /// Witness
        /// </summary>
        public Witness Witness;

        private UInt256 _hash = null;

        // <summary>
        // 区块hash
        // </summary>
        /// <summary>
        /// Block hash
        /// </summary>
        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    _hash = new UInt256(Crypto.Default.Hash256(this.GetHashData()));
                }
                return _hash;
            }
        }

        // <summary>
        // 获取见证人列表
        // </summary>
        /// <summary>
        /// Get Witness list
        /// </summary>
        Witness[] IVerifiable.Witnesses
        {
            get
            {
                return new[] { Witness };
            }
            set
            {
                if (value.Length != 1) throw new ArgumentException();
                Witness = value[0];
            }
        }

        // <summary>
        // 存储大小
        // </summary>
        /// <summary>
        /// size
        /// </summary>
        public virtual int Size => sizeof(uint) + PrevHash.Size + MerkleRoot.Size + sizeof(uint) + sizeof(uint) + sizeof(ulong) + NextConsensus.Size + 1 + Witness.Size;


        // <summary>
        // 反序列化
        // </summary>
        // <param name="reader">二进制输入流</param>
        /// <summary>
        /// Deserialize method
        /// </summary>
        /// <param name="reader">BinaryReader</param>
        public virtual void Deserialize(BinaryReader reader)
        {
            ((IVerifiable)this).DeserializeUnsigned(reader);
            if (reader.ReadByte() != 1) throw new FormatException();
            Witness = reader.ReadSerializable<Witness>();
        }
        // <summary>
        // 反序列化（区块头）
        // </summary>
        // <param name="reader">二进制输入</param>
        /// <summary>
        /// Deserialize method（Block header）
        /// </summary>
        /// <param name="reader">BinaryReader</param>
        void IVerifiable.DeserializeUnsigned(BinaryReader reader)
        {
            Version = reader.ReadUInt32();
            PrevHash = reader.ReadSerializable<UInt256>();
            MerkleRoot = reader.ReadSerializable<UInt256>();
            Timestamp = reader.ReadUInt32();
            Index = reader.ReadUInt32();
            ConsensusData = reader.ReadUInt64();
            NextConsensus = reader.ReadSerializable<UInt160>();
        }
        // <summary>
        // 获取原始哈希数据
        // </summary>
        // <returns>原始哈希数据</returns>
        /// <summary>
        /// Get hash data
        /// </summary>
        /// <returns>hash data</returns>
        byte[] IScriptContainer.GetMessage()
        {
            return this.GetHashData();
        }
        // <summary>
        // 获取用于验证的脚本哈希。实际为当前区块共识节点三分之二多方签名合约地址。
        // </summary>
        // <param name="snapshot">数据库快照</param>
        // <returns>脚本哈希的数组</returns>
        /// <summary>
        ///  Get the script hash collection for validation.
        ///  Actually, it is two-thirds of the multi-party signed contract address of the current block consensus node.
        /// </summary>
        /// <param name="snapshot">Database Snapshot</param>
        /// <returns>script hash collection</returns>
        UInt160[] IVerifiable.GetScriptHashesForVerifying(Snapshot snapshot)
        {
            if (PrevHash == UInt256.Zero)
                return new[] { Witness.ScriptHash };
            Header prev_header = snapshot.GetHeader(PrevHash);
            if (prev_header == null) throw new InvalidOperationException();
            return new UInt160[] { prev_header.NextConsensus };
        }

        // <summary>
        // 序列化
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
        // <description>梅克尔根</description>
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
        // <description>共识数据，默认为block nonce。议长出块时生成的一个伪随机数。</description>
        // </item>
        // <item>
        // <term>NextConsensus</term>
        // <description>下一个区块共识地址</description>
        // </item>
        // </list>
        // </summary>
        // <param name="writer"></param>
        /// <summary>
        /// Serialize method
        /// <list type="bullet">
        /// <item>
        /// <term>Version</term>
        /// <description>Version</description>
        /// </item>
        /// <item>
        /// <term>PrevHash</term>
        /// <description>Previous block hash</description>
        /// </item>
        /// <item>
        /// <term>MerkleRoot</term>
        /// <description>Merkle root</description>
        /// </item>
        /// <item>
        /// <term>Timestamp</term>
        /// <description>Timestamp</description>
        /// </item>
        /// <item>
        /// <term>Index</term>
        /// <description>Block height</description>
        /// </item>
        /// <item>
        /// <term>ConsensusData</term>
        /// <description>Consensus Data.The default is block nonce.A pseudo-random number generated by the speaker when the block is released</description>
        /// </item>
        /// <item>
        /// <term>NextConsensus</term>
        /// <description>Next block consensus address</description>
        /// </item>
        /// </list>
        /// </summary>
        /// <param name="writer">BinaryWriter</param>
        public virtual void Serialize(BinaryWriter writer)
        {
            ((IVerifiable)this).SerializeUnsigned(writer);
            writer.Write((byte)1); writer.Write(Witness);
        }
        /// <summary>
        /// 序列化（区块头）
        /// </summary>
        /// <param name="writer">二进制输出</param>
        /// <summary>
        /// Serialize method（block header）
        /// </summary>
        /// <param name="writer">BinaryWriter</param>
        void IVerifiable.SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(PrevHash);
            writer.Write(MerkleRoot);
            writer.Write(Timestamp);
            writer.Write(Index);
            writer.Write(ConsensusData);
            writer.Write(NextConsensus);
        }

        // <summary>
        // 转成json对象
        // </summary>
        // <returns>json对象</returns>
        /// <summary>
        /// Convert to a JObject object
        /// </summary>
        /// <returns>JObject object</returns>
        public virtual JObject ToJson()
        {
            JObject json = new JObject();
            json["hash"] = Hash.ToString();
            json["size"] = Size;
            json["version"] = Version;
            json["previousblockhash"] = PrevHash.ToString();
            json["merkleroot"] = MerkleRoot.ToString();
            json["time"] = Timestamp;
            json["index"] = Index;
            json["nonce"] = ConsensusData.ToString("x16");
            json["nextconsensus"] = NextConsensus.ToAddress();
            json["script"] = Witness.ToJson();
            return json;
        }

        // <summary>
        // 根据当前区块快照，校验该区块
        // </summary>
        // <param name="snapshot">区块快照</param>
        // <remark>
        // 若满足以下4个条件之一，则验证节点结果为false。<br/>
        // 1）若上一个区块不存在<br/>
        // 2）若上一个区块高度加一不等于当前区块高度<br/>
        // 3）若上一个区块时间戳大于等于当前区块时间戳<br/>
        // 4）若见证人校验失败<br/>
        // </remark>
        /// <summary>
        /// Verify the block based on the current block snapshot
        /// </summary>
        /// <param name="snapshot">block snapshot</param>
        /// <remark>
        /// If the following four conditions are met, the verification result is false.<br/>
        /// 1) If the previous block does not exist<br />
        /// 2) If the previous block height plus 1 is not equal to the current block height<br/>
        /// 3) If the previous block timestamp is greater than or equal to the current block timestamp<br/>
        /// 4) If witness witness verification fails<br/>
        /// </remark>
        public virtual bool Verify(Snapshot snapshot)
        {
            Header prev_header = snapshot.GetHeader(PrevHash);
            if (prev_header == null) return false;
            if (prev_header.Index + 1 != Index) return false;
            if (prev_header.Timestamp >= Timestamp) return false;
            if (!this.VerifyWitnesses(snapshot)) return false;
            return true;
        }
    }
}
