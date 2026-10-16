using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TcpVsUdp
{
    /// <summary>
    /// ═══════════════════════════════════════════════════
    /// 예제 C — TCP와 UDP, 뭐가 다른가
    ///
    /// 네트워크가 완벽하면 둘 다 잘 됩니다.
    /// 문제는 패킷이 유실될 때 벌어집니다.
    ///
    /// 실제 loopback(127.0.0.1)은 유실이 거의 없어서
    /// 차이를 볼 수 없습니다.
    /// 그래서 이 예제는 유실을 흉내내서 보여줍니다.
    /// ═══════════════════════════════════════════════════
    /// </summary>
    class Program
    {
        // 맵 크기 (ASCII 표시용)
        const int MAP_WIDTH = 40;

        static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            PrintTitle();

            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("──────────────────────────────────────────");
                Console.WriteLine(" 1. 유실이 없을 때  (TCP · UDP 둘 다 정상)");
                Console.WriteLine(" 2. ★ 유실이 생겼을 때 — TCP");
                Console.WriteLine(" 3. ★ 유실이 생겼을 때 — UDP");
                Console.WriteLine(" 4. ★ 나란히 비교하기");
                Console.WriteLine(" 5. 유실률 바꿔서 실험하기");
                Console.WriteLine(" 6. 늦게 온 패킷 버리기 (시퀀스 번호)");
                Console.WriteLine(" 0. 종료");
                Console.WriteLine("──────────────────────────────────────────");
                Console.Write(" 선택 > ");

                string? input = Console.ReadLine();
                Console.WriteLine();

                switch (input)
                {
                    case "1": await NoLoss();            break;
                    case "2": await TcpWithLoss();       break;
                    case "3": await UdpWithLoss();       break;
                    case "4": await SideBySide();        break;
                    case "5": await CustomLossRate();    break;
                    case "6": await SequenceNumber();    break;
                    case "0": return;
                    default:  Console.WriteLine("1~6 중에 골라주세요."); break;
                }
            }
        }

        // ═══════════════════════════════════════
        // 1. 유실이 없을 때
        // ═══════════════════════════════════════
        static async Task NoLoss()
        {
            Console.WriteLine("[유실 없음] 캐릭터가 오른쪽으로 이동합니다");
            Console.WriteLine();

            for (int x = 0; x < 30; x++)
            {
                DrawCharacter(x, $"좌표 {x,2} 수신");
                await Task.Delay(60);
            }

            Console.WriteLine();
            Console.WriteLine("부드럽게 움직였습니다.");
            Console.WriteLine("네트워크가 완벽하면 TCP든 UDP든 차이가 없습니다.");
        }

        // ═══════════════════════════════════════
        // 2. TCP + 유실
        //
        // TCP는 순서를 보장하므로
        // 유실된 패킷을 재전송받을 때까지
        // 그 뒤에 도착한 패킷들을 넘겨주지 않습니다.
        // ═══════════════════════════════════════
        static async Task TcpWithLoss()
        {
            Console.WriteLine("[TCP] 10번 좌표가 유실되는 상황");
            Console.WriteLine();

            for (int x = 0; x < 30; x++)
            {
                if (x == 10)
                {
                    // 유실 발생 → 재전송 대기
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine();
                    Console.WriteLine("  ✕ 10번 패킷 유실!");
                    Console.WriteLine("    TCP는 순서를 보장해야 하므로");
                    Console.WriteLine("    11, 12, 13번이 도착해도 넘겨주지 않습니다.");
                    Console.WriteLine("    재전송을 기다립니다...");
                    Console.ResetColor();

                    // 재전송 대기 (실제로는 수백 ms)
                    for (int wait = 0; wait < 8; wait++)
                    {
                        DrawCharacter(9, $"화면 멈춤... ({(wait + 1) * 60}ms)", ConsoleColor.Red);
                        await Task.Delay(60);
                    }

                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine();
                    Console.WriteLine("  ↻ 재전송 도착. 밀려 있던 패킷을 한꺼번에 처리합니다.");
                    Console.ResetColor();

                    // 밀린 것들이 한꺼번에 몰림 → 순간이동
                    for (int catchUp = 10; catchUp <= 13; catchUp++)
                    {
                        DrawCharacter(catchUp, $"좌표 {catchUp,2} (밀린 것 처리)", ConsoleColor.Yellow);
                        await Task.Delay(15);
                    }
                    x = 13;
                    continue;
                }

                DrawCharacter(x, $"좌표 {x,2} 수신");
                await Task.Delay(60);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("문제: 0.5초간 캐릭터가 멈췄다가 순간이동했습니다.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("이것을 head-of-line blocking 이라고 합니다.");
            Console.WriteLine("앞사람이 막히면 뒷사람도 다 막히는 현상입니다.");
        }

        // ═══════════════════════════════════════
        // 3. UDP + 유실
        //
        // UDP는 순서도 도착도 보장하지 않습니다.
        // 유실된 건 그냥 버리고 다음 것을 씁니다.
        // ═══════════════════════════════════════
        static async Task UdpWithLoss()
        {
            Console.WriteLine("[UDP] 똑같이 10번 좌표가 유실되는 상황");
            Console.WriteLine();

            for (int x = 0; x < 30; x++)
            {
                if (x == 10)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine();
                    Console.WriteLine("  ✕ 10번 패킷 유실 (그냥 버림)");
                    Console.ResetColor();
                    continue;   // 재전송 없음. 바로 다음으로
                }

                DrawCharacter(x, $"좌표 {x,2} 수신");
                await Task.Delay(60);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("10번 좌표 하나만 빠졌을 뿐, 멈춤은 없었습니다.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("위치는 '최신 값'만 중요합니다.");
            Console.WriteLine("10번을 놓쳐도 11번이 바로 오면 아무도 모릅니다.");
        }

        // ═══════════════════════════════════════
        // 4. 나란히 비교
        // ═══════════════════════════════════════
        static async Task SideBySide()
        {
            Console.WriteLine("[비교] 같은 상황에서 TCP와 UDP가 어떻게 다른가");
            Console.WriteLine();
            Console.WriteLine("  TCP ↓");
            Console.WriteLine("  UDP ↓");
            Console.WriteLine();

            int tcpX = 0;
            int udpX = 0;
            int tcpStall = 0;

            for (int tick = 0; tick < 30; tick++)
            {
                // 10틱에서 유실 발생
                if (tick == 10) tcpStall = 8;

                // TCP는 재전송 대기 중이면 멈춤
                if (tcpStall > 0) tcpStall--;
                else tcpX = Math.Min(tick, MAP_WIDTH - 1);

                // UDP는 유실돼도 계속 진행
                udpX = Math.Min(tick, MAP_WIDTH - 1);

                DrawTwoLines(tcpX, udpX, tick, tcpStall > 0);
                await Task.Delay(100);
            }

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("같은 유실인데 결과가 다릅니다.");
            Console.WriteLine("  TCP : 멈췄다가 순간이동");
            Console.WriteLine("  UDP : 한 프레임만 건너뛰고 계속");
        }

        // ═══════════════════════════════════════
        // 5. 유실률 실험
        // ═══════════════════════════════════════
        static async Task CustomLossRate()
        {
            Console.Write(" 유실률(%)을 입력하세요 (0~50) > ");
            if (!int.TryParse(Console.ReadLine(), out int rate)) rate = 10;
            rate = Math.Clamp(rate, 0, 50);

            Console.WriteLine();
            Console.WriteLine($"[UDP · 유실률 {rate}%] 100번 전송해봅니다");
            Console.WriteLine();

            Random rng = new Random();
            int received = 0;
            int lost = 0;
            var pattern = new System.Text.StringBuilder();

            for (int i = 0; i < 100; i++)
            {
                bool dropped = rng.Next(100) < rate;

                if (dropped) { lost++;     pattern.Append('.'); }
                else         { received++; pattern.Append('#'); }

                if ((i + 1) % 50 == 0)
                {
                    Console.WriteLine("  " + pattern.ToString());
                    pattern.Clear();
                }
            }

            Console.WriteLine();
            Console.WriteLine($"  수신 : {received}개   유실 : {lost}개");
            Console.WriteLine();
            Console.WriteLine("  # = 도착    . = 유실");
            Console.WriteLine();

            // 체감 설명
            if (rate <= 5)
                Console.WriteLine("  이 정도면 사람 눈으로는 거의 못 느낍니다.");
            else if (rate <= 20)
                Console.WriteLine("  약간 끊기는 느낌이 들 수 있지만 플레이는 가능합니다.");
            else
                Console.WriteLine("  눈에 띄게 끊깁니다. 보간(Interpolation)이 필요합니다.");

            Console.WriteLine();
            Console.WriteLine("  초당 30번 보내면 유실 몇 개는 티가 안 납니다.");
            Console.WriteLine("  다음 좌표가 33ms 뒤에 또 오니까요.");

            await Task.CompletedTask;
        }

        // ═══════════════════════════════════════
        // 6. 시퀀스 번호로 순서 뒤바뀜 처리
        //
        // UDP는 순서를 보장하지 않습니다.
        // 5번보다 6번이 먼저 올 수 있어요.
        // 그럴 때 5번을 적용하면 캐릭터가 뒤로 갑니다.
        // ═══════════════════════════════════════
        static async Task SequenceNumber()
        {
            Console.WriteLine("[순서 뒤바뀜] UDP는 보낸 순서대로 안 올 수 있습니다");
            Console.WriteLine();

            // 일부러 뒤섞인 순서로 도착
            int[] arrival = { 1, 2, 3, 6, 4, 5, 7, 9, 8, 10 };

            Console.WriteLine("── 처리 안 했을 때 ──");
            int x = 0;
            foreach (int seq in arrival)
            {
                bool wentBack = seq < x;
                x = seq;
                DrawCharacter(x * 3, $"seq {seq,2} 적용" + (wentBack ? "   ← 뒤로 갔습니다!" : ""),
                              wentBack ? ConsoleColor.Red : ConsoleColor.Gray);
                await Task.Delay(150);
            }
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("6번 다음에 4번이 적용돼서 캐릭터가 뒤로 갔습니다.");
            Console.ResetColor();

            Console.WriteLine();
            Console.WriteLine("── 시퀀스 번호로 거른 뒤 ──");

            int lastSeq = 0;
            foreach (int seq in arrival)
            {
                if (seq <= lastSeq)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  seq {seq,2} — 오래된 패킷이라 버림 (현재 {lastSeq})");
                    Console.ResetColor();
                    await Task.Delay(150);
                    continue;
                }

                lastSeq = seq;
                DrawCharacter(seq * 3, $"seq {seq,2} 적용", ConsoleColor.Green);
                await Task.Delay(150);
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("뒤로 가는 일이 없어졌습니다.");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine("규칙은 간단합니다:");
            Console.WriteLine("  내가 가진 번호보다 작거나 같으면 → 버린다");
            Console.WriteLine("  크면 → 적용하고 번호를 갱신한다");
        }

        // ═══════════════════════════════════════
        // 그리기 도우미
        // ═══════════════════════════════════════
        static void DrawCharacter(int x, string label, ConsoleColor color = ConsoleColor.Gray)
        {
            x = Math.Clamp(x, 0, MAP_WIDTH - 1);

            Console.ForegroundColor = color;
            Console.Write("  [");
            Console.Write(new string('-', x));
            Console.Write('O');
            Console.Write(new string('-', MAP_WIDTH - 1 - x));
            Console.Write("]  ");
            Console.WriteLine(label);
            Console.ResetColor();
        }

        static void DrawTwoLines(int tcpX, int udpX, int tick, bool stalled)
        {
            Console.ForegroundColor = stalled ? ConsoleColor.Red : ConsoleColor.Gray;
            Console.Write("  [");
            Console.Write(new string('-', tcpX));
            Console.Write('O');
            Console.Write(new string('-', MAP_WIDTH - 1 - tcpX));
            Console.Write("]  TCP");
            if (stalled) Console.Write("  ← 재전송 대기 중");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("  [");
            Console.Write(new string('-', udpX));
            Console.Write('O');
            Console.Write(new string('-', MAP_WIDTH - 1 - udpX));
            Console.Write("]  UDP");
            Console.WriteLine();
            Console.ResetColor();
            Console.WriteLine();
        }

        static void PrintTitle()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║   예제 C — TCP vs UDP                ║");
            Console.WriteLine("║   동양게임즈 서버개발팀 교육자료      ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
            Console.WriteLine(" 캐릭터가 오른쪽으로 이동하는 상황을 흉내냅니다.");
            Console.WriteLine(" 유실이 생겼을 때 화면이 어떻게 보이는지 확인하세요.");
        }
    }
}
