# 공방, 책상, 황동 금고와 나가기 원화

2026-10-05에 `image_gen.imagegen`으로 공방 전체, 금고 확대와 신규 나가기 배경을 제작했습니다. 모두 1672×941 PNG입니다. 외부 참고 이미지 출처 없음. 금고는 공방 전체와 금고 확대에서 책상 아래 바닥에 한 개만 표시됩니다. 짙은 호두나무 책상, 별 모양 황동 손잡이와 장난감 선반을 공유합니다. 책상 확대는 이후 요청에 따라 Git 원본의 책상 위만 가까이 보이는 그림으로 복원했습니다. 클릭 영역은 각 원화에 맞춰 1400×820 게임 좌표로 환산했습니다.

## 실제 생성 프롬프트

### workshop-desk-updated.png
참조: 기존 공방 원화, 기존 책상 확대 원화.

Edit image 1, the wide Christmas clockmaker workshop. Image 2 is the exact desk design reference. Keep the overview composition, arched snowy window, central astronomical clock, snow globe in lower foreground, fireplace with four yellow green red blue stockings, warm painted realistic festive style. Replace the left desk and its wall shelf with the SAME carved dark walnut desk and wooden toy shelf from image 2: brass star-shaped drawer handles, star-perforated brass lamp at left, blue star chart with compass at left of tabletop, red wax sealed cream envelope at center, brass instruments, candles at right; toy shelf in left-to-right order owl, fox, nutcracker, rabbit, brass bell, wooden train, rocking horse. REMOVE the safe from the tabletop. Put exactly one frosted square brass safe with star engraving, three blank round combination dials, hinge and handle ON THE FLOOR UNDER THIS DESK, clearly visible between desk legs. Move stool aside to leave safe door entirely unobstructed. Do not put any safe above tabletop. The desk should be open underneath instead of solid cabinetry, enabling clear safe view. Keep the safe within the left quarter and above the bottom story overlay area. No writing, numbers, UI, people. Wide 16:9 raster game background, 1672x941 composition. Return one complete edited image.

### desk-closeup.png (이전 편집 이력, 현재는 원본 복원)
참조: 기존 책상 확대 원화, 새 공방 원화.

Edit image 1 desk closeup to match image 2 newly updated workshop overview desk. Same dark carved walnut writing desk, brass star-shaped drawer handles, open underneath, same exact desk shelf and objects. Wide straight-on close-up, pull camera back slightly so lower floor and underneath desk are visible in bottom quarter. Keep toy shelf upper quarter: owl, fox, nutcracker, rabbit, brass bell, wooden train, rocking horse in exact order. Brass star perforated lamp far left, star chart and compass on tabletop left, large wax-sealed cream envelope center tabletop, candles/instruments/paper on right. SINGLE frosted brass safe with engraved star, three blank round dials and handle ON FLOOR UNDER THE DESK, unobstructed door at lower-left center. No safe on tabletop. Exact same design as image 2. Envelope and chart visibly separate selectable objects. Preserve blue snowy window edges, warm painterly detailed Christmas atmosphere. No text, no numbers, no UI, no people. 16:9 full raster game background 1672x941 composition.

### lanterns.png
참조: 기존 금고 확대 원화, 새 공방 원화. 기존 코드의 파일명을 유지했습니다.

Replace image 1 safe puzzle close-up with a close-up of the EXACT safe and desk underside from image 2. Camera near wooden FLOOR looking at ONE square frosted brass safe standing on the floor UNDER the walnut writing desk. Desk underside and carved apron with brass star handles form top background, sturdy walnut legs flank safe; clearly floorboards beneath safe, NOT a tabletop. Safe same compact square design as overview image 2: engraved star centered above THREE round blank brass combination dials in a horizontal row, small right handle and hinge. Same frost edges and amber lighting. Large safe door centered in lower middle, its top brass blank area can support game's overlay. Quiet uncluttered background. Warm highly detailed painterly Christmas game art, blue snowy light in right background, no numbers, letters, UI, people, extra safes. Wide 16:9 full raster 1672x941 composition.

### exit-background.png
참조 이미지 없이 신규 생성했습니다. 확인 문서와 버튼은 게임 코드에서 표시합니다.

Create an original 16:9 raster background for the exit confirmation screen of a warm painterly Christmas escape room game set in snowy Lumiere village. View from just outside an old walnut clockmaker workshop doorway, at blue twilight, looking out onto a quiet snow-covered village lane with distant warm golden lit windows and soft falling snow. Evergreen garlands with red berries and brass stars frame upper corners; warm lantern beside door far left, small wrapped gift and footprints at lower right, faint astronomical clock visible inside at extreme left. Central 65 percent is quiet dark blue winter mist with subtle distant buildings, uncluttered so a cream paper confirmation card and three buttons can be layered over it with high contrast. Welcoming restful departure mood, amber against midnight blue, detailed painted realism matching a handcrafted Victorian Christmas workshop, cinematic but gentle. No writing, signs, logo, letters, numbers, people, UI, or buttons baked into picture. Full wide image 1672x941 composition, opaque background.

## 양말 퍼즐 원화

stocking-logic.png는 앞선 수정에서 생성한 1672×941 원화를 유지합니다. 빈 황동 고리 네 개와 벽난로를 남기고 룰렛과 배경에 그려진 양말을 제거했습니다. 움직이는 양말은 게임 컨트롤로 표시합니다.

양말 편집 프롬프트: retain the warm winter fireplace scene and four empty brass hooks above the fire. Remove the central black and brass roulette/slot machine completely and replace it with a normal hearth and fireguard. Also remove all baked-in colored stockings from the floor, because the game draws draggable stockings. Keep an uncluttered lower rug area. No text, numbers, UI or people.
