# prop-recipe-kit (가칭)

코드 레시피로 로우폴리 소품·가구를 만들고, 감량하고, 필요하면 블렌더로 FBX까지 뽑는 도구 모음입니다.
한 게임 프로토타입(가구 193개)에서 쓰던 것을 프로젝트에 묶이지 않게 꺼냈습니다.

- **본체: Unity 생성기** (`unity/com.proprecipe.kit`, 에디터 전용 UPM 패키지) — 도형 생성(`RoundBox`·`Lathe`·`Extrude`·`Torus`), 부품 조립 DSL, 감량 값(`PropLod`)·묻힌 면 제거, 사양 JSON 입출력, 측정·촬영 도구.
- **보조: 블렌더 헤드리스 키트** (`blender/`) — 사양을 블렌더 5.2에서 다시 만들어 FBX·.blend로 내보내고, 삼각형 예산을 정확히 맞출 때 씁니다. MCP·애드온이 필요 없습니다.
- **프로젝트 어댑터** (`adapters/`) — 재질 만드는 법·예산·임포트 규칙처럼 프로젝트마다 다른 부분. `toon-copy-example/`은 예시, `_template/`은 빈 틀.
- **에이전트 스킬** (`skills/`) — 블렌더 헤드리스 / 시안 → 레시피 / 새 프로젝트에 붙이기.
- **도구** (`tools/`) — `check.ps1`(점검), `install.ps1`(프로젝트에 붙이기, `-WhatIf` 지원).

## 3분 사용법

```powershell
# 1) 점검: 블렌더(판)·Python·uv
powershell -ExecutionPolicy Bypass -File tools\check.ps1

# 2) 블렌더 쪽 예시 하나 (블렌더가 없으면 이 단계는 건너뛰어도 Unity 쪽은 됨)
python blender\scripts\blender_run.py build blender\examples\chair.json --out out\chair
#    -> PROP SM_Test_Chair: PASS ... / out\chair\SM_Test_Chair_views.webp, .fbx, .blend

# 3) 프로젝트에 붙이기: 무엇이 생기는지 먼저 보기
powershell -ExecutionPolicy Bypass -File tools\install.ps1 -Project D:\repo\MyGame -WhatIf
```

Unity 쪽 최소 예(에디터 스크립트):

```csharp
using System.Collections.Generic;
using PropRecipe;
using UnityEngine;

var parts = new PropParts()
    .Box("wood", new Vector3(0f, 0.44f, 0f), new Vector3(0.6f, 0.1f, 0.58f), 0.05f)   // 좌석
    .Cyl("cushion", new Vector3(0f, 0.49f, 0f), 0.22f, 0.06f);                         // 방석
foreach (float x in new[] { -0.21f, 0.21f })
    foreach (float z in new[] { -0.2f, 0.2f })
        parts.Box("wood", new Vector3(x, 0.21f, z), new Vector3(0.1f, 0.42f, 0.1f), 0.035f); // 다리
var palette = new Dictionary<string, PropPaletteEntry>
{
    { "wood", PropPaletteEntry.FromHex("D98A55") },
    { "cushion", PropPaletteEntry.FromHex("8CC63F") },
};
PropSpec spec = parts.ToSpec("stool", palette);
Mesh full = PropBaker.Bake(spec, new PropLod(), PropBakeMode.PerMaterial, out _);          // 4,052△
Mesh reduced = PropBaker.Bake(spec, PropLod.Reduced(), PropBakeMode.PerMaterial, out var report); // 636△
Debug.Log(PropMeasure.Measure(reduced).ToJson("reduced"));      // 삼각형·정점·재질·해시
PropSpecJson.WriteFile(spec, "out/stool.unity_recipe.json");    // 블렌더로 넘길 때(reduce·expect를 붙여 빌드)
```

(재질·촬영·팔레트까지 쓰는 예와 API 표는 `unity/com.proprecipe.kit/README.md`. 위 예의 삼각형 수는 2026-11-25 Unity 6000.6 실측.)

## 새 프로젝트에서 시험하는 순서

1. `tools\check.ps1`로 점검.
2. `tools\install.ps1 -Project <경로> -WhatIf` → 확인 뒤 `-WhatIf` 없이(스킬 복사). Unity 패키지는 안내대로 `manifest.json`에 `file:` 경로를 넣거나 `-CopyPackage`.
3. `adapters\_template`을 프로젝트에 복사해 재질 공급자·예산·임포트 규칙을 채움(`adapters\toon-copy-example` 참고).
4. `blender\examples\calib_axis.json`을 빌드해 FBX를 가져와 축·단위·앞쪽 확인(스킬 `prop-kit-new-project`).
5. 소품 하나를 레시피로 만들어 기본·감량을 비교(스킬 `prop-recipe-from-image`).

## 검증 기록 (2026-11-25, Windows, Unity 6000.6, 블렌더 5.2 LTS)

- 파일럿 가구 3개(의자·그루터기 의자·크리스마스 트리): 생성기 감량으로 삼각형 72~92% 감소, 실루엣 IoU 0.986~0.991, 두 번 구워 해시 동일. 블렌더 경로는 예산 정확히 맞춤(트리 38,228 → 4,899)·재질 1개, 의자 IoU 0.963.
- 이 저장소로 옮긴 뒤 다시 확인한 결과는 `CHANGELOG.md` 0.1.0 항목.

## 한계

- Windows에서만 검증했습니다(블렌더 찾기는 macOS·Linux 위치도 보지만 시험하지 않음).
- 블렌더 쪽 판(extrude) 둥근 테두리는 근사라 Unity 생성기와 실루엣이 조금 다릅니다.
- 재질을 하나로 합치는 팔레트 방식은 셰이더에 따라 색이 달라집니다(툰 셰이더 예: ΔE 14~20). 어댑터에서 재고 정하세요.
- 생성기 감량에는 목표 삼각형에 정확히 맞추는 수단이 없습니다(블렌더 `target_tris`로 맞춤).
- 라이선스·원격 저장소 위치는 정해지지 않았습니다.
