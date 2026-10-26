# Инструкции агентам CarsGame

## Перед работой

- Прочитать [README.md](README.md), [реестр рисков](Docs/AI/UnityProjectHealth.md) и актуальный конец [журнала](Docs/AI/ImprovementCycle-2026-10-25.md). Архитектурный контекст — [UnityProjectContext](Docs/AI/UnityProjectContext.md), срез 24 октября 2026.
- Проверить `git status --short`. В дереве могут быть незакоммиченные изменения другой текущей работы: сохранять их, не откатывать и не присваивать себе их результаты.
- Unity: версия из `ProjectSettings/ProjectVersion.txt`; основная сцена — `Assets/Scenes/SampleScene.unity`. Использовать существующие Core/Game/Platform/Editor assembly boundaries. Net под `CARS_NET` не означает готовый сетевой режим.
- Источник локализации — `Assets/CarsGame/Localization/Source/strings.csv`; при UI-изменениях учитывать все 9 языков, generated tables и CJK-атлас. Не менять только одну сгенерированную таблицу.

## Слабые места и доказательства

- Поддерживать стабильные ID CG-RISK-001…010 в `Docs/AI/UnityProjectHealth.md`; перед новой задачей проверять соответствующий критерий закрытия.
- Отличать воспроизведённый дефект, статический риск и отсутствие проверки. Не переносить старые before-failures в текущий backlog без сверки последующих записей.
- Не закрывать риск по наличию кода: нужны результат проверки, дата и snapshot/manifest. Mock SDK, эмуляция touch/gamepad и счётчики Unity не доказывают работу реальной площадки, физического устройства и стабильность всей памяти WebGL.
- Числа тестов из предыдущего отчёта не выдавать за проверку новых изменений. Не заявлять FPS-выигрыш без сопоставимого измерения.

## Unity и QA

- Batch Unity запускать только в изолированном SIM через `Tools/qa/common.sh`; основной Editor может быть открыт. Каждый одновременно работающий Unity требует отдельного SIM, независимые профили — отдельного `CG_QA_CONFIG_HOME`.
- Не перезаписывать пользовательские PlayerPrefs. `common.sh` задаёт отдельный `XDG_CONFIG_HOME`.
- Для новых Packages/ProjectSettings использовать свежий SIM до закрытия CG-RISK-006. Фиксировать реальные входы `Tools/qa/freeze-source.py <SIM> <artifact-dir>` до следующего resync.
- EditMode: `Tools/qa/edit-tests.sh`; проверять XML самостоятельно до закрытия CG-RISK-010. PlayMode: `Tools/qa/play-tests.sh [filter]`, полный набор требует GPU/RenderTextures; `CG_NOGRAPHICS=1` только для совместимого фильтра.
- WebGL: `Tools/qa/build-web.sh` с новым CG_WORK; не удалять чужие/предыдущие build-артефакты для освобождения пути. Node-логика SDK: `node Tools/qa/platform-save.mjs`, браузер не нужен.
- Выполнять проверки соразмерно изменению. Документация требует проверки ссылок и graphify-целостности, не обязательной пересборки Unity.

## Browser / WebGL: ограничение CPU

- Каждый браузерный тест запускать через `webtest [-t SEC] -- node tests/x.js` (`~/.local/bin`). Он ставит задачи в общую очередь (максимум 2), ограничивает CPU четырьмя ядрами, задаёт timeout (900 s по умолчанию, exit 124) и убирает дочерние процессы.
- Использовать RTX 4070 Ti: `chromium.launch({ channel: 'chrome', args: process.env.WEBTEST_CHROME_ARGS.split(' ') })`. Аргументы включают `--use-angle=vulkan --enable-features=Vulkan`; не обходить wrapper. Проверка — `webtest --check-gpu`, renderer должен содержать NVIDIA.
- SwiftShader разрешён только для pixel-exact эталонных снимков, с маленьким viewport (например, 640×360) и всё равно через webtest.
- Всегда `await browser.close()` в `finally`. Предпочитать state hooks/UI dumps, снимать только ключевые состояния. Логику и баланс проверять без браузера, когда он не нужен.
- `webtest-reap` убирает зависшие/осиротевшие Chrome. Никогда не проверять `-m 0` без `-p <profile-substring>`: можно остановить чужие браузеры.

## Звук

Всё запускаемое играет только в null sink `AgentSink`. `PULSE_SINK=AgentSink` предустановлен: не переопределять и не выбирать реальное устройство. Проверять звук через `agent-listen record out.wav`; никогда не запускать `agent-listen on`.

## Graphify

- Если есть `graphify-out/graph.json`, начинать вопросы об архитектуре/связях с `graphify query "<symbol or CG-RISK-ID>"`, затем сверять актуальные исходники. Пустая выдача не доказывает отсутствие поведения.
- При правке реестра рисков обновлять его семантические узлы и связи с файлами/символами; сохранять ID и source_file/source_location. Подтверждённые документом связи — EXTRACTED; гипотезы помечать INFERRED/AMBIGUOUS.
- Не заменять весь граф фрагментом. Пользоваться incremental merge, проверять отсутствующие endpoints, дубли ID и сохранение прежних узлов; обновлять отчёт и HTML. Для графа >5000 узлов использовать агрегированный HTML.
- `graphify update` с одним AST не индексирует смысл новых Markdown. Использовать семантический workflow навыка graphify; не запрашивать API key для работы, которую может выполнить текущий агент.
- `graphify-out/` игнорируется Git. Переносимый источник рисков — Markdown; описывать фактический охват индексации. Адресное обновление документации не означает свежесть всего графа кода.

## Git commits and pull requests

Never add attribution trailers or footers to commit messages or PR descriptions. Do not add `Co-Authored-By`, `Co-authored-by`, `Generated with` or any “made by AI” signature. Commit messages contain only the subject and body describing the change.
