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

## 알려진 한계
- `input_tap`의 `x`·`y`는 화면 픽셀이고 원점은 **왼쪽 아래**(Input System 화면 좌표 규약). 왼쪽 위로 재면 좌우는 맞고 위아래가 뒤집힌다.
- "맨 위" 판정(`ui_click`·`ui_wait clickable`)은 그 요소가 속한 `UIDocument`(패널) 안에서만 본다 — **다른 `UIDocument`(다른 `PanelSettings`) 위에 뜬 덮개**는 이 Pick으로 못 잡는다.
- 대상이 `pickingMode=Ignore`면 그 요소 자신 대신 가장 가까운 Pick 가능한 조상을 기준으로 맨 위를 본다(순수 장식용 Label이 보통 Ignore다).
- `input_tap`의 키(와 터치)는 Game 뷰에 OS 초점이 있어야 게임에 간다(11-01 연기 확인에서 재현) — 아래 매크로 확장에서 고친다.

## 다음: 매크로 확장(계획 중 — 아직 없다)

monkeyrunner식 매크로로 넓힌다(계획 `league-of-physical/docs/superpowers/plans/2026-11-01-editor-automation-macro.md`, 브랜치 `feature/automation-macro`).
머지되면 이 절을 실제 사용법으로 바꾼다. 무작위 몽키(아무 버튼이나 누르기)는 하지 않는다(사용자 11-01 결정).

- 새 단계: `touch`·`long_press`·`drag`(좌표는 0..1 정규화, 왼쪽 위 원점 — `ui_query`가 `x`·`y`를 준다), `ui_type`(글자 칸, 칠 글자는 `value`),
  `input_text`, `snapshot_compare`(기준 그림과 픽셀 비교, 허용치·비율·구역·제외 구역·다시 찍기, 실패 땐 지금 그림·차이 그림). 단계 공통 칸 `note`.
- 새 명령: `automation_record_start --name x`·`automation_record_stop`·`automation_record_status`(재생 중 손으로 한 것을
  `Automation/Scenarios/x.json`으로 — 선택자 우선, 안 되면 좌표, F9 스크린샷, F10 체크포인트), `automation_baseline --scenario x | --run <실행>`(기준 그림 만들기·받아들이기).
  메뉴 `LOP/Automation/녹화 시작`·`녹화 멈춤`.
- 초점 고치기: 입력 단계가 실행 동안 Input System을 "모든 입력이 게임으로"로 바꾸고 끝에 되돌린다 — Game 뷰 초점이 필요 없어진다.
- 기준 그림은 `Automation/Baselines/`에 커밋한다. 화면 비교를 쓰려면 Game 뷰 해상도를 고정한다.
