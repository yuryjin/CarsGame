# Карточка «Гонки с пушкой» для Яндекс Игр

Локальный черновик. Тексты подходят для сборки 0.2.0 с миссией «Последний перегон».

## Поля каталога

| Поле | Значение |
| --- | --- |
| Названия | Гонки с пушкой · Cannon Racers · Toplu Yarış |
| Жанр | Гонки, экшен |
| Управление | Клавиатура, геймпад, сенсорный экран |
| Целевые устройства | Компьютер, телефон, планшет. Альбомная ориентация |
| Языки | ru, en, tr, uk, de, es, fr, pt, zh |
| Монетизация | Межстраничная реклама после результатов с ограничением частоты. Удвоение награды по кнопке игрока |
| Сохранения | Локальный прогресс гостя и облачное сохранение через SDK |
| Таблицы рекордов | Идентификаторы, типы счёта и порядок сортировки перечислены в [листе приёмки](checklist.md) |

## Короткое описание

Лимит поля — 60 символов с пробелами.

| Язык | Текст |
| --- | --- |
| ru | Боевые гонки и погоня за поездом по двум дорогам каньона |
| en | Combat racing and a train chase through a desert canyon |
| tr | Çöl kanyonunda silahlı yarışlar ve tren kovalamacası |

## Полное описание

**Русский**

Начни со «Спринта». Стреляй на ходу, финишируй в тройке и открой следующий заезд. В гараже можно потратить призовые на новый кузов.

Хочется погони? «Последний перегон» доступен сразу в выборе гонки. По левой дороге догони хвостовой вагон и удержись рядом, чтобы забрать кубок. Или сверни вправо в сухое русло и успей к стрелке раньше локомотива. Тогда за кубком поедешь на заброшенную станцию.

**English**

Start with Sprint. Fire on the move, finish on the podium and unlock the next race. Spend your winnings on a new car body in the garage.

For a chase, pick The Last Run in Race Select. It's open from the start. Take the left road, catch the last car and stay alongside to claim the cup. Or turn right into the dry wash and beat the locomotive to the switch. You can then collect the cup at the abandoned station.

**Türkçe**

Sprint ile başla. Yarışırken ateş et, ilk üçe gir ve sonraki yarışın kilidini aç. Kazandığın parayla garajdan yeni bir araç gövdesi alabilirsin.

Bir kovalamaca için yarış seçiminden Son Sefer'i aç. Bu görev en baştan oynanabilir. Sol yoldan son vagona yetiş ve kupayı almak için yanında kal. İstersen sağa dönüp kuru dere yatağından git ve makasa lokomotiften önce var. Böylece kupayı terk edilmiş istasyonda alabilirsin.

## Игровые кадры для редактора

Все четыре кадра сняты в локальной WebGL-сборке при 1280×720. Первые два заезда доступны с чистого профиля в разных местах меню. «Кубок наоборот» открывается после подиума в «Спринте».

| Что обещает описание | Как увидеть в игре | Кадр и подтверждение |
| --- | --- | --- |
| Боевой заезд, стрельба на ходу | На главном экране нажать «В гонку», разогнаться и выстрелить | [Первый заезд](../../../Builds/Improvements-2026-11-22/base/first-session/first-race.png) |
| Подиум, призовые и продолжение карьеры | Завершить «Спринт» в тройке и нажать продолжение. В сохранённой сессии новый старт — e2 | [Результат](../../../Builds/Improvements-2026-11-22/base/first-session/results.png) · [Хронология](../../../Builds/Improvements-2026-11-22/base/first-session/report.json) |
| Покупка кузова | В гараже выбрать Arrow и купить за 800 монет | [Гараж](../../../Builds/Improvements-2026-11-22/base/first-session/garage.png) · [Покупка и восстановление](../../../Builds/Improvements-2026-11-22/base/purchase/evidence.json) |
| Два пути к кубку в погоне | В выборе гонки открыть «Последний перегон». В первом проезде взять кубок у хвоста, во втором опередить поезд у стрелки | [Погоня](../../../Builds/TrainBoss-2026-11-21/chase.png) · [Оба проезда](../../../Builds/TrainBoss-2026-11-21/qa/browser.json) |

Для иконки предусмотрен макет 512×512 с машиной крупным планом. Для обложки — кадр гонки 800×470. Частоту рекламы, вход и облачные операции на реальной площадке проверяют на том ZIP, который загружен в черновик. [Приёмка в консоли](checklist.md)
