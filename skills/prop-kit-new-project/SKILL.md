---
name: prop-kit-new-project
description: prop-recipe-kit(Unity 생성기 + 블렌더 헤드리스 키트)를 새 Unity 프로젝트에 붙일 때 쓴다 — 어댑터(재질·예산·임포트 규칙) 작성, 보정용 메시로 축·단위 확인, 첫 소품 시험까지. 「다른 프로젝트에서 써보자」, 「키트 붙여줘」, 「어댑터 만들어줘」.
---

# 새 프로젝트에 붙이기 (prop-recipe-kit)

## 반드시 지킬 것

1. **설치는 미리 보기부터.** `KIT\tools\install.ps1 -Project <경로> -WhatIf`로 무엇이 어디에 생기는지 보여 주고, 사용자가 승인하면 `-WhatIf` 없이 돌린다.
2. **프로젝트의 `Packages/manifest.json`을 바꾸는 것은 사용자 승인 뒤.** 패키지는 `file:` 경로나 프로젝트 `Packages/` 아래 복사로 붙인다(설치 스크립트가 방법을 안내한다).
3. **재질은 그 프로젝트 규칙대로.** 기존 셰이더·재질을 복사해 쓰는 프로젝트면 어댑터에서 그렇게 한다. 키트 기본 재질 공급자는 셰이더 중립(렌더 파이프라인 기본 재질)이다.
4. **축·단위는 보정용 메시로 확인하기 전에 믿지 않는다.**
5. **블렌더 MCP는 선택이다.** 헤드리스 키트만으로 된다. 등록·설치·애드온 갱신은 사용자가 시킬 때만.

## 순서

1. **점검**: `powershell -ExecutionPolicy Bypass -File KIT\tools\check.ps1` — 블렌더·판·Python·uv 유무.
2. **설치 미리 보기**: `KIT\tools\install.ps1 -Project <Unity 프로젝트> -WhatIf`. 스킬 3개를 프로젝트 `.claude\skills\`에 복사(또는 `-Link`로 연결)하고, 각 스킬 폴더에 `kit_root.txt`를 쓴다. Unity 패키지 붙이는 방법을 안내한다.
3. **어댑터 작성**: `KIT\adapters\_template\`을 프로젝트 쪽(예: `Assets/<프로젝트>/PropKitAdapter/Editor/`)에 복사해 채운다.
   - `IPropMaterialProvider`: 재질 키 → 재질(기존 재질 복사·색만 바꾸기 등), 팔레트 재질.
   - `budgets.json`: 소품 크기별 삼각형 예산과 재질 수.
   - `import_rules.md`: FBX 임포트 설정(축 굽기·스케일·탄젠트·재질), 팔레트 텍스처 설정, 앞쪽 규칙.
   - 예시: `KIT\adapters\toon-copy-example\`(툰 셰이더 재질 복사, 예산 600·2,500·5,000).
4. **축·단위 확인**: `KIT\blender\examples\calib_axis.json`(빨강 앞 −Y, 파랑 +X, 노랑 위, 초록 뒤, 1.2×1.1×1.0 m)을 블렌더로 빌드 → FBX를 프로젝트에 가져와 루트 회전·스케일·경계 크기·앞쪽을 잰다. 기존 아트 FBX는 `inspect-fbx`로 관례(노드 회전, UnitScaleFactor)를 비교한다.
5. **첫 소품**: 스킬 `prop-recipe-from-image`로 레시피 하나 → 기본·감량 굽기 → `PropMeasure`·`PropCapture` 비교. FBX가 필요하면 `PropSpecJson.WriteFile` → 스킬 `prop-blender-headless`.
6. **기록**: 프로젝트 문서에 어댑터 결정(재질 방식·예산·축)과 첫 소품 수치를 남긴다.

## 확인할 수치 (보정용 메시)

| 항목 | 기대 |
|---|---|
| 루트 회전·스케일 | 0·1(블렌더 키트 FBX, Unity 기본 임포트) |
| 경계 크기 | 블렌더 (x, y, z) = Unity (x, z, y), ±1 mm |
| 앞쪽 | 블렌더 −Y = Unity +Z |
| 팔레트 색 | 텍스처 설정 후 픽셀이 사양 HEX와 같음 |

다르면 어댑터의 임포트 규칙을 고치고, 키트 내보내기 설정은 바꾸지 않는다(다른 프로젝트와 갈라진다).
