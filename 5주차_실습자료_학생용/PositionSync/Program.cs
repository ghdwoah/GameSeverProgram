using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PositionSync
{
    /// <summary>
    /// ═══════════════════════════════════════════════════
    /// 예제 E — 콘솔로 만드는 위치 동기화
    ///
    /// Unity에 붙이기 전에 콘솔로 먼저 만들어봅니다.
    /// 여기서 만든 구조를 그대로 Unity로 옮기면 됩니다.
    ///
    /// 서버가 하는 일:
    ///   1. UDP로 위치를 받는다
    ///   2. 30fps로 모두에게 뿌린다
    ///
    /// 클라이언트가 하는 일:
    ///   1. 방향키로 내 위치를 바꾼다
    ///   2. 30fps로 서버에 보낸다
    ///   3. 받은 위치로 다른 사람을 그린다
    /// ═══════════════════════════════════════════════════
    /// </summary>
    class Program
    {
        public const int PORT   = 9000;
        public const int WIDTH  = 40;
        public const int HEIGHT = 14;
        public const int TICK_MS = 33;     // 약 30fps

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            PrintTitle();

            Console.WriteLine();
            Console.WriteLine("──────────────────────────────────────────");
            Console.WriteLine(" 1. 올인원 데모  (서버 + 봇 + 나)  ← 추천");
            Console.WriteLine(" 2. 서버만 실행");
            Console.WriteLine(" 3. 클라이언트만 실행 (다른 콘솔에서)");
            Console.WriteLine("──────────────────────────────────────────");
            Console.Write(" 선택 > ");

            string? input = Console.ReadLine();

            switch (input)
            {
                case "1": await AllInOne();      break;
                case "2": await ServerOnly();    break;
                case "3": await ClientOnly();    break;
                default:  await AllInOne();      break;
            }
        }

        // ═══════════════════════════════════════
        // 1. 올인원 — 서버 + 봇 + 플레이어
        // ═══════════════════════════════════════
        static async Task AllInOne()
        {
            Console.Clear();
            Console.WriteLine("서버를 켭니다...");

            var server = new GameServer(quiet: true);   // 로그가 화면을 깨지 않게
            _ = server.RunAsync();
            await Task.Delay(300);

            // 자동으로 움직이는 봇 (다른 플레이어 역할)
            var bot = new BotClient("봇");
            _ = bot.RunAsync();
            await Task.Delay(200);

            // 내가 조종하는 클라이언트
            var me = new GameClient("나");
            await me.RunAsync();
        }

        static async Task ServerOnly()
        {
            Console.Clear();
            var server = new GameServer();
            await server.RunAsync();
        }

        static async Task ClientOnly()
        {
            Console.Write(" 닉네임 > ");
            string name = Console.ReadLine() ?? "플레이어";
            Console.Clear();

            var client = new GameClient(name);
            await client.RunAsync();
        }

        static void PrintTitle()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║   예제 E — 위치 동기화 (콘솔판)      ║");
            Console.WriteLine("║   동양게임즈 서버개발팀 교육자료      ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(" 방향키로 캐릭터를 움직입니다.");
            Console.WriteLine(" 다른 캐릭터도 같이 움직이는 걸 확인하세요.");
        }
    }

    // ═══════════════════════════════════════════
    // 플레이어 정보
    // ═══════════════════════════════════════════
    public class PlayerInfo
    {
        public int    Id       { get; set; }
        public string Nickname { get; set; } = "";
        public int    X        { get; set; }
        public int    Y        { get; set; }
        public int    LastSeq  { get; set; }       // 마지막으로 받은 시퀀스
        public DateTime LastSeen { get; set; }     // 마지막 패킷 시각
        public EndPoint? EndPoint { get; set; }
    }

    // ═══════════════════════════════════════════
    // 서버
    // ═══════════════════════════════════════════
    public class GameServer
    {
        /// <summary>
        /// 로그를 찍지 않는 모드
        ///
        /// 올인원 데모에서는 서버 로그가 끼어들면
        /// 화면(맵)이 깨집니다. 그래서 조용히 돌립니다.
        /// </summary>
        private readonly bool _quiet;

        public GameServer(bool quiet = false) { _quiet = quiet; }

        private void Log(string msg)
        {
            if (!_quiet) Console.WriteLine(msg);
        }

        private Socket _socket = null!;
        private readonly ConcurrentDictionary<string, PlayerInfo> _players = new();
        private int _nextId = 0;

        public async Task RunAsync()
        {
            _socket = new Socket(AddressFamily.InterNetwork,
                                 SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, Program.PORT));

            Log($"[서버] UDP 포트 {Program.PORT} 대기 중");

            // 수신 루프와 브로드캐스트 루프를 따로 돌립니다
            _ = ReceiveLoopAsync();
            await BroadcastLoopAsync();
        }

        // ── 수신 루프 ──
        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[1024];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    var r = await _socket.ReceiveFromAsync(
                        new ArraySegment<byte>(buffer), SocketFlags.None, from);

                    string json = Encoding.UTF8.GetString(buffer, 0, r.ReceivedBytes);
                    HandlePacket(json, r.RemoteEndPoint);
                }
                catch { break; }
            }
        }

        // ── 패킷 처리 ──
        // 형식 : MOVE|닉네임|시퀀스|X|Y
        private void HandlePacket(string json, EndPoint from)
        {
            string[] parts = json.Split('|');
            if (parts.Length < 5 || parts[0] != "MOVE") return;

            string key  = from.ToString()!;
            string nick = parts[1];
            if (!int.TryParse(parts[2], out int seq)) return;
            if (!int.TryParse(parts[3], out int x))   return;
            if (!int.TryParse(parts[4], out int y))   return;

            // 처음 보는 주소면 새 플레이어로 등록
            if (!_players.TryGetValue(key, out PlayerInfo? p))
            {
                p = new PlayerInfo
                {
                    Id       = Interlocked.Increment(ref _nextId),
                    Nickname = nick,
                    EndPoint = from,
                    LastSeen = DateTime.UtcNow,
                };
                _players[key] = p;
                Log($"[서버] 새 플레이어 : {nick} ({key})");
            }

            // ★ 오래된 패킷은 버립니다
            //   UDP는 순서를 보장하지 않으므로 반드시 필요합니다
            if (seq <= p.LastSeq) return;

            p.LastSeq  = seq;
            p.X        = x;
            p.Y        = y;
            p.LastSeen = DateTime.UtcNow;
        }

        // ── 브로드캐스트 루프 (30fps) ──
        private async Task BroadcastLoopAsync()
        {
            while (true)
            {
                await Task.Delay(Program.TICK_MS);

                RemoveTimedOut();
                if (_players.IsEmpty) continue;

                // 전체 플레이어 상태를 한 패킷에 담습니다
                // 형식 : STATE|id,닉네임,x,y|id,닉네임,x,y|...
                var sb = new StringBuilder("STATE");
                foreach (var p in _players.Values)
                    sb.Append($"|{p.Id},{p.Nickname},{p.X},{p.Y}");

                byte[] data = Encoding.UTF8.GetBytes(sb.ToString());

                foreach (var p in _players.Values)
                {
                    if (p.EndPoint == null) continue;
                    try
                    {
                        await _socket.SendToAsync(new ArraySegment<byte>(data),
                                                  SocketFlags.None, p.EndPoint);
                    }
                    catch { }
                }
            }
        }

        // ── 5초간 소식 없으면 제거 ──
        //   UDP는 '접속 끊김'을 알려주지 않습니다
        //   그래서 타임아웃으로 직접 판단해야 합니다
        private void RemoveTimedOut()
        {
            var now = DateTime.UtcNow;
            foreach (var pair in _players)
            {
                if ((now - pair.Value.LastSeen).TotalSeconds > 5)
                {
                    _players.TryRemove(pair.Key, out _);
                    Log($"[서버] 타임아웃 : {pair.Value.Nickname}");
                }
            }
        }
    }

    // ═══════════════════════════════════════════
    // 클라이언트 (방향키 조작)
    // ═══════════════════════════════════════════
    public class GameClient
    {
        private readonly string _nickname;
        private Socket _socket = null!;
        private IPEndPoint _server = null!;

        private int _x = Program.WIDTH / 2;
        private int _y = Program.HEIGHT / 2;
        private int _seq = 0;

        // 서버에서 받은 전체 플레이어 목록
        private volatile string _lastState = "";

        /// <summary>
        /// 렌더링 간격 조절용
        ///
        /// 전송은 30fps로 하되, 화면 그리기는 10fps면 충분합니다.
        /// 콘솔은 전체를 다시 그리므로 너무 자주 하면 깜빡입니다.
        /// </summary>
        private int _renderTick = 0;

        /// <summary>수신 에러를 한 번만 보여주기 위한 플래그</summary>
        private string _error = "";

        public GameClient(string nickname) { _nickname = nickname; }

        public async Task RunAsync()
        {
            _socket = new Socket(AddressFamily.InterNetwork,
                                 SocketType.Dgram, ProtocolType.Udp);

            // ★ 중요 : 받으려면 Bind가 필요합니다
            //
            //   포트 0 = "아무 빈 포트나 OS가 골라줘"
            //
            //   Bind 없이 ReceiveFrom을 먼저 호출하면
            //   Windows에서 SocketException(10022) 이 납니다.
            //   SendTo를 먼저 하면 OS가 알아서 포트를 잡아주지만,
            //   수신 루프를 먼저 시작하는 구조라면 반드시 Bind해야 합니다.
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            _server = new IPEndPoint(IPAddress.Parse("127.0.0.1"), Program.PORT);

            _ = ReceiveLoopAsync();

            Console.CursorVisible = false;
            Console.Clear();

            while (true)
            {
                HandleInput();
                await SendPositionAsync();

                // 전송은 30fps, 화면은 10fps
                _renderTick++;
                if (_renderTick >= 3)
                {
                    Render();
                    _renderTick = 0;
                }

                await Task.Delay(Program.TICK_MS);
            }
        }

        // ── 입력 ──
        private void HandleInput()
        {
            while (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:    _y--; break;
                    case ConsoleKey.DownArrow:  _y++; break;
                    case ConsoleKey.LeftArrow:  _x--; break;
                    case ConsoleKey.RightArrow: _x++; break;
                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        Environment.Exit(0);
                        break;
                }
            }

            _x = Math.Clamp(_x, 0, Program.WIDTH - 1);
            _y = Math.Clamp(_y, 0, Program.HEIGHT - 1);
        }

        // ── 위치 전송 ──
        private async Task SendPositionAsync()
        {
            _seq++;
            string msg = $"MOVE|{_nickname}|{_seq}|{_x}|{_y}";
            byte[] data = Encoding.UTF8.GetBytes(msg);

            try
            {
                await _socket.SendToAsync(new ArraySegment<byte>(data),
                                          SocketFlags.None, _server);
            }
            catch { }
        }

        // ── 수신 ──
        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[2048];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    var r = await _socket.ReceiveFromAsync(
                        new ArraySegment<byte>(buffer), SocketFlags.None, from);

                    _lastState = Encoding.UTF8.GetString(buffer, 0, r.ReceivedBytes);
                }
                catch (Exception ex)
                {
                    // 조용히 죽으면 원인을 못 찾습니다.
                    // 화면 아래에 표시해둡니다.
                    _error = ex.Message;
                    break;
                }
            }
        }

        // ── 화면 그리기 ──
        private void Render()
        {
            var map = new char[Program.HEIGHT, Program.WIDTH];
            for (int y = 0; y < Program.HEIGHT; y++)
                for (int x = 0; x < Program.WIDTH; x++)
                    map[y, x] = ' ';

            int playerCount = 0;
            var names = new StringBuilder();

            // 서버가 보내준 전체 상태를 파싱
            string state = _lastState;
            if (state.StartsWith("STATE"))
            {
                string[] parts = state.Split('|');
                for (int i = 1; i < parts.Length; i++)
                {
                    string[] f = parts[i].Split(',');
                    if (f.Length < 4) continue;

                    string nick = f[1];
                    if (!int.TryParse(f[2], out int px)) continue;
                    if (!int.TryParse(f[3], out int py)) continue;

                    px = Math.Clamp(px, 0, Program.WIDTH - 1);
                    py = Math.Clamp(py, 0, Program.HEIGHT - 1);

                    // 내 캐릭터는 O, 남은 X
                    map[py, px] = (nick == _nickname) ? 'O' : 'X';

                    playerCount++;
                    names.Append($"{nick}({px},{py})  ");
                }
            }

            Console.SetCursorPosition(0, 0);

            Console.WriteLine($"  위치 동기화 데모   나: {_nickname}   접속자: {playerCount}명        ");
            Console.WriteLine($"  방향키 이동 · ESC 종료   전송 seq: {_seq,5}                        ");
            Console.WriteLine();

            Console.WriteLine("  ┌" + new string('─', Program.WIDTH) + "┐");
            for (int y = 0; y < Program.HEIGHT; y++)
            {
                Console.Write("  │");
                for (int x = 0; x < Program.WIDTH; x++)
                {
                    char c = map[y, x];
                    if (c == 'O')
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write('O');
                        Console.ResetColor();
                    }
                    else if (c == 'X')
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write('X');
                        Console.ResetColor();
                    }
                    else Console.Write(' ');
                }
                Console.WriteLine("│");
            }
            Console.WriteLine("  └" + new string('─', Program.WIDTH) + "┘");

            Console.WriteLine();
            Console.WriteLine("  O = 나(초록)   X = 다른 플레이어(빨강)                    ");
            Console.WriteLine($"  {names}                                                  ");

            if (_error.Length > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [수신 중단] {_error}                                  ");
                Console.ResetColor();
            }
            else
            {
                Console.WriteLine("                                                        ");
            }
        }
    }

    // ═══════════════════════════════════════════
    // 봇 — 자동으로 돌아다니는 가짜 플레이어
    // ═══════════════════════════════════════════
    public class BotClient
    {
        private readonly string _nickname;
        private Socket _socket = null!;
        private IPEndPoint _server = null!;
        private int _seq = 0;
        private double _angle = 0;

        public BotClient(string nickname) { _nickname = nickname; }

        public async Task RunAsync()
        {
            _socket = new Socket(AddressFamily.InterNetwork,
                                 SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            _server = new IPEndPoint(IPAddress.Parse("127.0.0.1"), Program.PORT);

            while (true)
            {
                // 원을 그리며 이동
                _angle += 0.06;
                int x = (int)(Program.WIDTH  / 2 + Math.Cos(_angle) * 14);
                int y = (int)(Program.HEIGHT / 2 + Math.Sin(_angle) * 5);

                x = Math.Clamp(x, 0, Program.WIDTH - 1);
                y = Math.Clamp(y, 0, Program.HEIGHT - 1);

                _seq++;
                string msg = $"MOVE|{_nickname}|{_seq}|{x}|{y}";
                byte[] data = Encoding.UTF8.GetBytes(msg);

                try
                {
                    await _socket.SendToAsync(new ArraySegment<byte>(data),
                                              SocketFlags.None, _server);
                }
                catch { }

                await Task.Delay(Program.TICK_MS);
            }
        }
    }
}
