﻿using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using VMArray = Neo.VM.Types.Array;
using VMBoolean = Neo.VM.Types.Boolean;

namespace Neo.SmartContract
{
    // <summary>
    // 标准服务类，用于提供智能合约需要的互操作服务等
    // </summary>
    /// <summary>
    /// Standard service class for interoperable services required to provide smart contracts, etc.
    /// </summary>
    public class StandardService : IDisposable, IInteropService
    {
        // <summary>
        // 通知事件委托
        // </summary>
        /// <summary>
        /// Notification event delegation
        /// </summary>
        public static event EventHandler<NotifyEventArgs> Notify;
        // <summary>
        // 日志事件委托
        // </summary>
        /// <summary>
        /// Log event delegation
        /// </summary>
        public static event EventHandler<LogEventArgs> Log;

        // <summary>
        // 触发器类型
        // </summary>
        /// <summary>
        /// Trigger type
        /// </summary>
        protected readonly TriggerType Trigger;
        // <summary>
        // 数据库快照
        // </summary>
        /// <summary>
        /// Database snapshot
        /// </summary>
        protected readonly Snapshot Snapshot;
        // <summary>
        // 待释放资源的列表
        // </summary>
        /// <summary>
        /// List of resources to be released
        /// </summary>
        protected readonly List<IDisposable> Disposables = new List<IDisposable>();
        // <summary>
        // 已创建合约的字典
        // </summary>
        // <remarks>
        // key是脚本的哈希值，value是执行引擎的当前上下文的哈希值。
        // 这个设计是用来保障调用的脚本和被调用的脚本之间的存储空间的安全性。
        // </remarks>
        /// <summary>
        /// Dictionary of contract created
        /// </summary>
        /// <remarks>Key is the hash value of the script, and value is the hash value of the current context of the execution engine.
        /// This design is used to secure the storage space between the calling script and the called script.
        /// </remarks>
        protected readonly Dictionary<UInt160, UInt160> ContractsCreated = new Dictionary<UInt160, UInt160>();
        private readonly List<NotifyEventArgs> notifications = new List<NotifyEventArgs>();
        private readonly Dictionary<uint, Func<ExecutionEngine, bool>> methods = new Dictionary<uint, Func<ExecutionEngine, bool>>();
        private readonly Dictionary<uint, long> prices = new Dictionary<uint, long>();

        // <summary>
        // 通知信息列表
        // </summary>
        /// <summary>
        /// Notification information list
        /// </summary>
        public IReadOnlyList<NotifyEventArgs> Notifications => notifications;
        // <summary>
        // 标准服务构造函数
        // </summary>
        // <param name="trigger">触发器类型</param>
        // <param name="snapshot">数据库快照</param>
        /// <summary>
        /// Standard service constructor
        /// </summary>
        /// <param name="trigger">Trigger type</param>
        /// <param name="snapshot">Database snapshot</param>
        public StandardService(TriggerType trigger, Snapshot snapshot)
        {
            this.Trigger = trigger;
            this.Snapshot = snapshot;
            Register("System.ExecutionEngine.GetScriptContainer", ExecutionEngine_GetScriptContainer, 1);
            Register("System.ExecutionEngine.GetExecutingScriptHash", ExecutionEngine_GetExecutingScriptHash, 1);
            Register("System.ExecutionEngine.GetCallingScriptHash", ExecutionEngine_GetCallingScriptHash, 1);
            Register("System.ExecutionEngine.GetEntryScriptHash", ExecutionEngine_GetEntryScriptHash, 1);
            Register("System.Runtime.Platform", Runtime_Platform, 1);
            Register("System.Runtime.GetTrigger", Runtime_GetTrigger, 1);
            Register("System.Runtime.CheckWitness", Runtime_CheckWitness, 200);
            Register("System.Runtime.Notify", Runtime_Notify, 1);
            Register("System.Runtime.Log", Runtime_Log, 1);
            Register("System.Runtime.GetTime", Runtime_GetTime, 1);
            Register("System.Runtime.Serialize", Runtime_Serialize, 1);
            Register("System.Runtime.Deserialize", Runtime_Deserialize, 1);
            Register("System.Blockchain.GetHeight", Blockchain_GetHeight, 1);
            Register("System.Blockchain.GetHeader", Blockchain_GetHeader, 100);
            Register("System.Blockchain.GetBlock", Blockchain_GetBlock, 200);
            Register("System.Blockchain.GetTransaction", Blockchain_GetTransaction, 200);
            Register("System.Blockchain.GetTransactionHeight", Blockchain_GetTransactionHeight, 100);
            Register("System.Blockchain.GetContract", Blockchain_GetContract, 100);
            Register("System.Header.GetIndex", Header_GetIndex, 1);
            Register("System.Header.GetHash", Header_GetHash, 1);
            Register("System.Header.GetPrevHash", Header_GetPrevHash, 1);
            Register("System.Header.GetTimestamp", Header_GetTimestamp, 1);
            Register("System.Block.GetTransactionCount", Block_GetTransactionCount, 1);
            Register("System.Block.GetTransactions", Block_GetTransactions, 1);
            Register("System.Block.GetTransaction", Block_GetTransaction, 1);
            Register("System.Transaction.GetHash", Transaction_GetHash, 1);
            Register("System.Contract.Destroy", Contract_Destroy, 1);
            Register("System.Contract.GetStorageContext", Contract_GetStorageContext, 1);
            Register("System.Storage.GetContext", Storage_GetContext, 1);
            Register("System.Storage.GetReadOnlyContext", Storage_GetReadOnlyContext, 1);
            Register("System.Storage.Get", Storage_Get, 100);
            Register("System.Storage.Put", Storage_Put);
            Register("System.Storage.PutEx", Storage_PutEx);
            Register("System.Storage.Delete", Storage_Delete, 100);
            Register("System.StorageContext.AsReadOnly", StorageContext_AsReadOnly, 1);
        }

        internal bool CheckStorageContext(StorageContext context)
        {
            ContractState contract = Snapshot.Contracts.TryGet(context.ScriptHash);
            if (contract == null) return false;
            if (!contract.HasStorage) return false;
            return true;
        }
        // <summary>
        // 将修改后的快照提交
        // </summary>
        /// <summary>
        /// Commit the modified snapshot
        /// </summary>
        public void Commit()
        {
            Snapshot.Commit();
        }
        // <summary>
        // 释放Disposables中所有资源
        // </summary>
        /// <summary>
        /// Release all resources in Disposables
        /// </summary>
        public void Dispose()
        {
            foreach (IDisposable disposable in Disposables)
                disposable.Dispose();
            Disposables.Clear();
        }
        // <summary>
        // 根据互操作服务哈希查找对应的Gas消耗
        // </summary>
        // <param name="hash">互操作服务哈希</param>
        // <returns>对应的Gas消耗。单位是千分之一GAS</returns>
        /// <summary>
        /// Find the corresponding Gas consumption according to the interoperation service hash
        /// </summary>
        /// <param name="hash">Interoperable service hash</param>
        /// <returns>Gas consumption. The unit is one thousandth of a GAS</returns>
        public long GetPrice(uint hash)
        {
            prices.TryGetValue(hash, out long price);
            return price;
        }
        // <summary>
        // 执行一个方法。
        // </summary>
        // <param name="method">
        // 如果 method为4个字节，则将其拼接为uint，并查询其方法。<br/>
        // 如果 method不为4个字节，则当作字符串查找其哈希值前32位uint，再查询其方法。
        // </param>
        // <param name="engine">执行引擎</param>
        // <returns></returns>
        /// <summary>
        /// Execute a method.
        /// </summary>
        /// <param name="method">If method is 4 bytes, concatenate it to uint and query its method. <br/> 
        /// If method is not 4 bytes, look for the first 32 bits of uint of its hash value as a string, and then query its method.</param>
        /// <param name="engine">Execution Engine</param>
        /// <returns>Execute method.</returns>
        bool IInteropService.Invoke(byte[] method, ExecutionEngine engine)
        {
            uint hash = method.Length == 4
                ? BitConverter.ToUInt32(method, 0)
                : Encoding.ASCII.GetString(method).ToInteropMethodHash();
            if (!methods.TryGetValue(hash, out Func<ExecutionEngine, bool> func)) return false;
            return func(engine);
        }
        // <summary>
        // 注册一个互操作服务
        // </summary>
        // <param name="method">互操作服务名</param>
        // <param name="handler">互操作服务方法</param>
        /// <summary>
        /// Register an interoperable service
        /// </summary>
        /// <param name="method">Interoperable service name</param>
        /// <param name="handler">Interoperable service method</param>
        protected void Register(string method, Func<ExecutionEngine, bool> handler)
        {
            methods.Add(method.ToInteropMethodHash(), handler);
        }
        // <summary>
        // 注册一个互操作服务
        // </summary>
        // <param name="method">互操作服务名</param>
        // <param name="handler">互操作服务方法</param>
        // <param name="price">互操作服务Gas消耗</param>
        /// <summary>
        /// Register an interoperable service
        /// </summary>
        /// <param name="method">Interoperable service name</param>
        /// <param name="handler">Interoperable service method</param>
        /// <param name="price">Gas consumption of interoperable services</param>
        protected void Register(string method, Func<ExecutionEngine, bool> handler, long price)
        {
            Register(method, handler);
            prices.Add(method.ToInteropMethodHash(), price);
        }
        // <summary>
        // 获得该智能合约的脚本容器（最开始的触发者）
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the script container for this smart contract (the first trigger)
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool ExecutionEngine_GetScriptContainer(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(engine.ScriptContainer));
            return true;
        }
        // <summary>
        // 获得该智能合约执行的脚本哈希
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the hash of the script executed by the smart contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool ExecutionEngine_GetExecutingScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.CurrentContext.ScriptHash);
            return true;
        }
        // <summary>
        // 获得该智能合约的调用者的脚本哈希
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the script hash of the caller of the smart contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool ExecutionEngine_GetCallingScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.CallingContext.ScriptHash);
            return true;
        }
        // <summary>
        // 获得该智能合约的入口点（合约调用链的起点）的脚本哈希
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the script hash of the entry point of the smart contract (the starting point of the contract call chain)
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool ExecutionEngine_GetEntryScriptHash(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(engine.EntryContext.ScriptHash);
            return true;
        }

        // <summary>
        // 获得运行该智能合约的平台
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the name of the platform running the smart contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool Runtime_Platform(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Encoding.ASCII.GetBytes("NEO"));
            return true;
        }
        // <summary>
        // 获得该智能合约的触发条件（应用合约 or 鉴权合约）
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功返回true</returns>
        /// <summary>
        /// Get the trigger condition for the smart contract (Application contract or Verification contract)
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Successful execution returns true</returns>
        protected bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)Trigger);
            return true;
        }
        // <summary>
        // 验证调用该智能合约的交易/区块所需的脚本哈希是否包含此哈希。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <param name="hash">需要验证的哈希</param>
        // <returns>验证结果，如果包含则返回true，否则返回false</returns>
        /// <summary>
        /// Verify that the script hash required to invoke the transaction/block of the smart contract contains this hash.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <param name="hash">Hash that needs to be verified</param>
        /// <returns>Return true if it is included, false otherwise</returns>
        protected bool CheckWitness(ExecutionEngine engine, UInt160 hash)
        {
            IVerifiable container = (IVerifiable)engine.ScriptContainer;
            UInt160[] _hashes_for_verifying = container.GetScriptHashesForVerifying(Snapshot);
            return _hashes_for_verifying.Contains(hash);
        }
        // <summary>
        // 验证调用该智能合约的交易/区块所需的脚本哈希是否包含此公钥。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <param name="pubkey">需要验证的公钥</param>
        // <returns>验证结果，如果包含则返回true，否则返回false</returns>
        /// <summary>
        ///  Verify that the script hash required to invoke the transaction/block of the smart contract contains this public key.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <param name="pubkey">Public key that needs to be verified</param>
        /// <returns>Return true if it is included, false otherwise</returns>
        protected bool CheckWitness(ExecutionEngine engine, ECPoint pubkey)
        {
            return CheckWitness(engine, Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash());
        }
        // <summary>
        // 验证调用该智能合约的交易/区块所需的脚本哈希是否包含此哈希/公钥。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>验证结果，如果包含则返回true，否则返回false</returns>
        /// <summary>
        /// Verify that the script hash required to invoke the transaction/block of the smart contract contains this hash/public key.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if it is included, false otherwise</returns>
        protected bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            byte[] hashOrPubkey = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            bool result;
            if (hashOrPubkey.Length == 20)
                result = CheckWitness(engine, new UInt160(hashOrPubkey));
            else if (hashOrPubkey.Length == 33)
                result = CheckWitness(engine, ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1));
            else
                return false;
            engine.CurrentContext.EvaluationStack.Push(result);
            return true;
        }
        // <summary>
        // 在智能合约中向执行该智能合约的客户端发送通知
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>发送成功则返回true</returns>
        /// <summary>
        /// Send a notification to the client executing the smart contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Runtime_Notify(ExecutionEngine engine)
        {
            StackItem state = engine.CurrentContext.EvaluationStack.Pop();
            NotifyEventArgs notification = new NotifyEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), state);
            Notify?.Invoke(this, notification);
            notifications.Add(notification);
            return true;
        }
        // <summary>
        // 在智能合约中向执行该智能合约的客户端发送日志
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>发送成功则返回true</returns>
        /// <summary>
        /// Send a log to the client executing the smart contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Runtime_Log(ExecutionEngine engine)
        {
            string message = Encoding.UTF8.GetString(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            Log?.Invoke(this, new LogEventArgs(engine.ScriptContainer, new UInt160(engine.CurrentContext.ScriptHash), message));
            return true;
        }
        // <summary>
        // 获取当前时间
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>获取成功则返回true</returns>
        /// <summary>
        /// Get current time
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Runtime_GetTime(ExecutionEngine engine)
        {
            // TODO 移植到Java时考虑如何解决此处可分叉的安全漏洞的方案。
            if (Snapshot.PersistingBlock == null)
            {
                Header header = Snapshot.GetHeader(Snapshot.CurrentBlockHash);
                engine.CurrentContext.EvaluationStack.Push(header.Timestamp + Blockchain.SecondsPerBlock);
            }
            else
            {
                engine.CurrentContext.EvaluationStack.Push(Snapshot.PersistingBlock.Timestamp);
            }
            return true;
        }

        private void SerializeStackItem(StackItem item, BinaryWriter writer)
        {
            List<StackItem> serialized = new List<StackItem>();
            Stack<StackItem> unserialized = new Stack<StackItem>();
            unserialized.Push(item);
            while (unserialized.Count > 0)
            {
                item = unserialized.Pop();
                switch (item)
                {
                    case ByteArray _:
                        writer.Write((byte)StackItemType.ByteArray);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case VMBoolean _:
                        writer.Write((byte)StackItemType.Boolean);
                        writer.Write(item.GetBoolean());
                        break;
                    case Integer _:
                        writer.Write((byte)StackItemType.Integer);
                        writer.WriteVarBytes(item.GetByteArray());
                        break;
                    case InteropInterface _:
                        throw new NotSupportedException();
                    case VMArray array:
                        if (serialized.Any(p => ReferenceEquals(p, array)))
                            throw new NotSupportedException();
                        serialized.Add(array);
                        if (array is Struct)
                            writer.Write((byte)StackItemType.Struct);
                        else
                            writer.Write((byte)StackItemType.Array);
                        writer.WriteVarInt(array.Count);
                        for (int i = array.Count - 1; i >= 0; i--)
                            unserialized.Push(array[i]);
                        break;
                    case Map map:
                        if (serialized.Any(p => ReferenceEquals(p, map)))
                            throw new NotSupportedException();
                        serialized.Add(map);
                        writer.Write((byte)StackItemType.Map);
                        writer.WriteVarInt(map.Count);
                        foreach (var pair in map.Reverse())
                        {
                            unserialized.Push(pair.Value);
                            unserialized.Push(pair.Key);
                        }
                        break;
                }
            }
        }
        // <summary>
        // 对数据流进行序列化。取出EvaluationStack栈顶的元素，序列化以后押回栈顶。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Serialize the data stream. Take the elements at the top of the EvaluationStack and push them back to the top of the stack after serialization.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Runtime_Serialize(ExecutionEngine engine)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                try
                {
                    SerializeStackItem(engine.CurrentContext.EvaluationStack.Pop(), writer);
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                writer.Flush();
                if (ms.Length > ApplicationEngine.MaxItemSize)
                    return false;
                engine.CurrentContext.EvaluationStack.Push(ms.ToArray());
            }
            return true;
        }

        private StackItem DeserializeStackItem(BinaryReader reader)
        {
            Stack<StackItem> deserialized = new Stack<StackItem>();
            int undeserialized = 1;
            while (undeserialized-- > 0)
            {
                StackItemType type = (StackItemType)reader.ReadByte();
                switch (type)
                {
                    case StackItemType.ByteArray:
                        deserialized.Push(new ByteArray(reader.ReadVarBytes()));
                        break;
                    case StackItemType.Boolean:
                        deserialized.Push(new VMBoolean(reader.ReadBoolean()));
                        break;
                    case StackItemType.Integer:
                        deserialized.Push(new Integer(new BigInteger(reader.ReadVarBytes())));
                        break;
                    case StackItemType.Array:
                    case StackItemType.Struct:
                        {
                            int count = (int)reader.ReadVarInt(ApplicationEngine.MaxArraySize);
                            deserialized.Push(new ContainerPlaceholder
                            {
                                Type = type,
                                ElementCount = count
                            });
                            undeserialized += count;
                        }
                        break;
                    case StackItemType.Map:
                        {
                            int count = (int)reader.ReadVarInt(ApplicationEngine.MaxArraySize);
                            deserialized.Push(new ContainerPlaceholder
                            {
                                Type = type,
                                ElementCount = count
                            });
                            undeserialized += count * 2;
                        }
                        break;
                    default:
                        throw new FormatException();
                }
            }
            Stack<StackItem> stack_temp = new Stack<StackItem>();
            while (deserialized.Count > 0)
            {
                StackItem item = deserialized.Pop();
                if (item is ContainerPlaceholder placeholder)
                {
                    switch (placeholder.Type)
                    {
                        case StackItemType.Array:
                            VMArray array = new VMArray();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                array.Add(stack_temp.Pop());
                            item = array;
                            break;
                        case StackItemType.Struct:
                            Struct @struct = new Struct();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                                @struct.Add(stack_temp.Pop());
                            item = @struct;
                            break;
                        case StackItemType.Map:
                            Map map = new Map();
                            for (int i = 0; i < placeholder.ElementCount; i++)
                            {
                                StackItem key = stack_temp.Pop();
                                StackItem value = stack_temp.Pop();
                                map.Add(key, value);
                            }
                            item = map;
                            break;
                    }
                }
                stack_temp.Push(item);
            }
            return stack_temp.Peek();
        }
        // <summary>
        // 将数据反序列化。取出栈顶元素进行反序列化，得到的结果押回栈顶。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Deserialize the data. The top element of the stack is taken out for deserialization, and the result is pushed back to the top of the stack.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Runtime_Deserialize(ExecutionEngine engine)
        {
            byte[] data = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            using (MemoryStream ms = new MemoryStream(data, false))
            using (BinaryReader reader = new BinaryReader(ms))
            {
                StackItem item;
                try
                {
                    item = DeserializeStackItem(reader);
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (IOException)
                {
                    return false;
                }
                engine.CurrentContext.EvaluationStack.Push(item);
            }
            return true;
        }
        // <summary>
        // 获得当前区块高度
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true</returns>
        /// <summary>
        /// Get the current block height
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Blockchain_GetHeight(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(Snapshot.Height);
            return true;
        }
        // <summary>
        // 通过区块高度或区块 Hash，查找区块头
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Find block headers by block height or block Hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Blockchain_GetHeader(ExecutionEngine engine)
        {
            byte[] data = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            UInt256 hash;
            if (data.Length <= 5)
                hash = Blockchain.Singleton.GetBlockHash((uint)new BigInteger(data));
            else if (data.Length == 32)
                hash = new UInt256(data);
            else
                return false;
            if (hash == null)
            {
                engine.CurrentContext.EvaluationStack.Push(new byte[0]);
            }
            else
            {
                Header header = Snapshot.GetHeader(hash);
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(header));
            }
            return true;
        }
        // <summary>
        // 通过区块高度或区块 Hash，查找区块
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Find block by block height or block Hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Blockchain_GetBlock(ExecutionEngine engine)
        {
            byte[] data = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            UInt256 hash;
            if (data.Length <= 5)
                hash = Blockchain.Singleton.GetBlockHash((uint)new BigInteger(data));
            else if (data.Length == 32)
                hash = new UInt256(data);
            else
                return false;
            if (hash == null)
            {
                engine.CurrentContext.EvaluationStack.Push(new byte[0]);
            }
            else
            {
                Block block = Snapshot.GetBlock(hash);
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(block));
            }
            return true;
        }
        // <summary>
        // 通过交易哈希查找交易
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true</returns>
        /// <summary>
        /// Find transaction by transaction hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Blockchain_GetTransaction(ExecutionEngine engine)
        {
            byte[] hash = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            Transaction tx = Snapshot.GetTransaction(new UInt256(hash));
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(tx));
            return true;
        }
        // <summary>
        // 通过交易哈希查找交易高度
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true</returns>
        /// <summary>
        /// Find transaction heights by transaction hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Blockchain_GetTransactionHeight(ExecutionEngine engine)
        {
            byte[] hash = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            int? height = (int?)Snapshot.Transactions.TryGet(new UInt256(hash))?.BlockIndex;
            engine.CurrentContext.EvaluationStack.Push(height ?? -1);
            return true;
        }
        // <summary>
        // 根据合约哈希获取合约内容
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true</returns>
        /// <summary>
        /// Get contract content by contract hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful</returns>
        protected bool Blockchain_GetContract(ExecutionEngine engine)
        {
            UInt160 hash = new UInt160(engine.CurrentContext.EvaluationStack.Pop().GetByteArray());
            ContractState contract = Snapshot.Contracts.TryGet(hash);
            if (contract == null)
                engine.CurrentContext.EvaluationStack.Push(new byte[0]);
            else
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(contract));
            return true;
        }
        // <summary>
        // 获得该区块的高度
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the height of the block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Header_GetIndex(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.CurrentContext.EvaluationStack.Push(header.Index);
                return true;
            }
            return false;
        }
        // <summary>
        // 获得该区块的哈希
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the hash of the block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Header_GetHash(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.CurrentContext.EvaluationStack.Push(header.Hash.ToArray());
                return true;
            }
            return false;
        }
        // <summary>
        // 获得前一个区块的哈希
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the hash of the previous block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Header_GetPrevHash(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.CurrentContext.EvaluationStack.Push(header.PrevHash.ToArray());
                return true;
            }
            return false;
        }
        // <summary>
        // 获得区块的时间戳
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the timestamp of the block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Header_GetTimestamp(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                BlockBase header = _interface.GetInterface<BlockBase>();
                if (header == null) return false;
                engine.CurrentContext.EvaluationStack.Push(header.Timestamp);
                return true;
            }
            return false;
        }
        // <summary>
        // 获得当前区块中交易的数量
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the number of transactions in the current block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Block_GetTransactionCount(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                if (block == null) return false;
                engine.CurrentContext.EvaluationStack.Push(block.Transactions.Length);
                return true;
            }
            return false;
        }
        // <summary>
        // 获得当前区块中所有的交易
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get all the transactions in the current block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Block_GetTransactions(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                if (block == null) return false;
                if (block.Transactions.Length > ApplicationEngine.MaxArraySize)
                    return false;
                engine.CurrentContext.EvaluationStack.Push(block.Transactions.Select(p => StackItem.FromInterface(p)).ToArray());
                return true;
            }
            return false;
        }
        // <summary>
        // 获得当前区块中指定索引的交易
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the transaction for the specified index in the current block
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Block_GetTransaction(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Block block = _interface.GetInterface<Block>();
                int index = (int)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
                if (block == null) return false;
                if (index < 0 || index >= block.Transactions.Length) return false;
                Transaction tx = block.Transactions[index];
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(tx));
                return true;
            }
            return false;
        }
        // <summary>
        // 获得当前交易的 Hash
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the current transaction Hash
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Transaction_GetHash(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                Transaction tx = _interface.GetInterface<Transaction>();
                if (tx == null) return false;
                engine.CurrentContext.EvaluationStack.Push(tx.Hash.ToArray());
                return true;
            }
            return false;
        }
        // <summary>
        // 获取当前存储区上下文
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the current storage context
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_GetContext(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
            {
                ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
                IsReadOnly = false
            }));
            return true;
        }
        // <summary>
        // 获取当前存储区上下文（只读）
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the current storage context.(read only)
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_GetReadOnlyContext(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
            {
                ScriptHash = new UInt160(engine.CurrentContext.ScriptHash),
                IsReadOnly = true
            }));
            return true;
        }
        // <summary>
        // 查询操作，在持久化存储区中通过 key 查询对应的 value
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Query operation, query the corresponding value by key in the persistent storage area
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_Get(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (!CheckStorageContext(context)) return false;
                byte[] key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
                StorageItem item = Snapshot.Storages.TryGet(new StorageKey
                {
                    ScriptHash = context.ScriptHash,
                    Key = key
                });
                engine.CurrentContext.EvaluationStack.Push(item?.Value ?? new byte[0]);
                return true;
            }
            return false;
        }
        // <summary>
        // 将当前存储区上下文设为只读
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Make the current storage context read-only
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool StorageContext_AsReadOnly(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (!context.IsReadOnly)
                    context = new StorageContext
                    {
                        ScriptHash = context.ScriptHash,
                        IsReadOnly = true
                    };
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(context));
                return true;
            }
            return false;
        }
        // <summary>
        // 获得合约的存储上下文
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Get the storage context of the contract
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Contract_GetStorageContext(ExecutionEngine engine)
        {
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                ContractState contract = _interface.GetInterface<ContractState>();
                if (!ContractsCreated.TryGetValue(contract.ScriptHash, out UInt160 created)) return false;
                if (!created.Equals(new UInt160(engine.CurrentContext.ScriptHash))) return false;
                engine.CurrentContext.EvaluationStack.Push(StackItem.FromInterface(new StorageContext
                {
                    ScriptHash = contract.ScriptHash,
                    IsReadOnly = false
                }));
                return true;
            }
            return false;
        }
        // <summary>
        // 销毁合约，如果合约使用了存储区，则同时将合约的存储区删除。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Destroy the contract, if the contract uses the storage, the storage of the contract is also deleted.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Contract_Destroy(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application) return false;
            UInt160 hash = new UInt160(engine.CurrentContext.ScriptHash);
            ContractState contract = Snapshot.Contracts.TryGet(hash);
            if (contract == null) return true;
            Snapshot.Contracts.Delete(hash);
            if (contract.HasStorage)
                foreach (var pair in Snapshot.Storages.Find(hash.ToArray()))
                    Snapshot.Storages.Delete(pair.Key);
            return true;
        }

        private bool PutEx(StorageContext context, byte[] key, byte[] value, StorageFlags flags)
        {
            if (Trigger != TriggerType.Application && Trigger != TriggerType.ApplicationR)
                return false;
            if (key.Length > 1024) return false;
            if (context.IsReadOnly) return false;
            if (!CheckStorageContext(context)) return false;
            StorageKey skey = new StorageKey
            {
                ScriptHash = context.ScriptHash,
                Key = key
            };
            StorageItem item = Snapshot.Storages.GetAndChange(skey, () => new StorageItem());
            if (item.IsConstant) return false;
            item.Value = value;
            item.IsConstant = flags.HasFlag(StorageFlags.Constant);
            return true;
        }
        // <summary>
        // 普通的插入操作，以 key-value 的形式向持久化存储区中插入数据。
        // 保存以后可修改。
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// A normal insert operation that inserts data into the persistent store as a key-value.
        /// Can be modified after saving.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_Put(ExecutionEngine engine)
        {
            if (!(engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface))
                return false;
            StorageContext context = _interface.GetInterface<StorageContext>();
            byte[] key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            byte[] value = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            return PutEx(context, key, value, StorageFlags.None);
        }
        // <summary>
        // 有存储标记插入操作，以 key-value 的形式向持久化存储区中插入数据
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// There is a store flag insert operation that inserts data into the persistent store as a key-value
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_PutEx(ExecutionEngine engine)
        {
            if (!(engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface))
                return false;
            StorageContext context = _interface.GetInterface<StorageContext>();
            byte[] key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            byte[] value = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            StorageFlags flags = (StorageFlags)(byte)engine.CurrentContext.EvaluationStack.Pop().GetBigInteger();
            return PutEx(context, key, value, flags);
        }
        // <summary>
        // 删除操作，在持久化存储区中通过 key 删除对应的 value
        // </summary>
        // <param name="engine">当前执行引擎</param>
        // <returns>执行成功则返回true,否则返回false</returns>
        /// <summary>
        /// Delete operation, delete the corresponding value by key in the persistent storage.
        /// </summary>
        /// <param name="engine">Current execution engine</param>
        /// <returns>Return true if successful,false otherwise.</returns>
        protected bool Storage_Delete(ExecutionEngine engine)
        {
            if (Trigger != TriggerType.Application && Trigger != TriggerType.ApplicationR)
                return false;
            if (engine.CurrentContext.EvaluationStack.Pop() is InteropInterface _interface)
            {
                StorageContext context = _interface.GetInterface<StorageContext>();
                if (context.IsReadOnly) return false;
                if (!CheckStorageContext(context)) return false;
                StorageKey key = new StorageKey
                {
                    ScriptHash = context.ScriptHash,
                    Key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray()
                };
                if (Snapshot.Storages.TryGet(key)?.IsConstant == true) return false;
                Snapshot.Storages.Delete(key);
                return true;
            }
            return false;
        }
    }
}
