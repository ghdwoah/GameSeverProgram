using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UdpBasics
{
    /// <summary>
    /// ═══════════════════════════════════════════════════
    /// 예제 D — UDP 소켓 직접 다뤄보기
    ///
    /// TCP와 API가 어떻게 다른지 손으로 익힙니다.
    ///
    /// 이 예제는 서버와 클라이언트를 한 프로그램 안에서
    /// 같이 띄웁니다. (포트만 다르게)
    /// 그래서 F5 한 번이면 바로 확인할 수 있습니다.
    /// ═══════════════════════════════════════════════════
    /// </summary>
    class Program
    {
        const int SERVER_PORT = 8888;

        static async Task Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            PrintTitle();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("──────────────────────────────────────────");
                Console.WriteLine(" 1. UDP 서버 열기 — Listen도 Accept도 없다");
                Console.WriteLine(" 2. 보내고 받아보기");
                Console.WriteLine(" 3. 받는 쪽이 없어도 에러가 안 난다");
                Console.WriteLine(" 4. 누가 보냈는지 구분하기 (EndPoint)");
                Console.WriteLine(" 5. 여러 명이 한 소켓으로 들어온다");
                Console.WriteLine(" 6. TCP와 코드 비교");
                Console.WriteLine(" 0. 종료");
                Console.WriteLine("──────────────────────────────────────────");
                Console.Write(" 선택 > ");

                string? input = Console.ReadLine();
                Console.WriteLine();

                switch (input)
                {
                    case "1": OpenServer();            break;
                    case "2": await SendReceive();     break;
                    case "3": await NoReceiver();      break;
                    case "4": await WhoSentIt();       break;
                    case "5": await MultipleClients(); break;
                    case "6": CompareCode();           break;
                    case "0": return;
                    default:  Console.WriteLine("1~6 중에 골라주세요."); break;
                }
            }
        }

        // ═══════════════════════════════════════
        // 1. UDP 서버 열기
        // ═══════════════════════════════════════
        static void OpenServer()
        {
            Console.WriteLine("[UDP 서버 열기]");
            Console.WriteLine();

            // TCP와 다른 점: SocketType.Dgram, ProtocolType.Udp
            Socket socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,          // TCP는 Stream, UDP는 Dgram
                ProtocolType.Udp);

            socket.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));

            Console.WriteLine($"  포트 {SERVER_PORT}번에 바인드 완료");
            Console.WriteLine();
            Console.WriteLine("  여기서 끝입니다.");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  Listen() 없음");
            Console.WriteLine("  Accept() 없음");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("  UDP는 '연결'이라는 개념이 없습니다.");
            Console.WriteLine("  그냥 포트를 열어두고 뭐가 오면 받습니다.");
            Console.WriteLine();
            Console.WriteLine("  편지함과 같습니다.");
            Console.WriteLine("  누가 넣을지 미리 약속하지 않아도 되고,");
            Console.WriteLine("  아무나 넣을 수 있습니다.");

            socket.Close();
        }

        // ═══════════════════════════════════════
        // 2. 보내고 받기
        // ═══════════════════════════════════════
        static async Task SendReceive()
        {
            Console.WriteLine("[보내고 받기]");
            Console.WriteLine();

            // ── 서버 쪽 ──
            Socket server = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));
            Console.WriteLine($"  [서버] 포트 {SERVER_PORT} 대기 시작");

            // 수신 루프를 백그라운드로
            var cts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                byte[] buffer = new byte[1024];
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        // ReceiveFrom : 데이터 + "누가 보냈는지"를 같이 받음
                        var result = await server.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer), SocketFlags.None, from);

                        string msg = Encoding.UTF8.GetString(buffer, 0, result.ReceivedBytes);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [서버] 수신 : \"{msg}\"  (보낸이 {result.RemoteEndPoint})");
                        Console.ResetColor();
                    }
                    catch { break; }
                }
            });

            await Task.Delay(300);

            // ── 클라이언트 쪽 ──
            Socket client = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
            var target = new IPEndPoint(IPAddress.Parse("127.0.0.1"), SERVER_PORT);

            Console.WriteLine();
            Console.WriteLine("  [클라] 연결 과정 없이 바로 보냅니다");
            Console.WriteLine();

            string[] messages = { "안녕하세요", "UDP 테스트", "좌표 (10, 20)" };
            foreach (string m in messages)
            {
                byte[] data = Encoding.UTF8.GetBytes(m);

                // SendTo : 받을 사람 주소를 매번 지정
                await client.SendToAsync(new ArraySegment<byte>(data),
                                         SocketFlags.None, target);

                Console.WriteLine($"  [클라] 전송 : \"{m}\"");
                await Task.Delay(400);
            }

            await Task.Delay(500);
            cts.Cancel();
            client.Close();
            server.Close();

            Console.WriteLine();
            Console.WriteLine("  TCP와 다른 점:");
            Console.WriteLine("    Connect() 를 안 했습니다");
            Console.WriteLine("    보낼 때마다 받을 주소를 지정합니다 (SendTo)");
            Console.WriteLine("    받을 때 보낸 사람 주소가 같이 옵니다 (ReceiveFrom)");
        }

        // ═══════════════════════════════════════
        // 3. 받는 쪽이 없어도
        // ═══════════════════════════════════════
        static async Task NoReceiver()
        {
            Console.WriteLine("[아무도 없는 곳에 보내기]");
            Console.WriteLine();
            Console.WriteLine("  포트 9999번에는 아무도 없습니다.");
            Console.WriteLine("  그래도 보내봅니다.");
            Console.WriteLine();

            Socket client = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
            var nowhere = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9999);

            try
            {
                byte[] data = Encoding.UTF8.GetBytes("아무도 안 듣는 메시지");
                await client.SendToAsync(new ArraySegment<byte>(data),
                                         SocketFlags.None, nowhere);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  전송 성공 (에러 없음)");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  에러 : {ex.Message}");
            }

            client.Close();

            Console.WriteLine();
            Console.WriteLine("  에러가 안 났습니다.");
            Console.WriteLine();
            Console.WriteLine("  UDP는 '보냈다'까지만 책임집니다.");
            Console.WriteLine("  도착했는지는 신경 쓰지 않아요.");
            Console.WriteLine();
            Console.WriteLine("  TCP라면 Connect 단계에서 바로 실패했을 겁니다.");
        }

        // ═══════════════════════════════════════
        // 4. 누가 보냈는지 구분
        // ═══════════════════════════════════════
        static async Task WhoSentIt()
        {
            Console.WriteLine("[보낸 사람 구분하기]");
            Console.WriteLine();
            Console.WriteLine("  UDP는 소켓 하나로 모두의 패킷을 받습니다.");
            Console.WriteLine("  그래서 EndPoint(IP + 포트)로 구분해야 합니다.");
            Console.WriteLine();

            Socket server = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));

            var cts = new CancellationTokenSource();
            var known = new Dictionary<string, string>();   // EndPoint → 닉네임

            _ = Task.Run(async () =>
            {
                byte[] buffer = new byte[1024];
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var r = await server.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer), SocketFlags.None, from);

                        string msg = Encoding.UTF8.GetString(buffer, 0, r.ReceivedBytes);
                        string key = r.RemoteEndPoint.ToString()!;

                        if (!known.ContainsKey(key))
                        {
                            known[key] = msg;   // 첫 메시지를 닉네임으로
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"  [서버] 새 플레이어 등록 : {msg}");
                            Console.WriteLine($"         주소 {key}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.WriteLine($"  [서버] {known[key]} 님의 메시지 : \"{msg}\"");
                        }
                    }
                    catch { break; }
                }
            });

            await Task.Delay(300);

            // 클라이언트 3명 (각자 다른 포트를 자동 할당받음)
            var clients = new List<Socket>();
            string[] names = { "홍길동", "김철수", "이영희" };
            var target = new IPEndPoint(IPAddress.Parse("127.0.0.1"), SERVER_PORT);

            foreach (string name in names)
            {
                Socket c = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Dgram, ProtocolType.Udp);
                clients.Add(c);

                byte[] data = Encoding.UTF8.GetBytes(name);
                await c.SendToAsync(new ArraySegment<byte>(data), SocketFlags.None, target);
                await Task.Delay(300);
            }

            await Task.Delay(300);

            // 각자 메시지 하나씩 더
            for (int i = 0; i < clients.Count; i++)
            {
                byte[] data = Encoding.UTF8.GetBytes($"좌표 ({i * 5}, {i * 3})");
                await clients[i].SendToAsync(new ArraySegment<byte>(data),
                                             SocketFlags.None, target);
                await Task.Delay(300);
            }

            await Task.Delay(500);
            cts.Cancel();
            foreach (var c in clients) c.Close();
            server.Close();

            Console.WriteLine();
            Console.WriteLine("  포트 번호가 다르니까 구분이 됩니다.");
            Console.WriteLine();
            Console.WriteLine("  우리 게임 서버도 이 방식을 씁니다:");
            Console.WriteLine("    TCP로 로그인할 때 EndPoint를 등록해두고");
            Console.WriteLine("    UDP 패킷이 오면 그 EndPoint로 누군지 찾습니다.");
        }

        // ═══════════════════════════════════════
        // 5. 여러 명이 한 소켓으로
        // ═══════════════════════════════════════
        static async Task MultipleClients()
        {
            Console.WriteLine("[소켓 하나로 여러 명 받기]");
            Console.WriteLine();
            Console.WriteLine("  TCP : 접속자 1명당 소켓 1개");
            Console.WriteLine("  UDP : 전체가 소켓 1개를 공유");
            Console.WriteLine();
            Console.WriteLine("  10명이 동시에 보내봅니다.");
            Console.WriteLine();

            Socket server = new Socket(AddressFamily.InterNetwork,
                                       SocketType.Dgram, ProtocolType.Udp);
            server.Bind(new IPEndPoint(IPAddress.Any, SERVER_PORT));

            var cts = new CancellationTokenSource();
            int count = 0;
            var senders = new HashSet<string>();

            _ = Task.Run(async () =>
            {
                byte[] buffer = new byte[1024];
                EndPoint from = new IPEndPoint(IPAddress.Any, 0);

                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var r = await server.ReceiveFromAsync(
                            new ArraySegment<byte>(buffer), SocketFlags.None, from);

                        Interlocked.Increment(ref count);
                        lock (senders) senders.Add(r.RemoteEndPoint.ToString()!);
                    }
                    catch { break; }
                }
            });

            await Task.Delay(300);

            var target = new IPEndPoint(IPAddress.Parse("127.0.0.1"), SERVER_PORT);
            var clients = new List<Socket>();

            for (int i = 1; i <= 10; i++)
            {
                Socket c = new Socket(AddressFamily.InterNetwork,
                                      SocketType.Dgram, ProtocolType.Udp);
                clients.Add(c);

                for (int n = 0; n < 5; n++)
                {
                    byte[] data = Encoding.UTF8.GetBytes($"P{i} 좌표{n}");
                    await c.SendToAsync(new ArraySegment<byte>(data),
                                        SocketFlags.None, target);
                }
            }

            await Task.Delay(1000);
            cts.Cancel();

            Console.WriteLine($"  받은 패킷 수   : {count}개");
            Console.WriteLine($"  보낸 사람 수   : {senders.Count}명");
            Console.WriteLine($"  서버 소켓 개수 : 1개");
            Console.WriteLine();

            foreach (var c in clients) c.Close();
            server.Close();

            Console.WriteLine("  소켓 하나로 10명을 전부 처리했습니다.");
            Console.WriteLine();
            Console.WriteLine("  장점 : 소켓 자원을 아낍니다");
            Console.WriteLine("  단점 : 누가 누군지 직접 관리해야 합니다");
        }

        // ═══════════════════════════════════════
        // 6. 코드 비교
        // ═══════════════════════════════════════
        static void CompareCode()
        {
            Console.WriteLine("[TCP와 UDP 코드 비교]");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ── TCP 서버 ──");
            Console.ResetColor();
            Console.WriteLine("  var s = new Socket(.., SocketType.Stream, ProtocolType.Tcp);");
            Console.WriteLine("  s.Bind(endPoint);");
            Console.WriteLine("  s.Listen(100);                    ← UDP에 없음");
            Console.WriteLine("  var client = await s.AcceptAsync(); ← UDP에 없음");
            Console.WriteLine("  await client.ReceiveAsync(buf);   ← 그 사람 것만 옴");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ── UDP 서버 ──");
            Console.ResetColor();
            Console.WriteLine("  var s = new Socket(.., SocketType.Dgram, ProtocolType.Udp);");
            Console.WriteLine("  s.Bind(endPoint);");
            Console.WriteLine("  await s.ReceiveFromAsync(buf, .., ref from);  ← 전부 여기로");
            Console.WriteLine("                                   from = 보낸 사람 주소");
            Console.WriteLine();

            Console.WriteLine("  ── 보낼 때 ──");
            Console.WriteLine("  TCP : await socket.SendAsync(data);          주소 불필요");
            Console.WriteLine("  UDP : await socket.SendToAsync(data, .., to); 매번 주소 지정");
            Console.WriteLine();

            Console.WriteLine("  정리하면:");
            Console.WriteLine("    TCP는 전화 — 걸어두면 그 사람하고만 통화");
            Console.WriteLine("    UDP는 우편 — 보낼 때마다 주소를 적음");
        }

        static void PrintTitle()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║   예제 D — UDP 소켓 다뤄보기         ║");
            Console.WriteLine("║   동양게임즈 서버개발팀 교육자료      ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(" 서버와 클라이언트를 한 프로그램에서 같이 띄웁니다.");
            Console.WriteLine(" F5 한 번이면 바로 확인할 수 있습니다.");
        }
    }
}
