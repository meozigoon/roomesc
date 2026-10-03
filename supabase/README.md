# 온라인 순위 서버

- Supabase project ref: `cpjiqlrjxchjipceiyus`
- Edge Function: `thirteenth-bell-leaderboard`
- 테이블: `public.thirteenth_bell_leaderboard`

클라이언트에는 공개 가능한 publishable key만 포함합니다. 닉네임 예약과 기록 저장은 Edge Function이 서버 전용 키로 RPC를 호출하며, 테이블에 대한 `anon`과 `authenticated` 직접 쓰기는 허용하지 않습니다. 공개 SELECT도 닉네임, 클리어 시간, 완료 시각 열과 완료된 행으로 제한합니다.

닉네임은 앞뒤 공백 제거, 연속 공백 축약, 소문자 비교를 적용한 고유 키로 보호합니다. 클리어 시간은 닉네임 예약 시각부터 완료 RPC 호출 시각까지의 서버 시간으로 계산하며, 같은 소유 토큰의 재도전에서는 최고 기록만 유지합니다.

Supabase CLI를 사용할 수 없는 환경에서 연결된 Supabase 도구로 마이그레이션과 Edge Function을 배포했습니다. 이 폴더의 SQL과 TypeScript는 재현 및 검토용 원본입니다.
