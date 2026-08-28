# 버전 관리 규칙

## 1. 버전 체계

메모짱은 [유의적 버전 2.0.0](https://semver.org/lang/ko/) 을 따릅니다.

```
MAJOR . MINOR . PATCH
  1   .   0   .   0
```

| 자리 | 올리는 때 |
| --- | --- |
| **MAJOR** | 설정 파일 형식이 바뀌어 이전 버전과 호환되지 않을 때, 기본 동작이 크게 달라질 때 |
| **MINOR** | 기능이 추가될 때 (기존 동작은 그대로) |
| **PATCH** | 버그 수정, 문구 수정, 성능 개선 |

사용자에게 보이는 짧은 버전(`ver1.0`)은 `MAJOR.MINOR` 입니다.
창 제목에는 이 짧은 버전이, `도움말 > 메모짱 정보` 에는 전체 버전이 표시됩니다.

## 2. 단일 출처

버전 값은 **`Directory.Build.props` 한 곳**에만 적습니다.

```xml
<VersionPrefix>1.0.0</VersionPrefix>
<DisplayVersion>1.0</DisplayVersion>
```

여기서 다음이 자동으로 파생됩니다.

- `AssemblyVersion` / `FileVersion` / `Version` (`.exe` 속성 탭에 표시)
- `AssemblyMetadata("DisplayVersion")` → `AppInfo.DisplayVersion` → **창 제목**
- `AppInfo.FullVersion` → 정보 대화 상자

> 소스 코드 어디에도 버전 문자열을 직접 적지 않습니다.
> `AppInfo.cs` 는 어셈블리 메타데이터를 읽기만 합니다.

## 3. 브랜치 전략

| 브랜치 | 용도 |
| --- | --- |
| `main` | 항상 빌드되는 상태를 유지 |
| `feature/<이름>` | 기능 작업 |
| `fix/<이름>` | 버그 수정 |

## 4. 커밋 메시지

[Conventional Commits](https://www.conventionalcommits.org/ko/) 형식을 사용합니다.

```
feat(encoding): BOM 없는 UTF-16 추정 로직 추가
fix(save): 저장 실패 시 임시 파일이 남던 문제 수정
docs(readme): 단축키 표 정리
chore(ci): windows-latest 러너로 전환
```

`feat` → MINOR, `fix` → PATCH 로 이어집니다.

## 5. 릴리스 절차

1. `CHANGELOG.md` 의 `[Unreleased]` 항목을 새 버전 항목으로 옮기고 날짜를 적습니다.
2. `Directory.Build.props` 의 `VersionPrefix` / `DisplayVersion` 을 올립니다.
3. 커밋합니다.
   ```powershell
   git commit -am "chore(release): v1.1.0"
   ```
4. 태그를 만들고 밀어 넣습니다.
   ```powershell
   git tag -a v1.1.0 -m "메모짱 v1.1.0"
   git push origin main --follow-tags
   ```
5. GitHub Actions 가 Windows 에서 빌드해
   `MemoJJang-win-x64-framework-dependent.zip` 과
   `MemoJJang-win-x64-self-contained.zip` 을 릴리스에 첨부합니다.

## 6. 설정 파일 호환성

설정은 `%LocalAppData%\MemoJJang\settings.json` 에 저장됩니다.
읽기에 실패하면 예외를 던지지 않고 기본값으로 되돌아가므로,
**필드 추가는 MINOR**, **필드 삭제·의미 변경은 MAJOR** 로 취급합니다.
