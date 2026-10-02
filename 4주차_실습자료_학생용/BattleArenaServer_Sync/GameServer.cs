using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace BattleArenaServer
{
    /// <summary>
    /// 배틀아레나 게임 서버 — 동기 버전
    ///
    /// ═══════════════════════════════════════════════
    /// [4주차 — TICKET-007]
    /// "유저 100명이 동시에 못 붙습니다"
    ///
    /// 이 서버는 한 번에 한 명만 처리할 수 있습니다.
    ///
    /// 왜 그럴까요?
    ///   1. Accept 로 손님을 받고
    ///   2. session.Run() 을 호출하면
    ///   3. 그 손님이 나갈 때까지 Run() 이 안 끝납니다
    ///   4. 그래서 다음 Accept 로 못 갑니다
    ///
    /// 직접 확인해보세요:
    ///   클라이언트 1개 → 잘 붙음
    ///   클라이언트 2개 → 두 번째가 안 붙음
    ///
    /// 여러분이 할 일:
    ///   이 코드를 비동기로 바꿔서 여러 명이 붙게 만들기
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class GameServer
    {
        public static GameServer Instance { get; private set; } = null!;

        private const int PORT = 7777;

        private Socket? _listenSocket;
        private int _nextPlayerId = 0;

        private readonly ConcurrentDictionary<int, ClientSession> _sessions = new();

        public GameServer()
        {
            Instance = this;
        }

        // ═══════════════════════════════════════
        // 서버 시작
        // ═══════════════════════════════════════
        public async Task Start()
        {
            PrintBanner();

            // 1. 소켓 만들기
            _listenSocket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp);

            // 2. Bind
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, PORT);
            _listenSocket.Bind(endPoint);

            // 3. Listen
            _listenSocket.Listen(100);

            Console.WriteLine($"[서버] 포트 {PORT}번에서 접속을 기다립니다...");
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("※ 이 서버는 동기 버전입니다.");
            Console.WriteLine("   한 번에 한 명만 처리할 수 있습니다.");
            Console.WriteLine("   클라이언트를 2개 띄워서 확인해보세요.");
            Console.ResetColor();
            Console.WriteLine();

            // 4. Accept 반복
            while (true)
            {
                // ★ 동기 Accept — 접속이 올 때까지 멈춤
                Socket clientSocket = await _listenSocket.AcceptAsync();

                int playerId = Interlocked.Increment(ref _nextPlayerId);

                ClientSession session = new ClientSession(clientSocket, playerId);
                _sessions.TryAdd(playerId, session);

                Console.WriteLine($"[현재 접속자] {_sessions.Count}명");

                // ★★★ 여기가 문제입니다 ★★★
                //
                // session.Run() 은 이 클라이언트가 나갈 때까지 안 끝납니다.
                // 그래서 while 루프가 다음 Accept 로 돌아가지 못합니다.
                //
                // 결과: 두 번째 손님은 영원히 대기
                //
                _= session.RunAsync();

                // ↑ 이 줄이 끝나야 아래로 내려옵니다
                Console.WriteLine($"[세션 종료] ID:{playerId}");
            }
        }

        // ═══════════════════════════════════════
        // 세션 관리
        // ═══════════════════════════════════════
        public void RemoveSession(int playerId)
        {
            _sessions.TryRemove(playerId, out _);
            Console.WriteLine($"[현재 접속자] {_sessions.Count}명");
        }

        // ═══════════════════════════════════════
        // 브로드캐스트
        // ═══════════════════════════════════════
        public async Task Broadcast(object packet, int exceptPlayerId = -1)
        {
            foreach (var pair in _sessions)
            {
                if (pair.Key == exceptPlayerId) continue;
                await pair.Value.SendAsync(packet);
            }
        }

        // ═══════════════════════════════════════
        private void PrintBanner()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("╔════════════════════════════════════╗");
            Console.WriteLine("║   배틀아레나 서버 [동기 버전]        ║");
            Console.WriteLine("║   TICKET-007  장애 재현용            ║");
            Console.WriteLine("╚════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ═══════════════════════════════════════
        public static async Task Main()
        {
            GameServer server = new GameServer();

            try
            {
                await server.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[서버 에러] {ex.Message}");
                Console.WriteLine("아무 키나 누르면 종료합니다.");
                Console.ReadKey();
            }
        }
    }
}
