using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleArena
{
    /// <summary>
    /// 서버에서 받은 다른 플레이어 정보
    /// </summary>
    public class RemotePlayer
    {
        public int    PlayerId;
        public string Nickname;
        public int    X;
        public int    Y;
    }

    /// <summary>
    /// ═══════════════════════════════════════════════
    /// [5주차 실습] UDP 위치 동기화 클라이언트
    ///
    /// ★ 여러분이 채워야 할 파일입니다 ★
    ///
    /// TODO가 3개 있습니다.
    ///
    ///   TODO 5  UDP 소켓 만들기        ★
    ///   TODO 6  내 위치 보내기          ★★
    ///   TODO 7  최신 상태만 꺼내기      ★★
    ///
    /// ── 막히면 여기를 보세요 ──
    ///
    ///   PositionSync 프로젝트 (예제 E)
    ///   → GameClient 클래스가 거의 같은 일을 합니다
    ///
    /// ── 주고받는 것 ──
    ///
    ///   보내기 : MOVE|플레이어ID|시퀀스|X|Y
    ///   받기   : STATE|id,닉네임,x,y|id,닉네임,x,y|...
    /// ═══════════════════════════════════════════════
    /// </summary>
    public class UdpNetworkClient
    {
        private const string SERVER_IP   = "127.0.0.1";
        private const int    SERVER_PORT = 7778;

        private Socket _socket;
        private IPEndPoint _serverEndPoint;
        private bool _isRunning;

        private int _playerId;
        private int _seq;

        /// <summary>서버에서 받은 상태 (백그라운드 → 메인 쓰레드 전달용)</summary>
        private readonly ConcurrentQueue<string> _stateQueue = new ConcurrentQueue<string>();

        public bool IsRunning => _isRunning;

        // ═══════════════════════════════════════
        // 시작
        // ═══════════════════════════════════════
        public void Start(int playerId)
        {
            _playerId = playerId;

            try
            {
                // ─────────────────────────────────────────
                // TODO 5 — UDP 소켓을 만드세요                ★
                //
                //   힌트
                //   · 서버의 TODO 1과 똑같은 방식입니다
                //
                //   · ★ Bind도 해야 합니다
                //       _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                //
                //       포트 0 = "아무 빈 포트나 OS가 골라줘"
                //
                //       왜 필요한가:
                //       Bind 없이 ReceiveFrom을 먼저 호출하면
                //       Windows에서 에러가 나면서 수신이 죽습니다.
                //       → 보내는 건 되는데 받는 게 안 되는 증상
                //
                //   · _serverEndPoint 에 서버 주소를 담아두세요
                //     new IPEndPoint(IPAddress.Parse(SERVER_IP), SERVER_PORT)
                //
                //   주의
                //   · Connect()는 하지 않습니다
                //     Bind(내 포트 정하기)와 Connect(상대 지정)는 다른 겁니다
                //     UDP는 Connect라는 개념이 없습니다
                // ─────────────────────────────────────────

                //_socket = null;            // ← 이 두 줄을 지우고
                //_serverEndPoint = null;    //    직접 만드세요
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(IPAddress.Any, 0));

                _serverEndPoint = new IPEndPoint(IPAddress.Parse(SERVER_IP), SERVER_PORT);

                _isRunning = true;
                _ = ReceiveLoopAsync();

                Debug.Log($"[UDP] 시작 (PlayerId {playerId})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UDP] 시작 실패 : {ex.Message}");
                _isRunning = false;
            }
        }

        // ═══════════════════════════════════════
        // 내 위치 보내기 (30fps로 호출됨)
        // ═══════════════════════════════════════
        public async void SendPosition(int x, int y)
        {
            if (!_isRunning || _socket == null) return;

            try
            {
                _seq++;

                // ─────────────────────────────────────────
                // TODO 6 — MOVE 패킷을 만들어 보내세요          ★★
                //
                //   만들 문자열
                //     MOVE|플레이어ID|시퀀스|X|Y
                //
                //   예시
                //     MOVE|3|152|10|7
                //
                //   쓸 변수
                //     _playerId, _seq, x, y
                //
                //   순서
                //     1) 문자열 조립 ($"MOVE|{...}|{...}|{...}|{...}")
                //     2) Encoding.UTF8.GetBytes 로 byte[] 변환
                //     3) _socket.SendToAsync 로 전송
                //        - 받을 주소(_serverEndPoint)를 매번 지정합니다
                //        - TCP와 다른 점이에요
                //
                //   시퀀스 번호를 왜 보내나
                //     서버가 "이게 최신인지" 판단하는 데 씁니다
                //     (서버 TODO 3과 짝입니다)
                // ─────────────────────────────────────────

                string msg = $"MOVE|{_playerId}|{_seq}|{x}|{y}";
                byte[] data = Encoding.UTF8.GetBytes(msg);

                await _socket.SendToAsync(new ArraySegment<byte>(data),
                                          SocketFlags.None, _serverEndPoint);

                //await Task.CompletedTask;   // ← 구현했으면 이 줄은 지우세요
            }
            catch
            {
                // UDP는 실패해도 넘어갑니다.
                // 다음 프레임에 또 보낼 거니까요.
            }
        }

        // ═══════════════════════════════════════
        // 수신 루프 (이 부분은 완성되어 있습니다)
        // ═══════════════════════════════════════
        private async Task ReceiveLoopAsync()
        {
            byte[] buffer = new byte[2048];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            while (_isRunning)
            {
                try
                {
                    var result = await _socket.ReceiveFromAsync(
                        new ArraySegment<byte>(buffer), SocketFlags.None, from);

                    string msg = Encoding.UTF8.GetString(buffer, 0, result.ReceivedBytes);

                    // 큐에 넣어두고 메인 쓰레드에서 꺼냅니다.
                    // Unity는 다른 쓰레드에서 GameObject를 못 건드립니다.
                    _stateQueue.Enqueue(msg);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UDP] 수신 중단 : {ex.Message}");
                    break;
                }
            }
        }

        // ═══════════════════════════════════════
        // 메인 쓰레드에서 호출 — 최신 상태 꺼내기
        // ═══════════════════════════════════════

        /// <summary>
        /// 큐에 쌓인 것 중 가장 최신 것만 꺼냅니다.
        /// </summary>
        public bool TryGetLatestState(out List<RemotePlayer> players)
        {
            players = null;

            // ─────────────────────────────────────────
            // TODO 7 — 큐를 비우고 마지막 것만 남기세요      ★★
            //
            //   왜 이렇게 하나
            //   · 큐에 패킷이 3개 쌓여 있어도 마지막 것만 쓰면 됩니다
            //   · 위치는 최신 값만 의미가 있으니까요
            //   · 과거 위치를 처리해봐야 소용없습니다
            //
            //   · TCP와 반대되는 사고방식입니다
            //     TCP : 하나도 빠뜨리면 안 됨
            //     UDP : 오래된 건 버리는 게 맞음
            //
            //   힌트
            //     string latest = null;
            //     while (_stateQueue.TryDequeue(out string msg))
            //         latest = msg;          // 계속 덮어쓰기
            //
            //     if (latest == null) return false;
            //     if (!latest.StartsWith("STATE")) return false;
            //
            //     players = Parse(latest);
            //     return true;
            // ─────────────────────────────────────────

            //return false;   // ← 이 줄을 지우고 직접 구현하세요
            string latest = null;
            while (_stateQueue.TryDequeue(out string msg))
                latest = msg;

            if (latest == null) return false;
            if (!latest.StartsWith("STATE")) return false;

            players = Parse(latest);
            return true;
        }

        /// <summary>STATE|id,닉네임,x,y|... 파싱 (완성되어 있습니다)</summary>
        private List<RemotePlayer> Parse(string state)
        {
            var list = new List<RemotePlayer>();
            string[] parts = state.Split('|');

            for (int i = 1; i < parts.Length; i++)
            {
                string[] f = parts[i].Split(',');
                if (f.Length < 4) continue;

                if (!int.TryParse(f[0], out int id)) continue;
                if (!int.TryParse(f[2], out int x))  continue;
                if (!int.TryParse(f[3], out int y))  continue;

                list.Add(new RemotePlayer
                {
                    PlayerId = id,
                    Nickname = f[1],
                    X = x,
                    Y = y,
                });
            }

            return list;
        }

        // ═══════════════════════════════════════
        // 종료
        // ═══════════════════════════════════════
        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            try { _socket?.Close(); } catch { }

            Debug.Log("[UDP] 종료");
        }
    }
}
