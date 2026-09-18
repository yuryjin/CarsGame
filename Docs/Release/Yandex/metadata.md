# Карточка игры в консоли Яндекс Игр (черновик)

Заполняется вручную в консоли разработчика. Тексты ниже — готовые к вставке.
Версия сборки: `Builds/Yandex/CarsGame-web-<version>.zip` (uncompressed ≤ 100 МБ — с запасом).

## Основное

| Поле | Значение |
|---|---|
| Название (ru) | Гонки с пушкой |
| Название (en) | Cannon Racers |
| Название (tr) | Toplu Yarış |
| Жанр | Гонки, экшен |
| Управление | Клавиатура, геймпад, сенсорный экран |
| Устройства | Десктоп, мобильные, планшеты |
| **Ориентация** | **Только альбомная** (меню и HUD рассчитаны на широкий экран; в портрете HUD переносит HP/патроны под таймер, но меню остаётся альбомным) |
| Языки | ru, en, tr, uk, de, es, fr, pt, zh |
| Монетизация | Interstitial после результатов (не чаще, чем разрешает `AdPacer`), rewarded по кнопке «Удвоить награду» |
| Сохранения | Облачные (`player.setData`), гостевой прогресс в localStorage |
| Лидерборды | `best_e1` … `best_e5`, тип «время», сортировка по возрастанию, единица — миллисекунды |

## Короткое описание (до 60 символов)

- ru: Боевые гонки: обгоняй, стреляй, прокачивай машину.
- en: Combat racing: overtake, shoot, upgrade your car.
- tr: Savaş yarışı: geç, ateş et, aracını geliştir.

## Полное описание

**ru**
Боевые гонки на одной трассе с пятью режимами заездов. У машины есть пушка: соперника можно
не только обогнать, но и разбить. По трассе разбросаны аптечки, патроны и нитро.
За места и убийства платят монетами — они идут на три линии прокачки (двигатель, броня, пушка),
у каждой свой компромисс. Есть призраки лучших заездов, таблицы рекордов и ночные гонки.
Управление: клавиатура, геймпад или сенсорные кнопки с автогазом.

**en**
Combat racing on one circuit with five race types. Your car has a cannon: you can wreck a rival
instead of just passing them. Health, ammo and nitro pickups are scattered around the track.
Positions and kills pay coins for three upgrade lines (engine, armour, cannon), each with a
trade-off. Ghosts of best laps, leaderboards and night races included.
Keyboard, gamepad or on-screen touch controls with auto-throttle.

**tr**
Tek pistte beş yarış türüyle savaş yarışı. Aracınızda top var: rakibi sadece geçmekle kalmaz,
hurdaya da çevirebilirsiniz. Pistte can, cephane ve nitro kutuları var. Sıralama ve vuruşlar
para kazandırır; motor, zırh ve top olmak üzere üç geliştirme hattına harcanır.
Klavye, oyun kolu veya otomatik gazlı dokunmatik kontroller.

## Заметки для модератора

- SDK инициализируется до старта движка; `LoadingAPI.ready()` вызывается один раз, когда меню
  готово к вводу.
- `GameplayAPI.start/stop` парные: старт по сигналу GO, стоп на финише и на время рекламы.
- Реклама: interstitial только на экране результатов, rewarded — по явной кнопке игрока.
- `game_api_pause` / `game_api_resume` ставят игру на паузу (звук и симуляция).
- Игра полностью офлайн после загрузки, внешних запросов нет.

## Медиа

| Файл | Размер | Содержание |
|---|---|---|
| Иконка | 512×512 | Машина крупно, красная, на фоне трассы |
| Обложка | 800×470 | Кадр гонки в движении, без текста |
| Скриншот 1 | 1920×1080 | Гонка днём: HUD, прицел, соперники |
| Скриншот 2 | 1920×1080 | Ночная гонка (Night Brawl), фары |
| Скриншот 3 | 1920×1080 | Экран результатов с наградой |
| Скриншот 4 | 1920×1080 | Гараж (три линии прокачки) |
| Скриншот 5 | 1080×1920 или 720×1280 | Мобильная раскладка с тач-кнопками |

Снимаются скриптом из `Docs/AI/qa-browser-smoke.md` (chrome-devtools, локальная сборка с мок-SDK).
