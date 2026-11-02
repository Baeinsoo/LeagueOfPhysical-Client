#!/usr/bin/env bash
# EditMode 시험을 사람 손 없이 끝까지 돌린다 — 에디터가 뒤에 있어 명령을 못 받거나, 씬이 "수정됨"이라
# "Scene(s) Have Been Modified" 창이 떠서 메인 스레드가 멈추는 일을 미리·도중에 처리한다.
#
#   Automation/run-editmode.sh [--project LeagueOfPhysical-Server] [--filter 이름]
#
# 씬이 수정됨일 때 버리는 것은 두 경우뿐이다(사용자 상시 허락, 메모리 entrance-layout-noise-reload-ok):
#   ① 경로 없는 씬(Untitled) — 시험이 남긴 임시 씬
#   ② 마지막 기록이 "Driving RectTransform" — UI 레이아웃이 저절로 바꾼 흔적
# 그 밖의 수정은 사람의 작업일 수 있으니 버리지 않고 멈춘다.
set -u
PROJECT="LeagueOfPhysical-Client"
FILTER=""
while [ $# -gt 0 ]; do
  case "$1" in
    --project) PROJECT="$2"; shift 2 ;;
    --filter) FILTER="$2"; shift 2 ;;
    *) echo "모르는 인자: $1"; exit 64 ;;
  esac
done

HERE="$(cd "$(dirname "$0")" && pwd -W)"
ROOT="C:/Users/re5na/workspace/LOP/$PROJECT"
RESULTS="$USERPROFILE/AppData/LocalLow/BSGames/$PROJECT/TestResults.xml"
export MSYS_NO_PATHCONV=1

ucmd() { timeout 60 unity command "$@" --project-path "$ROOT" 2>&1 | tail -1; }
result_of() { grep -o '"result":"[^"]*"' | head -1 | sed 's/"result":"//; s/"$//'; }
focus() { powershell -NoProfile -ExecutionPolicy Bypass -File "$HERE/focus-editor.ps1" -Project "$PROJECT" >/dev/null 2>&1; }

# 수정됨 판정 — "clean" / "discardable <경로>" / "keep <이유>"
SCENE_CHECK='var s=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
for (int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++){var x=UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); if(x.isDirty){s=x;break;}}
if(!s.isDirty) return "clean";
string undo=UnityEditor.Undo.GetCurrentGroupName();
if(string.IsNullOrEmpty(s.path)) return "discardable untitled";
if(undo=="Driving RectTransform") return "discardable "+s.path;
return "keep "+s.path+" undo="+undo;'

ensure_alive() {
  for i in 1 2 3; do
    r=$(ucmd eval --code 'return "alive";')
    echo "$r" | grep -q '"result":"alive"' && return 0
    focus; sleep 5
  done
  echo "에디터가 명령을 받지 않는다(창 올리기 3번 실패) — 사람이 다시 켜야 한다"; return 1
}

clean_scene() {
  state=$(ucmd eval --code "$SCENE_CHECK" | result_of)
  case "$state" in
    clean) return 0 ;;
    discardable*)
      echo "씬 정리: $state → Entrance를 저장 없이 다시 연다"
      ucmd eval --code 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Entrance.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); return "ok";' >/dev/null
      return 0 ;;
    *) echo "씬이 수정됨($state) — 사람의 작업일 수 있어 시험을 시작하지 않는다"; return 1 ;;
  esac
}

# 시험 도중 뜬 "Scene(s) Have Been Modified" 창 처리 — 버려도 되는 수정이면 Don't Save, 아니면 Cancel.
handle_modal() {
  powershell -NoProfile -ExecutionPolicy Bypass -File "$HERE/scene-modal.ps1" -Project "$PROJECT" -Mode probe 2>/dev/null | grep -q '^modal' || return 0
  # 모달이 메인 스레드를 잡아 eval을 못 하므로 창 제목의 씬 이름으로 가른다.
  title=$(powershell -NoProfile -ExecutionPolicy Bypass -File "$HERE/scene-modal.ps1" -Project "$PROJECT" -Mode title 2>/dev/null)
  if echo "$title" | grep -q " - Untitled - "; then
    powershell -NoProfile -ExecutionPolicy Bypass -File "$HERE/scene-modal.ps1" -Project "$PROJECT" -Mode dontsave >/dev/null
    echo "도중에 뜬 저장 창: 시험이 남긴 Untitled → Don't Save"
  else
    powershell -NoProfile -ExecutionPolicy Bypass -File "$HERE/scene-modal.ps1" -Project "$PROJECT" -Mode cancel >/dev/null
    echo "도중에 뜬 저장 창: $title → Cancel(사람 판단 필요)"; return 1
  fi
}

ensure_alive || exit 2
r=$(ucmd eval --code 'return UnityEditor.EditorApplication.isPlaying+" "+UnityEditor.EditorApplication.isCompiling;' | result_of)
[ "$r" = "False False" ] || { echo "재생 중이거나 컴파일 중이다($r) — 시험을 시작하지 않는다"; exit 3; }
clean_scene || exit 4

before=$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)
if [ -n "$FILTER" ]; then ucmd run_tests --mode EditMode --filter "$FILTER" >/dev/null; else ucmd run_tests --mode EditMode >/dev/null; fi

for i in $(seq 180); do   # 최대 30분
  sleep 10
  [ "$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)" != "$before" ] && break
  handle_modal || exit 5
done
[ "$(stat -c %Y "$RESULTS" 2>/dev/null || echo 0)" != "$before" ] || { echo "30분 안에 결과가 안 나왔다"; exit 6; }
sleep 2
handle_modal   # 끝나고 원래 씬으로 돌아갈 때 뜨는 창

head -c 800 "$RESULTS" | grep -o 'total="[0-9]*"\|passed="[0-9]*"\|failed="[0-9]*"' | head -3 | tr '\n' ' '; echo
grep -o '<test-case [^>]*result="Failed"' "$RESULTS" | grep -o 'fullname="[^"]*"' | sed 's/^/FAILED /'
exit 0
