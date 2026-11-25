# 프로젝트 어댑터

프로젝트마다 다른 것(재질 만드는 법, 삼각형 예산, FBX 임포트 규칙)을 키트 밖에 둡니다.

| 파일 | 내용 |
|---|---|
| `Editor/*MaterialProvider.cs` | `IPropMaterialProvider` 구현. 프로젝트 `Assets/.../Editor/`에 복사(패키지 asmdef는 자동 참조) |
| `budgets.json` | 크기별 삼각형·재질 예산, 감량 프리셋 값 |
| `import_rules.md` | FBX 임포트 설정, 앞쪽 규칙, 팔레트 텍스처 설정, 재질 규칙 |

- `toon-copy-example/`: 툰 셰이더 모바일 게임 예시(원본 툰 재질 복사 + 색만 변경, 예산 600·2,500·5,000, 2026-11-25 보정용 메시로 확인한 축·단위).
- `_template/`: 빈 틀. 채우는 순서는 스킬 `prop-kit-new-project`.
