# 에디터 자동화 러너

설계: `league-of-physical/docs/superpowers/specs/2026-11-01-editor-automation-design.md`

```bash
export PATH="$HOME/.unity/bin:$PATH"
P=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client

# 0) 준비 — 한 번(에디터 세션 동안 유지): 에디터가 뒤에 있어도 틱이 돈다
unity cmd set_autotick --project-path $P --enable true
#    전제: 로컬 클러스터·백엔드가 떠 있고, 손으로 Flappy 한 판이 된다. 클라 에디터는 재생 중이 아니다.

# 1) 시나리오 반복
unity cmd automation_start --project-path $P --scenario flappy-quest-grind --repeat 5

# 2) 기다리기 — 재생에 들어가는 몇 초는 응답이 없을 수 있다(도메인 다시 불러오기). 그동안은 계속 돈다
until unity cmd automation_status --project-path $P --json 2>/dev/null | grep -q '"done": *true'; do sleep 15; done
unity cmd automation_status --project-path $P

# 3) 멈추기(안전) — 지금 단계를 끊고 teardown(재생 끄기)과 설정 되돌리기. 한 번 더 부르면 teardown도 건너뜀
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

기록은 `Logs/automation/`(git 무시)
단계가 바꾼 설정은 실행이 끝나면 되돌아간다 — 오래 바꾸려면 메뉴를

## 알려진 한계
- `input_tap`의 `x`·`y`는 화면 픽셀이고 원점은 **왼쪽 아래**(Input System 화면 좌표 규약). 왼쪽 위로 재면 좌우는 맞고 위아래가 뒤집힌다.
- "맨 위" 판정(`ui_click`·`ui_wait clickable`)은 그 요소가 속한 `UIDocument`(패널) 안에서만 본다 — **다른 `UIDocument`(다른 `PanelSettings`) 위에 뜬 덮개**는 이 Pick으로 못 잡는다.
- 대상이 `pickingMode=Ignore`면 그 요소 자신 대신 가장 가까운 Pick 가능한 조상을 기준으로 맨 위를 본다(순수 장식용 Label이 보통 Ignore다).
