# CarsGame: рефакторинг. Промпты для агента по этапам

Дата: 2026-09-25. Источник: [`RefactorPlan.md`](RefactorPlan.md). Каждый этап — это один промпт для агента (Claude Code, Codex, Cursor), сессия одна. Этапы идут строго по порядку:

`подготовка → R0 → R1 → R2 → R3 → R7 → R8 → R9 → R10 → D`

Пункты B — каждый отдельной сессией:
- B2, B3 и B5 можно делать в любой момент после R0: метрики `race.sh` они не двигают.
- B1 и B4 — только после R10 или с новой базой `race.sh`. Они меняют порядок шага и бой, после них коридор R0 уже не гейт для R7 и R10.

После каждого этапа три шага:
1. Адверсариальное ревью диффа двумя моделями.
2. Запись в `ProgressLog.md`.
3. Остановка до решения пользователя о слиянии в `main`.

## Преамбула (вставлять в начало каждого промпта)

```
Проект: /home/user/projects/games/Unity/newgames/CarsGame, Unity 6000.5.9f1, URP 17.5, цель — WebGL (Яндекс Игры).
Код в Assets/CarsGame, сборки: RacingGame.Core (чистый C#), Game, Platform, Editor, Tests/EditMode, Tests/PlayMode.
Работаешь ТОЛЬКО в worktree: /home/user/projects/games/Unity/newgames/CarsGame/.worktrees/refactor,
ветка refactor/code-health. Основное дерево main не трогать: там открыт Unity Editor и работают другие сессии.

Перед работой прочитай:
- Docs/AI/RefactorPlan.md — весь документ. Особенно «Принципы», «Чек-лист каждого этапа» и раздел своего этапа.
- Последние записи Docs/AI/ProgressLog.md.
- Каждый файл, который собираешься менять.
Если промпт расходится с планом, прав план.
Если план расходится с кодом в фактах (номера строк, имена, числа), доверяй коду и поправь план.
Если план расходится с кодом в поведении, ОСТАНОВИСЬ и опиши это в отчёте. Код ради плана не менять.

Правила:
- Рефакторинг (этапы R) не меняет поведение. Ожидаемые числа в существующих тестах не менять.
  Если правка меняет поведение, её место в B или D: остановись и опиши её в отчёте, не делай.
- Сначала характеризационный тест на текущее поведение, потом перенос.
- ECS-стиль: состояние тика — struct в RacingGame.Core без UnityEngine (Mathf и Vector3 можно там, где они уже есть);
  шаг — static Step(ref state, in input). MonoBehaviour хранит struct и применяет побочные эффекты.
  Публичный API хостов (свойства, события) сохраняется. Пакеты Entities, Burst, Collections не добавлять.
  DI-контейнер и базовые классы ради регистрации не заводить.
- Новый и переносимый код шага вызывается из SimulationStep(int tick) под SimulationDriver (50 Гц).
  Существующую логику вне тика (RaceManager.Update, RaceClock, PerfectStart, автосброс, колбэки PhysX)
  в этапах R НЕ переводить в тик: это этап D.
  Случайность — только RaceDirector.Rng / RngStreams. В горячем пути нет аллокаций, LINQ и замыканий.
- Новое правило игры = ID в Docs/Requirements.md + тест с [Category("REQ:<ID>")].
  Реестр — индекс «ID → источник → тест», дизайн в нём не пересказывать.
- Сейв хранит enum как int: целые значения enum не менять. Формат SaveData без миграции не менять.
- Тесты на рефлексии падают только во время выполнения. Перед каждым коммитом ищи по Tests имена приватных
  членов, которые ты переименовал или перенёс (список — в RefactorPlan.md, «Чек-лист каждого этапа»).
- Перед правкой перечитывай файл. Если файл из списка своего этапа менялся в main после создания ветки
  (git log main -- <файл>), сначала сделай rebase ветки на main, потом правь.

Инструменты (batch Unity на копии проекта; мост к Editor не использовать):
- Переменные: export PROJECT=<путь к worktree> CG_WORK=/tmp/cars-qa/refactor SIM=/tmp/cars-qa/refactor/sim
- Первая копия: если /tmp/cars-qa/sim уже есть, сделай rsync -a /tmp/cars-qa/sim/ $SIM/ — это пропустит
  переимпорт на ~5 минут.
- EditMode: Tools/qa/edit-tests.sh (печатает total/passed/failed и сообщения падений).
- Гонка: Tools/qa/race.sh <tag> CG_EVENT=e1 CG_TIER=normal CG_SEED=12345 CG_LAPS=2 → JSON-отчёт в $CG_WORK.
  Долгие прогоны запускать через setsid nohup … & и ждать файл-признак: перезапуск сессии убивает фоновые шеллы.
- Никогда не запускай batch Unity на пути основного проекта: это ломает мост открытого Editor.
- pkill -f <шаблон> убивает и твой собственный шелл — не использовать.

Коммиты:
- Только в refactor/code-health, одна логическая правка — один коммит, стиль «refactor(<область>): …» как в git log.
- В main не сливать и не пушить. Слияние решает пользователь после ревью.

Конец этапа:
1. edit-tests.sh зелёный, набор тестов равен базе R0 плюс новые. Для R7, R10 и D ещё race.sh (см. этап).
2. Адверсариальное ревью диффа этапа (git diff <начало этапа>..HEAD) двумя моделями, параллельно, только на чтение:
   node ~/.claude/plugins/cache/openai-codex/codex/1.0.6/scripts/codex-companion.mjs adversarial-review "<фокус этапа>"
   agent -p --trust --mode ask --model cursor-grok-4.6-high-fast --output-format text "<промпт ревью с путём к диффу>"
   Главный вопрос ревью: где поведение изменилось. Исправить подтверждённое, ложные находки перечислить с опровержением.
3. Запись в Docs/AI/ProgressLog.md: что сделано, цифры тестов и гонок, что осталось, какие пункты ушли в B или D.
4. Отчёт пользователю: коммиты, цифры до и после, расхождения плана с кодом, находки ревью.
```

---

## Подготовка (выполняет пользователь или агент с его разрешения)

Цель — чистая база для ветки.

```
[Преамбула — кроме строки про worktree: здесь работаешь в основном дереве, только на чтение и git]

1. git status в основном дереве. Если есть незакоммиченные изменения, ОСТАНОВИСЬ. Спроси пользователя, чьи они
   и кто их коммитит. Сам не коммить и не откатывай: это работа других сессий.
2. Docs/AI/RefactorPlan.md и Docs/AI/RefactorPrompts.md должны быть в main, иначе их не будет в worktree.
   Если они не закоммичены, спроси пользователя и закоммить только эти два файла: «docs(refactor): plan and prompts».
3. Когда main чистый: git worktree add .worktrees/refactor -b refactor/code-health main.
4. Проверь, что Tools/qa/edit-tests.sh с PROJECT=<worktree> проходит на копии. Запиши число тестов.
5. Отчёт: хэш main, путь worktree, число тестов.
```

---

## R0. База, реестр требований, общие фикстуры

```
[Преамбула]

Задача: этап R0 из Docs/AI/RefactorPlan.md. Игровой код не менять, только тесты, документы и Tools/qa.

1. База (каждый прогон дважды, чтобы получить коридор шума):
   - edit-tests.sh: total и failed.
   - race.sh на e1/normal, сиды 12345, 777, 4242, CG_LAPS=2.
   - race.sh на одном боевом событии на тех же сидах. На e1 у ИИ огонь начинается через 30 с (RaceSpawner.cs,
     startGraceSeconds), поэтому бой на нём не проверишь. Выбери событие с обычной задержкой огня ИИ,
     в котором ИИ реально стреляет, и запиши в отчёт, какое выбрал.
   - Размер zip из Tools/qa/build-web.sh.
   Коридор = min..max по двум прогонам для времени гонки, кругов, автосбросов, выстрелов и попаданий.
   Записать обе пары значений, а не только границы.
   Если два прогона совпали побитно, коридор нулевой, и любой шум PhysX провалит R7/R10. Тогда предложи
   пользователю минимальную ширину (например ±3 % по времени и ±2 по счётчикам) и запиши согласованный порог.
   Коридор записать в ProgressLog.md и не менять на следующих этапах.
2. Инвентарь: системы, появившиеся после 2026-09-25 (CarBodies, MenuScreens.Bodies, HotBombRace, TrackFork,
   MusicDirector и всё новое на диске). Для каждой указать этап R или пометку «вне R», если резать её
   не нужно. Список дописать в пункт «Инвентарь» раздела R0 в RefactorPlan.md.
3. Docs/Requirements.md — индекс требований.
   - Группы: ECO, UPG, WPN, COS, ADS, RWD, EVT, STAR, TWIST, DAILY, LB, SAVE, RACE, COMBAT, FLOW.
   - Строка на каждое требование: ID | формулировка в одну строку | «текущее»/«будущее» | источник (файл:строка
     или документ) | тест.
   - Правила брать из кода: XML-комментарии Core, Economy, AdPacer, DailyChallenge, SaveMerge, SaveMigrator,
     RewardedOffers, EventRules, ассеты событий. Дополнительно — MidcoreDesign.md и GameplayImprovements.md.
   - Не выдумывать. На механики, которых нет в коде (очки и «Угар» из ArenaMode.md), ID не заводить.
4. Tools/qa/req-coverage.sh (bash + grep, без Unity):
   - сравнивает ID из Requirements.md с [Category("REQ:…")] в Assets/CarsGame/Tests;
   - печатает требования без тестов и теги без требования;
   - --groups FLOW,EVT,… ограничивает проверку группами;
   - с --report всегда возвращает 0, без флага — 1, если в выбранных группах есть пробелы.
5. Проставить [Category("REQ:…")] существующим тестам, которые уже проверяют правило: EconomyTests,
   AdPacerTests, DailyChallengeTests, DailyRaceTests, SaveMergeTests, SaveMigratorTests, SaveServiceTests,
   EventRulesTests, StarGoalsTests, ArenaModeTests, RewardedOffersTests, PlayerProgressionTests.
   Тесты не переименовывать, утверждения не менять.
6. Общие фикстуры в Assets/CarsGame/Tests/EditMode/Support/:
   - TestCarRig: Rigidbody + CarController + CarStatus + CarWeapons + CarCannon + CarHealth. Сейчас эта сборка
     скопирована примерно в 7 файлах.
   - FakePlatform: IAds, ILeaderboards, IServerClock, ICloudSave из Core/Platform/Contracts.cs. Запоминает вызовы,
     результат настраивается.
   - TestSave.
   В PlayMode: общий Boot() (ждёт PlatformGlue.IsReady, затем 2 кадра, затем GameFlow) и Ticks(n).
   Перевести на фикстуры 1–2 старых теста, чтобы доказать, что они работают. Остальные — по ходу этапов.

Готово, когда:
- реестр и скрипт есть, категории проставлены;
- req-coverage.sh --report печатает список пробелов — это вход для R2;
- edit-tests.sh даёт тот же набор зелёных тестов, что база;
- коридоры записаны.
Фокус ревью: «R0: реестр требований соответствует коду, фикстуры не меняют утверждения тестов».
```

---

## R1. Точечный DRY, enum оружия в Core

```
[Преамбула]

Задача: этап R1 из Docs/AI/RefactorPlan.md. Каждый пункт — отдельный коммит.

1. CarWeapons шагает один раз. Живой путь — вызов из CarController.SimulationStep, в порядке CarId, до CarStatus.
   Регистрация в SimulationDriver (SimulationOrder 950+id) сейчас пустая: её отсекает lastTickedTick.
   - Убери Register/Unregister в CarWeapons.OnEnable/OnDisable и свойство SimulationOrder, если оно больше
     нигде не нужно. Защиту lastTickedTick оставь.
   - Вызов из CarController НЕ убирай.
   - Проверка: CarWeaponsTests целиком, включая SingleTickDeduplication_… и тест вторичного огня
     через CarController.SimulationStep.
2. RaceDirector.Get() вместо цепочек «RaceDirector.Current ?? … ?? Instance» (около 12 мест). Семантику
   выбора не менять: сначала Current, потом то, что было дальше в цепочке.
   Конструкцию GetComponent<T>() ?? AddComponent<T>() НЕ трогать: в 6000.5 она работает.
3. RaceDirector.LiveCars — один обход машин. Условие отбора передаётся параметром и в каждом месте остаётся
   побитно прежним. Места: MineHazard (два разных условия), MineField, OilHazardPool, TargetFinder (×3),
   SpecialWeaponAttack. Условия НЕ объединять: мины сейчас не учитывают неуязвимость, TargetFinder учитывает.
   Именованные предикаты CanTarget и CanTouchHazard заводить, только если у двух и более мест условие
   совпадает полностью. Кэш car.Health на CarController допустим.
4. Enum оружия в Core (например Core/Combat/WeaponTypes.cs). Переезжают:
   - PrimaryWeaponType и SecondaryWeaponType из CarWeapons.cs;
   - PlayerProgression.KindOf;
   - PrimaryWeaponCatalog с Profile, вместе с ProjectileKind, WeaponMode, MineSkin.
   Целые значения и namespace оставить, чтобы не трогать сериализованные ассеты. Если namespace меняется,
   проверь *.asset и *.prefab на ссылки по типу. Проверка: SaveMigratorTests, SaveMergeTests, CarWeaponsTests,
   ArsenalExpansionTests, PlayerProgressionTests.
5. DailyRace.IsDaily вместо копий разбора «'d' + цифра» в PlatformGlue и GameFlow.
6. ProceduralCarBuilder — в Editor-сборку. Перед переносом grep: рантайм его вызывать не должен,
   только Editor/RaceCarPrefabBaker.cs.
7. Удалить Core/Combat/StatusEffect.cs и его .meta, предварительно проверив grep, что ссылок нет.
8. Повторы чисел — к одному имени, значения не менять:
   - формула EffectiveMaxSteer в CarController (свойство и копия в ApplySteering);
   - радиус колеса НЕ трогать: литерал 0.35 и поле wheelRadius = 0.34 различаются. Это пункт B5;
   - демпфирование в CarController.Awake и CarHealth.Respawn;
   - база вторичного оружия (3, 3f, 2, 4f) в PlayerProgression и значения по умолчанию в CarWeapons;
   - урон мины 25f (CarWeapons, MineHazard) и база заряда 45f (MineHazard, PlayerProgression).
     Поле chargeDamage остаётся (его меняет гараж), оно берёт базу из каталога.
Не делать: фабрику Shader.Find, базовый класс SimulatedBehaviour.

Готово, когда:
- затронутые тесты зелёные;
- grep StatusEffect пуст;
- ProceduralCarBuilder вне RacingGame.Game;
- enum лежат в Core, старые сейвы читаются.
Фокус ревью: «R1: порядок шага CarWeapons, условия отбора целей побитно прежние, целые значения enum».
```

---

## R2. Правила Flow и меты в Core

```
[Преамбула]

Задача: этап R2 из Docs/AI/RefactorPlan.md. Цель — бизнес-правила из GameFlow, PlatformGlue, RewardedOffers
и MenuScreens* покрыты EditMode-тестами без сцены.
Владение: GameFlow и MenuScreens* правят и другие сессии. Перед стартом сделай rebase на main
и перечисли в отчёте, что в этих файлах поменялось.

Уже в Core — формулы не трогать и не копировать, только вызывать:
- Economy.RaceReward и WithFirstRaceFloor (покрыты EconomyTests);
- DailyChallenge.FirstFinishReward (150 + 30 × (серия − 1), не больше 330), StreakEndingAt, CurrentStreak,
  EncodeScore (покрыты DailyChallengeTests);
- trial в RewardedOffers (RewardedOffersTests).
В R2 переносится только политика хоста вокруг этих формул.

Вынести в Core plain-типы. Хост в Game вызывает их и пишет в сейв, UI и рекламу:
1. Прогресс: звёзды объединяются через OR, лучшее время не ухудшается (сейчас в GameFlow.OnFinished).
2. Анлоки: IsUnlocked, UnlockNext, следующее событие. Плюс тест на данные: цепочка по реальным ассетам
   событий (Assets/Data/Events e1–e5, Resources/Events e6+), включая события, открытые изначально.
3. Дейлик (политика из GameFlow, около строк 525–545): запись дня в finishDays, обрезка старше KeepDays,
   один бонус в день, удаление старых строк d* из bests. Бонус считает существующий FirstFinishReward.
4. TrackHash (FNV) для сида призрака.
5. Лоадаут: соответствие слота и equippedSecondary. Сейчас продублировано в MenuScreens примерно 3 раза.
   Использует enum из R1.
6. Гараж: покупка, экипировка, выбор билда. Нехватка денег и максимальный уровень не меняют сейв
   (проверить побайтно через JsonUtility).
7. Удвоение награды: один раз за гонку.
8. Продолжение: один раз. Отказ и ошибка рекламы машину не оживляют.
9. REQ-FLOW-GEN: поздний асинхронный ответ со старым raceGeneration не применяется.
   - В GameFlow семь ручных сравнений (около строк 250, 396, 400, 408, 417, 587, 592).
   - Свести их к одному IsCurrent(generation). Отдельный тип-токен не заводить.
   - Остальная часть Valid() (состояние сцены) остаётся в хосте.
10. Лидерборд: выбор доски в SubmitAllBestsAsync (PlatformGlue.cs:76–92) — чистая функция
    «строка рекорда → (доска, счёт) или пропуск». Правила: строки без времени пропускаются; дейлик идёт
    только сегодняшний, в DailyRace.BoardName через существующий EncodeScore; остальные — в best_<id>
    в миллисекундах. Отправка остаётся в хосте.
11. Порядок после финиша: сейв → лидерборд → interstitial, одна реклама за раз
    (PlatformGlue, около строк 174–177). Тест на FakePlatform проверяет порядок вызовов.
Каждое правило — ID в Requirements.md и тест с [Category("REQ:…")].

Готово, когда:
- req-coverage.sh --groups FLOW,EVT,STAR,LB,DAILY,ADS,RWD возвращает 0. Группы COMBAT, RACE, WPN закрываются
  в R3 и R7, сейчас их пробелы допустимы;
- в GameFlow не осталось формул этих правил и ручных сравнений поколения;
- PlayMode TwistPlayTests зелёные.
Фокус ревью: «R2: правила наград, анлоков, дейлика и лидербордов совпадают с прежним GameFlow/PlatformGlue».
```

---

## R3. Компоненты боя в Core

```
[Преамбула]

Задача: этап R3 из Docs/AI/RefactorPlan.md. Фасады CarStatus, CarWeapons, CarCannon и CarHealth
сохраняют публичный API.

1. StatusTimers (Core/Combat): таймеры по StatusKind вместо пяти скопированных switch-блоков в CarStatus.
   До переноса отдельный тест на особый иммунитет Frozen (CarStatus.cs, около строк 119–123 и 166–167).
   Старт — от CarStatusTests.
2. HeatGauge из нагрева CarWeapons.
   - ApplyHeatProfile как сейчас: ВСЕГДА перезаписывает heatPerShot, heatCooldownRate, overheatThreshold и
     overheatRecoveryThreshold.
   - Числа 20, 100, 40 перенести в профиль каталога с теми же значениями. 2.5 для пулемёта уже в
     PrimaryWeaponCatalog; множитель machineGunHeatFactor сохранить.
   - Отказ от перезаписи инспектора — смена поведения, это не R3.
3. MagazineState из CarCannon: магазин, перезарядка, парковка магазина при смене основного оружия.
4. HitResolver из разбора попадания в CarHealth (HitInfo → HitResult).
5. Вторичный огонь (мина, заряд, заморозка): три функции в хосте, им нужен пул. В Core — только решение
   «можно ли выстрелить» и остаток патронов. Иерархию стратегий не заводить.
Состояние турели (turretYaw, barrelPitch, turretTarget) пока остаётся в CarCannon.

Готово, когда: CarWeaponsTests, CarStatusTests, CarHealthHitTests, MineHazardTests, ArsenalExpansionTests,
MineFieldAndChargeTests зелёные без смены ожидаемых чисел. Боевое событие из R0 на race.sh — в коридоре.
req-coverage.sh --groups COMBAT,WPN возвращает 0.
Фокус ревью: «R3: нагрев, магазин, статусы и попадания численно те же; иммунитет Frozen; порядок внутри тика».
```

---

## R7. ИИ

```
[Преамбула]

Задача: этап R7 из Docs/AI/RefactorPlan.md.

1. В Core — чистые планировщики от снимка чисел, выход — DriveCommand:
   - задержка команды (линия задержки реакции);
   - скорость в повороте (v = √(vc² + 2ax)); кривизну брать из TrackProfile, не копировать;
   - выход из застревания и переворота;
   - выбор полосы со штрафом за мину.
   Запросы к миру (лучи, список машин) остаются в Game/AI: хост собирает снимок, планировщик решает.
2. AiGunner: одна формула каденса вместо двух, одна проверка «цель мертва / заглушен / перегрет».
   LateralRateSampler — в Core.
3. Автострельбу вторичным на таче в PlayerDriverInput.Sample НЕ трогать: перенос сдвинет момент выстрела,
   это этап D.
Тесты на планировщики — таблицами входов и выходов, числа снять с текущего кода до переноса.

Готово, когда:
- AiCombatAndInputTests, AiRecoveryTests, TrackProfileTests зелёные;
- race.sh на e1 и на боевом событии, на трёх сидах, в коридоре R0.
Выход за коридор — это ошибка переноса: найти, а не расширять коридор.
Фокус ревью: «R7: планировщики ИИ дают те же команды, что старый AiDriver/AiGunner».
```

---

## R8. HUD

```
[Преамбула]

Задача: этап R8 из Docs/AI/RefactorPlan.md. Файл Game/UI/HudScreen.cs, около 1360 строк.
Владение: перед стартом rebase на main, перечислить изменения HudScreen в main.

Коммит 1 — partial без правки кода: HudScreen.Build.cs, HudScreen.Combat.cs, HudScreen.Feed.cs.
Методы переносятся как есть, поля остаются в корне.

Коммит 2 — plain-виджеты (не MonoBehaviour) только для частей с собственным состоянием:
прицел, маркеры угрозы, лента убийств, индикаторы статусов (StatusIndicatorWidget — в свой файл),
панель оружия с колесом.
- Интерфейс виджета: Build, Bind(ссылки на игрока), Tick, LateTick, ApplyLayout(touch, narrow), Localize.
- У корня два прохода, как сейчас: Update и LateUpdate. Прицел, маркеры угрозы и тосты — в LateTick,
  после камеры. Порядок вызова виджетов — как порядок кода сейчас.
- Простые подписи и полоски остаются методами корня.

Коммит 3 — одна таблица прямоугольников для десктопа и тача (сейчас смещения задаются и при построении,
и в ApplyLayout) и тест на неё для narrow/touch.
Чистую математику угрозы («за спиной», «целится в меня») и разбор имени в ленте вынести в Core,
если там нет Camera и RectTransform.
Тесты на рефлексии: KillFeedTests вызывает HudScreen.OnEnable через рефлексию. Имя сохранить или обновить тест.

Готово, когда:
- KillFeedTests и тест раскладки зелёные;
- Tools/qa/browser-smoke.mjs дополнен скриншотом HUD на десктопе и таче. Скриншоты до и после — для
  просмотра глазами, пиксельного совпадения WebGL не требовать. Гейт — тест таблицы прямоугольников.
  Приложи пути к скриншотам.
Фокус ревью: «R8: порядок Update/LateUpdate виджетов, раскладка touch/narrow, подписки и отписки событий».
```

---

## R9. Меню

```
[Преамбула]

Задача: этап R9 из Docs/AI/RefactorPlan.md.
Владение: перед стартом rebase на main, перечислить изменения MenuScreens* в main.

1. Продолжить partial-разрез по экранам: MenuScreens.Main.cs, .Garage.cs, .Results.cs
   (.Extras и .Bodies уже есть).
2. Общие виджеты (Label, Item, секция, строка таблицы, разметка звёзд) — в один хелпер.
   Копии звёзд и строки «загрузка…» убрать.
3. Покупка, экипировка и выбор билда — только через сервисы из R2. Экран показывает результат и toast.
MenuRouter не заводить.
Тесты на рефлексии: UiInteractionTests обращается к MenuScreens.flow, race, BuildResults через рефлексию.
Имена сохранить или обновить тест.

Готово, когда:
- UiInteractionTests зелёные;
- «не хватает монет» не меняет сейв (тест из R2);
- в экранах нет формул Economy;
- смоук меню в browser-smoke.mjs проходит.
Фокус ревью: «R9: покупки только через сервис, навигация и фокус экранов прежние».
```

---

## R10. Машина

```
[Преамбула]

Задача: этап R10 из Docs/AI/RefactorPlan.md. Файл Game/Vehicle/CarController.cs. От него зависит ощущение езды,
поэтому числа не менять ни в одном коммите.

1. Настройка Rigidbody, колёс-сфер и материала трения — отдельный статический класс.
2. Визуал колёс и звук — отдельно от шага сил.
3. Модель руля — в Core, если считается из скорости и угла без Rigidbody.
4. ApplyCoastDeceleration и ApplyOffTrackDrag не сливать, пока замер не покажет, что это одна формула.
5. Масса и демпфирование — из одного места (с R1 они уже под одним именем).

Готово, когда:
- DriftPlayTests и GrassReversePlayTests зелёные;
- соло-круг race.sh CG_AI=0 CG_LAPS=1 CG_MAX_LAP_SECONDS=<из базы R0> проходит;
- три сида на e1 и боевом событии — в коридорах R0.
Фокус ревью: «R10: силы, руль и дрифт численно прежние, порядок внутри SimulationStep».
```

---

## B. Баги и изменения поведения (отдельные сессии, по одному пункту)

Каждый пункт: тест, который сейчас падает или фиксирует старое поведение, → исправление → отдельный коммит `fix(...)`.

B2, B3 и B5 — в любой момент после R0. B1 и B4 — после R10 или с новой базой `race.sh`: они двигают метрики гонки.

```
[Преамбула — кроме правила «поведение не меняется»: здесь меняется ровно одно описанное поведение]

B1. Полосы порядка шага пересекаются.
    nextCarId в RaceDirector только растёт. Порядки: CarController = id, CarStatus 900+id, оружие 950+id,
    CarHealth 1000+id, масло 1050, HotBomb 1100. Уже примерно через 7 гонок за сессию полосы перемешиваются.
    Тест: 8 гонок подряд в одной сессии, порядок шага на восьмой совпадает с первой.
    Исправление: ключ сортировки (полоса, id) ИЛИ сброс id на каждую гонку. Сброс id пересекается с D,
    поэтому выбор согласовать с пользователем.
B2. Свечение в WebGL.
    - EnableKeyword("_EMISSION") на материалах URP/Lit из Shader.Find: MineVisual, Pickup, RaceOverlays,
      WeaponArt. Ни один материал проекта на URP/Lit не включает _EMISSION, и стоит m_StripUnusedVariants: 1.
    - Сначала проверить: build-web.sh, затем браузерный смоук. Свечение мин и пикапов видно?
      Приложить скриншот.
    - Если свечения нет: сериализованные материалы в WorldAssets (см. TODO в CarCannon) или
      ShaderVariantCollection. Размер zip сравнить с базой.
B3. Отправка всех рекордов после каждого финиша (PlatformGlue.SubmitAllBestsAsync).
    Найти лимиты частоты лидербордов в документации Яндекс Игр. Если лимит есть — отправлять только
    изменившийся рекорд, тест на FakePlatform.
B4. Единое условие выбора цели для мин и прицела. Только по решению геймдизайна (спросить пользователя),
    тест на неуязвимую машину после респауна.
B5. Литерал радиуса колеса 0.35 и поле wheelRadius 0.34 в CarController — визуал вращения колёс.
    Решить с пользователем.
```

---

## D. Детерминизм под сеть (строго после R10)

```
[Преамбула — кроме правила «поведение не меняется»: здесь поведение меняется, критерий — допуск]

Задача: этап D из Docs/AI/RefactorPlan.md. До любого кода:
- записать допуск race.sh (коридор R0 плюс ожидаемые сдвиги) в ProgressLog.md;
- показать допуск пользователю и дождаться согласия.
Делать по одному подпункту за коммит:
1. Сид гонки из RaceSetup вместо Environment.TickCount. CarId = слот 0..7 со сбросом каждую гонку
   (если B1 решён иначе, согласовать).
2. Гонка на тиках: отсчёт, GO, финиш, grace, таймер арены, rubber-band, рейтинг. Туда же RaceManager.Update,
   PerfectStart, автосброс RaceParticipant.FixedUpdate. RaceClock остаётся только для отображения.
   Рекорды best_<id> и призраки на тиковое время НЕ переводить без отдельного решения пользователя:
   квант 20 мс сделает старые рекорды несравнимыми.
3. Попадания в тике: снаряды — sweep; мины, масло, пикапы и BoostGate — по расстоянию; таран — колбэк
   PhysX кладёт контакт в очередь, урон применяется в тике. Машины остаются на Rigidbody. Тесты,
   вызывающие OnTriggerEnter напрямую (RaceDirectorPoolTests, TrackForkPlayTests, SpaceTrackPlayTests),
   переписать в том же коммите.
4. Автострельба вторичным на таче: из PlayerDriverInput.Sample в шаг, с тестом момента выстрела.
5. SimWorld: массивы слотов фиксированной длины по текущим потолкам (машины Range(0,7)+1,
   maxLiveProjectiles 48, maxLiveMines 24, MaxSlicks 3; мины поля отдельно). Состояние турели в снапшоте.
   Снапшот — копия массивов. Хэш 500 тиков со скриптовым вводом — только для шагов без смены PhysX.
Критерий: три сида race.sh на e1 и боевом событии внутри допуска; в тике исхода гонки нет чтения Time.*
и RaceClock; колбэки PhysX только складывают события в очередь.
Фокус ревью: «D: детерминизм тика, сдвиг метрик в пределах допуска, совместимость рекордов и призраков».
```
