using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BattleArena
{
    /// <summary>
    /// TCP 패킷 송수신 담당
    ///
    /// 패킷 형식 : [ 본문길이(4바이트) ][ JSON 본문 ]
    ///
    /// 길이를 앞에 붙이는 이유는 TCP가 경계를 보장하지 않기 때문입니다.
    /// </summary>
    public static class PacketHelper
    {
        /// <summary>한 패킷의 본문 최대 크기</summary>
        private const int MAX_BODY = 8192;

        // ═══════════════════════════════════════
        // 전송
        // ═══════════════════════════════════════
        public static async Task SendAsync(Socket socket, object packet)
        {
            string json = JsonConvert.SerializeObject(packet);
            byte[] body = Encoding.UTF8.GetBytes(json);

            byte[] full = new byte[4 + body.Length];
            BitConverter.GetBytes(body.Length).CopyTo(full, 0);
            body.CopyTo(full, 4);

            int sent = 0;
            while (sent < full.Length)
            {
                sent += await socket.SendAsync(
                    new ArraySegment<byte>(full, sent, full.Length - sent),
                    SocketFlags.None);
            }
        }

        // ═══════════════════════════════════════
        // 수신
        // ═══════════════════════════════════════
        public static async Task<string?> ReceiveAsync(Socket socket)
        {
            byte[] header = new byte[4];

            // TCP는 한 번의 Receive가 요청한 바이트를 다 준다는 보장이 없습니다.
            // 기다리는 동안 스레드를 붙잡아 두지 않도록 ReceiveAsync로 채웁니다.
            if (!await ReadExactAsync(socket, header, 4)) return null;

            int bodyLength = BitConverter.ToInt32(header, 0);

            // 말도 안 되는 길이가 오면 끊습니다 (악성 패킷 방어)
            if (bodyLength <= 0 || bodyLength > MAX_BODY) return null;

            byte[] body = new byte[bodyLength];

            if (!await ReadExactAsync(socket, body, bodyLength)) return null;

            return Encoding.UTF8.GetString(body, 0, bodyLength);
        }

        /// <summary>요청한 바이트 수를 다 받을 때까지 반복해서 읽습니다.</summary>
        private static async Task<bool> ReadExactAsync(Socket socket, byte[] buffer, int size)
        {
            int read = 0;

            while (read < size)
            {
                int n = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer, read, size - read),
                    SocketFlags.None);

                if (n == 0) return false;
                read += n;
            }

            return true;
        }
    }
}
