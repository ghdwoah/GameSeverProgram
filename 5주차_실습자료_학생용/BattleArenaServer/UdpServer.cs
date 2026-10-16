using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace BattleArenaServer
{
    /// <summary>
    /// ═══════════════════════════════════════════════
    /// [5주차 실습] UDP 위치 동기화 서버
    ///
    /// ★ 여러분이 채워야 할 파일입니다 ★
    ///
    /// TODO가 4개 있습니다. 순서대로 채우세요.
    ///
    ///   TODO 1  UDP 소켓 만들기          ★
    ///   TODO 2  패킷 받기                ★★
    ///   TODO 3  오래된 패킷 버리기        ★★
    ///   TODO 4  30fps 브로드캐스트        ★★★
    ///
    /// ── 막히면 여기를 보세요 ──
    ///
    ///   PositionSync 프로젝트 (예제 E)
    ///   → GameServer 클래스가 거의 같은 일을 합니다
    ///   → ReceiveLoopAsync, HandlePacket, BroadcastLoopAsync 참고
    ///
    /// ── 포트 구성 ──
    ///
    ///   TCP 7777 — 로그인, 채팅 (이미 동작함)
    ///   UDP 7778 — 위치        (지금 만드는 것)
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class UdpServer
    {
        public const int UDP_PORT = 7778;

        /// <summary>초당 몇 번 브로드캐스트할지 (30fps)</summary>
        private const int TICK_MS = 33;

        private Socket _socket = null!;

        // ═══════════════════════════════════════
        // 시작
        // ═══════════════════════════════════════
        public async Task StartAsync()
        {
            // ─────────────────────────────────────────
            // TODO 1 — UDP 소켓을 만들고 포트에 묶으세요   ★
            //
            //   힌트
            //   · new Socket(AddressFamily.InterNetwork, ?, ?)
            //   · TCP는 SocketType.Stream / ProtocolType.Tcp 였습니다
            //   · UDP는 뭘 쓸까요? (예제 D 1번 메뉴 참고)
            //   · Bind는 TCP와 똑같습니다
            //
            //   주의
            //   · Listen()과 Accept()는 쓰지 않습니다
            //     UDP는 '연결'이라는 개념이 없기 때문입니다
            // ─────────────────────────────────────────

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, UDP_PORT));   // ← 이 줄을 지우고 직접 만드세요



            Console.WriteLine($"[UDP] 포트 {UDP_PORT}번에서 위치 패킷을 기다립니다");
            Console.WriteLine();

            // 수신 루프와 브로드캐스트 루프를 따로 돌립니다
            // (await를 안 붙인 이유는 4주차 '던져놓기 패턴'과 같습니다)
            _ = ReceiveLoopAsync();
            await BroadcastLoopAsync();
        }

        // ═══════════════════════════════════════
        // 수신 루프 — 모든 플레이어의 위치가 여기로 들어옵니다
        // ═══════════════════════════════════════
        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[1024];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    // ─────────────────────────────────────────
                    // TODO 2 — 패킷을 받으세요                    ★★
                    //
                    //   힌트
                    //   · TCP는 ReceiveAsync 였습니다
                    //   · UDP는 "누가 보냈는지"도 같이 받아야 합니다
                    //     → ReceiveFromAsync 를 쓰세요
                    //
                    //   · 받은 뒤에 할 일
                    //     1) byte[] 를 문자열로 바꾸기 (Encoding.UTF8.GetString)
                    //     2) HandlePacket(문자열, 보낸사람주소) 호출
                    //
                    //   · 반환값에서 꺼낼 것
                    //     result.ReceivedBytes    받은 바이트 수
                    //     result.RemoteEndPoint   보낸 사람 주소
                    // ─────────────────────────────────────────

                    //await Task.Delay(100);   // ← 이 줄을 지우고 직접 구현하세요
                    var r = await _socket.ReceiveFromAsync(
                        new ArraySegment<byte>(buffer), SocketFlags.None, from);

                    string json = Encoding.UTF8.GetString(buffer, 0, r.ReceivedBytes);
                    HandlePacket(json, r.RemoteEndPoint);


                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UDP 수신 에러] {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════
        // 패킷 처리
        //
        // 형식 : MOVE|플레이어ID|시퀀스|X|Y
        // 예시 : MOVE|3|152|10|7
        // ═══════════════════════════════════════
        private void HandlePacket(string msg, EndPoint from)
        {
            // ── 파싱 (이 부분은 완성되어 있습니다) ──
            string[] parts = msg.Split('|');
            if (parts.Length < 5) return;
            if (parts[0] != UdpPacket.MOVE) return;

            if (!int.TryParse(parts[1], out int playerId)) return;
            if (!int.TryParse(parts[2], out int seq))      return;
            if (!int.TryParse(parts[3], out int x))        return;
            if (!int.TryParse(parts[4], out int y))        return;

            // TCP로 로그인한 세션을 찾습니다
            ClientSession? session = GameServer.Instance.FindSession(playerId);
            if (session == null) return;      // 로그인 안 한 사람은 무시

            // ── 첫 UDP 패킷이면 주소를 기억해둡니다 ──
            //   앞으로 이 주소로 위치를 보내줄 겁니다
            if (session.UdpEndPoint == null)
            {
                session.UdpEndPoint = from;
                Console.WriteLine($"[UDP] {session.Nickname} 주소 등록 : {from}");
            }

            // ─────────────────────────────────────────
            // TODO 3 — 오래된 패킷을 버리세요               ★★
            //
            //   왜 필요한가
            //   · UDP는 순서를 보장하지 않습니다
            //   · 6번이 5번보다 먼저 올 수 있어요
            //   · 그때 5번을 적용하면 캐릭터가 뒤로 갑니다
            //
            //   힌트
            //   · session.LastSeq 에 마지막 번호가 들어 있습니다
            //   · 지금 받은 seq 가 그것보다 작거나 같으면?
            //     → 오래된 패킷이므로 return 으로 버리세요
            //
            //   확인
            //   · 예제 C 6번 메뉴에서 이 동작을 볼 수 있습니다
            // ─────────────────────────────────────────

            // ← 여기에 한 줄 추가하세요
            if (seq <= session.LastSeq) return;


            // 위치 갱신 (이 부분은 완성되어 있습니다)
            session.LastSeq = seq;
            session.X = x;
            session.Y = y;
        }

        // ═══════════════════════════════════════
        // 브로드캐스트 루프 — 30fps로 전체 위치를 뿌립니다
        // ═══════════════════════════════════════
        private async Task BroadcastLoopAsync()
        {
            while (true)
            {
                await Task.Delay(TICK_MS);

                var sessions = GameServer.Instance.GetSessions();
                if (sessions.Count == 0) continue;

                // ─────────────────────────────────────────
                // TODO 4 — 전체 위치를 모아서 모두에게 보내세요   ★★★
                //
                //   만들 패킷 형식
                //     STATE|id,닉네임,x,y|id,닉네임,x,y|...
                //
                //   예시
                //     STATE|1,홍길동,10,7|3,철수,22,4
                //
                //   순서
                //     1) StringBuilder 로 STATE 문자열을 조립
                //        - foreach 로 sessions 를 돌면서
                //        - s.UdpEndPoint 가 null 인 사람은 건너뛰기
                //          (아직 UDP 패킷을 한 번도 안 보낸 사람)
                //        - $"|{s.PlayerId},{s.Nickname},{s.X},{s.Y}" 를 붙이기
                //
                //     2) 보낼 사람이 없으면 continue
                //        (조립 결과가 "STATE" 그대로면 아무도 없는 것)
                //
                //     3) 문자열을 byte[] 로 변환
                //
                //     4) foreach 로 모두에게 SendToAsync
                //        - UdpEndPoint 가 null 인 사람은 건너뛰기
                //        - try-catch 로 감싸기 (UDP는 실패해도 그냥 넘어감)
                //
                //   왜 한 패킷에 다 담나
                //     사람마다 따로 보내면 패킷 수가 제곱으로 늘어납니다
                //     100명이면 9,900개 vs 100개
                //
                //   참고
                //     PositionSync 의 BroadcastLoopAsync 가 거의 같습니다
                // ─────────────────────────────────────────
                var sb = new StringBuilder("STATE");
                foreach (var s in sessions)
                    sb.Append($"|{s.PlayerId},{s.Nickname},{s.X},{s.Y}");

                byte[] data = Encoding.UTF8.GetBytes(sb.ToString());

                foreach (var p in sessions)
                {
                    if (p.UdpEndPoint == null) continue;
                    try
                    {
                        await _socket.SendToAsync(new ArraySegment<byte>(data),
                                                  SocketFlags.None, p.UdpEndPoint);
                    }
                    catch { }
                }



            }
        }
    }
}
