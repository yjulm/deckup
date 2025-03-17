// >>
//--------------------------------------------------------------
//Project: DeckupClient
//File: \Program.cs
//File Created: 2021-12-01 09:25:30
//--------------------------------------------------------------
//Author: Yjulm
//Email: yjulm@hotmail.com
//--------------------------------------------------------------
//Last Modified By: Yjulm
//Last Modified Date: 2025-01-03 16:58:20
//--------------------------------------------------------------
// <<


/*
 * 创作者：yjulm@hotmail.com
 * 生成时间：2021/6/23 11:48:03
 * CLR版本：4.0.30319.42000
 */

using Deckup.Extend;
using Deckup;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DeckupTestClient
{
    internal class Program
    {
        private static int loop = 0;

        private static async Task Receive(DeckupClientWrap client, CancellationTokenSource source)
        {
            await Task.Run(() =>
            {
                Stopwatch ts = Stopwatch.StartNew();
                int index = 0;
                long byteCount = 0;

                while (!source.IsCancellationRequested)
                {
                    FilePart rcv = client.Receive();
                    if (rcv != null)
                    {
                        (index++ != rcv.DebugIndex).Break();
                        byteCount += rcv.ValidSize + Segment.StructSize;

                        client.SetReceivePart(rcv);
                        if (rcv.Length != rcv.MaxDataSize)
                            break;
                    }
                    else if (!client.ProcessReceive() && client.Disconnected)
                    {
                        Console.WriteLine("[Loop:{0}] Receive detected disconnect!", loop);
                        break;
                    }

                    if (ts.ElapsedMilliseconds >= 1000)
                    {
                        Console.WriteLine("RTT:{0} SPEED:{1:F4} MB/S", client.Rtt, Math.Round((byteCount / 1024 / 1024) / ts.Elapsed.TotalSeconds, 4));
                        byteCount = 0;
                        ts.Restart();
                    }
                }

                source.Cancel();
                Console.WriteLine("Receive End!");
            });
        }

        private static async Task Send(DeckupClientWrap client, CancellationTokenSource source)
        {
            await Task.Run(() =>
            {
                uint index = 0;
                FilePart file = null;
                bool resend = false;
                bool end = false;

                while (!source.IsCancellationRequested)
                {
                    if (!end && !resend)
                    {
                        file = client.GetSendPart();
                        file.DebugIndex = index++;
                    }

                    if (!end)
                    {
                        resend = !client.Send(file);
                        end = client.SendOver && !resend;
                    }

                    if (!client.ProcessSend() && client.Disconnected)
                    {
                        Console.WriteLine("[Loop:{0}] Send detected disconnect!", loop);
                        break;
                    }
                }

                Console.WriteLine("Send End!");
            });
        }

        private static void Main(string[] args)
        {
            bool ret = false;
            CancellationTokenSource source = null;

            while (!ret)
            {
                Console.WriteLine("[Loop:{0}] Try Connect!", loop);

            retry:
                DeckupClientWrap client = new DeckupClientWrap();
                if (client.Connect("192.168.0.3", 30319))
                {
                    Console.WriteLine("[Loop:{0}] Connect success!", loop);

                    source = new CancellationTokenSource();
                    Task s = Send(client, source);
                    Task r = Receive(client, source);
                    Task.WaitAll(s, r);
                    client.CheckSum();
                    client.Verify();

                    Console.WriteLine("WaitAll end!");
                    Console.WriteLine("[Loop:{0}] Disconnect: {1}!", loop, client.Disconnect());

                    loop++;
                    client.Dispose();
                    source.Dispose();
                    Console.WriteLine(string.Format("{0}", Environment.NewLine));
                    Debug.WriteLine(string.Format("[Loop:{0}] {1}{1}{1}", loop, Environment.NewLine));
                }
                else
                {
                    client.Dispose();
                    Console.WriteLine("[Loop:{0}] Connect failed!", loop);
                    Thread.Sleep(3000);
                    goto retry;
                }
            }

            Console.WriteLine("enter！");
            Console.ReadLine();
            ret = true;
            source.Cancel();
            Console.WriteLine("enter！");
            Console.ReadLine();
        }
    }
}