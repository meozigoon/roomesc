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

메인 메뉴와 시작 제목 뒤에는 크기, 속도와 투명도가 다른 눈송이 120개가 내려옵니다. 눈송이 지름은 최초 눈 효과보다 45% 커졌으며 좌우로 조금 흔들리고, 알파값 48 / 78 / 112로 배경과 섞입니다. 눈을 그리는 별도 컨트롤을 버튼 위에 올리지 않으므로 메뉴 클릭을 가리지 않습니다. 게임으로 들어가거나 메뉴가 숨겨지면 눈 타이머가 멈춥니다. 최소화 중에는 눈의 이동과 다시 그리기를 건너뜁니다.

메인 배경 디코딩은 화면 구성과 병행하며 우편실, 공방 순서로 다른 배경을 미리 준비합니다. 시작 제목과 제작자 표시는 기존 4초를 유지합니다. 제목의 글자와 장식은 한 번 그린 비트맵을 재사용하고 눈이 움직일 때 버튼과 순위 표를 반복해서 다시 그리지 않습니다. 지연된 프레임 뒤에는 눈이 갑자기 먼 거리를 이동하지 않도록 이동 시간을 제한합니다.

눈의 투명도와 이동, 시작 중 화면 응답과 세 창 크기의 배경 그리기 비용을 확인하는 명령:

```powershell
dotnet run --project tools\PostalRoomSmoke -c Release -- artifacts\snow-loading --snow-loading
```

이중 버퍼링과 화면 스레드 작업 분리에 참고한 공식 문서 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/ko-kr/dotnet/desktop/winforms/advanced/how-to-reduce-graphics-flicker-with-double-buffering-for-forms-and-controls

https://learn.microsoft.com/ko-kr/dotnet/desktop/winforms/controls/how-to-make-thread-safe-calls

https://learn.microsoft.com/en-us/dotnet/desktop/winforms/advanced/using-double-buffering

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

이야기 상자와 문서 상자는 실제 글자를 측정해 내용에 맞춰 배치합니다. 퍼즐 지시문은 계속 표시되며 힌트와 오답 메시지는 지시문 아래에 추가됩니다. 돌아가기 버튼과 힌트 버튼은 상단 메뉴를 열지 않아도 볼 수 있습니다. 힌트는 현재 장면에서 가능한 미완료 작업만 안내하고, 그 장면의 작업이 끝나면 ‘제공할 힌트가 없습니다’를 표시합니다. 영어 답안은 대문자로 입력하며 한글 입력과 붙여넣기를 막습니다. 닉네임에는 기존과 같이 한글을 사용할 수 있습니다. 획득한 기억의 박스는 실제 내용에 맞춰 크기를 조절합니다.

공방 전체 화면과 책상 확대 화면은 같은 짙은 호두나무 책상과 장난감 선반을 사용합니다. 서리 낀 황동 금고는 책상 아래 바닥에 있으며 공방 전체 화면에서 누르면 금고 문제를 엽니다. 책상 확대는 이전 원본처럼 책상 위만 가까이 보여 주며 편지, 별자리 도면과 펜이 놓인 종이의 클릭 영역도 원본 그림에 맞췄습니다. 펜이 놓인 종이를 누르면 제작자 정보와 게임당 한 번의 힌트 1개 보상을 받습니다. 보상 여부는 진행 복구에도 유지됩니다. 양말 문제 배경에는 룰렛이 없고, 양말과 지시문에는 노랑(1), 초록(2), 빨강(3), 파랑(4)을 함께 표시합니다. 관련 원화와 나가기 배경의 생성 프롬프트는 `src\ThirteenthBell\Assets\UI-ART.md`에 기록했습니다. 편지는 실제 문서 이미지를 봉투에서 꺼낸 뒤 펼치는 방식으로 표시하며 시작 편지는 상단 상태 표시와 겹치지 않습니다.

바늘땀 문제는 조각 번호, 영어 단어, 바늘땀 수를 같은 간격의 열에 표시합니다. 게임 방법은 별도 제목과 두 문단의 본문으로 구성합니다. 게임 종료와 진행 포기 화면은 눈 덮인 마을의 새 배경을 사용합니다. 온라인 순위는 등수, 닉네임, 시간 / 결과를 고정된 열에 표시하며 긴 닉네임은 `...`으로 줄여 표시합니다.

시작 화면의 제목과 제작자는 4초 동안 표시된 후 사라집니다. 다음 행동을 알려 주는 설명과 발견한 단서, 실제 힌트는 7초 동안 표시됩니다. 일반적인 배경 반응은 기존 3초를 유지하고, 퍼즐 지시문은 계속 표시합니다. 발견한 물건과 기록은 시간 아래 오른쪽에 정렬됩니다.

시간은 시작 편지에서 흐르지 않으며 처음 우편실에 들어올 때부터 측정합니다. 복구를 위해 게임을 닫은 시간도 제외합니다. 힌트는 처음 1개이며 실제 플레이 시간이 3분 지날 때마다 1개씩 충전됩니다. 사용하지 않은 힌트는 누적되고, 복구 시 충전과 사용 내역도 이어집니다. 할 일이 없는 장면에서는 힌트를 차감하지 않습니다. 오답은 10회까지 계속 도전할 수 있으며 11번째 오답에서 클리어 실패로 끝납니다. 우편실의 종 문제를 맞추면 완료 종소리가 한 번 울리며 다시 확인해도 반복하지 않습니다.

메인 메뉴의 온라인 순위는 상위 5개만 표시합니다. 같은 클리어 시간(밀리초)이면 실패 횟수와 사용한 힌트 수의 합이 적은 기록을 먼저 표시합니다. 합도 같으면 완료 시각과 닉네임 순으로 정렬합니다. 힌트 보상 획득 자체는 사용 횟수에 포함하지 않습니다. ‘순위 확인’ 버튼으로 들어가면 완료 기록을 분할 조회한 스크롤 목록이 표시됩니다. 기록의 체크박스를 개별 선택하거나 ‘전체 선택’, ‘선택 해제’를 사용할 수 있습니다. ‘선택 기록 삭제’를 누르면 비밀번호 입력과 삭제 확인 창이 열립니다. 한 번에 최대 1,000개를 삭제하며 비밀번호가 맞을 때만 서버에서 삭제합니다. 성공 후 목록과 순위를 다시 불러옵니다. 삭제한 기록은 복구할 수 없으며 삭제한 기록의 닉네임은 다시 예약할 수 있습니다. 실패 기록의 시간 칸은 ‘실패’이며, 모든 실패 기록은 성공 기록 뒤에 배치되어 공동 최하위 순위를 받습니다. 진행 포기, 새 게임으로 이동, 백업 없이 종료, 복구 거부도 실패로 기록합니다. 백업 후 종료하여 복구하는 경우에는 실패로 기록하지 않습니다. 인터넷 오류가 있으면 실패 전송을 로컬에 보관하고 30초마다 또는 다음 실행에서 재시도합니다.

서버에는 `supabase/migrations/20261005063453_failed_run_rankings.sql`과 갱신된 Edge Function이 필요합니다. 이번 수정에서는 연결된 프로젝트에 두 변경을 적용하고 실제 온라인 등록과 공개 목록 조회까지 확인했습니다. 새로운 판마다 소유 토큰을 새로 발급하여 이전 성공과 실패 기록을 보존합니다.

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

- `player-data.json`: 최초 안내 확인 여부, 닉네임, 온라인 소유 토큰, 실패 전송 대기 목록과 마지막 실패 판의 식별자
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

터치 위치, 퍼즐 지시문 유지, 한글 차단과 대문자 변환, 클립보드 붙여넣기, 장면별 힌트, 순위 전체 조회와 편지 애니메이션을 검증하는 명령입니다.

```powershell
dotnet run --project .\tools\PostalRoomSmoke -c Release -- .\artifacts\ui-refinement --ui-refinement
```

입력 및 전체 순위 조회에 참고한 공식 문서 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/ko-kr/dotnet/api/system.windows.forms.control.imemode?view=windowsdesktop-10.0

https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.textbox.charactercasing?view=windowsdesktop-10.0

https://supabase.com/docs/guides/api

https://supabase.com/docs/reference/javascript/using-modifiers-range

## 온라인 순위 설정

이번 플레이 시간, 제작자 보상, 양말 번호와 종 완료 효과음의 회귀 검사:

```powershell
dotnet run --project tools\PostalRoomSmoke -c Release -- artifacts\gameplay-refinement --gameplay-refinement
```

동점 순위는 `supabase\tests\run-statistics.sql`에서 트랜잭션 후 롤백으로 검사합니다. 실제 서버 요청을 확인하는 `tools\OnlineStatisticsSmoke.ps1`은 검증용 기록 3개를 만들므로 결과 JSON의 식별자에 해당하는 기록만 검사 뒤 정리해야 합니다.

타이머와 동점 정렬에 참고한 공식 문서 (한국어와 영어 검색으로 확인):

https://learn.microsoft.com/ko-kr/dotnet/api/system.diagnostics.stopwatch.start?view=net-10.0

https://www.postgresql.org/docs/17/queries-order.html

https://www.postgresql.org/docs/17/ddl-generated-columns.html

`src\ThirteenthBell\Assets\online-config.json`에 Supabase URL과 publishable key, Edge Function 이름을 설정합니다. 관리자 키를 클라이언트에 넣으면 안 됩니다. 서버 구성은 `supabase\migrations`와 `supabase\functions`를 사용합니다.

닉네임 중복이면 소유자가 같은 경우에도 새 게임을 시작하지 않습니다. 중복 입력은 지우고 입력란으로 포커스를 돌립니다. 서버의 등록 승인과 요청한 이름이 일치할 때만 시작합니다. 신규 서버 구성에서는 `supabase\migrations`의 SQL을 파일 이름 순서대로 적용합니다. 실패 기록 마이그레이션에 닉네임 중복 판정도 포함됩니다.

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
