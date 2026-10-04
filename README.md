# 13번째 종: 잊힌 선물

C# WinForms로 제작한 Windows x64용 방탈출 게임입니다.

## 실행

배포본은 `dist\ThirteenthBell-win-x64.zip`의 압축을 완전히 푼 뒤 `ThirteenthBell.exe`를 실행합니다. 배포본에는 .NET 런타임이 포함됩니다. 원화, 글꼴, 음악 파일을 찾을 수 있도록 압축 파일의 폴더 구조를 유지해야 합니다.

소스에서 실행하려면 Windows와 .NET 10 SDK가 필요합니다.

```powershell
dotnet restore .\ThirteenthBell.sln
dotnet run --project .\src\ThirteenthBell\ThirteenthBell.csproj -c Release
```

게임은 전체 화면으로 시작합니다. `F11`로 전체 화면과 창 모드를 전환할 수 있습니다.

메인 메뉴는 눈이 쌓인 크리스마스 마을의 항공 뷰를 배경으로 표시합니다. 메뉴의 큰 시계 로고는 제거했습니다. 배경은 이미지 생성으로 제작한 `Assets\christmas-village-aerial.png`를 사용하며, 생성 프롬프트는 `Assets\MENU-ART.md`에 기록했습니다. 우편실과 공방의 배경 조사 설명은 존댓말로 표시합니다.

새 게임은 ‘수취인 없는 우편실’에서 시작합니다. 가방과 선물 상자를 클릭하거나 끌어 옮겨 황동 열쇠를 찾고, 담요의 종 기록과 잠긴 서랍의 배송 기록을 조사합니다. 다섯 장소의 배송 순서와 여섯 번의 종 울림을 해결한 뒤 열쇠로 문을 열면 기존 공방으로 이어집니다.

우편실 도입부와 우편실의 모든 조사 화면에서는 사용자 제공 `first_game-bgmusic.mp3`를 반복 재생합니다. 공방으로 들어가면 기존 `game-bgmusic.mp3`로 부드럽게 전환됩니다. 원본 파일은 변경 없이 `src\ThirteenthBell\Assets\Music`에 포함했습니다.

우편실의 발견물, 짐의 위치, 두 봉인과 문 상태도 진행 백업에 저장됩니다. 추가 이전의 백업은 기존 공방 진행을 유지합니다. 새 원화 네 장과 생성 프롬프트는 `src\ThirteenthBell\Assets\POSTAL-ART.md`에 기록했습니다. 이야기와 퍼즐은 프로젝트용 창작물이며 외부 출처 없음.

## 빌드와 배포

```powershell
dotnet build .\ThirteenthBell.sln -c Release
dotnet publish .\src\ThirteenthBell\ThirteenthBell.csproj -c Release -r win-x64 --self-contained true -o .\dist\ThirteenthBell
Compress-Archive -Path .\dist\ThirteenthBell\* -DestinationPath .\dist\ThirteenthBell-win-x64.zip -Force
```

## 프로젝트 구조

- `src\ThirteenthBell`: WinForms 화면, 오디오, 온라인 순위, 게임 자산
- `src\ThirteenthBell.Core`: 퍼즐 판정, 게임 상태, 사용자 데이터와 진행 백업
- `supabase`: 온라인 닉네임과 순위용 SQL 마이그레이션, Edge Function
- `tools\AudioAssetBuilder`: CC0 원본 효과음을 게임용 WAV로 변환하는 도구
- `tools\BuildLogoAssets.ps1`: 원본 PNG에서 게임 로고 PNG와 다중 크기 ICO를 만드는 도구
- `tools\PostalRoomSmoke`: 진행, 저장 복구, 화면, 음악과 회귀 검사
- `tools\LeaderboardSmoke.ts`: 운영 서버에 접속하지 않는 서버 요청 검사

WinForms 화면은 디자이너 파일 없이 코드에서 구성합니다. 기준 해상도는 1400×820이며 현재 창에 맞춰 등비 확대 또는 축소합니다.

이야기 상자와 문서 상자는 실제 글자 크기를 측정해 내용에 맞춰 배치합니다. 글꼴과 버튼, 입력란은 더 작게 표시하며, 짧은 안내는 작은 이야기 상자에 담습니다. 공통 힌트 버튼과 F1은 대기 없이 사용할 수 있습니다. 현재 방의 미확인 장소를 먼저 안내하고 모두 조사한 뒤에는 미해결 문제의 작은 힌트를 제공합니다. 조사 이력도 진행 백업에 저장합니다. 문제 화면의 해법과 정답을 알려주는 추가 설명은 제거했습니다. 두 결말에서 메인 메뉴로 돌아갈 수 있습니다.

글자 측정과 투명 클릭 영역의 배경 처리에 참고한 Microsoft 공식 문서 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/ko-kr/dotnet/api/system.windows.forms.textrenderer.measuretext?view=windowsdesktop-10.0

https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-give-your-control-a-transparent-background

## 스토리와 장식 계산식

우편실의 도입 기록부터 편지, 기억 조각, 마지막 선택과 두 결말까지 마지막 선물의 수취인은 노엘 애스터로 이어집니다. 마리 벨은 시계공으로서 기록을 남기고, 엘리아스는 마지막 선물을 지키는 배달부입니다. 책상의 세 기억은 굴뚝 통로, 자정, 북쪽 하늘의 오로라를 알려 주며 마지막 배달로 연결됩니다.

장식 계산식은 눈사람, 트리, 선물의 이모티콘으로 표시합니다. 식에는 Windows의 Segoe UI Emoji 글꼴을 사용하며, 창 크기를 바꾸거나 글씨 크기를 자동 조절할 때도 이 글꼴을 유지합니다. 화면 읽기 프로그램에는 그림 이름으로 식을 설명합니다. 스노글로브는 세 장식의 값을 함께 구하는 연립방정식으로, 편지는 바늘땀 수에 따라 글자를 고른 뒤 조각 번호순으로 읽는 문제로 구성했습니다. 계산판 정답은 44, 편지 정답은 CHIMNEY입니다. 기억 조각의 숫자와 최종 별시계 설정, 완료 화면의 돌아가기 동작은 유지합니다.

글꼴 자료 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/ko-kr/windows/apps/design/signature-experiences/typography

https://learn.microsoft.com/en-us/typography/font-list/segoe-ui-emoji

## 사용자 데이터

실행 중 생성되는 파일은 `%LOCALAPPDATA%\ThirteenthBell`에 저장됩니다.

- `player-data.json`: 최초 안내 확인 여부, 닉네임, 온라인 소유 토큰
- `progress-backup.json`: 끝나지 않은 게임의 일회용 복구 데이터
- `Logs\error.log`: 처리되지 않은 오류 기록

한 판이 결말에 도달하면 진행 백업과 해당 판의 임시 메모 상태를 삭제합니다.

## 우편실 검증

다음 명령은 별도 사용자 데이터와 오프라인 순위 대체 서비스를 사용해 열쇠 탐색, 클릭과 드래그, 봉인 판정, 공방 진입, 백업 복원과 이전 백업 호환을 확인하고 실제 WinForms 컨트롤의 렌더링 캡처를 저장합니다.

```powershell
dotnet run --project .\tools\PostalRoomSmoke\PostalRoomSmoke.csproj -c Release -- .\artifacts\postal-smoke
```

텍스트 배치, 투명 클릭 영역, 공통 힌트, 조사 이력 복원, 결말 복귀, 닉네임 확인 화면의 성공과 중복, 오류 처리를 검증하는 명령입니다. 실제 온라인 계정을 생성하지 않는 테스트 서비스를 사용합니다.

```powershell
dotnet run --project .\tools\PostalRoomSmoke\PostalRoomSmoke.csproj -c Release -- .\artifacts\ui-refinement --requirements
```

전체 게임 진행과 두 결말, 저장 및 복구, 확인창 단축키를 검사하거나 잘못된 온라인 응답과 반복 오디오를 검사할 수 있습니다.

```powershell
dotnet run --project .\tools\PostalRoomSmoke -c Release -- .\artifacts\comprehensive --comprehensive
dotnet run --project .\tools\PostalRoomSmoke -c Release -- .\artifacts\regression --regression
```

Deno가 설치되어 있다면 서버의 요청과 RPC 응답 검증을 네트워크 권한 없이 실행할 수 있습니다.

```powershell
deno check .\supabase\functions\thirteenth-bell-leaderboard\index.ts .\tools\LeaderboardSmoke.ts
deno run --allow-env --allow-read .\tools\LeaderboardSmoke.ts
```

확인창에서 Esc는 확인창만 닫으며 F1과 Ctrl+N은 뒤쪽 게임 상태를 변경하지 않습니다. 같은 배경을 다시 표시할 때는 크기가 바뀌지 않았다면 캐시를 유지합니다. 사용자 데이터와 진행 백업은 공통 임시 파일 저장 처리로 기록합니다. 빌드와 게시 결과에는 프로젝트 LICENSE도 포함합니다.

마우스 입력 구현에 참고한 Microsoft 공식 문서 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/en-us/dotnet/desktop/winforms/input-mouse/events

## 온라인 순위 설정

`src\ThirteenthBell\Assets\online-config.json`에 Supabase URL과 publishable key, Edge Function 이름을 설정합니다. 관리자 키를 클라이언트에 넣으면 안 됩니다. 서버 구성은 `supabase\migrations`와 `supabase\functions`를 사용합니다.

닉네임 중복이면 소유자가 같은 경우에도 새 게임을 시작하지 않습니다. 중복 입력은 지우고 입력란으로 포커스를 돌립니다. 서버의 등록 승인과 요청한 이름이 일치할 때만 시작합니다. 신규 서버 구성에서는 기존 초기 마이그레이션 뒤에 `supabase\strict-nickname.sql`을 적용합니다. 이 SQL은 기존 테이블이나 사용자 데이터를 삭제하지 않고 예약 함수의 중복 판정 기준을 갱신합니다.

Supabase 보안 문서:

https://supabase.com/docs/guides/database/secure-data

https://supabase.com/docs/guides/database/postgres/row-level-security

https://supabase.com/docs/guides/functions

## 자산 제작

시작할 때 모든 배경의 로딩 완료를 기다리는 대신 나머지 장면을 배경 작업에서 미리 읽습니다. 원본 PNG는 수정하지 않고 메모리에서 화면용 비트맵을 만들며, 창 크기별 배경을 캐시합니다. 클릭 영역도 캐시된 배경의 같은 위치를 직접 그립니다. 화면 전환은 배경만 사용하며 전환 이미지의 픽셀 좌표를 다시 축소하지 않습니다. 여러 텍스트 변경으로 발생한 레이아웃 요청은 하나로 합칩니다. 음악 파일과 출력 장치 준비도 배경 작업에서 수행합니다.

실행 환경에서 시작 시간과 화면 캡처 비용을 측정하는 명령입니다. 시작 시간은 폼 구성 시작부터 제목 표시 완료까지이며 효과음과 음악 재생은 끈 상태입니다. 화면 캡처 비용은 실제 디스플레이의 FPS와 다른 측정값입니다.

```powershell
dotnet run --project .\tools\PostalRoomSmoke\PostalRoomSmoke.csproj -c Release -- .\artifacts\loading --performance
```

한국어와 영어 검색으로 확인한 Microsoft 문서에서는 투명 배경이 부모의 배경 그리기에 의존하며, Button의 Transparent 설정만으로는 효과가 없다고 설명합니다. 따라서 클릭 영역을 명시적으로 배경 이미지로 채웁니다. 전체 컨트롤 캡처 대신 배경 캐시를 쓰는 방식은 DrawToBitmap의 컨테이너 순서 제한도 피합니다.

https://learn.microsoft.com/en-us/dotnet/desktop/winforms/controls/how-to-give-your-control-a-transparent-background

https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.drawtobitmap?view=windowsdesktop-10.0

https://learn.microsoft.com/en-us/archive/msdn-magazine/2006/march/practical-tips-for-boosting-the-performance-of-windows-forms-apps

장면 PNG, 로고 PNG, 앱 아이콘, Noto Serif KR 글꼴, 배경 음악, WAV 효과음은 `src\ThirteenthBell\Assets`에 둡니다. 프로젝트 파일이 빌드 출력으로 자산을 복사합니다.

새 로고 PNG를 적용하는 명령은 다음과 같습니다.

```powershell
.\tools\BuildLogoAssets.ps1 -InputPng .\new-logo.png -AssetsDirectory .\src\ThirteenthBell\Assets
```

글꼴과 효과음 라이선스는 다음 파일에 보존합니다.

- `src\ThirteenthBell\Assets\Fonts\OFL.txt`
- `src\ThirteenthBell\Assets\Sounds\LICENSE-KENNEY.txt`
- `src\ThirteenthBell\Assets\Sounds\SOURCES.md`

NAudio 공식 자료:

https://github.com/naudio/NAudio

https://naudio.github.io/NAudio/

Noto Serif CJK 공식 자료:

https://github.com/notofonts/noto-cjk/tree/main/Serif

Kenney Interface Sounds 공식 자료:

https://kenney.nl/assets/interface-sounds

https://creativecommons.org/publicdomain/zero/1.0/
