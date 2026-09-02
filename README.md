# 메모짱 (MemoJJang)

> **메모짱 ver1.0 by Earlln.com** — 심플하면서도 세련된 윈도우 메모장

윈도우 기본 메모장(Notepad)의 익숙한 동작은 그대로 두고,
**탭 편집 · 다크 모드 · 세션 유지** 같은 Windows 11 메모장(윈도우 노트)의 편의 기능을 얹었습니다.
특히 **파일 인코딩 관리**를 정면으로 다룹니다.

![플랫폼](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4)
![런타임](https://img.shields.io/badge/.NET-8.0--windows-512BD4)
![버전](https://img.shields.io/badge/version-1.0.0-2F6FED)

---

## 내려받기 — 설치 필요 없음

[**Releases 페이지**](https://github.com/earlln/memoJJang/releases/latest)에서 `MemoJJang-vX.Y.Z-win-x64.exe` 를 받아
더블클릭하면 바로 실행됩니다. 설치 과정도, 관리자 권한도, .NET 런타임 설치도 필요 없습니다.

| 파일 | 설명 |
| --- | --- |
| `MemoJJang-<버전>-win-x64.exe` | **대부분 이걸 받으세요.** 런타임을 품고 있어 그대로 실행됩니다. |
| `MemoJJang-<버전>-win-x64-runtime-required.exe` | 용량이 작지만 [.NET 8 데스크톱 런타임](https://dotnet.microsoft.com/download/dotnet/8.0/runtime)이 필요합니다. |

요구 사항은 Windows 10(1607) 이상 64비트입니다.
지울 때는 exe 파일만 삭제하면 되고, 설정까지 지우려면 `%LocalAppData%\MemoJJang\` 폴더도 함께 삭제하세요.

> 코드 서명을 하지 않아 첫 실행 시 SmartScreen 경고가 뜰 수 있습니다. `추가 정보` → `실행`을 누르세요.

---

## 화면 구성

```
┌────────────────────────────────────────────────────────────┐
│ 파일  편집  서식  보기  도움말                        🌙  │  ← 메뉴 막대
├────────────────────────────────────────────────────────────┤
│ ● 메모.txt  ×  │ 제목 없음 2  ×  │                         │  ← 탭
├────────────────────────────────────────────────────────────┤
│ 찾기 [________]  ∧ ∨  Aa ab ↻   3 / 12              ×     │  ← 찾기/바꾸기 막대
├────────────────────────────────────────────────────────────┤
│                                                            │
│  본문 편집 영역                                            │
│                                                            │
├────────────────────────────────────────────────────────────┤
│ 줄 12, 열 5   1,204자 · 210단어      100%  CRLF  UTF-8     │  ← 상태 표시줄
└────────────────────────────────────────────────────────────┘
```

창 제목은 항상 `<파일 이름> - 메모짱 ver1.0 by Earlln.com` 형식이며,
저장하지 않은 변경이 있으면 앞에 `*` 가 붙습니다.

---

## 기능

### 기본 메모장과 동일한 것

| 메뉴 | 기능 |
| --- | --- |
| 파일 | 새로 만들기 · 열기 · 저장 · 다른 이름으로 저장 · 인쇄 · 끝내기 |
| 편집 | 실행 취소/다시 실행 · 잘라내기/복사/붙여넣기/삭제 · 찾기 · 다음 찾기 · 바꾸기 · 이동 · 모두 선택 · 시간/날짜(F5) |
| 서식 | 자동 줄 바꿈 · 글꼴 |
| 보기 | 확대/축소 · 상태 표시줄 |
| 도움말 | 메모짱 정보 |

### 여기에 더한 것 (윈도우 노트 계열)

- **탭 편집** — 한 창에서 여러 파일. 탭마다 실행 취소 기록과 캐럿 위치가 따로 유지됩니다.
- **다크 / 라이트 / 시스템 테마** — 메뉴·팝업·스크롤바까지 전부 테마를 따릅니다.
- **세션 유지** — 저장하지 않은 내용까지 포함해 종료 시 탭을 그대로 보관했다가 다음 실행에서 복원합니다.
- **인라인 찾기·바꾸기 막대** — 별도 창 없이 상단에 붙습니다. 대/소문자 구분, 단어 단위, 순환 검색, `3 / 12` 형식의 진행 표시.
- **글자 수 / 단어 수 / 선택 글자 수** 실시간 표시.
- **끌어다 놓기로 열기** — 탐색기에서 파일을 창 어디에 놓아도 새 탭으로 열립니다.
  **폴더를 놓으면** 그 위치에서 시작하는 열기 창이 뜹니다. 놓는 동안 무엇이 열릴지 안내가 표시됩니다.
- **최근 파일 목록**(최대 10개).
- **줄 바꿈 형식(CRLF / LF / CR)** 표시 및 변환.

### 인코딩 관리 (핵심)

메모짱은 인코딩을 "알아서 처리"하지 않고 **항상 보이게** 합니다.

- **자동 감지** — BOM(UTF-8 / UTF-16 LE·BE / UTF-32 LE·BE) → BOM 없는 UTF-16 통계 추정 → 엄격한 UTF-8 유효성 검사 → 시스템 ANSI 코드 페이지 순서로 판별합니다.
- **감지 근거 표시** — 상태 표시줄에 `(UTF-8 BOM)`, `(순수 ASCII)` 처럼 왜 그렇게 판단했는지 보여 줍니다.
- **BOM 유무를 분리해서 취급** — `UTF-8` 과 `UTF-8 (BOM 포함)` 은 별개의 선택지입니다.
- **한국어/일본어/중국어 레거시 인코딩 지원** — CP949(EUC-KR), CP932(Shift-JIS), GB18030, Big5, Windows-1252, ISO-8859-1.
  (.NET 의 `CodePagesEncodingProvider` 를 등록해 사용합니다.)
- **다른 인코딩으로 다시 열기** — 자동 감지가 틀려 글자가 깨졌을 때 원본 바이트에서 다시 디코딩합니다.
- **저장 전 손실 검사** — 선택한 인코딩으로 표현할 수 없는 문자가 있으면 어떤 문자인지 알려 주고 UTF-8 저장을 권합니다.
- **원자적 저장** — 임시 파일에 먼저 기록한 뒤 교체하므로, 저장 도중 오류가 나도 원본이 손상되지 않습니다.
- **새 문서 기본 인코딩 지정** — 현재 문서의 인코딩을 새 문서 기본값으로 저장할 수 있습니다.

> 내부적으로는 항상 `CRLF` 로 정규화해 편집하고, 저장 시점에 문서에 지정된 줄 바꿈 형식으로 되돌립니다.
> 자세한 규칙은 [docs/ENCODING.md](docs/ENCODING.md) 를 참고하세요.

---

## 단축키

| 단축키 | 기능 | | 단축키 | 기능 |
| --- | --- | --- | --- | --- |
| `Ctrl+N` / `Ctrl+T` | 새 탭 | | `Ctrl+F` | 찾기 |
| `Ctrl+Shift+N` | 새 창 | | `F3` / `Shift+F3` | 다음 / 이전 찾기 |
| `Ctrl+O` | 열기 | | `Ctrl+H` | 바꾸기 |
| `Ctrl+S` | 저장 | | `Ctrl+G` | 줄 이동 |
| `Ctrl+Shift+S` | 다른 이름으로 저장 | | `Ctrl+A` | 모두 선택 |
| `Ctrl+W` | 탭 닫기 | | `F5` | 시간/날짜 삽입 |
| `Ctrl+P` | 인쇄 | | `Ctrl` `+` / `-` / `0` | 확대 / 축소 / 원래대로 |
| `Ctrl+Z` / `Ctrl+Y` | 실행 취소 / 다시 실행 | | `Esc` | 찾기 막대 닫기 |

---

## 빌드

필요한 것: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows)

```powershell
git clone https://github.com/earlln/memoJJang.git
cd memoJJang

dotnet build MemoJJang.sln -c Release
dotnet run --project src/MemoJJang/MemoJJang.csproj
```

### 배포용 단일 실행 파일 만들기

```powershell
# .NET 8 런타임이 설치된 PC 용 (파일 크기 작음)
dotnet publish src/MemoJJang/MemoJJang.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish/framework-dependent

# 런타임 없이 바로 실행 (파일 크기 큼)
dotnet publish src/MemoJJang/MemoJJang.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/self-contained
```

> Linux/macOS 에서도 `EnableWindowsTargeting` 옵션 덕분에 컴파일 검증은 가능합니다. 실행은 Windows 에서만 됩니다.

GitHub Actions(`.github/workflows/build.yml`)가 푸시마다 Windows 에서 빌드하고,
`v*` 태그를 밀면 두 가지 형태의 zip 을 릴리스에 자동으로 첨부합니다.

---

## 프로젝트 구조

```
memoJJang/
├─ Directory.Build.props        ← 버전 단일 출처
├─ MemoJJang.sln
├─ CHANGELOG.md
├─ docs/
│  ├─ VERSIONING.md             ← 버전 관리 규칙 / 릴리스 절차
│  └─ ENCODING.md               ← 인코딩 감지·저장 규칙 상세
└─ src/MemoJJang/
   ├─ AppInfo.cs                ← 제품명 / 버전 / 창 제목 문자열
   ├─ App.xaml(.cs)
   ├─ MainWindow.xaml(.cs)      ← 셸, 탭, 파일 입출력, 메뉴
   ├─ MainWindow.Search.cs      ← 찾기 / 바꾸기 / 줄 이동
   ├─ Models/                   ← DocumentTab, EncodingOption, LineEndingKind
   ├─ Services/                 ← 인코딩 감지, 파일 입출력, 설정, 세션, 테마
   ├─ Dialogs/                  ← 글꼴 · 인코딩 선택 · 줄 이동 · 정보
   └─ Themes/                   ← Light / Dark 팔레트, 공통 컨트롤 스타일
```

설정과 세션은 `%LocalAppData%\MemoJJang\` 에 저장됩니다.

---

## 라이선스

[MIT](LICENSE) © Earlln.com
