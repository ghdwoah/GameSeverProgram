# 5주차 실습 — TICKET-012 위치 동기화

## 오늘 할 일

```
1~2교시   콘솔 예제로 UDP 배우기   (완성본 제공)
3~4교시   서버·클라이언트 직접 구현 (TODO 8개 채우기)
```

---

## 폴더 구성

| 폴더 | 상태 | 설명 |
|---|---|---|
| `TcpVsUdp/` | 완성 | 예제 C — TCP와 UDP 차이 |
| `UdpBasics/` | 완성 | 예제 D — UDP 소켓 다루기 |
| `PositionSync/` | 완성 | 예제 E — 콘솔 위치 동기화 **← 정답 참고용** |
| `BattleArenaServer/` | **TODO 4개** | 서버 |
| `BattleArenaClient_v3/` | **TODO 4개** | Unity 클라이언트 |
| `TestClient/` | 완성 | 접속 테스트 도구 |

**예제 E(PositionSync)가 여러분의 교과서입니다.** 거기서 콘솔로 만든 걸 서버와 Unity로 옮기는 게 오늘 과제예요.

---

## TODO 목록

### 서버 — `BattleArenaServer/UdpServer.cs`

| # | 내용 | 난이도 |
|---|---|---|
| 1 | UDP 소켓 만들고 Bind | ★ |
| 2 | ReceiveFromAsync로 패킷 받기 | ★★ |
| 3 | 오래된 패킷 버리기 (시퀀스 검사) | ★★ |
| 4 | 30fps로 전체 위치 브로드캐스트 | ★★★ |

### 클라이언트 — `BattleArenaClient_v3/Assets/Scripts/`

| # | 파일 | 내용 | 난이도 |
|---|---|---|---|
| 5 | UdpNetworkClient.cs | UDP 소켓 만들기 | ★ |
| 6 | UdpNetworkClient.cs | MOVE 패킷 보내기 | ★★ |
| 7 | UdpNetworkClient.cs | 큐에서 최신 것만 꺼내기 | ★★ |
| 8 | GameManager.cs | 보간 (Lerp) | ★ |

---

## 권장 순서

```
TODO 1, 2  →  서버가 패킷을 받는지 확인 (콘솔 로그)
TODO 5, 6  →  클라이언트가 보내는지 확인
TODO 4     →  서버가 뿌리는지 확인
TODO 7     →  클라이언트가 받는지 확인
TODO 3     →  캐릭터가 뒤로 안 가는지 확인
TODO 8     →  부드러워졌는지 확인
```

1·2번과 5·6번을 먼저 하면 **서버 콘솔에 로그가 찍히는지**로 중간 확인이 됩니다.

---

## 단계별 확인 방법

### 1단계 — TODO 1, 2를 끝낸 뒤

서버를 실행하면 이렇게 떠야 합니다.

```
[TCP] 포트 7777번에서 접속을 기다립니다...
[UDP] 포트 7778번에서 위치 패킷을 기다립니다
```

### 2단계 — TODO 5, 6까지

Unity를 실행하고 방향키를 누르면 서버 콘솔에 이게 떠야 합니다.

```
[UDP] Player123 주소 등록 : 127.0.0.1:54322
```

한 번만 뜨고 더 안 떠도 정상입니다. 등록은 처음 한 번만 하니까요.

### 3단계 — TODO 4, 7까지

Unity를 2개 띄우면 **서로의 캐릭터가 보입니다.** 다만 움직임이 뚝뚝 끊길 거예요.

### 4단계 — TODO 3, 8까지

- 캐릭터가 뒤로 갔다 앞으로 가는 현상이 사라짐 (TODO 3)
- 움직임이 부드러워짐 (TODO 8)

---

## 테스트 방법

### 서버 실행

```
BattleArena.sln 열기
→ BattleArenaServer를 시작 프로젝트로 설정
→ F5
```

### Unity 2개 띄우기

```
File → Build Settings → Add Open Scenes → Build
→ 빌드된 exe를 두 번 실행
```

전체화면으로 뜨면 나란히 못 보니 창 모드로 실행하세요.

```
BattleArena.exe -screen-fullscreen 0 -screen-width 800 -screen-height 600
```

---

## 패킷 형식

```
클라 → 서버   MOVE|플레이어ID|시퀀스|X|Y
              예) MOVE|3|152|10|7

서버 → 클라   STATE|id,닉네임,x,y|id,닉네임,x,y|...
              예) STATE|1,홍길동,10,7|3,철수,22,4
```

JSON을 안 쓰는 이유는 초당 30번씩 보내기 때문입니다. 짧을수록 좋아요.

---

## 막힐 때

### 서버가 패킷을 못 받음
- 포트 7778이 맞는지 확인
- 방화벽에서 UDP를 허용했는지 확인 (TCP와 별도입니다)

### 다른 캐릭터가 안 보임
- 서버 콘솔에 `[UDP] ... 주소 등록`이 떴는지 확인
- 안 떴으면 클라이언트가 안 보내고 있는 겁니다 (TODO 6)
- 떴는데 안 보이면 서버가 안 뿌리는 겁니다 (TODO 4)

### 캐릭터가 뒤로 갔다 옴
- TODO 3을 안 했습니다

### 캐릭터가 뚝뚝 끊김
- TODO 8을 안 했습니다

### 컴파일 에러가 남
- TODO 자리에 남겨둔 임시 줄(`return false;` 등)을 지웠는지 확인

---

## 제출

```
두 캐릭터가 같이 보이는 화면 스크린샷
→ Discord #과제제출

수정한 코드
→ GitHub 커밋
```

---

## 참고

**AI를 써도 됩니다.** 다만 왜 그렇게 짜는지는 설명할 수 있어야 합니다. 나중에 구두로 물어봅니다.

막히면 예제 E(PositionSync)를 열어보세요. 거의 같은 코드가 들어 있습니다.

---

## 수정 이력 (v2)

| 내용 | 영향 |
|---|---|
| UDP 소켓 Bind 추가 | 받기가 안 되던 문제 해결 |
| manifest.json에 Visual Studio Editor 패키지 추가 | .sln 파일이 안 생기던 문제 해결 |
| 수신 에러를 화면·Console에 표시 | 조용히 죽는 대신 원인이 보임 |
| PositionSync 렌더링 10fps로 분리 | 콘솔 깜빡임 완화 |

### Bind가 왜 필요한가

```
SendTo   → OS가 포트를 자동 할당 (implicit bind)  → Bind 없어도 됨
ReceiveFrom → 받을 포트가 정해져 있어야 함        → Bind 필수
```

예제 E의 BotClient는 보내기만 해서 Bind가 없습니다. 받는 쪽(GameClient, UdpNetworkClient)은 전부 Bind가 필요합니다.
