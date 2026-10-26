# CarsGame — контекст для Claude

Обязательные правила работы находятся в [AGENTS.md](AGENTS.md): соблюдать их полностью, включая запрет attribution trailers, изоляцию Unity/PlayerPrefs, webtest/NVIDIA Vulkan и звук только в AgentSink.

## Точки входа

- [README.md](README.md): запуск, структура и команды проверки.
- [Docs/AI/UnityProjectHealth.md](Docs/AI/UnityProjectHealth.md): актуальный реестр слабых мест CG-RISK-001…010, приоритеты и критерии закрытия.
- [Docs/AI/ImprovementCycle-2026-10-25.md](Docs/AI/ImprovementCycle-2026-10-25.md): текущая хронология; поздняя запись может отменять pending/failure ранней.
- [Docs/AI/UnityProjectContext.md](Docs/AI/UnityProjectContext.md): архитектурный срез 24 октября; не использовать старые количества тестов как текущую верификацию.
- [Docs/Release/Yandex/checklist.md](Docs/Release/Yandex/checklist.md): реальная приёмка платформы перед публикацией.

## Рабочая модель

Unity 6000.5.9f1, одна сцена SampleScene, URP, Input System, runtime UGUI/TMP и 9 языков. GameFlow ведёт переходы, RaceManager — гонку, SimulationDriver — фиксированный шаг, SaveService — локальные/облачные сохранения, PlatformRuntime — сервисы. Ghost v2 — визуальная траектория; Net/CARS_NET — опциональный код, не обещание готового multiplayer.

Перед правкой смотреть git status и сохранять чужую текущую работу. В реестре приоритетны touch release, ошибки локальной записи, единый интеграционный кандидат и реальные SDK/устройства. Исправление NewerVersion уже есть: не описывать его как ещё открытый дефект. Не менять физику ради неудачного тестового маршрута без подтверждения причины.

## graphify

Если существует `graphify-out/graph.json`, сначала выполнить `graphify query "<symbol>"` или `graphify query "CG-RISK-001"`, затем сверить текущий код и документы. Graphify — карта связей с источниками, не доказательство свежести рабочего дерева.

После изменения рисков обновлять семантическую часть графа по workflow навыка graphify, сохранять старые узлы через merge и пересоздавать report/HTML. Code-only update не индексирует смысл Markdown. Сохранять стабильные CG-RISK-ID, evidence, статус, source_file и source_location; не представлять гипотезу как подтверждённый дефект.

Срез 25 октября добавляет адресно реестр рисков и корневые документы; остальные участки графа могут быть старее кода. Каталог graphify-out игнорируется Git, поэтому реестр Markdown является источником для восстановления графа на другом checkout.
