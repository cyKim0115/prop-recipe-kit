---
name: prop-blender-headless
description: 치수 사양(JSON)이나 Unity에서 내보낸 레시피로 블렌더를 헤드리스로 돌려 로우폴리 소품 메시를 만들고 수치 검증한 뒤 FBX·.blend로 내보낼 때 쓴다 — 「블렌더로 소품 FBX 뽑아줘」, 「예산 맞춰 FBX로」, 「아티스트에게 넘길 .blend」, 「블렌더 헤드리스」. MCP·애드온 없이 Claude Code·Codex·Cursor가 같은 명령을 쓴다.
---

# 블렌더 헤드리스 소품 (prop-recipe-kit)

시험적인 도구다. 프로토타이핑·시안 제작용이며 출시용 최종 에셋을 대신하지 않는다(최종 모델은 아티스트 제작을 전제).

키트 루트(`KIT`) = 이 폴더의 `kit_root.txt`에 적힌 경로, 없으면 환경변수 `PROP_RECIPE_KIT`. 명령은 `python KIT/blender/scripts/blender_run.py ...`.
이 키트의 본체는 Unity 생성기 감량이다. 블렌더는 **FBX·.blend 전달과 삼각형 예산을 정확히 맞출 때** 쓴다.

## 반드시 지킬 것

1. **블렌더는 래퍼로만 띄운다.** `blender.exe`를 직접 부르지 않는다. 래퍼가 `--factory-startup --offline-mode --python-exit-code 1`과 임시 사용자 폴더로 사용자 설정·애드온을 막는다.
2. **블렌더 판.** 래퍼는 `--blender` → `BLENDER_EXE` → 표준 설치 위치(최신판) → PATH 순으로 찾는다. 검증한 판은 5.2 LTS다. 다른 판이면 경고가 나오니 결과를 더 꼼꼼히 보고, 판을 고정하려면 `--strict-version`.
3. **좌표 규칙.** 손으로 쓰는 사양: 단위 m, 블렌더 축(Z 위), 앞면 −Y, 파트 `location` = 파트 중심. Unity 레시피(`"frame": "unity"`): Unity 축(Y 위, 앞 +Z) 그대로 두면 키트가 바꾼다.
4. **PASS와 시트 확인 없이 완료라고 하지 않는다.** 종료 코드 0과 `PROP <name>: PASS`를 보고, `<name>_views.webp`를 열어 모양·앞쪽을 본다. 실패면 사양을 고친다. `expect` 기준을 낮춰 통과시키지 않는다.
5. **모르는 bpy API는 설치된 블렌더에 묻는다.** `introspect bpy.ops.export_scene.fbx` / `bpy.types.Mesh` / `bmesh.ops.create_cone`.
6. **MCP·애드온·블렌더 설정을 건드리지 않는다.** 대화형 MCP는 선택이고 이 흐름에 필요 없다.

## 명령

```powershell
python KIT\blender\scripts\blender_run.py version
python KIT\blender\scripts\blender_run.py build <spec.json> --out <폴더> [--no-render] [--no-blend]
python KIT\blender\scripts\blender_run.py introspect <bpy.ops.X.Y | bpy.types.X | bmesh.ops.X>
python KIT\blender\scripts\blender_run.py inspect-fbx <a.fbx> [...]
```

- 종료 코드: 0 통과, 1 검사 실패·Python 오류, 2 사용법·환경 오류. 한 번에 2~4초. 프로세스마다 독립이라 동시에 돌려도 된다(출력 폴더만 다르게).
- 산출물: `<name>.fbx`, `<name>.blend`, `<name>_views.webp`(위: 정면·오른쪽·위 / 아래: 원근·와이어·뒤), `<name>_report.json`, 손 사양이면 `<name>_palette.png`.

## 사양 두 가지

**A. 손으로 쓰는 사양** (`KIT/blender/examples/desk.json`·`lamp.json`·`chair.json`)

| 파트 `type` | 필드 |
|---|---|
| `box` | `size`[x,y,z], `bevel`{`width`,`segments`} |
| `cylinder` | `radius`, `depth`, `segments`, `caps` |
| `cone` | `radius_bottom`, `radius_top`, `depth`, `segments`, `caps` |
| `sphere` | `radius`, `segments`, `rings` |
| `torus` | `major_radius`, `minor_radius`, `segments`, `minor_segments` |

공통 `location`·`rotation`(도)·`material`·`shade`(`flat`|`smooth`). `materials`{키: `#RRGGBB`}, `expect`{`dims_m`, `tolerance_m`, `max_triangles`, `max_materials`, `allow_ngons`}.
둥근 파트는 `segments`를 4의 배수로(X·Y 지름이 모두 2r). 원점은 바닥 가운데로 자동 이동.

**B. Unity 레시피** (`"frame": "unity"`, Unity 패키지 `PropSpecJson`이 씀, 예: `KIT/blender/examples/recipe_stump_stool.json`)

- `parts[]` = {`mat`, `group`, `matrix`(Unity 4×4 행 우선), `gen`}. `gen.type` = `roundbox`·`lathe`·`extrude`·`utorus`.
- `reduce`: `seg_scale`, `seg_min`, `box_seg`, `simplify`(외곽선·단면 Douglas–Peucker, 크기 대각선 비율), `cull_hidden`, `planar_angle_deg`, `target_tris`(넘으면 데시메이트), `single_material`(팔레트 UV + 재질 1개).
- 피벗은 Unity 프리팹 피벗 그대로(바닥만 검사). `expect.dims_m`은 Unity 크기를 (x, z, y)로.

## FBX와 Unity

- 내보내기: `bake_space_transform=True`, `FBX_SCALE_UNITS`, `-Z`/`Y`, `mesh_smooth_type='OFF'`. 정적 단일 메시 전용.
- Unity 기본 임포트로 루트 회전 0·스케일 1·치수 일치(2026-11-25 Unity 6000.6 확인). 블렌더 −Y = Unity +Z. 프로젝트마다 기존 FBX 관례를 `inspect-fbx`로 먼저 본다.
- 정적 소품 임포트: Tangents·Animation·Material Import 끔, 재질은 프로젝트 어댑터 규칙으로 리맵. 팔레트 PNG는 Point·밉맵 끔·무압축·sRGB·Clamp.

## 함정

- 외곽선을 N개마다 솎으면 모서리 점이 빠져 모양이 깨진다 → `simplify`(Douglas–Peucker)를 쓴다.
- 꽃잎 굴곡(`lobes`) 있는 회전체를 너무 적게 나누면 굴곡 끝을 놓쳐 폭이 준다 → 키트가 굴곡 × 4 이상·굴곡의 배수로 올린다.
- 판(extrude) 둥근 테두리는 블렌더 쪽 근사라 Unity 생성기 결과와 실루엣이 조금 다르다(파일럿 의자 IoU 0.963 대 0.986).
- 4.1+: `use_auto_smooth` 없음. 5.0+: `use_nodes` 항상 켜짐, FBX 가져오기는 C++(`wm.fbx_import`), 내보내기는 `export_scene.fbx`.
- 재질 1개(팔레트)로 합치면 셰이더에 따라 색이 원본과 달라진다(그림자 색이 재질마다 다른 툰 셰이더). 어댑터에서 확인한다.
