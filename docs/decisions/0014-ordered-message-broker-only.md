# 0014. 메시지 브로커는 구독 순서를 지키는 `OrderedMessageBroker`만 쓴다

- 상태: Accepted
- 날짜: 2026-08-24
- 관련: GameFramework 커밋 `46b01f4`(브로커 추가), `c29c7bc`(IL2CPP 대비 팩토리 등록) · `ROADMAP.md` "메시지 버스 순서 보장" 절 · 앞선 이전 spec [2026-07-16-eventbus-messagepipe-migration-design](../archive/specs/2026-07-16-eventbus-messagepipe-migration-design.md)

## 맥락

클라이언트와 서버는 메시지 전달에 MessagePipe(발행·구독 라이브러리)를 쓴다. 한 메시지를 여러 구독자가 나눠 받는 경우가 많고(`GameInfoToC`만 해도 넷이 받는다), 누가 먼저 받느냐가 곧 동작이다. 순서가 뒤집히면 "내 캐릭터를 못 알아보는" 식의 버그가 난다.

MessagePipe 기본 브로커는 이 순서를 지키지 않는다. 핸들러를 배열 칸 순서대로 부르는데, 구독이 풀려 빈 칸이 생기면 다음 구독자가 그 빈 칸을 다시 쓴다. 그래서 구독·해제를 반복하면 세 번째쯤부터 나중에 구독한 쪽이 먼저 불린다. 브로커는 앱 전체 수명 동안 살아 있으므로 한 세션에서 매치를 여러 판 할수록 어긋나고, 새로 켜면 재현되지 않아 "가끔 나는 버그"로 보였다.

이것은 라이브러리 내부 동작이라 우리 쪽 호출 방법을 바꿔서는 막을 수 없다. 등록이 VContainer 확장 메서드로 되어 있어 VContainer 문제처럼 보이지만 아니다.

## 결정

- GameFramework에 구독 순서대로 부르는 `OrderedMessageBroker<T>`와 키 버전을 두고, 클라·서버 모두 `RegisterOrderedMessageBroker<T>()`로만 등록한다. 기본 `RegisterMessageBroker`는 새로 쓰지 않는다.
- 발행·구독 인터페이스(`IPublisher`/`ISubscriber`)는 MessagePipe 것을 그대로 쓴다. 그래서 호출하는 코드는 바뀌지 않고, 순서만 고정된다.
- 필요 없는 기능은 만들지 않았다. 필터는 넘기면 예외가 나고, Async·Buffered 변형은 등록하지 않는다. 필요해지면 그때 만든다.

## 결과

- 쉬워지는 것: 여러 구독자의 순서에 기대는 코드가 몇 판을 돌아도 같은 순서로 동작한다.
- 어려워지는 것: MessagePipe의 필터·Async·Buffered 기능을 바로 쓸 수 없다. 우리가 만든 브로커를 우리가 관리해야 한다.
- 버린 대안:
  - **기본 브로커를 쓰되 순서에 기대지 않게 구독자를 고치기**: 순서에 기대는 곳이 여러 군데이고, 새 코드가 또 기댈 수 있다. 문제의 뿌리가 라이브러리에 있으니 거기서 막는 쪽을 골랐다.
  - **MessagePipe를 버리고 다른 버스로 옮기기**: 07-16에 MessagePipe로 옮긴 이유(DI 연동, R3 생태계)가 여전히 유효하다. 브로커 한 층만 바꾸면 충분했다.
- 다시 검토할 조건: MessagePipe가 구독 순서를 보장하도록 바뀌거나, 필터·Async·Buffered가 꼭 필요해질 때.
