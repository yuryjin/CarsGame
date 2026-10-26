# CarsGame

Аркадные гонки с боем на Unity: кампания, специальные трассы, миссия с боссом, заезд дня, гараж, практика и визуальные призраки заездов. Основная поставка — WebGL для Yandex Games. Интерфейс поддерживает 9 языков; ориентация — альбомная.

## Запуск

1. Открыть проект в **Unity 6000.5.9f1** (точная версия — `ProjectSettings/ProjectVersion.txt`) и дождаться импорта Packages/Assets.
2. Открыть `Assets/Scenes/SampleScene.unity` и запустить Play Mode.
3. Зависимости закреплены в `Packages/manifest.json` и `packages-lock.json`: URP 17.5.0, Input System 1.20.0, Localization 1.5.12, UGUI 2.5.0, Test Framework 1.7.0.

## Карта проекта

| Каталог | Назначение |
| --- | --- |
| `Assets/CarsGame/Core` | Правила, экономика, сохранения, математика трасс, контракты платформы |
| `Assets/CarsGame/Game` | Машины, гонки/миссии, UI, звук, камера и переходы |
| `Assets/CarsGame/Platform` | Mock/standalone/Yandex и локальное хранилище |
| `Assets/CarsGame/Editor` | Сборка, локализация и генерация контента |
| `Assets/CarsGame/Net` | Опциональный код под `CARS_NET`; не готовый сетевой режим |
| `Assets/CarsGame/Tests` | EditMode и PlayMode |
| `Assets/WebGLTemplates/Yandex` | WebGL-шаблон, JS SDK-адаптер и mock |
| `Tools/qa` | Изолированные тесты, сборки, браузерные probes |

## Проверки и WebGL-сборка

Скрипты рассчитаны на Linux: Unity указанной версии, bash, rsync, Python 3, Node, Chrome, `webtest`; графические PlayMode требуют Xvfb/Vulkan. Browser probes используют playwright-core: при другом расположении задать `PLAYWRIGHT_CORE=/absolute/path/to/playwright-core/index.mjs`. Путь редактора переопределяется через `UNITY`. Полного bootstrap окружения пока нет — CG-RISK-009.

Запускать из корня проекта; каждой независимой задаче выделять собственные новые `CG_WORK`, `SIM` и профиль `CG_QA_CONFIG_HOME`. Не запускать batch Unity в основном проекте с открытым Editor. Текущий `common.sh` не обновляет все настройки повторно используемой копии (CG-RISK-006), поэтому для проверки изменений Packages/ProjectSettings нужна новая копия.

```bash
CG_WORK=/tmp/cars-edit-check Tools/qa/edit-tests.sh
CG_WORK=/tmp/cars-play-check Tools/qa/play-tests.sh
CG_WORK=/tmp/cars-pause-check Tools/qa/play-tests.sh 'PauseSettingsPlayTests'
node Tools/qa/platform-save.mjs
CG_WORK=/tmp/cars-web-check Tools/qa/build-web.sh
```

Выбирать ещё не занятые каталоги. Результаты тестов и логи лежат в соответствующем CG_WORK. Пока CG-RISK-010 открыт, дополнительно проверять XML EditMode: результат должен быть непустым, завершённым и без failed; одного exit=0 скрипта недостаточно.

`build-web.sh` сначала проверяет контрольный круг, затем вызывает `RacingGame.EditorTools.BuildCommands.BuildYandex`. Выход — `$CG_WORK/build/web`, ZIP и отчёт размера. При существующей папке build скрипт останавливается; выбрать новый CG_WORK.

Локальная браузерная проверка (сервер оставить в отдельном терминале):

```bash
python3 Tools/qa/serve.py /tmp/cars-web-check/build/web 8840
mkdir -p /tmp/cars-browser-check/shots
URL='http://127.0.0.1:8840/?debug=1' SP=/tmp/cars-browser-check webtest -t 300 -- node Tools/qa/browser-smoke.mjs
```

Все браузерные тесты — через `webtest`, Chrome с `$WEBTEST_CHROME_ARGS` (NVIDIA Vulkan), закрытие браузера в `finally`. Звук только через `AgentSink`: не менять предустановленный `PULSE_SINK`, проверять записью `agent-listen record out.wav`, никогда `agent-listen on`. Full PlayMode не запускать с `CG_NOGRAPHICS=1`: гараж использует RenderTextures.

## Текущее состояние и дальнейшая работа

Срез документации — **25 октября 2026**. Активные изменения ещё проходят интеграцию; результаты разных snapshots не означают PASS последнего дерева. Самые срочные пункты: отпускание multitouch, надёжность локальной записи, единый финальный кандидат, реальные сервисы/устройства.

- [Слабые места и критерии закрытия CG-RISK-001…010](Docs/AI/UnityProjectHealth.md).
- [Текущий журнал улучшений и доказательства](Docs/AI/ImprovementCycle-2026-10-25.md).
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
