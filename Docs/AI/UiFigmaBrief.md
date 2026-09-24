# Интерфейс — бриф для Figma AI

Как пользоваться: в каждый запрос к Figma AI (Make или First Draft) вставляем **общий блок стиля**
плюс **один блок экрана**. Промпты на английском — генератор понимает его лучше; тексты внутри макетов
остаются русскими, как в игре.

Порядок: сначала блок 1 (дизайн-система), затем экраны. В начало каждого следующего запроса добавляйте
строку `use the existing styles and components on this file`, иначе экраны разъедутся по стилю.

Стиль, палитра и границы содержания — `ArtDirection.md`. Оружие — `WeaponsFigmaBrief.md`.

---

## Общий блок стиля (вставлять каждый раз)

> Game UI for "Rust Carnival" — a post-apocalyptic demolition-derby racer for web and mobile
> (Yandex Games, WebGL). Scavenged cars with bolted-on cannons, saws and flamethrowers race through
> junkyards, wastelands, ruined cities and, late in the game, an abandoned spaceport. Framing: it is a
> SHOW — a carnival of scrap staged by survivors, with sponsor banners, stands built from buses and bets
> on every race. Tone: gleeful metal-on-metal mayhem, never grim, never gory. Pilots are repair drones and
> helmeted mannequins, so cars are torn apart, never people: bolts, sparks and springs fly instead of blood.
> Influences to evoke without copying: Mario Kart readability, Twisted Metal cast of characters, 1950s
> retro-futurist signage, heavy over-armoured silhouettes. Never use trademarked iconography.
>
> Landscape only, 1920×1080 frame. The UI sits over a bright flat-shaded low-poly 3D world, so panels are
> dark, compact and high-contrast.
>
> **Tokens:** background `#14100C` (warm tar); surface `#241E18` at 96% with a riveted metal edge;
> raised `#3A302A`; text `#F2E8D0` (bleached bone); muted `#A2968A`; ink `#14100C`;
> accent `#FF8A1F` (hazard orange — primary actions, progress, numbers); positive `#8CC63F`
> (radioactive lime — records, rewards); danger `#E03B2A`; energy `#3FE0FF` (nitro, plasma, space gates);
> plus 45° black-and-yellow hazard stripes for locks, traps and warnings.
>
> **Shapes:** metal plates with 45°-cut corners, rivets along the edge, welded seams. Worn paint, rust and
> streaks appear ONLY on frames and backgrounds — never over text or numbers; the inside of a panel stays
> clean and readable. Signature motif: three slanted (-15°) stencil-sprayed stripes with ragged edges, and
> big stencilled numbers.
>
> **Typography:** bold stencil sans with Cyrillic support (Stardos Stencil / stencilled Oswald; Rubik One
> as a safe fallback). Titles and buttons UPPERCASE. Seven-segment terminal digits for timers, speed and
> counters.
>
> **Buttons:** primary = hazard-orange stamped plate with dark text; secondary = dark steel plate with bone
> text. States: default, hover (+30% lighter), pressed (darker, pushed in), disabled (rusted over, muted),
> gamepad focus (2px bone-white welded outline).
>
> **Layout:** content max width 1360px, 40px outer margins, 16px gap between list cards. All UI text in
> Russian. Flat vector shapes and simple icons only — no photos, no heavy blur, no gore, no blood.

---

## 1. Дизайн-система (запускать первой)

```
Create a UI kit page for this game.
Color styles: all tokens above. Text styles: Display 64, H1 46, H2 34, Body 26, Caption 22,
Terminal digits 40 (timer), Terminal digits 46 (speed).
Components with variants:
- Button (primary / secondary / positive / disabled × default / hover / pressed / focus)
- Metal plate card (with rivets; variants: plain, with left accent bar, locked with hazard tape)
- List row, Section header with a sprayed underline
- Segmented control (2 and 3 options), Stepper "‹ value ›"
- Segmented progress bar (4 and 5 steps), Star row (3 stars, earned = lime)
- Fuel-can wallet chip, Toast plate, Scrollbar
- Health bar (10 segments: healthy / low / with orange damage trail), Ammo pips,
  Reticle (idle / locked), Speedometer gauge (normal / nitro)
Icon set, 2px stroke, chunky and readable at 48px: coin, fuel can, padlock, garage/wrench, trophy,
gear, pause, health cross, shell, nitro flame, reset arrow, moon, sun, reverse arrows, explosion,
hourglass, play-video, skull-free hazard symbol, star, calendar (daily race), arena crossed hammers.
```

## 2. Главное меню

```
Screen: MAIN MENU, framed as a show poster nailed to a scrap wall.
Top bar: left — logo "РЖАВЫЙ КАРНАВАЛ" in stencil lettering with three slanted sprayed stripes;
right — fuel-can wallet chip "Монеты: 1 240". Thin welded divider under the top bar with an orange
segment on the left.
Body, two columns:
- Left hero plate (58% width): orange eyebrow "ВЫБОР ГОНКИ", huge stencilled title "ХАОС",
  muted "4 кругов / 3 соперников", big counter "03 / 17" with a 17-segment progress strip
  (3 orange, 14 dark), large primary button "ГОНКА ›" at the bottom.
- Right column: three stacked tile buttons with a stencilled index (01, 02, 03), title and muted caption,
  chevron ›: "ГАРАЖ" — "Кузова и прокачка"; "ЛИДЕРЫ" — "Ваши рекорды"; "НАСТРОЙКИ" — "Язык".
Bottom-right: small muted player name "Игрок_4821".
```

## 3. Выбор гонки

```
Screen: CHOOSE RACE, styled as a fight card.
Header: secondary "‹ НАЗАД" plate, title "ВЫБОР ГОНКИ" sprayed through a stencil, wallet chip, divider.
3-segment difficulty switch: "Легко" / "Нормально" (selected, hazard orange) / "Сложно".
Scrollable list of race plates (~154px tall). Each: big stencilled number, race name, muted
"{n} кругов / {n} соперников", a row of 3 stars (earned lime, empty dark), a lime record line
"Рекорд: 2:58.412", and a stamped "ГОНКА ›" button. Each plate carries a small biome badge.
Show these rows: 01 "Спринт" (junkyard badge, 3 stars), 02 "Кубок наоборот" (reverse arrows),
03 "Хаос" (explosion), 04 "Ночная драка" (floodlights, 1 star), 05 "Выносливость" (hourglass),
06 "Минное поле" (mine) — and a locked one: crossed with black-and-yellow hazard tape, padlock,
muted "Займите топ-3 в прошлой гонке, чтобы открыть", disabled button.
At the bottom of the list, a special plate for the last event "Космические врата" with a cyan-glowing
gate badge and a cyan rim light.
```

## 4. Гараж

```
Screen: GARAGE, a workshop bay with hanging cables and tool shadows.
Header "‹ НАЗАД" / "ГАРАЖ" / wallet chip "Монеты: 380".
Left 42%: a scavenged low-poly car on a lift platform, rim-lit orange, with welded armour plates and a
cannon on the roof. Under it a class tab strip: "Багги" / "Спорткар" (selected) / "Броневик" / "Танк".
Under that a horizontal strip of small body thumbnails with names "Стрела", "Гадюка", "Комета",
"Фантом" — one selected with an orange frame, one locked with hazard tape.
Right 58%, two blocks:
- Stat block: 5 labelled bars "Скорость", "Разгон", "Управление", "Броня", "Бездорожье",
  each as a 10-segment meter; a ghosted comparison marker shows the previously selected body.
  Small lines "1 240 кг" and "Прочность 120".
- Upgrade plates: "Двигатель" — muted "+ макс. скорость, − сцепление в поворотах" — orange "Ур. 2/4" —
  4-segment bar — primary "Купить · 384"; "Броня" — "Ур. 4/4" — full bar — disabled "МАКС";
  "Пушка" — disabled "Купить · 614" with muted "Не хватает монет" under it.
Use small lime up-arrow / red down-arrow icons for the + / − trade-offs.
```

## 5. Ливреи

```
Screen: LIVERY PICKER inside the garage. Same workshop bay, car in the centre on a turntable.
Left column: paint swatches as sprayed paint blobs in a grid (8 colours, one selected with a welded ring).
Right column: stencil pattern list, each row a small car silhouette preview plus name:
"Без рисунка", "Гоночная полоса", "Двойные полосы", "Боковая полоса", "Капот", "Корма".
Bottom: a stencilled racing number picker "‹ 07 ›" and a primary button "ПРИМЕНИТЬ".
Everything reads as spray paint through a stencil, not as glossy racing decals.
```

## 6. Арсенал и колесо оружия

```
Screen: ARSENAL. Header "‹ НАЗАД" / "АРСЕНАЛ" / wallet chip.
Left: a radial weapon wheel — 10 wedge slots around a hub, each wedge holding a flat weapon icon with a
number key 1-0; the selected wedge glows hazard orange, empty wedges are dark with a welded cross.
Page dots under the wheel for 5 pages, with page labels "Пушки", "Ближний бой", "Лучи", "Весёлые",
"Ловушки".
Right: detail plate for the selected weapon — big icon, name "Кувалда", muted description, stat meters
"Урон", "Скорострельность", "Дальность", and a primary button "ПОСТАВИТЬ".
Colour-code the icons by family: ballistic = gunmetal + orange, melee = steel + red, beams = dark + cyan,
sprays = effect colour, pulses = magenta, traps = hazard stripes, funny = candy colours.
```

## 7. Заезд дня

```
Screen: DAILY RACE, framed as today's poster freshly pasted over an older torn one.
Title "ЗАЕЗД ДНЯ", a big stencilled twist badge "ТОЛЬКО МИНЫ", muted line "Новый заезд через 6 ч 12 мин"
with a terminal countdown, "Серия: 4 дн." as a row of 7 day marks (4 lit lime), a lime reward plate
"Первый финиш сегодня: +250", muted "Гонка против призраков других игроков", primary button "ГОНКА ›".
Below: a compact "Лидеры дня" table — rank, player name, time — top three ranks with scrap medals.
Also draw a claimed state: reward plate stamped over with "Бонус дня получен".
```

## 8. Лидеры

```
Screen: LEADERBOARD. Header "‹ НАЗАД" / "ЛИДЕРЫ" / wallet chip.
Section header "ВАШИ РЕКОРДЫ" with a sprayed underline. Rows (90px): stencilled rank "01",
race name "Спринт", lime terminal time "2:58.412".
An account plate: muted "Вход сохранит прогресс на всех устройствах и отправит время в таблицу."
with a full-width secondary button "ВОЙТИ".
Section "ЛИДЕРЫ": top-10 rows with rank, player name (long names truncated with …), time; ranks 1-3 get
scrap medals welded from bolts and washers.
Also draw an empty state: centred muted "Пока нет результатов" plus primary "ГОНКА ›".
```

## 9. Настройки

```
Screen: SETTINGS, styled as a fuse box / control panel.
Header "‹ НАЗАД" / "НАСТРОЙКИ" / wallet chip.
Rows (122px metal plates), label left, control right (520px wide):
- "Язык": stepper "‹  Русский  ›" with square stamped arrow buttons
- "Звук": segmented "Выкл" / "Вкл" (Вкл selected, hazard orange)
- "Автогаз": "Выкл" (selected) / "Вкл"
- "Графика": "Авто" (selected) / "Низкая" / "Высокая"
- "Тряска камеры": "Выкл" / "Вкл" (selected)
Selected segments look like a thrown knife switch; unselected ones are dark steel.
```

## 10. Результаты

```
Screen: RACE RESULTS.
Header "‹ НАЗАД" / "РЕЗУЛЬТАТЫ" / wallet chip.
Summary plate: huge stencilled orange "#2" on the left; right side lime "+176 монет" with a coin icon,
muted "Хаос", and a row of 3 stars with captions "Топ-3 на финише" (earned), "Разбейте 2 соперников"
(earned), "Без аварий" (empty).
Row of three buttons: primary "СЛЕДУЮЩАЯ", secondary "ЕЩЁ РАЗ", secondary "ГАРАЖ".
Full-width lime button "УДВОИТЬ (РЕКЛАМА)" with a small video icon.
Results table, 4 plates: stencilled rank, driver name with a small colour chip of their car, muted
"круг 0:58.120 / У2/С1", terminal time right-aligned.
01 "Синий" 3:51.204 | 02 "Вы" (player plate: orange bar on the left edge, orange rank, lime time)
3:52.880 | 03 "Жёлтый" 3:58.310 | 04 "Зелёный" muted "Сход".
Add sparks and flying bolts at the top instead of confetti.
```

## 11. HUD гонки (ПК)

```
In-race HUD overlay, 1920×1080, over a chase-camera view of a scavenged car on a junkyard track with
rivals ahead. All panels are dark riveted plates (#241E18 at 96%), compact, 30px from the edges.
- Top-left: race status plate 280×150 with an orange vertical bar: muted "КРУГ 2/3",
  bold large "П1/4".
- Top-centre: timer plate 280×70 "1:23.456" in seven-segment digits.
- Top-right: square pause button 76×76 with two orange bars.
- Bottom-left: speedometer plate 244×224 — a 220° gauge 0–240 km/h built like a salvaged dial: ticks,
  labels 0/60/120/180/240, last 20% of the arc red, filled arc hazard orange, big terminal "147",
  small "км/ч".
- Bottom-right: health plate 324×100 — welded cross icon, bold "HP 85", a 10-segment bar in lime with an
  orange damage trail. Above it an ammo plate 324×48: orange "Боезапас 4/5".
- Centre, over the road: reticle of 4 bone-white corner brackets (72×72, 4px) with 5 small ammo pips
  below it (4 lit, 1 dark).
- Small health bars floating above rival cars.
- Left, above the speedometer: hint plate with
  "Газ: W или стрелка вверх    Руль: A/D или стрелки / Тормоз: Пробел    Огонь: Ctrl или ЛКМ /
   Сброс на трассу: R    Пауза: Esc".
```

## 12. Состояния HUD

```
Create 5 variants of the same race HUD:
1) Countdown: huge stencilled orange "3" (140px) on a centre plate; a second variant with "СТАРТ!".
2) Nitro: speedometer arc and the "НИТРО" label in cyan #3FE0FF, speed "212", speed streaks at the edges.
3) Target lock + low HP: reticle brackets in red #E03B2A, slightly scaled up over a rival car, a red "✕"
   hit marker in the centre, health bar red "HP 22", and thin 16px red strips glowing along all four edges.
4) Kill toast: plate at 70% screen height, orange "Синий разбит!"; a second variant in red
   "Вас разбил Жёлтый".
5) Wrong way: centre plate with red "НЕ ТУДА!" (80px) and hazard stripes along the plate edge.
```

## 13. Пауза

```
Pause overlay over the race HUD: full-screen dim #14100C at 80%. Centred plate 600×600 with an orange
stencil stroke top-left, "ПАУЗА" 56px, three stacked full-width buttons: primary "ПРОДОЛЖИТЬ",
secondary "ЗАНОВО", secondary "В МЕНЮ", muted hint at the bottom "Esc — продолжить".
The plate looks like a hatch bolted shut over the action.
```

## 14. Мобильный HUD

```
Mobile landscape race HUD, frame 844×390 (also produce 1280×720). Big touch buttons as dark stamped
plates with orange glyphs:
- Bottom-left: steering "◄" and "►", 160×160 each.
- Bottom-right: the biggest button "ОГОНЬ" 220×220 in hazard orange, beside it "ТОРМОЗ" 200×120.
- Top-right: "R" reset 100×100 with the pause button below it.
- Speedometer moves above the steering pair; health and ammo move to the top-right, left of the R button;
  timer top-centre; lap and position top-left.
- Hint under the timer: "Руль — стрелки слева, ОГОНЬ и ТОРМОЗ справа. Машина едет сама."
Keep the thumb zones clear — nothing but controls in the bottom corners.
```

## 15. Арена CARNAGE

```
Screen: ARENA MODE entry, titled "CARNAGE" in battered stencil letters over a dusty pit ringed with tyre
walls, floodlights and cheering scrap stands.
Left: mode plate — muted "Бой на выбывание", settings rows "Соперники: ‹ 5 ›", "Жизни: ‹ 3 ›",
"Оружие: ‹ Случайное ›", primary button "В БОЙ ›".
Right: a scoreboard preview — 6 rows with car colour chips, names and kill counts, the leader marked with
an orange bar.
Also produce the in-arena HUD variant: no lap or position plates; instead a lives row (3 welded skull-free
tokens, one spent), a kill counter, and a "ПОСЛЕДНИЙ СОПЕРНИК" red banner variant.
```

## 16. Загрузочный экран

```
Loading screen shown before the engine starts (HTML page, 1920×1080 and a 844×390 variant).
Centred stencilled logo "РЖАВЫЙ КАРНАВАЛ" over a dark tar background with faint scrap silhouettes and
drifting dust. Under it a 620px progress bar built like a welded strip filling with hazard orange, a
terminal percentage "42%", and a rotating tip line in muted text ("Совет: тяжёлый кузов выигрывает
толкотню, но дольше тормозит.").
Also produce an error state: red plate "Не удалось загрузить" with a secondary button "Повторить".
```

## 17. Логотип, иконка, обложка

```
Logo "РЖАВЫЙ КАРНАВАЛ" and its English version "RUST CARNIVAL": chunky uppercase stencil lettering,
hazard orange #FF8A1F sprayed over dark metal, ragged stencil edges, three slanted stripes, a small
cannon or saw blade integrated into one letter. Provide a horizontal and a stacked version, on dark and
on light.
App icon 512×512: a scavenged red car with a roof cannon firing, 3/4 front view, chunky low-poly, bright
hazard-orange rim light, simple dark background, readable at 64px.
Cover 800×470: four scrap cars racing and firing through a junkyard at sunset, dust and sparks, no text.
```
