# 에디터 자동화 러너

설계 배경(닫히면 `archive/`로 옮겨가는 spec이라 경로를 링크하지 않는다): 에디터 전용 CLI 명령
(automation_start/step/status/stop/list, ui_query, ui_click)으로 시나리오(JSON, setup·loop·teardown 단계 목록)를
에디터 틱마다 한 단계씩 돌리고, 도메인 다시 불러오기·재생 진입을 건너 이어 돈다. 실행 상태·기록은
`Logs/automation/<run-id>/`에.

```bash
export PATH="$HOME/.unity/bin:$PATH"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client

# 0) 준비 — 한 번(에디터 세션 동안 유지): 에디터가 뒤에 있어도 틱이 돈다
unity cmd set_autotick --project-path $P --enable true
#    전제: 로컬 클러스터·백엔드가 떠 있고, 손으로 Flappy 한 판이 된다. 클라 에디터는 재생 중이 아니다.

# 1) 시나리오 반복
unity cmd automation_start --project-path $P --scenario flappy-quest-grind --repeat 5

# 2) 기다리기 — 재생에 들어가는 몇 초는 응답이 없을 수 있다(도메인 다시 불러오기). 그동안은 계속 돈다
#    상한을 둔다(예: 120번 × 15s = 30분) — 에디터가 죽으면 automation_status가 계속 idle·오류를 내고 영원히 안 끝난다.
for i in $(seq 120); do
  unity cmd automation_status --project-path $P --json 2>/dev/null | grep -q '"done": *true' && break
  sleep 15
done
unity cmd automation_status --project-path $P

# 3) 멈추기(안전) — 지금 단계를 끊고 teardown(재생 끄기)과 설정 되돌리기. 한 번 더 부르면 teardown도 건너뜀
#    teardown을 건너뛰면(두 번째 automation_stop) 재생이 켜진 채로 끝난다 — 아래 "재생이 켜진 채 끝나는 경우" 참고
unity cmd automation_stop --project-path $P

# 4) 결과 보기
ls -t "$P/Logs/automation" | head -1                 # 가장 최근 실행
open "$P/Logs/automation/$(ls -t "$P/Logs/automation" | head -1)"

# 부품 하나씩(재생 중 로비에서)
unity cmd automation_list --project-path $P
unity cmd ui_query        --project-path $P --name play-button
unity cmd ui_click        --project-path $P --name nav-quests
unity cmd automation_step --project-path $P --step '{"op":"ui_wait","name":"quest-daily-list","text_contains":"/","timeout_s":10}'
unity cmd automation_step --project-path $P --step '{"op":"ui_read","name":"quest-daily-list","as":"daily"}'
unity cmd automation_step --project-path $P --step '{"op":"input_tap","key":"Space","hold_ms":120}'
unity cmd automation_step --project-path $P --step '{"op":"wait_state","state":"PostMatch","scene":"Result","timeout_s":600}'
unity cmd automation_step --project-path $P --step '{"op":"screenshot","as":"now"}'
unity cmd automation_step --project-path $P --step '{"op":"log","message":"여기까지 손으로 확인"}'

# 매크로(재생 중) — 좌표·글자·화면 비교
unity cmd automation_step --project-path $P --step '{"op":"touch","x":0.5,"y":0.5}'
unity cmd automation_step --project-path $P --step '{"op":"drag","x":0.2,"y":0.8,"to_x":0.8,"to_y":0.8,"duration_ms":400}'
unity cmd automation_step --project-path $P --step '{"op":"ui_type","name":"cdn-input","value":"봇01","submit":true}'
unity cmd automation_step --project-path $P --step '{"op":"snapshot_compare","baseline":"lobby/home","ignore":[[0.8,0,0.2,0.1]]}'

# 녹화 → 재생
unity cmd automation_record_start  --project-path $P --name lobby-tour --overwrite true --max_wait 8   # Game 뷰에서 손으로. F9 스크린샷, F10 체크포인트
unity cmd automation_record_status --project-path $P                                                   # 녹화 중인지, 이름·초·이벤트 수
unity cmd automation_record_stop   --project-path $P
unity cmd editor_stop              --project-path $P
unity cmd automation_start         --project-path $P --scenario lobby-tour

# 화면 비교 기준
unity cmd automation_baseline --project-path $P --scenario lobby-tour     # 비교 단계마다 지금 그림을 기준으로
unity cmd automation_baseline --project-path $P --run 20261101-153005-lobby-tour   # 그 실행의 실패한 비교를 받아들인다
```

`automation_step`의 결과(읽은 글자·스크린샷 경로)는 그 실행의 `report.json`에 있다(`automation_status`의 `report`).

기록은 `Logs/automation/`(git 무시).
단계가 바꾼 설정은 실행이 끝나면 되돌아간다 — 오래 바꾸려면 메뉴(`LOP/Environment/…`, `LOP/Debug/Auto Flap`)를 쓴다.

## 재생이 켜진 채 끝나는 경우
의도된 동작이고 둘뿐이다 — 끝나면 직접 재생을 끈다(에디터에서 또는 `automation_step play_exit`).
- 두 번째 `automation_stop`: 첫 멈춤 요청은 teardown(`play_exit` 포함)을 돌지만, 같은 실행에 또 멈춤을 부르면
  남은 teardown을 건너뛴다(`AutomationRunner.RequestStop`) — 재생이 켜져 있었다면 그대로 켜진 채 끝난다.
- `automation_step play_enter`를 부품 하나씩 손으로 실행했을 때: `automation_stop`·teardown 없이 재생만 켠 것이므로
  당연히 켜진 채 남는다.

## 좌표·글자

- 좌표 단계(`touch`·`long_press`·`drag`)는 **0..1 정규화, 왼쪽 위 원점**이다(`input_tap`의 `x`·`y`만 옛 그대로 화면 픽셀·왼쪽 아래). `ui_query`가
  돌려주는 요소의 `x`·`y`도 같은 정규화 좌표라 그대로 좌표 단계에 넘길 수 있다.
- 좌표는 **Input System 포인터 장치 하나**(기본 마우스, `device: "touch"`로 합성 터치스크린)로 들어간다 — 게임 코드(`Mouse.current`)와
  UI Toolkit(EventSystem)이 사람 손처럼 같이 받는다. 요소를 집어 누르는 선택자 길(`ui_click`)과는 다른 길이라, 섞어 쓰지 않는다.
- 선택자는 지금까지와 같은 패널 길(`name`·`text`·`text_contains`)이다.
- `ui_type`은 글자 칸(TextField)에 진짜 키보드처럼 친다 — 칠 글자는 `value`, 대상을 고르는 선택자는 `name`·`text`·`text_contains`(일반
  단계의 `text`와 다른 뜻이니 헷갈리지 않는다). `input_text`는 Input System 키보드 글자 이벤트만 넣는다(받는 UI가 없어도 된다).

## 녹화 → 재생

- 재생 중에만 시작한다(로비 같은 시작 화면까지 간 뒤). `automation_record_start --name <이름>`(영소문자·숫자·-·_, 시나리오 파일 이름이 된다) →
  Game 뷰에서 손으로 → **F9**는 스크린샷, **F10**은 화면 비교 체크포인트 → `automation_record_stop`으로 `Automation/Scenarios/<이름>.json`에 쓴다.
  메뉴 `LOP/Automation/녹화 시작`·`녹화 멈춤`도 같은 일을 한다(멈추면 Finder로 파일을 보여 준다).
- 선택자 우선(다시 정확히 가리킬 때만, 못 가리키면 좌표로 남는다), 기다림은 시간이 아니라 대상으로(대상이 늦게 나타난 클릭만 `ui_wait`가 앞에
  붙는다), 좌표·키·스크린샷 앞 기다림은 상한 5초(그보다 긴 간격은 `note`에 원래 값을 남긴다).
- 녹화 파일은 손으로 쓴 시나리오와 똑같은 모양이다 — 열어서 고쳐 써도 된다(이름 바꾸기, 기다림 조정, `ui_wait` 더하기…).
- 녹화와 실행은 **동시에 못 한다** — 녹화 중엔 `automation_start`·`automation_step`·`automation_baseline --scenario`를 거절하고, 실행 중엔
  녹화 시작을 거절한다. 재생을 끄거나 스크립트가 다시 불러와지면 그 자리까지 자동으로 닫는다.
- `ui_click`도 녹화 중엔 거절한다 — `UiClicker`는 패널 이벤트라 녹화기의 Input System 엿보기에 안 걸려, 쓰면 그 클릭이
  시나리오에서 조용히 빠지기 때문이다(기다리는 길인 `automation_step`의 `ui_click`을 쓰거나, 손으로 눌러 녹화에 남긴다).
- `--all_input true`로 시작하면 Game 뷰에 OS 초점이 없어도 들어온 입력을 게임이 받고 녹화한다(`simulate_pointer`·`simulate_key` 연기
  확인용) — 멈추면 되돌린다.

## 화면 비교

| 인자 | 기본값 | 뜻 |
|---|---|---|
| `baseline` | — | 기준 이름(`.png` 없이 `/`로 나눈 영문·숫자·_·-, 예: `lobby/home`) |
| `tolerance` | 16 | 픽셀 RGB 최대 차 허용치 |
| `max_diff_percent` | 1.0 | 허용되는 다른 픽셀 비율(%) |
| `region`·`ignore` | 전체 | `[x,y,w,h]`(0..1, 왼쪽 위) — `ignore`는 흔들리는 자리를 뺀다 |
| `settle_ms`·`retry_s` | 500·3 | 첫 찍기 전 가라앉는 시간, 다시 찍는(0.5초마다) 동안 |

`snapshot_compare`는 실패하면 가장 덜 달랐던 순간의 지금 그림·차이 그림(빨강=다름, 짙은 파랑=안 셈)을 실행 폴더에 남긴다. 기준 그림은
`Automation/Baselines/`에 **커밋한다**. 기준이 없으면(또는 의도한 화면 변경 뒤) `automation_baseline --scenario <이름>`으로 새로 찍거나,
`automation_baseline --run <실행 이름>`으로 그 실행에서 실패한 비교의 지금 그림을 기준으로 받아들인다. 화면 비교를 쓰려면 Game 뷰 해상도를
고정한다 — 가로세로 **비율**이 달라져도 캡처는 시나리오의 `width`·`height`(기본 1280×720)로 늘여 찍히므로 크기는 같다, 대신 그림이 늘어나
거의 모든 픽셀이 달라져 높은 차이 %로 실패한다. "크기가 다르다"는 시나리오의 `width`·`height`가 **기준 그림과 다를 때만** 난다.

## 알려진 한계
- "맨 위" 판정(`ui_click`·`ui_wait clickable`)은 그 요소가 속한 `UIDocument`(패널) 안에서만 본다 — **다른 `UIDocument`(다른 `PanelSettings`) 위에 뜬 덮개**는 이 Pick으로 못 잡는다.
- 대상이 `pickingMode=Ignore`면 그 요소 자신 대신 가장 가까운 Pick 가능한 조상을 기준으로 맨 위를 본다(순수 장식용 Label이 보통 Ignore다).
- 녹화는 마우스·키보드만 본다(게임패드·펜 같은 다른 장치는 안 잡는다).
- 끌기(drag) 녹화는 시작·끝 점만 남긴다 — 중간 궤적은 버린다.
- 키 조합(Shift+클릭 등)은 녹화가 따로 적지 않는다 — 누른 각 키가 따로 `input_tap`이 된다.
- 재생은 에디터 틱(초당 10번) 단위다 — 프레임 정밀한 조작은 안 된다. 녹화는 틱이 아니라 Input System 이벤트 시각을 그대로 적지만,
  누르고 있는 동안의 움직임(`PointerMove`)은 0.05초 표본이라 그보다 빠른 궤적은 뭉개진다.
- 녹화 중 글자 칸에 초점이 있으면 **F9·F10은 칸에 안 들어가고 그냥 버려진다**(먼저 걸리는 검사가 글자 칸 쪽이라 `_keysDown`에도
  안 남아 뗄 때도 무시된다) — 스크린샷·체크포인트를 찍으려면 칸 밖을 한 번 눌러 초점을 뺀 뒤 누른다.
- 도메인이 다시 불러와져 녹화가 자동으로 닫히면(`beforeAssemblyReload`) 그 결과(`_last`)는 다시 불러온 뒤 비어 있다 — 파일은 남으니
  `Automation/Scenarios/`에서 찾는다.
- 실행 동안(`EnsureGameInput`이 입력 모드를 바꾼 뒤)엔 Game 뷰 초점과 상관없이 모든 장치 입력이 게임으로 간다 — 다른 창에 친 키·마우스도
  게임이 받을 수 있다는 뜻이니, 자동화가 도는 동안은 다른 작업을 자제한다.
