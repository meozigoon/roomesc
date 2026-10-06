# 실제 게임 자동 클리어

현재 GameForm 원본을 컴파일하고 정상 버튼, 입력란, 마우스 드래그 이벤트를 실행합니다. 화면 전환과 편지 애니메이션, 음향을 활성화하며 게임의 Stopwatch 측정값을 기존 SupabaseLeaderboardService로 제출합니다. 우편실의 열쇠와 두 봉인, 공방의 여섯 퍼즐, 별시계 복구, 선물 전달 결말을 순서대로 완료합니다.

실행마다 새로운 출력 폴더를 사용합니다. 그 폴더 아래 player-data에 세션 정보를 보관하므로 기존 사용자 데이터와 복구 파일을 보존합니다. 이 세션 파일에는 온라인 소유 토큰이 포함되므로 공개 배포하지 않습니다.

```powershell
dotnet build tools/Speedrun/Speedrun.csproj -c Release
# DB에 쓰지 않는 사전 실행
& tools/Speedrun/bin/Release/net10.0-windows10.0.19041.0/Speedrun.exe admin artifacts/new-rehearsal --rehearsal
# 사용 가능한 새 닉네임으로 실제 DB에 기록을 등록하는 실행
& tools/Speedrun/bin/Release/net10.0-windows10.0.19041.0/Speedrun.exe NewNickname artifacts/new-live-run
```

실제 실행은 닉네임을 예약하고 성공 기록을 공개 순위에 영구 등록합니다. 성공 후 result.json, ending.png, leaderboard.png를 저장합니다. 진행 중 오류가 발생하면 창과 정상 복구 데이터를 유지합니다. 종료나 진행 포기는 기존 게임의 실패 기록 규칙을 따릅니다.

2026-10-06의 실제 실행 결과는 artifacts/speedrun-20261006/live에 있습니다. admin, 13,562ms, 힌트 0회, 오답 0회, 완료 직후 1위를 DB 직접 조회와 공개 API 및 게임 화면으로 확인했습니다. 이는 당시 등록된 기록 중 최단 기록이며 가능한 모든 실행의 이론적 하한을 증명한 값은 아닙니다.
