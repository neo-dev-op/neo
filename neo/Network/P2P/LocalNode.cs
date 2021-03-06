﻿using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;

namespace Neo.Network.P2P
{
    /// <summary>
    /// 描述本地节点的类，是Peer类的子类
    /// </summary>
    public class LocalNode : Peer
    {
        /// <summary>
        /// 定义的接收Akka传递的消息类型，描述需要转发的数据
        /// </summary>
        public class Relay {
            /// <summary>
            /// Inventory数据
            /// </summary>
            public IInventory Inventory;
        }
        internal class RelayDirectly { public IInventory Inventory; }
        internal class SendDirectly { public IInventory Inventory; }
        /// <summary>
        /// 协议版本
        /// </summary>
        public const uint ProtocolVersion = 0;

        private static readonly object lockObj = new object();
        private readonly NeoSystem system;
        internal readonly ConcurrentDictionary<IActorRef, RemoteNode> RemoteNodes = new ConcurrentDictionary<IActorRef, RemoteNode>();
        /// <summary>
        /// 已连接节点数量
        /// </summary>
        public int ConnectedCount => RemoteNodes.Count;
        /// <summary>
        /// 备用节点数量
        /// </summary>
        public int UnconnectedCount => UnconnectedPeers.Count;
        /// <summary>
        /// 本地节点的一个随机数。用来在交换数据时标识节点身份。
        /// </summary>
        public static readonly uint Nonce;
        /// <summary>
        /// 节点软件的名称和版本的描述信息
        /// </summary>
        public static string UserAgent { get; set; }

        private static LocalNode singleton { get; set; }
        /// <summary>
        /// LocalNode的单例
        /// </summary>
        public static LocalNode Singleton
        {
            get
            {
                // TODO 有待改进
                while (singleton == null) Thread.Sleep(10);
                return singleton;
            }
        }
        /// <summary>
        /// 初始化模块
        /// </summary>
        static LocalNode()
        {
            Random rand = new Random();
            Nonce = (uint)rand.Next();
            UserAgent = $"/{Assembly.GetExecutingAssembly().GetName().Name}:{Assembly.GetExecutingAssembly().GetVersion()}/";
        }
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="system">NeoSystem系统对象</param>
        /// <exception cref="InvalidOperationException">此对象只允许创建一个实例，创建第二实例时抛出异常</exception>
        public LocalNode(NeoSystem system)
        {
            lock (lockObj)
            {
                if (singleton != null)
                    throw new InvalidOperationException();
                this.system = system;
                singleton = this;
            }
        }

        private void BroadcastMessage(string command, ISerializable payload = null)
        {
            BroadcastMessage(Message.Create(command, payload));
        }

        private void BroadcastMessage(Message message)
        {
            Connections.Tell(message);
        }

        private static IPEndPoint GetIPEndpointFromHostPort(string hostNameOrAddress, int port)
        {
            if (IPAddress.TryParse(hostNameOrAddress, out IPAddress ipAddress))
                return new IPEndPoint(ipAddress, port);
            IPHostEntry entry;
            try
            {
                entry = Dns.GetHostEntry(hostNameOrAddress);
            }
            catch (SocketException)
            {
                return null;
            }
            ipAddress = entry.AddressList.FirstOrDefault(p => p.AddressFamily == AddressFamily.InterNetwork || p.IsIPv6Teredo);
            if (ipAddress == null) return null;
            return new IPEndPoint(ipAddress, port);
        }
        /// <summary>
        /// 从种子节点列表中读取指定个数个备用节点
        /// </summary>
        /// <param name="seedsToTake">读取个数</param>
        /// <returns>读取的备用节点</returns>
        private static IEnumerable<IPEndPoint> GetIPEndPointsFromSeedList(int seedsToTake)
        {
            if (seedsToTake > 0)
            {
                Random rand = new Random();
                foreach (string hostAndPort in Settings.Default.SeedList.OrderBy(p => rand.Next()))
                {
                    if (seedsToTake == 0) break;
                    string[] p = hostAndPort.Split(':');
                    IPEndPoint seed;
                    try
                    {
                        seed = GetIPEndpointFromHostPort(p[0], int.Parse(p[1]));
                    }
                    catch (AggregateException)
                    {
                        continue;
                    }
                    if (seed == null) continue;
                    seedsToTake--;
                    yield return seed;
                }
            }
        }
        /// <summary>
        /// 获取远端节点
        /// </summary>
        /// <returns>远端节点</returns>
        public IEnumerable<RemoteNode> GetRemoteNodes()
        {
            return RemoteNodes.Values;
        }
        /// <summary>
        /// 获取未连接备用节点列表
        /// </summary>
        /// <returns>未连接备用节点列表</returns>
        public IEnumerable<IPEndPoint> GetUnconnectedPeers()
        {
            return UnconnectedPeers;
        }
        /// <summary>
        /// 获取更多的节点
        /// 1、当本地节点与其他节点有连接时，会向其请求备用节点列表
        /// 2、当本地节点未与其他节点有连接时，会从配置文件中读取备用节点列表
        /// </summary>
        /// <param name="count">需求的数量</param>
        protected override void NeedMorePeers(int count)
        {
            count = Math.Max(count, 5);
            if (ConnectedPeers.Count > 0)
            {
                BroadcastMessage("getaddr");
            }
            else
            {
                AddPeers(GetIPEndPointsFromSeedList(count));
            }
        }
        /// <summary>
        /// 用于LocalNode对象接收和处理其他对象通过Akka框架传来的消息
        /// </summary>
        /// <param name="message">消息</param>
        protected override void OnReceive(object message)
        {
            base.OnReceive(message);
            switch (message)
            {
                case Message msg:
                    BroadcastMessage(msg);
                    break;
                case Relay relay:
                    OnRelay(relay.Inventory);
                    break;
                case RelayDirectly relay:
                    OnRelayDirectly(relay.Inventory);
                    break;
                case SendDirectly send:
                    OnSendDirectly(send.Inventory);
                    break;
                case RelayResultReason _:
                    break;
            }
        }

        private void OnRelay(IInventory inventory)
        {
            if (inventory is Transaction transaction)
                system.Consensus?.Tell(transaction);
            system.Blockchain.Tell(inventory);
        }

        private void OnRelayDirectly(IInventory inventory)
        {
            Connections.Tell(new RemoteNode.Relay { Inventory = inventory });
        }

        private void OnSendDirectly(IInventory inventory)
        {
            Connections.Tell(inventory);
        }
        /// <summary>
        /// 创建一个本地节点对象。（AKKA框架）
        /// </summary>
        /// <param name="system">NEO系统对象</param>
        /// <returns>本地节点对象</returns>
        public static Props Props(NeoSystem system)
        {
            return Akka.Actor.Props.Create(() => new LocalNode(system));
        }
        /// <summary>
        /// 创建一个远端节点对象
        /// </summary>
        /// <param name="connection">一个连接对象</param>
        /// <param name="remote">远端节点的IP和端口</param>
        /// <param name="local">本地节点的IP和端口</param>
        /// <returns>远端节点对象的AKKA引用</returns>
        protected override Props ProtocolProps(object connection, IPEndPoint remote, IPEndPoint local)
        {
            return RemoteNode.Props(system, connection, remote, local);
        }
    }
}
