# Gameplay improvements — 2026-09-24

Implemented in `Assets/CarsGame` using the existing vehicle, combat, save and UI systems; no new dependencies.

- **Driving:** service braking preserves tyre grip, steering is immediate while tactical commands retain reaction delay, traffic speed follows the car ahead, and stuck recovery ignores frozen cars. Low-speed steering assistance is reflected in the AI's curvature/speed model; the existing track's tight final hairpin is now reachable. `CarController.lowSpeedSteeringBoost` remains tunable (default 3).
- **Pickups:** deterministic sectors place health/ammo outside bends, nitro before bends, and items in the opposing lane. Narrow box triggers require an intentional lane choice.
- **Combat feedback:** resolved-hit events distinguish shields, damage and destruction. The HUD shows an incoming direction/source and an outgoing confirmation; shield hits have a separate sound.
- **Rivals:** named, coloured profiles equip racer, shooter and defender loadouts. Names appear above nearby cars. Machine-gun bursts respect the weapon's fire interval.
- **Goals:** events share descriptions between selection, live HUD and results. Third-star variants include no deaths, winning without repairs, and a time limit. Stats are captured at the finish line so later damage cannot remove earned stars. Existing earned stars remain valid.
- **Builds:** free balanced/speed/survival/firepower presets multiply purchased upgrades and equip only owned weapons. Purchases are retained; save migration and cloud merge preserve the selected build. Weapon setup occurs before damage/fire-rate upgrades.
- **Ghost fix found during validation:** copy baked meshes without instantiating a live car, avoiding race registration and RequireComponent removal errors.

## Checks

`GameplayImprovementsTests`, `AiRecoveryTests` and `GhostSpawnTests` cover tuning, ownership, saves, goals, hit outcomes, pickup placement, rival loadouts, recovery and ghost isolation.

Batch Unity runs on `/tmp/cars-qa/sim`, separate from the open editor. Artifacts use `/tmp/cars-qa/improvements`.

Initial two-lap solo baseline: 336.5 simulated seconds, 91 wall impacts, 9 automatic resets. After driving changes, the six-car two-lap seed-12345 run completed in 122.8 simulated seconds; the player finished in 99.3 seconds. All six cars finished, with one AI recovery and one automatic reset across the field. These are deterministic harness measurements, not a promise of identical human race times or zero combat collisions.

Full EditMode suite: **215 passed, 0 failed** (`edit.xml`), including the recovery, ghost-isolation and HUD event-path play-mode transitions. New localization rows are complete in all nine languages, with no duplicate keys.

Reverse track `e2`, seed 67890, two laps and five opponents: **6/6 finished** in 121.8 simulated seconds. Three AI recovery resets and one automatic reset across the field; no car remained stuck.

Prebuild solo gate: **49.3 seconds**, zero wall impacts, deaths or resets (limit 70 seconds). Final localization import: 217 keys × nine languages; directional triangle included in the static atlas.

WebGL build succeeded: `Builds/GameplayImprovements/CarsGame-web-0.2.0.zip` (about 15 MiB). Browser checks passed at 1280×720 and 812×375 with touch controls explicitly enabled. Inspected garage, live goals, combat source feedback and results. Real clicks selected speed/survival/firepower; speed remained selected after page reload. Both race flows reached results and balanced gameplay start/stop calls.

Final browser logs contain no C# exceptions, missing localization keys or missing glyphs. Remaining local warnings: unavailable host SDK (mock is intentional), favicon/resource 404s, and SwiftShader/URP fallback warnings. A physical phone and production host SDK were not tested.

Persistent evidence: `Builds/GameplayImprovements/qa/` (test XML, race JSON, browser logs, screenshots and size report). Earlier temporary baseline files were removed when the environment restarted; their measurements above were recorded before that restart.

The shared working tree is being edited by other sessions. The archived build is the tested snapshot; a concurrent mines-only targeting-range change in `AiGunner.cs` / `EventRules.cs` arrived afterward and is not included in that archive.

## Варианты будущих карт и режимов — 2026-09-24

Статус: идеи для обсуждения, не утверждённый план реализации. Сохранены по просьбе пользователя.

Карты:

- **Вулкан:** дорога над лавой; извержения временно перекрывают отдельные полосы.
- **Ледяная станция:** скользкие повороты, снежные заносы и тоннели с хорошим сцеплением для разгона.
- **Крыши мегаполиса:** прыжки между зданиями, короткие опасные маршруты и длинные безопасные объезды.
- **Орбитальное кольцо:** участки пониженной гравитации позволяют перепрыгивать разрывы, но усложняют приземление.

Режимы:

- **На выбывание:** после каждого круга последний участник покидает гонку.
- **Горячая бомба:** бомба передаётся при столкновении; нужно избавиться от неё до взрыва.
- **Погоня:** игрок убегает, остальные пытаются остановить его до финиша.
- **Заезд на время:** без оружия, с призраком своего лучшего результата.

**«Горячая бомба»** выбрана пользователем и реализована отдельным событием `e18`; правила и проверки описаны ниже.

## Режим «Арена»

Дизайн скоростной арены на дюне (очки из пяти долей, «Угар», бомба с моста, налёты) ведётся в одном документе: [ArenaMode.md](ArenaMode.md).

## Развилка на северном повороте — 2026-09-25

Обычные гонки автоматически получают узкий мост по внутренней стороне северного поворота: около 149 м вместо 180 м, ширина 4,4 м, без отбойников. Основная дуга сохраняется; на ней появляется дополнительный ремонт, если событие разрешает пикапы. Жёлтые стрелки и полосатые края отмечают срезку. Арена, космическая трасса и трасса над пропастью её не получают.

`TrackFork` строится из существующего `TrackPath` при старте и удаляется при выходе/повторе. Проекция обоих маршрутов использует общие контрольные точки; падение с моста сохраняет непрерывный прогресс, а автоматический возврат ставит машину на основную дорогу. Версия раскладки призраков повышена до 2. Сцена и запечённые ассеты не менялись.

Проверка: `TrackForkPlayTests` — физический проезд вперёд/назад, проходимые коллайдеры, круг по обоим маршрутам, возврат после падения, ремонт и очистка. Снимок и результаты прогонов: `Builds/TrackFork/qa/`. Место развилки (85–265 м исходного направления) рассчитано на текущую горную трассу; при замене её геометрии участок нужно выбрать заново. Боты продолжают пользоваться основной дорогой.

Итог проверки: 33 связанных EditMode-теста прошли; PlayMode-тест развилки прошёл в обоих направлениях, включая падение, возврат на дорогу и ремонт. Одиночный круг по основной трассе — 49,2 с, без смертей и сбросов (одно касание ограждения); в двухкруговой гонке с пятью ботами финишировали 6/6 машин. Визуальный снимок получен из Unity с рендерингом. Проверка выполнялась на копии проекта; сборка WebGL в рамках этой задачи не создавалась.

## «Горячая бомба» — 2026-09-25

Событие `e18` доступно сразу в списке заездов: два круга, пять соперников, существующая горная трасса с развилкой. Побеждает первый на финише. Оружие, пикапы и урон от столкновений отключены в этом событии; физические столкновения работают.

- Первый носитель — игрок. Запал длится 18 секунд и не сбрасывается при передаче касанием. После передачи действует блокировка на 1,5 секунды, позволяющая оторваться.
- Взрывается носитель, даже со щитом или неуязвимостью после возврата на трассу. Последний передавший получает фраг. Машина возрождается штатно; через 3 секунды появляется новая бомба, начальные носители чередуются.
- Пауза останавливает запал. Кнопка возврата не снимает бомбу. Финишировавшие машины не участвуют в передаче. После окончания заезда, повтора и выхода в меню старое состояние очищается.
- HUD показывает владельца, секунды и подсказку действия; над машиной видна пульсирующая бомба, последние секунды выделяются красным и звуком. Тексты добавлены на все девять языков, атлас пересобран без пропущенных символов.
- Боты-носители выбирают полосу ближайшего соперника и при необходимости замедляются, чтобы его подпустить. Остальные уклоняются. Существующие руление и торможение перед поворотами сохранены.

Реализация: `HotBombRace`, правило `RaceTwist.HotBomb`, контакт в `RaceParticipant`, HUD в `RaceOverlays`, событие в Resources. В ротацию ежедневных заездов режим пока не включён. Проверки: `HotBombPlayTests` (реальный контакт, пауза, неизменный запал, защита от обратной передачи, щит/неуязвимость, респаун, фраг, исключение финишировавших, повтор и полный заезд ботов). Артефакты: `Builds/HotBomb/qa/`.

Проверка реализации: PlayMode 2/2, связанные EditMode 65/65. Контрольный полный заезд (seed 74321, normal): 10911 тиков, 12 передач, 10 взрывов, игрок финишировал. WebGL-сборка успешна (`Builds/HotBomb/web`); локальный браузер показал новый HUD и маркер без C#-исключений, проверены окна 1280×720 и 812×375 с включённым сенсорным управлением. На сенсорном прогоне зафиксирована передача от игрока боту. Проверка SDK Яндекса и публикация в этот прогон не входят.
