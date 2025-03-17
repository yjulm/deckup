/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/23 11:48:15
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup.Lock;
using DeckupTestClient;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deckup;

namespace DeckupTestServer
{
    internal class Program
    {
        private static FilePart rcv = null;
        private static int loop = 0;

        public static async Task Send(DeckupClient client, ReadWriteOneByOneLock rcvLock, CancellationToken token)
        {
            await Task.Run(() =>
            {
                try
                {
                    bool resend = false;
                    int index = 0;

                    while (!token.IsCancellationRequested)
                    {
                        if (resend || rcvLock.EnterRead(false))
                        {
                            if (rcv != null)
                            {
                                if (!resend)
                                    (index++ != rcv.DebugIndex).Break();
                                resend = !client.Send(rcv);
                            }

                            if (!resend)
                                rcvLock.ExitRead();
                        }

                        if (!client.ProcessSend() && client.Disconnected)
                        {
                            Console.WriteLine("[Loop:{0}] Send detected disconnect!", loop);
                            if (resend)
                                rcvLock.ExitRead();
                            break;
                        }
                    }

                    Console.WriteLine("[Loop:{0}] Send End!", loop);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    Debugger.Break();
                }
            });
        }

        public static async Task Receive(DeckupClient client, ReadWriteOneByOneLock rcvLock, CancellationToken token)
        {
            await Task.Run(() =>
            {
                try
                {
                    Stopwatch ts = Stopwatch.StartNew();
                    int index = 0;

                    while (!token.IsCancellationRequested)
                    {
                        if (!client.ProcessReceive() && client.Disconnected)
                        {
                            Console.WriteLine("[Loop:{0}] Receive detected disconnect!", loop);
                            break;
                        }

                        if (rcvLock.EnterWrite(false))
                        {
                            //TODO: 当前Receive取出空时，此时出现 CanReadSize = 0 且同时 ReceiveMargin = 0 将导致传输停止
                            rcv = client.Receive<FilePart>();
                            if (rcv != null)
                                (index++ != rcv.DebugIndex).Break();

                            rcvLock.ExitWrite();
                        }

                        if (ts.ElapsedMilliseconds >= 1000)
                        {
                            Console.WriteLine("[Loop:{0}] RTT:{1}", loop, client.Rtt);
                            ts.Restart();
                        }
                    }

                    Console.WriteLine("[Loop:{0}] Receive End!", loop);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                    Debugger.Break();
                }
            });
        }

        private static void Main(string[] args)
        {
            bool ret = false;
            DeckupServer server = new DeckupServer();
            server.Listen(100);
            server.Bind("0.0.0.0", 30319);

            ReadWriteOneByOneLock rcvLock = new ReadWriteOneByOneLock();
            CancellationTokenSource source = new CancellationTokenSource();

            while (!ret)
            {
                Console.WriteLine("[Loop:{0}] Try Accept!", loop);
                DeckupClient client = server.Accept(); //TODO: 发起多个连接请求，后面的连接将出现RTT非常高
                if (client != null)
                {
                    Console.WriteLine("[Loop:{0}] Accept success!", loop);
                    Debug.Assert(client.Connected);

                    Task s = Send(client, rcvLock, source.Token);
                    Task r = Receive(client, rcvLock, source.Token);
                    Task.WaitAll(s, r);

                    Console.WriteLine("[Loop:{0}] Disconnect!", loop);
                    Console.WriteLine(string.Format("{0}", Environment.NewLine));
                    Debug.WriteLine(string.Format("[Loop:{0}] {1}{1}{1}", loop, Environment.NewLine));

                    loop++;
                }
                else
                    Console.WriteLine("[Loop:{0}] Accept failed!", loop);
            }

            Console.WriteLine("enter!");
            Console.ReadLine();
            ret = true;
            source.Cancel();
            Console.WriteLine("enter!");
            Console.ReadLine();
        }
    }
}