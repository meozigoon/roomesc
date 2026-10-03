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

WinForms 화면은 디자이너 파일 없이 코드에서 구성합니다. 기준 해상도는 1400×820이며 현재 창에 맞춰 등비 확대 또는 축소합니다.

## 사용자 데이터

실행 중 생성되는 파일은 `%LOCALAPPDATA%\ThirteenthBell`에 저장됩니다.

- `player-data.json`: 최초 안내 확인 여부, 닉네임, 온라인 소유 토큰
- `progress-backup.json`: 끝나지 않은 게임의 일회용 복구 데이터
- `Logs\error.log`: 처리되지 않은 오류 기록

한 판이 결말에 도달하면 진행 백업과 해당 판의 임시 메모 상태를 삭제합니다.

## 온라인 순위 설정

`src\ThirteenthBell\Assets\online-config.json`에 Supabase URL과 publishable key, Edge Function 이름을 설정합니다. 관리자 키를 클라이언트에 넣으면 안 됩니다. 서버 구성은 `supabase\migrations`와 `supabase\functions`를 사용합니다.

Supabase 보안 문서:

https://supabase.com/docs/guides/database/secure-data

https://supabase.com/docs/guides/database/postgres/row-level-security

https://supabase.com/docs/guides/functions

## 자산 제작

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
