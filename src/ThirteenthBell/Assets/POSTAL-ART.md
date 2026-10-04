# 우편실 원화 제작 기록

제작일: 2026-10-04 (Asia/Seoul).

도구: 내장 image_gen 이미지 생성 도구. 배경과 소품은 생성된 PNG를 사용한다. 소품은 투명 PNG 아틀라스의 원본 영역을 읽어 표시하며, 코드에서 물건의 모양을 그리지 않는다.

스토리와 퍼즐: 이 프로젝트를 위해 새로 작성한 창작물. 외부 출처 없음.

기존 workshop-desk-updated.png의 짙은 목재, 황동, 촛불과 푸른 눈빛을 시각적 기준으로 삼았다.

## 사용한 최종 프롬프트

### postal-room.png

Use case: stylized-concept. Asset: 16:9 landscape background for a Christmas mystery point-and-click escape game, rich painterly photoreal 3D illustration. Reference image role: match the provided ornate snowy clockmaker workshop style, but create an adjoining smaller old postal antechamber. Dark carved walnut, warm amber candlelight, icy blue moonlight, pine garlands, burgundy, antique brass. Camera straight eye level wide view. Central wooden arched locked door at x 55%, occupies x45-68%, y15-68%, ornate star escutcheon and brass lock. Left third: frosted tall window above a postmaster desk with CLOSED brass handled drawer, mail cubbies, envelopes, blank notes. Right third: a clearly visible mechanical array of four antique brass bells on a wooden mount at x76-94%, y30-58%, with an intricate winding mechanism. Lower quarter y68-88% unobstructed worn wooden floor suitable for adding movable parcel sprites; NO parcels, rugs, keys, suitcases on that floor. Lower y88-100% dark floor so interface text is readable. No people, no UI, no legible writing, no watermark. Distinct detailed playable objects, quietly mysterious Christmas story.

### postal-props.png

Use case: stylized-concept. Asset: transparent PNG game sprite atlas, square image in exactly 2 columns x 2 rows equal cells, no visible grid lines. Each object fully isolated centered within its own quadrant with at least 12% empty padding to edges; no overlap. Top left: weathered brown leather postal suitcase with brass buckles, three-quarter view. Top right: single burgundy gift parcel with green velvet ribbon, three-quarter view. Bottom left: neatly folded dark forest-green wool blanket with golden star embroidery, three-quarter view. Bottom right: antique brass door key with a star-shaped bow, horizontal. Rich detailed painterly photoreal 3D Christmas style, amber light from upper left, cool blue fill, match old dark walnut clockmaker workshop. Actual alpha transparency throughout empty space, no floor/background, no labels, no numbers, no outlines, no UI.

### postal-ledger.png

Use case: stylized-concept. Asset: landscape 16:9 close-up background for a Christmas mail sorting puzzle. Same dark walnut clockmaker workshop, amber candlelight from left, blue moonlight, antique brass and burgundy palette. Wooden postmaster desk viewed from slightly above. Large worn cream blank ledger page centered occupying x18-82% and y10-60% with clear blank writable space. FIVE wax-sealed envelopes neatly aligned along lower part y65-80%, ornate but no readable letters, quill, twine, brass stamp, frost at edges. No people, no UI, no watermark, NO legible text. Rich detailed painterly photoreal 3D illustration, quiet mystery.

### postal-bells.png

Use case: stylized-concept. Asset: landscape 16:9 close-up illustration background for a Christmas mechanical bell puzzle. Antique dark walnut board with four beautiful polished brass bells hanging evenly across x20-80%, y25-50%, numbered nowhere. Visible brass winding gear, scrollwork, burgundy velvet, candles, blue frost glimmer, pine sprigs. Top center x20-80%,y8-22% a blank aged cream instruction plaque; lower center x20-80%, y60-82% a flat dark walnut shelf with clear open space for interface. Rich detailed painterly photoreal 3D, match ornate snowy clockmaker workshop, amber and blue lighting. No key, no figures, no letters, no readable text, no UI, no watermark.

