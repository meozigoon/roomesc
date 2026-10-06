# 온라인 순위 서버

- Supabase project ref: `cpjiqlrjxchjipceiyus`
- Edge Function: `thirteenth-bell-leaderboard`
- 테이블: `public.thirteenth_bell_leaderboard`

클라이언트에는 공개 가능한 publishable key만 포함합니다. 닉네임 예약과 기록 저장은 Edge Function이 서버 전용 키로 RPC를 호출하며, 테이블에 대한 `anon`과 `authenticated` 직접 쓰기는 허용하지 않습니다. 공개 SELECT는 기록 UUID, 닉네임, 클리어 시간, 실패 여부, 실패 횟수, 사용한 힌트 수, 정렬용 합계, 완료 시각 열과 완료된 행으로 제한합니다. 소유 토큰의 해시는 공개하지 않습니다.

닉네임은 앞뒤 공백 제거, 연속 공백 축약, 소문자 비교를 적용한 고유 키로 보호합니다. 새 판마다 새 소유 토큰을 사용하고 기존 판의 기록은 유지합니다. 새 클라이언트는 초기 편지와 복구 사이의 종료 시간을 제외한 실제 플레이 시간(밀리초), 실패 횟수, 사용한 힌트 수를 전송합니다. 서버는 자료형과 범위를 확인하고 플레이 시간이 예약 이후의 서버 경과 시간보다 5초 넘게 길면 거절합니다. 이전 클라이언트처럼 시간이 없으면 예약 시각부터의 서버 경과 시간을 사용합니다. 클라이언트가 전송한 플레이 시간과 횟수를 서버가 독립적으로 재구성하지는 않습니다. 첫 완료 결과를 보존하므로 전송 재시도는 중복 결과를 만들지 않습니다. 성공한 판을 실패로 바꾸거나 실패한 판을 성공으로 바꿀 수 없습니다.

실패 판은 `failed = true`, `clear_time_ms = null`로 저장합니다. 완료 목록은 성공 기록부터 시간순으로 표시합니다. 시간이 같으면 `ranking_penalty = failed_attempts + hint_count`가 작은 기록을 먼저 표시하고, 합도 같으면 완료 시각과 닉네임 순으로 정렬합니다. 합계는 bigint 저장 생성 열로 계산합니다. 과거에 횟수를 저장하지 않은 기록은 0으로 유지합니다. 실패 기록은 성공 뒤에 표시하며 전체 완료 기록 수를 공동 최하위 순위로 사용합니다. 아직 진행 중인 예약은 공개 목록에서 제외합니다.

마이그레이션은 파일명 순서대로 적용한 후 Edge Function을 배포합니다. `20261005082725_run_statistics_ranking.sql`은 CLI로 생성한 이번 순위 변경 마이그레이션입니다. 기존 함수와 동일하게 클라이언트 API 키를 함수 본문에서 검사하며 `verify_jwt = false` 설정을 유지합니다.

2026-10-05에 연결된 프로젝트에 이 마이그레이션을 적용하고 Edge Function 버전 5를 배포했습니다. 실제 요청 검사 11개를 통과한 뒤 검증용 기록 3개만 삭제했고 기존 게임 기록 1개는 유지했습니다.

`tests/run-outcomes.sql`은 성공과 실패의 보존, 재시도, 순위와 공개 권한에 대한 15개 검사를 하나의 트랜잭션에서 실행하고 검증용 행을 롤백합니다. `tools/LeaderboardSmoke.ts`는 실제 네트워크 없이 함수 요청과 응답을 검사합니다. `tools/OnlineOutcomeSmoke.ps1`은 실제 서버에 검증용 닉네임 2개를 등록하므로 결과 JSON의 식별자에 해당하는 검증용 행을 테스트 후 삭제해야 합니다.

`tests/run-statistics.sql`은 동점 순위, 시간 우선, 범위 검증, 재시도와 권한에 대한 17개 검사를 실행하고 검증용 행을 롤백합니다. 기존 결과 검사의 15개 조건도 함께 검증합니다. 연결된 Supabase 도구로 마이그레이션과 Edge Function을 배포하며 이 폴더의 SQL과 TypeScript는 재현 및 검토용 원본입니다.

## 비밀번호로 선택 기록 삭제

`20261006135448_leaderboard_record_deletion.sql`을 적용한 뒤 Edge Function을 배포합니다. 클라이언트는 `action = "delete"`, `recordIds` UUID 배열, `password`를 POST로 전송합니다. 서버는 입력 범위를 검사한 뒤 서버 전용 RPC `thirteenth_bell_delete_records`를 호출합니다. `anon`과 `authenticated`는 이 RPC나 테이블 DELETE에 접근할 수 없습니다. 비밀번호의 bcrypt 해시와 인증 실패 상태는 비공개 `bell_private.leaderboard_admin`에 저장합니다. 클라이언트 코드에는 비밀번호 검증값을 포함하지 않으며 비밀번호 입력 내용은 파일에 저장하지 않습니다.

RPC는 1~1,000개의 UUID를 한 트랜잭션에서 처리합니다. 성공 및 실패가 확정된 행만 삭제하므로 진행 중인 예약은 유지합니다. 중복 UUID는 한 번만 삭제하고, 이미 삭제된 UUID로 재시도하면 0개 삭제로 성공합니다. 닉네임이 재사용되더라도 다른 UUID이므로 오래된 목록이 새 기록을 삭제하지 않습니다. 삭제 후 순위는 남은 행으로 다시 계산합니다. 연속 비밀번호 실패 10회 뒤에는 전체 삭제 인증을 1분간 제한합니다. 이 제한은 DB 행 잠금으로 모든 서버 인스턴스에 공유됩니다.

새 환경의 초기 비밀번호 설정과 이후 변경은 Supabase SQL Editor에서 아래 SQL의 `<새 비밀번호>`를 교체하여 실행합니다. UTF-8 기준 1~72바이트를 사용합니다. 마이그레이션에는 비밀번호나 비밀번호의 해시를 포함하지 않습니다. 새 환경에서는 마이그레이션 적용 후 이 설정이 끝나기 전까지 삭제 기능이 서버 오류로 거절됩니다. 실제 비밀번호가 들어간 SQL 파일을 저장하거나 커밋하지 않습니다. 현재 연결된 서버에는 요청한 초기 비밀번호가 별도로 설정되어 있습니다.

```sql
insert into bell_private.leaderboard_admin (singleton, password_hash)
values (true, extensions.crypt('<새 비밀번호>', extensions.gen_salt('bf', 10)))
on conflict (singleton) do update set
    password_hash = excluded.password_hash, failed_attempts = 0, blocked_until = null;
```

`tests/leaderboard-deletion.sql`은 오인 삭제 방지, 잘못된 비밀번호, 중복 및 재시도, 성공/실패 일괄 삭제, 진행 중 예약 보존, 닉네임 재사용, 잠금 해제, 공개 권한에 관한 22개 조건을 확인하고 테스트 행과 비밀번호 변경을 전부 롤백합니다. `tools/LeaderboardSmoke.ts`는 실제 네트워크 없이 삭제 요청과 서버 응답을 검사합니다. `dotnet run --project tools/LeaderboardDeleteSmoke/LeaderboardDeleteSmoke.csproj -c Release`는 실제 네트워크 없이 클라이언트의 UUID 파싱, 요청, 오류 및 취소 처리를 검사합니다.

2026-10-06에 삭제 마이그레이션을 적용하고 Edge Function 버전 6을 배포했습니다. 데이터베이스 삭제 검사 22개와 기존 결과/통계 검사 32개를 트랜잭션 롤백으로 통과했습니다. 공개 API에서 잘못된 비밀번호 거절, 초기 비밀번호 인증, UUID 목록 조회, 직접 RPC 접근 거절을 확인하고 새 검증용 성공/실패 기록 2개를 일괄 삭제 및 재시도했습니다. 검증용 기록 2개가 삭제되었으며 기존 기록 7개는 유지했습니다. Windows 실제 화면 조작 검증은 실행 환경의 제한으로 수행하지 않았습니다.
