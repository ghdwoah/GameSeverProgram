using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BattleArena
{
    /// <summary>
    /// 클라이언트 한 명과의 연결 담당
    ///
    /// 접속자 1명당 이 객체가 1개 만들어집니다.
    /// </summary>
    public class ClientSession
    {
        public int    PlayerId { get; private set; }
        public string Nickname { get; private set; } = "Unknown";

        // ── 위치 정보 (UDP로 갱신됨) ──
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>이 플레이어의 UDP 주소. 첫 MOVE 패킷을 받을 때 채워집니다.</summary>
        public EndPoint? UdpEndPoint { get; set; }

        /// <summary>마지막으로 적용한 시퀀스 번호</summary>
        public int LastSeq { get; set; }

        private readonly Socket _socket;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private bool _isConnected = true;
        private int _alive = 1;

        public ClientSession(Socket socket, int playerId)
        {
            _socket  = socket;
            PlayerId = playerId;
        }

        // ═══════════════════════════════════════
        // 수신 루프
        // ═══════════════════════════════════════
        public async Task RunAsync()
        {
            try
            {
                while (_isConnected)
                {
                    string? json = await PacketHelper.ReceiveAsync(_socket);
                    if (json == null) break;

                    await HandlePacketAsync(json);
                }
            }
            catch (Exception ex)
            {
                // 측정이 끝나면 클라이언트가 소켓을 닫는다.
                // 그때 나는 끊김은 서버 오류가 아니라서 통계 화면을 가리지 않는다.
                if (!IsClientGone(ex))
                    Console.WriteLine($"[에러] ID:{PlayerId}  {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        /// <summary>상대가 연결을 끊어서 수신이 취소된 경우</summary>
        private static bool IsClientGone(Exception ex)
        {
            if (ex is ObjectDisposedException)
                return true;

            if (ex is SocketException se)
            {
                return se.SocketErrorCode == SocketError.ConnectionReset
                    || se.SocketErrorCode == SocketError.ConnectionAborted
                    || se.SocketErrorCode == SocketError.OperationAborted
                    || se.SocketErrorCode == SocketError.Interrupted
                    || se.SocketErrorCode == SocketError.Shutdown;
            }

            return false;
        }

        // ═══════════════════════════════════════
        // 패킷 처리
        //
        // 한 연결의 패킷은 도착한 순서대로 처리합니다.
        // 잘못된 JSON 하나는 무시하고, 그 연결은 유지합니다.
        // ═══════════════════════════════════════
        private async Task HandlePacketAsync(string json)
        {
            try
            {
                dynamic? packet = JsonConvert.DeserializeObject(json);
                if (packet == null) return;

                int type = (int)packet.Type;

                switch (type)
                {
                    case PacketType.LOGIN:
                        await HandleLoginAsync(json);
                        break;

                    case PacketType.CHAT:
                        await HandleChatAsync(json);
                        break;

                    default:
                        Console.WriteLine($"[경고] 모르는 패킷 타입 : {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[경고] ID:{PlayerId} 패킷을 건너뜀 : {ex.Message}");
            }
        }

        private async Task HandleLoginAsync(string json)
        {
            LoginPacket? login = JsonConvert.DeserializeObject<LoginPacket>(json);
            if (login == null) return;

            Nickname = string.IsNullOrWhiteSpace(login.Nickname)
                ? $"Player{PlayerId}"
                : login.Nickname;

            await SendAsync(new WelcomePacket
            {
                PlayerId = PlayerId,
                Message  = $"{Nickname}님, 배틀아레나에 오신 것을 환영합니다!"
            });

            GameServer.Instance.Broadcast(new NoticePacket
            {
                Type    = PacketType.PLAYER_IN,
                Message = $"{Nickname}님이 입장했습니다."
            }, exceptPlayerId: PlayerId);
        }

        private async Task HandleChatAsync(string json)
        {
            ChatPacket? chat = JsonConvert.DeserializeObject<ChatPacket>(json);
            if (chat == null) return;

            // 받은 그대로 전체에게 중계합니다
            GameServer.Instance.Broadcast(new ChatEchoPacket
            {
                Nickname = Nickname,
                Message  = chat.Message
            });

            await Task.CompletedTask;
        }

        // ═══════════════════════════════════════
        // 전송
        // ═══════════════════════════════════════
        public async Task SendAsync(object packet)
        {
            if (!_isConnected) return;

            bool failed = false;
            await _sendLock.WaitAsync();
            try
            {
                if (!_isConnected) return;
                await PacketHelper.SendAsync(_socket, packet);
            }
            catch
            {
                failed = true;
            }
            finally
            {
                _sendLock.Release();
            }

            if (failed) Disconnect();
        }

        // ═══════════════════════════════════════
        // 종료
        // ═══════════════════════════════════════
        public void Disconnect()
        {
            if (Interlocked.Exchange(ref _alive, 0) != 1) return;
            _isConnected = false;

            if (Nickname != "Unknown")
            {
                GameServer.Instance.Broadcast(new NoticePacket
                {
                    Type    = PacketType.PLAYER_OUT,
                    Message = $"{Nickname}님이 퇴장했습니다."
                }, exceptPlayerId: PlayerId);
            }

            try
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            catch { }

            GameServer.Instance.RemoveSession(PlayerId);
        }
    }
}
