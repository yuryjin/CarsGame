# CarsGame — журнал выполнения плана

План: `~/.claude/plans/enumerated-fluttering-koala.md` (утверждён 2026-09-02, сверен с Codex gpt-5.6-sol — 40 находок учтены).

## 2026-09-02 — M0 (частично), M1, M2, M3 (код)

### Сделано
- **M0:** 20 материалов извлечены из сцены в `Assets/Materials/*.mat` (сцена 76 632 → 74 192 строки); теги/слои `Opponent`, `Car=8`, `Projectile=9`, `Track=10`, `Ground=11`; матрица коллизий Projectile×Projectile off (`DynamicsManager.asset`); дорога/кербы/барьеры → слой Track, `Ground_Terrain` → Ground; `Time.maximumDeltaTime = 0.1`.
- **Структура:** `Assets/CarsGame/{Core,Platform,Game,Editor,Tests/EditMode}` с asmdef; 8 скриптов перенесены через `move_asset` (GUID сохранены, ссылки сцены целы); `Assets/Scripts` удалена.
- **M1:** `DriveCommand`, `ICarInputSource`, `EdgeLatch`, `RngStreams`, `RaceRanking` (Core); `SimulationDriver` (единый FixedUpdate-цикл, тик), `RaceDirector` (CarId, RNG, пул снарядов), `EffectPool` (вспышки без аллокаций, без лайтов на мобиле), `PlayerDriverInput` (карта `Driving` в `InputSystem_Actions.inputactions`: Throttle/Steer/Brake/Fire/Reset/Pause; фолбэк на опрос устройств), `ScriptedDriverInput`; `CarController` → `ApplyCommand/SimulationStep/Teleport/GroundQuery`, слой Car; `CarCannon` в тиках через `RequestProjectile`; `Projectile` пулится с полным сбросом; `CarHealth` — атрибуция урона, `EffectPool`; `OpponentDriver` как источник команд.
- **M2:** `TrackSpline` (Core, чистая модель кольца: проекция, тангенс, кривизна, реверс), `LapCounter` (Core: упорядоченные ворота, телепорт/реверс не засчитываются, `TotalProgress` корректен для решётки), `TrackPath`, `RaceProgress`, `RaceClock` (unscaled, с паузой), `IRespawnPoseProvider`, `RaceParticipant` (респаун на трассе с проверкой занятости), off-track торможение и потолок скорости, неуязвимость после респауна, `CarPaint` (покраска переживает wreck-tint).
- **M3 (код):** `AiDriverProfile` (SO), `AiDriver` (heading-steering по lookahead, скорость по кривизне, обгон SphereCast'ами, застревание → реверс → респаун, задержка реакции, шум из RNG-потока AI), `StartGrid`, `RaceSpawner`, `Editor/RaceCarPrefabBaker` (префаб из `ProceduralCarBuilder`, меши в `Assets/Generated/Meshes`, колесо = 1–2 меша).
- **Codex adversarial-review диффа M1–M3 (gpt-5.6-sol):** 4 находки high. Исправлены три: круг засчитывался на воротах 11 вместо повторного пересечения линии (`LapCounter.LineArmed` + 2 регрессионных теста); `AiDriver` инициализировался в `OnEnable` до присвоения профиля (`Initialize(profile, lane)` из спавнера); выбор полосы при обгоне осциллировал (латч направления, гистерезис 0.5 с, «зажат» → держать полосу и притормаживать). Четвёртая — сцена не подключена — ждёт моста (скрипт `m3_scene_setup.cs`).
- **Тесты:** 25 EditMode-тестов (`EdgeLatch`, `RaceRanking`, `TrackSpline`, `LapCounter`) — проходят офлайн (Roslyn из поставки Unity + рефлексивный раннер, см. память `pipeline-run-tests-deadlock`).

### 2026-09-02 (вечер) — мост разблокировался сам, M1–M3 проверены в редакторе, M4 сделан
- Мост восстановился, когда зависший HTTP-запрос отвалился по сети. Тесты (25) прошли в редакторе асинхронно.
- **M1 в Play-mode:** скриптовый ввод разгоняет машину, за 32 тика удержания огня — ровно 2 выстрела (кулдаун 20 тиков), снаряд возвращается в пул, сброс без ошибок.
- **M3 в сцене:** `RaceCar.prefab` (20 рендереров), меши в `Assets/Generated/Meshes`, 4 профиля AI, `TrackPath` на `Track_Markings` (940.3 м), `Race_Manager` (`StartGrid`+`RaceSpawner`), ручные машины удалены. Гонка 3 AI: круги 59.5 / 59.9 / 64.8 с, без застреваний. Тюнинг AI: демпфирование по угловой скорости (убрало виляние), защита от сноса, возврат к центру на траве. Остаток — выезды на траву в связке у 280–355 м (M6).
- **M4:** `RaceManager` (отсчёт с FreezeAll, ранжирование, финиш, «резинка», результаты, DNF-отсечка), `DifficultyTier` ×3 (`Assets/Data/Difficulty`), `GameFlow` (пауза Esc, звуковые события, рестарт), `PauseController` (биты источников), HUD на TMP (скорость, HP, круг, позиция, таймер, отсчёт, wrong-way, подсказки 30 с, результаты, пауза), HP-бары над AI, тач-кнопки `OnScreenButton`, процедурный звук (`SfxSynth`: двигатель, выстрел, попадание, круг, отсчёт, финиш). TMP Essential Resources импортированы. `OnGUI` удалены. Проверено в Play-mode: отсчёт → гонка → уникальные позиции → финиш AI с временами; HUD и двигатель активны.
- Скриншоты `capture_game_view` (source=camera) не показывают overlay-UI — визуальная проверка HUD ждёт режима захвата Game View.

### 2026-09-02 (ночь) — Codex-ревью M4 закрыто, M5 написан
- **Codex по M4** (6 находок): спавн атомарный (`RaceSpawner.Spawn()` → bool, иначе `Idle`); `CarHealth.SetMaxHealth` для множителя сложности; синглтоны не пересоздаются при выгрузке (`SimulationDriver.Current`/`RaceDirector.Current`); обработчики кругов отписываются, поздние финиши после результатов игнорируются; звук выстрела в `CarCannon.TryFire`; тач-кнопки: латч клавиатуры и восстановление `autoThrottle`.
- **M5 (код):** контракты в `Core/Platform` (`IPlatformServices` + capability-интерфейсы `IAds/ICloudSave/ILeaderboards/IAsyncSessions`, `Result<T>`, `PauseReason`), `SaveData`/`SaveMerge` (монотонные earned/spent, min/max/union)/`SaveMigrator`/`SaveService` (коалесценция ≥5 с, чекпоинты), `AdPacer`, `LanguageResolver` (34 языка, фолбэки Яндекса) — 15 новых тестов; `PlatformRuntime` (`[Preserve]`, unscaled-таймауты, `SendMessage`-цели), `PlatformBootstrap` по дефайнам, `EditorMockPlatform` (задержки/сбои/реклама с паузой), `StandalonePlatform`, `WebMockPlatform`; `YandexPlatform` + `YandexNative` + `Plugins/WebGL/YandexBridge.jslib`; шаблон `WebGLTemplates/Yandex` (`index.html` без баннера Unity, DPR-кап, `cars-platform.js` с `sdkReady`, буфером host-pause до старта Unity, `once`-семантикой рекламы, коалесценцией `setData`, лидербордами и `multiplayer.sessions`; `ysdk-mock.js` для локального теста; `style.css`); `link.xml`; `Editor/Build/BuildCommands` (WebGL: Gzip+fallback, stripping High, дефайн витрины) и `WebZipPostprocessor` (валидация путей/размера, отчёт, zip с `index.html` в корне); `PlatformGlue` (сейвы по чекпоинтам, маркеры геймплея, `ready()`, host-pause, interstitial по пейсеру).

### M5 проверен в Play-mode, M6 (бой) сделан
- **M5 (мок в редакторе):** init, `lang=ru`, `ready()` один раз, `gameplay start` на GO / `stop` на финише, interstitial после результатов с паузой `Ad`; 40/40 тестов в редакторе. Сейв пишется на любом завершении гонки (в т.ч. DNF).
- **M6:** `AiGunner` (конус 9°, 6–45 м, LOS, кулдаун тира × агрессия, 3 с без огня после старта, подавление при отрыве >120 м от игрока), магазин пушки 5 выстрелов / перезарядка 4 с (`AmmoChanged`, `RefillAmmo`), событие `RaceDirector.ShellHit` → hit-marker в HUD, боезапас в HUD, пикапы HP/боезапас/нитро (`Pickup`, `PickupSpawner`: 9 штук по сиду, респаун 20 с), нитро-буст в `CarController`. Проверено: AI стреляют (4 выстрела, 1 попадание за 40 с), пикапы спавнятся.

### Codex-ревью M5 закрыто (6 находок)
- Мок SDK только на localhost/file; на проде без SDK — экран «Повторить», Unity не стартует (требование «SDK до геймплея»).
- Облачный сейв: `loadSave` не выдаёт таймаут за успех; `SaveService.CloudSynced` — до успешной загрузки облака записи в облако запрещены (нельзя затереть более новый прогресс), ретраи с бэкоффом; JS-писатель сериализован, данные держатся до подтверждения `setData`, размер считается в байтах UTF-8, скользящая квота 90/5 мин; `FlushAsync` возвращает `Result`, при сбое данные остаются dirty.
- Мерж: счётчики (монеты earned/spent, гонки, победы, килы, смерти, дистанция) хранятся по устройствам (`DeviceLedger`), мержатся по max на строку и суммируются — траты с двух устройств не теряются (тест `ConcurrentSpendingOnTwoDevicesKeepsBothPurchases`).
- Счёт лидерборда через `double`/`Number` (без i64 ABI).
- Реклама: без пре-open таймеров в JS (поздний `onOpen` больше не ставит паузу без владельца); внешний таймаут — в C#.
- `WebZipPostprocessor` бросает `BuildFailedException`, удаляет старый zip до валидации, проверяет `index.html` в корне архива.

### Codex-ревью M6 закрыто (8 находок)
- Сейв: `SaveMigrator.Parse` различает Empty/Ok/Corrupt/NewerVersion — битые или более новые облачные данные блокируют запись; загрузка single-flight, облако мержится в актуальные данные (прогресс во время загрузки не теряется); JS-писатель — один drain-цикл, после записи сразу отправляет более новую очередь; watchdog на открытую рекламу (90/150 с) снимает паузу и завершает запрос при потерянном колбэке, поздние колбэки игнорируются.
- Бой: `TakeDamage` возвращает bool, попадания/hit-marker только при реальном уроне; кил — текущему атакующему (стена = без кила); пикапы не для разбитых машин, респаун откладывается, пока на точке стоит машина; тени у префаба — только кузов и стекло (2 кастера вместо 20).

### 2026-09-02/03 (продолжение) — M7–M9 сделаны, WebGL-модуль установлен
- **WebGL:** диск освобождён пользователем (23 ГБ), модуль поставлен `unity install-modules -m webgl`. Установщик распаковал его вложенно (`WebGLSupport/Editor/Data/PlaybackEngines/WebGLSupport/…`) — редактор его не видел; раскладка исправлена вручную, редактор перезапущен через `EditorApplication.OpenProject`, активная платформа переключена на WebGL (Addressables собирают контент только для активной платформы). Первая пробная сборка запущена.
- **M7:** `com.unity.localization` 1.5.12 (+Addressables); `strings.csv` — 78 ключей × 9 языков (en, ru, tr, zh, de, es, fr, pt, uk); `LocImporter` (валидация до записи, реконсиляция удалённых ключей, preload-таблица `Game`); `Loc`-фасад (готовность только после загрузки таблицы выбранной локали, ожидание инициализации без блокировки); HUD и меню полностью на ключах; язык = override из сейва → платформа → фолбэки Яндекса; субсет Noto Sans CJK SC (187 иероглифов, 65 КБ) + `FontAtlasBaker` (статический SDF-атлас как фолбэк TMP) — запечь в редакторе после сборки.
- **M8:** экономика (`Economy`: таблица мест, убывающий бонус за килы, DNF платит только за круги), `UpgradeCatalog` (3 линии с компромиссами, 4 уровня, 150·1.6^lvl), `RaceEventDefinition` ×5 (Sprint, Reverse Cup, Chaos, Night Brawl, Endurance; анлок «топ-3»), `GameFlow` как цикл меню→гонка→результаты (правила события: реверс трассы, ночь, множитель урона, число пикапов и AI), `MenuScreens` (главное меню, выбор события×сложность, гараж, настройки с циклом языков/звуком/автогазом, лидеры с гостевыми рекордами и кнопкой входа, результаты с удвоением награды за rewarded), лучшие времена и отправка в лидерборды после входа.
- **M9:** `GhostRecord` (16 байт/сэмпл, gzip+CRC, лимиты, тесты), `GhostRecorder`, `GhostCar`; свой лучший призрак в сейве; чужие через `ysdk.multiplayer.sessions` (нормализация payload в JS → `{ghost,name}`), поздние призраки стартуют с учётом времени гонки.
- **Codex по M7–M9 (read-only `task`, 30 находок):** все high/medium применены — асинхронная инициализация локализации, гейт меню до загрузки сейва, `ready()` при интерактивном меню, снапшот/защита удвоения награды, отмена призраков по поколению гонки, стабильный FNV-хэш трассы, лимиты записи призрака, экранирование имён игроков, восстановление constraints, `Rank()` до события финиша, единый порядок «награда → flush → лидерборд → interstitial», DNF без выплат за 0 кругов, отказ старта заблокированных событий, идемпотентный `Teardown`, тач-кнопки только в гонке и локализованы, автоподбор размера текста кнопок, tr `km/sa`. Тесты: 48 офлайн.

### Первая WebGL-сборка и проверка M8 в редакторе
- **Пробная WebGL-сборка (дефолтный шаблон, Brotli): успех за 3.7 мин, 16.7 МБ** (data 6.6 МБ, wasm 9.9 МБ). Первая попытка упала на линковке из-за импорта `_free` (заменён на `CarsYa_Free` в jslib). В Chrome (локальный сервер с `Content-Encoding: br`) игра стартует: WebGL2, Addressables подгружают бандлы локализации, ошибок нет.
- **M8 в Play-mode:** главное меню, выбор события (5 гонок, цикл сложности, замки с подсказкой), гараж (3 линии, цены) — все на русском; гонка стартует из меню; запись призрака идёт. CJK-атлас запечён (205 символов, 1024², фолбэк TMP).
- Исправлено: `RestoreDay` восстанавливает `ambientMode`/`ambientIntensity` (запись `ambientLight` переводила освещение во Flat и затемняла сцену).

### Сборка под Яндекс Игры готова к черновику
- `BuildCommands.BuildYandex`: шаблон `Yandex`, Gzip + decompression fallback, stripping High, `PLATFORM_YANDEX` → `Builds/Yandex/CarsGame-web-0.1.0.zip` (16 МБ, 15.6 МБ распаковано, `index.html` в корне, валидация путей прошла), отчёт `build-size-report.txt`.
- Проверено в Chrome с мок-SDK (`?sdk=mock&lang=ru`): SDK инициализируется до Unity, `attached`, `LoadingAPI.ready()` ровно один раз, меню на русском во весь экран, клик «ГОНКА → Спринт» запускает гонку и `GameplayAPI.start` (счётчик 1/0). Ошибок в консоли нет (только локальный 404 `/sdk.js`).
- Размер: убраны `com.unity.ai.inference` (6.6 МБ compute-шейдера попадали в сборку) и заставка Unity (2.7 МБ) — следующая сборка ожидаемо ~7 МБ.

### Полный цикл M8/M9 подтверждён в редакторе
- Меню → Спринт (easy) → финиш игрока (машину вёл AI): награда 176, монеты 176 → покупка «Двигатель» (−150), лучшее время записано, открыт «Кубок наоборот», призрак сохранён (781 сэмпл, 78 с), interstitial показан, `gameplay 1/1`, освещение после гонки остаётся Skybox.
- В WebGL найдено и исправлено: игрок с автогазом без ввода уезжал с трассы навсегда → авто-возврат на трассу (5 с на траве или >20 м от оси, `RaceParticipant`); тач-кнопки на десктопе только после реального касания; подсказка на таче перенесена под таймер.

### Codex-ревью последних правок (10 находок) — применено
- Смена личности при входе: `IdentityChanged` в контракте → `SaveService.OnIdentityChanged` блокирует облачные записи до загрузки/мержа сейва нового аккаунта; JS сбрасывает очередь гостя.
- Сессии Яндекса: числовые `meta1` (хэш события) / `meta2` (время), нормализация `timeline[].payload.d` и имени игрока в JS.
- Чекпоинт финиша строго последовательный: flush → лидерборды → interstitial. Лучший призрак помечает сейв dirty. AI сбрасывает состояние застревания/буфер задержки при телепорте. `authoredConstraints` очищаются. Тач-кнопки: восстановление автогаза в `OnDisable`, перестройка при смене языка. Импортёр удаляет локали/таблицы, исчезнувшие из CSV. Переход из результатов сразу на нужный экран (`MenuScreens.GoTo`).

### Что не сделано и почему
- **M10 (релиз):** архив `Builds/Yandex/CarsGame-web-0.1.0.zip` готов к загрузке как черновик в консоль Яндекс Игр. Остаётся ручное: создать игру в консоли, залить zip, создать лидерборды `best_e1…best_e5` (ascending, мс), проверить на реальном SDK (реклама, `game_api_pause`, авторизация, sessions), заполнить метаданные/иконки/скриншоты, объявить языки ru/en/tr (+zh после проверки CJK-шрифта на устройстве). Реальный `ysdk.multiplayer.sessions` не проверялся (только по документации).
- Языки сверх девяти (остальные из 34) — после скриншот-QA; RTL/индийские — v1.1.
- Тюнинг физики/AI в связке поворотов у 280–355 м и общий баланс «весело ли» — на оценку пользователя.
- Тюнинг физики/AI в связке поворотов у 280–355 м (выезды на траву) — открыт.
- Скриншот-проверка HUD на тач-раскладке (360×640) — не делалась (нет эмуляции Touchscreen в редакторе без устройства).

### Заблокировано (нужен пользователь)
1. **WebGL-модуль не установлен:** свободно 7.3 ГБ, модулю нужно 6.78 ГБ + 1.5 ГБ загрузки. Освободить ≥10 ГБ, затем `unity --proxy-disable install-modules -e 6000.5.9f1 -m webgl --accept-eula -y`.
2. ~~Live bridge завис~~ — восстановился; правило: `run_tests` только с `--async_tests true`.

### Следующие шаги после разблокировки моста (по порядку)
1. `unity command recompile` → `recompile_status`; `run_tests --async_tests true` → `test_status`.
2. Play-mode проверка M1: `scratchpad/pm_setup.cs` → через 3 с `pm_telemetry.cs` (скорость >50), `pm_fire_on.cs` → 0.1 с → `pm_fire_off.cs` (ровно 1 снаряд), `pm_reset.cs`.
3. Edit-mode: `scratchpad/m3_scene_setup.cs` (префаб, профили AI, `TrackPath`, `Race_Manager`, удаление ручных машин, сохранение сцены).
4. Play-mode M3: `m3_verify.cs` на 10 с и 75 с (все AI `laps ≥ 1`, `minPairDist > 4`, `resets == 0`).
5. Codex `adversarial-review` по диффу (запущен, результат в scratchpad `codex-m1-m3-review.md`) — применить находки.
6. Далее M4 (RaceManager, HUD, звук, онбординг).

## 2026-09-10 — мидкор-план и адверсариальная проверка
- `Docs/AI/MidcoreDesign.md`: черновик v1 (статусы, слоты оружия, 9 видов оружия, опасности трассы, режимы, 5 слоёв меты) → проверка пятью внешними моделями (Codex gpt-6-astra max, Grok 4.6 через Cursor CLI, MiMo / big-pickle / Nemotron через OpenCode; Unity AI Assistant запущен headless через `AssistantApi.RunHeadless`, ответ не получен) → план v2: MVP P0–P4 на 4–6 недель, очередь после MVP, условия возврата моста. Отзывы: `Docs/AI/reviews/midcore-2026-09-10/`.
- Найдено по коду в ходе проверки (не исправлено, в P0): респаун/неуязвимость/кулдаун столкновений на `Time.time` (`CarHealth`), пул снарядов без потолка и не чистится в `Teardown`, `KillsPerVictim` передаёт общее число килов (43 монеты вместо 75 за трёх жертв), `RaceRanking.Rank` аллоцирует ~10 раз/с, Ctrl уже занят под Fire, `TrackSpline.Position` интерполирует высоту, но проекция/кривизна плоские.
- Kimi/GLM/DeepSeek/Qwen в OpenCode не настроены. Hunyuan3D-2 с весами найден в `~/projects8/3d2/Hunyuan3D-2` (RTX 4070 Ti работает).
- `Docs/AI/MidcorePrompts.md`: план v2 разбит на 12 этапов (0–11) с готовыми промптами для агента и общей преамбулой; плюс короткие промпты после MVP (Арена, шипы/сюрикены, образец кузова, ракета, мост).
