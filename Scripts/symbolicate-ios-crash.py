#!/usr/bin/env python3
"""아이폰 크래시의 주소를 소스 줄로 바꾼다.

CI가 빌드마다 dSYM을 S3에 남겨 두므로(`client-app-deploy-ios`), 빌드 번호만 있으면
그것을 받아 와 주소를 푼다.

    Scripts/symbolicate-ios-crash.py --build 7 크래시.ips
    Scripts/symbolicate-ios-crash.py --build 7 --offsets 0x1a2b3c 0x4d5e6f
    Scripts/symbolicate-ios-crash.py --dsym ~/Downloads/lop.app.dSYM.zip 크래시.ips

필요한 것: `aws`(S3에서 받을 때), Xcode의 `atos`·`dwarfdump`.

**게임 코드는 거의 다 `UnityFramework`에 있다** — IL2CPP가 C#을 C++로 옮겨 그 프레임워크로
묶기 때문이다. 앱 바이너리(`LeagueOfPhysical-Client`)에는 실행 진입점 정도만 남는다.

**나오는 줄 번호는 C#이 아니라 IL2CPP가 만든 .cpp의 줄이다.** 예:

    ArcheryHitOutcome_get_Delta_m07062... (in UnityFramework) (baegames.LOP.Shared.Runtime.cpp:10838)

쓸 것은 줄 번호가 아니라 **이름**이다 — `ArcheryHitOutcome_get_Delta`처럼 C# 타입·메서드가
그대로 박혀 있다. 뒤의 `_m07062...`는 IL2CPP가 붙인 구분자이니 무시하면 된다.
"""
import argparse, json, os, re, shutil, subprocess, sys, zipfile

BUCKET = "s3://lop-client"
CACHE = os.path.expanduser("~/Library/Caches/lop-symbolicate")


def 실행(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def dSYM_내려받기(build):
    """S3에서 그 빌드의 dSYM을 찾아 받아 푼다. 이미 받아 뒀으면 그걸 쓴다."""
    dest = os.path.join(CACHE, f"build{build}")
    if os.path.isdir(dest) and any(n.endswith(".dSYM") for n in os.listdir(dest)):
        return dest

    이름 = f"lop-build{build}.app.dSYM.zip"
    r = 실행(["aws", "s3", "ls", f"{BUCKET}/builds/", "--recursive"])
    if r.returncode != 0:
        sys.exit(f"S3 목록을 못 읽었다. `aws login` 했는지 확인할 것.\n{r.stderr.strip()}")
    키 = [l.split(None, 3)[3] for l in r.stdout.splitlines() if l.strip().endswith(이름)]
    if not 키:
        sys.exit(f"빌드 {build}의 dSYM이 S3에 없다({이름}).\n"
                 "  · 2026-09-24 이전 빌드에는 dSYM을 안 남겼다.\n"
                 "  · 90일이 지나 수명 규칙으로 지워졌을 수도 있다.")

    os.makedirs(dest, exist_ok=True)
    zip경로 = os.path.join(dest, 이름)
    print(f"받는 중: {BUCKET}/{키[0]}", file=sys.stderr)
    if 실행(["aws", "s3", "cp", f"{BUCKET}/{키[0]}", zip경로]).returncode != 0:
        sys.exit("dSYM을 못 받았다.")
    with zipfile.ZipFile(zip경로) as z:
        z.extractall(dest)
    os.remove(zip경로)
    return dest


def dSYM_풀기(경로):
    """zip이면 풀고, 폴더면 그대로 쓴다."""
    if os.path.isdir(경로):
        return 경로
    dest = os.path.join(CACHE, "local")
    shutil.rmtree(dest, ignore_errors=True)
    os.makedirs(dest, exist_ok=True)
    with zipfile.ZipFile(경로) as z:
        z.extractall(dest)
    return dest


def 바이너리_찾기(dsym_dir):
    """{이미지 이름: DWARF 바이너리 경로}. dSYM 안에 여러 개가 들어 있다."""
    결과 = {}
    for root, dirs, _ in os.walk(dsym_dir):
        for d in dirs:
            if not d.endswith(".dSYM"):
                continue
            dwarf = os.path.join(root, d, "Contents", "Resources", "DWARF")
            if os.path.isdir(dwarf):
                for name in os.listdir(dwarf):
                    결과[name] = os.path.join(dwarf, name)
    return 결과


def uuid_읽기(바이너리):
    r = 실행(["dwarfdump", "--uuid", 바이너리])
    m = re.search(r"([0-9A-Fa-f-]{36})", r.stdout)
    return m.group(1).upper() if m else None


def 풀기(바이너리, 오프셋들, arch="arm64"):
    """오프셋(이미지 시작으로부터의 거리)을 심볼로. 로드 주소를 0으로 두면 오프셋이 곧 주소다."""
    r = 실행(["atos", "-arch", arch, "-o", 바이너리, "-l", "0"] + [hex(o) for o in 오프셋들])
    if r.returncode != 0:
        return [f"(atos 실패: {r.stderr.strip()})"] * len(오프셋들)
    return r.stdout.strip().splitlines()


def ips_읽기(경로):
    """iOS 15+ .ips는 첫 줄이 헤더 JSON, 나머지가 본문 JSON이다."""
    with open(경로, encoding="utf-8") as f:
        내용 = f.read()
    조각 = 내용.split("\n", 1)
    try:
        return json.loads(조각[1])
    except Exception:
        return json.loads(내용)   # 본문만 저장한 경우


def main():
    ap = argparse.ArgumentParser(description="아이폰 크래시 주소를 소스 줄로 바꾼다")
    ap.add_argument("crash", nargs="?", help=".ips 크래시 파일")
    ap.add_argument("--build", help="TestFlight 빌드 번호 (S3에서 dSYM을 받아 온다)")
    ap.add_argument("--dsym", help="dSYM 폴더 또는 zip (S3 대신 직접 지정)")
    ap.add_argument("--offsets", nargs="*", default=[], help="이미지 시작으로부터의 오프셋들")
    ap.add_argument("--image", default="UnityFramework", help="--offsets를 풀 이미지 (기본 UnityFramework)")
    args = ap.parse_args()

    if not args.dsym and not args.build:
        ap.error("--build 또는 --dsym 중 하나는 있어야 한다")
    dsym_dir = dSYM_풀기(args.dsym) if args.dsym else dSYM_내려받기(args.build)

    바이너리들 = 바이너리_찾기(dsym_dir)
    if not 바이너리들:
        sys.exit(f"dSYM 안에서 DWARF 바이너리를 못 찾았다: {dsym_dir}")
    print(f"dSYM: {dsym_dir}  (이미지 {', '.join(sorted(바이너리들))})", file=sys.stderr)

    if args.offsets:
        b = 바이너리들.get(args.image)
        if not b:
            sys.exit(f"'{args.image}'가 dSYM에 없다. 있는 것: {', '.join(sorted(바이너리들))}")
        오프셋 = [int(o, 16) if o.lower().startswith("0x") else int(o) for o in args.offsets]
        for o, s in zip(오프셋, 풀기(b, 오프셋)):
            print(f"{hex(o):>12}  {s}")
        return

    if not args.crash:
        ap.error("크래시 파일을 주거나 --offsets를 줘야 한다")

    보고서 = ips_읽기(args.crash)
    이미지 = 보고서.get("usedImages", [])

    #  UUID가 다르면 **다른 빌드의 dSYM**이다. 이걸 안 보면 엉뚱한 줄 번호를 믿게 된다.
    for img in 이미지:
        이름 = img.get("name")
        if 이름 in 바이너리들 and img.get("uuid"):
            내것 = uuid_읽기(바이너리들[이름])
            if 내것 and 내것 != img["uuid"].upper():
                print(f"⚠️  {이름}의 UUID가 다르다 — 크래시 {img['uuid']} / dSYM {내것}.\n"
                      f"    다른 빌드의 dSYM이다. 빌드 번호를 다시 볼 것.", file=sys.stderr)

    쓰레드 = 보고서.get("threads", [])
    죽은쓰레드 = next((i for i, t in enumerate(쓰레드) if t.get("triggered")), None)
    for i, t in enumerate(쓰레드):
        if 죽은쓰레드 is not None and i != 죽은쓰레드:
            continue   # 죽은 쓰레드만 본다
        print(f"\n=== 쓰레드 {i}{' (여기서 죽었다)' if t.get('triggered') else ''} ===")
        for n, f in enumerate(t.get("frames", [])):
            img = 이미지[f["imageIndex"]] if f.get("imageIndex", -1) < len(이미지) else {}
            이름 = img.get("name", "?")
            오프셋 = f.get("imageOffset", 0)
            if 이름 in 바이너리들:
                심볼 = 풀기(바이너리들[이름], [오프셋])[0]
            else:
                심볼 = f.get("symbol") or f"{이름} + {오프셋}"
            print(f"{n:>3}  {이름:<28} {심볼}")


if __name__ == "__main__":
    main()
