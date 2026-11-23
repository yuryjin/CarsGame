# CarsGame

Аркадные гонки с боем на Unity включают кампанию, специальные трассы, миссии с боссами, заезд дня, гараж, практику и визуальные призраки заездов. Основная поставка — WebGL для Yandex Games. Интерфейс поддерживает 9 языков и альбомную ориентацию.

## Запуск

1. Открыть проект в **Unity 6000.5.9f1** (точная версия — `ProjectSettings/ProjectVersion.txt`) и дождаться импорта Packages/Assets.
2. Открыть `Assets/Scenes/SampleScene.unity` и запустить Play Mode.
3. Зависимости закреплены в `Packages/manifest.json` и `packages-lock.json`: URP 17.5.0, Input System 1.20.0, Localization 1.5.12, UGUI 2.5.0, Test Framework 1.7.0.

На главном экране **«В гонку»** сразу запускает показанный заезд с выбранной сложностью. Чтобы выбрать другую трассу или изменить сложность, нажмите **«Выбор гонки»**.

В **«Выборе гонки»** сразу доступна миссия **«Последний перегон»**. По левой дороге догоните хвостовой вагон, обойдите охрану и держитесь рядом три секунды, чтобы забрать кубок. По правой сверните в сухое русло, опередите поезд у стрелки и заберите кубок у вагона на заброшенной станции.

Там же можно выбрать гонку на выбывание, погоню, эстафету со сменой машины или собрать маршрут через **«Своя гонка»**. Для знакомой компании откройте на главном экране **«Игра с друзьями»**, создайте закрытую гонку или совместную миссию и передайте второму игроку восьмизначный код. Обоим клиентам нужен доступ к одному серверу комнат. [Запуск сервера и подключение WebGL](Docs/AI/Multiplayer.md#закрытая-комната)

Новые кузова включены по умолчанию. В правом верхнем углу главного меню и гаража нажмите **«Новые модели»** или **«Классические модели»**, чтобы сменить набор. В компактной раскладке переключатель находится в меню паузы. Выбор сохраняется в профиле.

## Карта проекта

| Каталог | Назначение |
| --- | --- |
| `Assets/CarsGame/Core` | Правила, экономика, сохранения, математика трасс, контракты платформы |
| `Assets/CarsGame/Game` | Машины, гонки/миссии, UI, звук, камера и переходы |
| `Assets/CarsGame/Platform` | Mock/standalone/Yandex и локальное хранилище |
| `Assets/CarsGame/Editor` | Сборка, локализация и генерация контента |
| `Assets/CarsGame/Net` | Закрытые комнаты на Unity Transport, клиент и сервер WebSocket под `CARS_NET` |
| `Assets/CarsGame/Tests` | EditMode и PlayMode |
| `Assets/WebGLTemplates/Yandex` | WebGL-шаблон, JS SDK-адаптер и mock |
| `Tools/qa` | Изолированные тесты, сборки, браузерные probes |

## Проверки и WebGL-сборка

Скрипты рассчитаны на Linux: Unity указанной версии, bash, rsync, Python 3, Node, Chrome, `webtest`; графические PlayMode требуют Xvfb/Vulkan. Browser probes используют playwright-core: при другом расположении задать `PLAYWRIGHT_CORE=/absolute/path/to/playwright-core/index.mjs`. Путь редактора переопределяется через `UNITY`. Полного bootstrap окружения пока нет — CG-RISK-009.

Запускать из корня проекта. Для параллельных запусков использовать отдельные `CG_WORK`, `SIM` и профили `CG_QA_CONFIG_HOME`. Batch Unity работает в копии проекта. При первом запуске без `Library` Unity импортирует проект с нуля. Перед каждым прогоном `common.sh` сверяет содержимое Assets, Packages и ProjectSettings по контрольным суммам.

```bash
CG_WORK=/tmp/cars-edit-check Tools/qa/edit-tests.sh
CG_WORK=/tmp/cars-play-check Tools/qa/play-tests.sh
CG_WORK=/tmp/cars-pause-check Tools/qa/play-tests.sh 'PauseSettingsPlayTests'
node Tools/qa/platform-save.mjs
CG_WORK=/tmp/cars-web-check Tools/qa/build-web.sh
```

Выбирать ещё не занятые каталоги. Результаты тестов и логи лежат в соответствующем `CG_WORK`. Общий скрипт проверяет код завершения Unity и результат тестов в XML. Число прошедших проверок берётся из XML конкретного запуска.

`build-web.sh` проверяет контрольный круг, затем открывает Editor с `-buildTarget WebGL` и вызывает `RacingGame.EditorTools.BuildCommands.BuildYandex`. Перед упаковкой проверяется наличие пакетов Addressables для WebGL. В новом `CG_WORK/build` появятся каталог `web`, ZIP и отчёт о размере.

Локальная браузерная проверка (сервер оставить в отдельном терминале):

```bash
python3 Tools/qa/serve.py /tmp/cars-web-check/build/web 8840
mkdir -p /tmp/cars-browser-check/shots
URL='http://127.0.0.1:8840/?debug=1' SP=/tmp/cars-browser-check webtest -t 300 -- node Tools/qa/browser-smoke.mjs
```

Все браузерные тесты — через `webtest`, Chrome с `$WEBTEST_CHROME_ARGS` (NVIDIA Vulkan), закрытие браузера в `finally`. Звук только через `AgentSink`: не менять предустановленный `PULSE_SINK`, проверять записью `agent-listen record out.wav`, никогда `agent-listen on`. Full PlayMode не запускать с `CG_NOGRAPHICS=1`: гараж использует RenderTextures.

## Текущее состояние и дальнейшая работа

На 24 ноября в игре 34 события, из них 20 доступны на новом профиле. Проверены одиночные заезды, погони, совместный бой с «Левиафаном» и закрытая гонка с двумя независимыми браузерными клиентами и двумя ботами. Для проверки на физических устройствах, первых сессий новичков и приёмки на Яндекс Играх подготовлен [лист наблюдений](Docs/AI/DeviceAndPlayerSessions-2026-11-23.md).

- [Слабые места и критерии закрытия CG-RISK-001…010](Docs/AI/UnityProjectHealth.md).
- [Комплексное улучшение игры в ноябре, сборки и результаты](Docs/AI/ImprovementCycle-2026-11-22.md).
- [Октябрьский журнал улучшений](Docs/AI/ImprovementCycle-2026-10-25.md).
- [Контекст архитектуры](Docs/AI/UnityProjectContext.md) — срез 24 октября; исторические количества тестов не актуальный gate.
- [Ghost replay v2](Docs/AI/GhostReplay-2026-10-25.md) — для поздних проверок сверять журнал.
- [Приёмка Yandex](Docs/Release/Yandex/checklist.md) и [метаданные](Docs/Release/Yandex/metadata.md).
- [Правила работы агентов](AGENTS.md), [контекст Claude](CLAUDE.md).

## Graphify

```bash
graphify query "CG-RISK-001"
graphify query "CG-RISK-004"
graphify query "GameFlow"
```

Локальные результаты: `graphify-out/graph.json`, `GRAPH_REPORT.md`, `graph.html`. Каталог игнорируется Git и может отсутствовать в свежем checkout. Документы с рисками — переносимый источник истины; при восстановлении графа нужна семантическая индексация Markdown. Добавление рисков 25 октября адресное, не полная переиндексация всех текущих изменений. Для изменения кода/документов пользоваться workflow graphify с сохранением существующего графа; code-only update не обновляет смысл Markdown.
