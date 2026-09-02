import json
CC="Assets/Scripts/CarController.cs"; OD="Assets/Scripts/OpponentDriver.cs"; TC="Assets/Scripts/TargetCar.cs"; CH="Assets/Scripts/CarHealth.cs"
nodes=[]; edges=[]
def node(**k): nodes.append(k)
def edge(s,t,ty,w): edges.append({"source":s,"target":t,"type":ty,"direction":"forward","weight":w})

# ---- file nodes
node(id=f"file:{CC}",type="file",name="CarController.cs",filePath=CC,
 summary="Реализует физику автомобиля на Rigidbody: тягу, торможение, руление, боковое сцепление, прижимную силу, визуальное вращение колёс и звук двигателя. Читает ввод игрока через Unity Input System (клавиатура/геймпад) либо принимает команды от AI через SetInput и рисует HUD-спидометр с подсказками через OnGUI.",
 tags=["monobehaviour","физика-автомобиля","input-system","игровой-контроллер","hud"],complexity="complex",
 languageNotes="Атрибут [RequireComponent(typeof(Rigidbody))] гарантирует наличие Rigidbody на объекте. Публичные поля с [Header] сериализуются в Inspector Unity; [SerializeField] private показывает приватное состояние в редакторе. Статические методы CreateWheelColliders и ApplyFrictionlessBodyMaterial имеют приватные перегрузки-обёртки без параметров, что позволяет переиспользовать настройку физики из TargetCar. Сцепление шин моделируется вручную, поэтому все коллайдеры получают PhysicsMaterial с нулевым трением.")
node(id=f"file:{OD}",type="file",name="OpponentDriver.cs",filePath=OD,
 summary="AI-водитель оппонента: строит маршрут из дочерних waypoint-трансформов со смещением полосы, вычисляет угол до следующей точки и подаёт газ/руль/тормоз в CarController через SetInput. Автоматически сбрасывает перевернувшуюся машину через ResetCar.",
 tags=["monobehaviour","ai","waypoint-навигация","оппонент","игровой-контроллер"],complexity="moderate",
 languageNotes="Класс объявлен sealed и требует CarController через [RequireComponent]. Используется Debug.Assert для проверки маршрута на дубликаты waypoint-ов; при невалидном маршруте компонент отключает сам себя (enabled = false).")
node(id=f"file:{TC}",type="file",name="TargetCar.cs",filePath=TC,
 summary="Машина-мишень без водителя, по которой можно стрелять и таранить. Без CarController самостоятельно настраивает ту же физику (масса, сферические коллайдеры колёс, материал без трения) через статические методы CarController, применяет сопротивление качению и возвращается на стартовую позицию после уничтожения по событию CarHealth.OnRespawned.",
 tags=["monobehaviour","мишень","физика-автомобиля","respawn","event-handler"],complexity="moderate",
 languageNotes="[RequireComponent(typeof(Rigidbody), typeof(CarHealth))] задаёт зависимости компонента. Подписка на событие health.OnRespawned += ReturnToStart — типичный паттерн делегатов/событий C#; XML-документация /// <summary> с <see cref=\"CarController\"/> используется для описания класса.")

# ---- class nodes
node(id=f"class:{CC}:CarController",type="class",name="CarController",filePath=CC,lineRange=[6,367],
 summary="MonoBehaviour-контроллер автомобиля: управляет Rigidbody через силы и импульсы в FixedUpdate, обрабатывает ввод игрока или внешние команды, отвечает за сброс машины и HUD.",
 tags=["monobehaviour","физика-автомобиля","игровой-контроллер","компонент"],complexity="complex")
node(id=f"class:{OD}:OpponentDriver",type="class",name="OpponentDriver",filePath=OD,lineRange=[5,144],
 summary="Sealed MonoBehaviour, ведущий машину оппонента по кольцевому маршруту из waypoint-ов и передающий управляющие сигналы в CarController.",
 tags=["monobehaviour","ai","waypoint-навигация","компонент"],complexity="moderate")
node(id=f"class:{TC}:TargetCar",type="class",name="TargetCar",filePath=TC,lineRange=[11,73],
 summary="MonoBehaviour для бесхозной машины-мишени: настраивает физику как у игрока при отсутствии CarController, гасит скорость и возвращается на старт после респауна.",
 tags=["monobehaviour","мишень","respawn","компонент"],complexity="simple")

# ---- CarController functions
F=[
 ("Awake",[48,63],"Инициализирует Rigidbody (масса 1250 кг, центр масс, демпфирование, непрерывная детекция столкновений), создаёт колёсные коллайдеры, применяет материал без трения и запоминает стартовую позицию для сброса.",["lifecycle","инициализация","физика"],"simple"),
 ("CreateWheelColliders",[74,88],"Статический метод: создаёт дочерние GameObject со SphereCollider в позициях колёс, чтобы машина катилась через бордюры и ступеньки трассы. Используется также TargetCar.",["static","физика","коллайдеры","utility"],"simple"),
 ("ApplyFrictionlessBodyMaterial",[99,114],"Статический метод: назначает всем коллайдерам машины PhysicsMaterial с нулевым трением и отскоком, так как сцепление моделируется скриптом. Используется также TargetCar.",["static","физика","physics-material","utility"],"simple"),
 ("Update",[116,128],"Покадровый цикл: читает ввод игрока (если playerControlled), обновляет визуал колёс и звук, сбрасывает машину при падении ниже -10 или по клавише R.",["lifecycle","game-loop","input"],"simple"),
 ("FixedUpdate",[130,144],"Физический цикл: проверяет контакт с землёй, считает скорость, применяет мотор, руление, сцепление и прижимную силу; запоминает последнюю безопасную позицию при медленном движении.",["lifecycle","физика","game-loop"],"simple"),
 ("ReadInput",[146,176],"Считывает управление с клавиатуры (WASD/стрелки/пробел) и геймпада (левый стик, триггеры, кнопка A) через Unity Input System в moveInput, steerInput и isBraking.",["input-system","клавиатура","геймпад"],"moderate"),
 ("SetInput",[178,183],"Публичный API для внешнего управления (AI): задаёт газ, руль и тормоз с ограничением диапазона [-1, 1].",["public-api","input","ai"],"simple"),
 ("ApplyMotor",[196,244],"Основная логика тяги: импульсное торможение с ограничением до полной остановки, разгон вперёд с падением силы к maxSpeed, реверс и торможение при смене направления, накат через ApplyCoastDeceleration.",["физика","мотор","торможение"],"moderate"),
 ("ApplySteering",[255,274],"Плавно доводит угол руля к целевому (уменьшая максимум на высокой скорости) и прикладывает крутящий момент вокруг оси Y пропорционально скорости.",["физика","руление","torque"],"simple"),
 ("ApplyFriction",[276,285],"Имитирует сцепление шин: гасит боковую составляющую скорости импульсом, ослабляя сцепление при торможении для заноса.",["физика","сцепление","boковая-сила"],"simple"),
 ("UpdateWheelVisuals",[296,323],"Поворачивает передние колёса на угол руления и вращает меши всех четырёх колёс пропорционально скорости.",["визуал","колёса","анимация"],"simple"),
 ("ResetCar",[332,338],"Публичный метод сброса: обнуляет скорости и телепортирует машину на последнюю безопасную позицию с ориентацией по горизонту.",["public-api","respawn","utility"],"simple"),
 ("OnGUI",[340,366],"Рисует IMGUI-оверлей со спидометром в км/ч и подсказками по управлению (на русском) для машины игрока.",["hud","imgui","ui"],"simple"),
]
for n,lr,s,t,c in F:
    node(id=f"function:{CC}:{n}",type="function",name=n,filePath=CC,lineRange=lr,summary=s,tags=t,complexity=c)

# ---- OpponentDriver functions
F=[
 ("Awake",[18,47],"Находит CarController, отключает управление игроком, валидирует наличие минимум трёх waypoint-ов, строит маршрут, проверяет его на дубликаты и разворачивает машину к ближайшей точке маршрута.",["lifecycle","инициализация","валидация"],"simple"),
 ("Update",[49,72],"Каждый кадр вычисляет угол до целевого waypoint-а, преобразует его в руление, снижает целевую скорость в поворотах, тормозит при превышении и вызывает car.SetInput; после 2 секунд переворота сбрасывает машину через car.ResetCar.",["ai","game-loop","waypoint-навигация"],"moderate"),
 ("BuildRoute",[74,92],"Строит массив точек маршрута из дочерних трансформов waypoints, смещая каждую точку вбок по касательной на laneOffset, чтобы оппоненты ехали по разным полосам.",["waypoint-навигация","маршрут","геометрия"],"simple"),
 ("AdvancePassedWaypoints",[94,108],"Продвигает индекс waypoint-а, пропуская уже пройденные точки (в радиусе или позади машины), и возвращает горизонтальный вектор до следующей цели.",["waypoint-навигация","ai"],"simple"),
 ("FindNearestWaypoint",[110,126],"Возвращает индекс ближайшей по горизонтали точки маршрута к текущей позиции машины.",["waypoint-навигация","поиск","utility"],"simple"),
 ("IsValidRoute",[128,138],"Статическая проверка, что соседние точки маршрута не совпадают (расстояние по горизонтали не меньше 0.5 м).",["static","валидация","utility"],"simple"),
]
for n,lr,s,t,c in F:
    node(id=f"function:{OD}:{n}",type="function",name=n,filePath=OD,lineRange=lr,summary=s,tags=t,complexity=c)

# ---- TargetCar functions
F=[
 ("Awake",[23,53],"Определяет наличие CarController; без него настраивает Rigidbody как у игрока и вызывает CarController.CreateWheelColliders/ApplyFrictionlessBodyMaterial. Запоминает стартовую позу, настраивает отображение полосы здоровья CarHealth и подписывается на OnRespawned.",["lifecycle","инициализация","физика","event-handler"],"simple"),
 ("FixedUpdate",[55,65],"Для машины без контроллера применяет импульс сопротивления качению, чтобы сдвинутая мишень постепенно останавливалась.",["физика","game-loop"],"simple"),
]
for n,lr,s,t,c in F:
    node(id=f"function:{TC}:{n}",type="function",name=n,filePath=TC,lineRange=lr,summary=s,tags=t,complexity=c)

# ---- edges
for n in nodes:
    if n["type"] in ("function","class"):
        edge(f"file:{n['filePath']}", n["id"], "contains", 1.0)
for cid in (f"class:{CC}:CarController", f"class:{OD}:OpponentDriver", f"class:{TC}:TargetCar"):
    edge(f"file:{cid.split(':')[1]}", cid, "exports", 0.8)
for fn in ("CreateWheelColliders","ApplyFrictionlessBodyMaterial","SetInput","ResetCar"):
    edge(f"file:{CC}", f"function:{CC}:{fn}", "exports", 0.8)
# imports — 1:1 from batchImportData
imp={CC:[],OD:[CC],TC:[CC,CH]}
for f,ts in imp.items():
    for t in ts: edge(f"file:{f}", f"file:{t}", "imports", 0.7)
# calls (cross-file)
edge(f"function:{OD}:Update", f"function:{CC}:SetInput", "calls", 0.8)
edge(f"function:{OD}:Update", f"function:{CC}:ResetCar", "calls", 0.8)
edge(f"function:{TC}:Awake", f"function:{CC}:CreateWheelColliders", "calls", 0.8)
edge(f"function:{TC}:Awake", f"function:{CC}:ApplyFrictionlessBodyMaterial", "calls", 0.8)
# depends_on (RequireComponent runtime deps)
edge(f"file:{OD}", f"file:{CC}", "depends_on", 0.6)
edge(f"file:{TC}", f"file:{CH}", "depends_on", 0.6)

out={"nodes":nodes,"edges":edges}
p="/home/user/projects/games/Unity/newgames/CarsGame/.ua/intermediate/batch-2.json"
json.dump(out,open(p,"w"),ensure_ascii=False,indent=2)

# ---- self-validate
ids={n["id"] for n in nodes}
assert len(ids)==len(nodes), "duplicate ids"
known_files={CC,OD,TC,CH}
from collections import Counter
bad=[]
for e in edges:
    assert e["source"]!=e["target"]
    for ref in (e["source"],e["target"]):
        if ref in ids: continue
        if ref.startswith("file:") and ref[5:] in known_files: continue
        bad.append((e,ref))
print("bad refs:",bad)
print("nodes:",len(nodes),Counter(n["type"] for n in nodes))
print("edges:",len(edges),Counter(e["type"] for e in edges))
print("imports expected:",sum(len(v) for v in imp.values()))
for n in nodes:
    assert n["summary"] and n["tags"] and n["complexity"] in ("simple","moderate","complex")
