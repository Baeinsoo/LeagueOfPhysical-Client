# LookDev — 인게임 룩 개발 (슬라이스 1)

빌드에 안 들어가는 룩 개발 전용 폴더. 스펙: `league-of-physical/docs/superpowers/specs/2026-09-28-lop-look-design.md`.

## 캐릭터 베이스

- PolyOne Studio "Free Pack - Chibi Character" — 에셋 스토어에서 받아 `Assets/Art/PolyOne/Chibi Character/`에 들어 있다
  (Art 서브모듈 안, **아직 커밋 안 됨** — 슬라이스 2에서 Art 레포에 올린다. 그 전엔 다른 PC에서 이 씬의 캐릭터가 빈다).
- 라이선스: Standard Unity Asset Store EULA(게임에 포함 가능).
- 휴머노이드 리그, 키 0.49m(LookDev에서 3배). 눈은 별도 메시 `SM_Chibi_Eye` — 얼굴 판을 쓸 때 끈다.
- 애니메이션 12개: Idle, Walk, Run, Run Backward, Jumping Up/Down, Happy, Sad, Angry, Dying, Pointing Forward.

## 메뉴

- `LOP/LookDev/Build Scene` — `LookDev.unity`를 처음부터 다시 조립한다.
- `LOP/LookDev/Capture` — 카메라 셋(가까이·게임 거리·얼굴 정면)을 `output/lookdev/*.png`로 남긴다.
- `LOP/LookDev/Bake Outfit Regions` — 몸 메시에 옷 영역(뼈 기준 6영역)을 굽는다(빌더가 자동으로 부른다).

## 얼굴 아틀라스

원본은 `Scripts/lookdev/face_atlas.html`(SVG, 스타일 C — 평소 동숲, 사건 때 바람의 지휘봉). 파일 머리 주석의 크롬 명령으로 `Face/FaceAtlas.png`를 다시 뽑는다.
