# Образец: машина из Hunyuan3D-2 по контракту MidcoreDesign §G (2026-09-24)

Порядок запуска — `Tools/art/README.md`. Эталонные картинки: `Docs/QA/screens/hunyuan/`.

## Результат
- Форма: Hunyuan3D-2 DiT fp16, 30 шагов, octree 256, сид 20260924 — 50 с, пик VRAM ≈2.5 ГБ, 235 тыс. граней сырого меша.
- `conform_car.py`: масштаб 1.91 (ограничен шириной/высотой, а не длиной — сгенерированный кузов шире эталона),
  кузов 4247 треугольников, всего 4607, 6 рендереров, симметрия по X, колёса `Wheel_FL/FR/RL/RR` со спиннерами,
  турель + отдельный ствол + `Muzzle`. Все проверки `validation.json` пройдены.
- Префаб `Assets/CarsGame/Art/Cars/HunyuanSample/HunyuanSampleCar.prefab` (бейкер копирует компоненты и настройки
  `RaceCar.prefab`): кузов — `M_HunyuanSample_CarPaint` (`CarsGame/StylizedCarPaint`, перекрашивается `CarPaint`),
  остальное — `M_HunyuanSample_Trim` (`CarsGame/StylizedLit`), коллайдер-бокс как у штатной машины.
- PlayMode `HunyuanCarPlayTests`: на реальной трассе за 3 с с полным газом образец проезжает 23.2 м / 52 км/ч,
  штатная машина 23.3 м / 52 км/ч; обе на асфальте, не опрокидываются.

## Исправлено по дороге
- `hunyuan_shape.py`: не был выключен autograd → DiT съедал всю VRAM (OOM при 8+ ГБ свободных) → `torch.set_grad_enabled(False)`.
- `conform_car.py`: масштаб только по длине давал 2.17 × 1.36 м (лимит 1.95 × 1.25); проверка «на земле» смотрела на кузов
  без колёс и на устаревшие `matrix_world` колёс.
- `HunyuanSampleCarBaker`: пустышка `Muzzle` приходит из FBX с поворотом осей (Blender не запекает его в empty);
  `GetComponent(...) ?? AddComponent(...)` не работал из-за «фальшивого null» Unity в редакторе.
- После обновления ОС в uv не было CPython 3.10 (`uv python install 3.10`).

## Не сделано
- **В WebGL-сборку образец не попадает**: на него пока ничего не ссылается. Чтобы выбрать его как кузов, нужен выбор
  префаба в `RaceSpawner` (файл другой сессии — carsgame-35) — например, поле `playerCarPrefab` и пункт «Кузов» в гараже
  рядом с покрасками (`MenuScreens.Extras.cs`). Шейдер `StylizedCarPaint` в WebGL поэтому ещё не проверен.
- Перенос вершинных цветов с эталона даёт рваные тёмные пятна отделки на кузове (видно на `conformed_sheet.png`);
  для витрины нужна чистка масок отделки в `conform_car.py` (порог по нормали/высоте вместо ближайшей поверхности).
