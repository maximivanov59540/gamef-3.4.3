using UnityEngine;
using System.Collections.Generic;
public class ResourceProducer : MonoBehaviour
{
    [Tooltip("Данные о 'рецепте' (время, затраты, выход)")]
    public ResourceProductionData productionData;
    
    private BuildingInputInventory _inputInv;
    private BuildingOutputInventory _outputInv;
    [Header("Рабочая Сила")]
    [Tooltip("Сколько рабочих 'потребляет' это здание")]
    public int workforceRequired = 10;
    
    [Header("Разгон")]
    [Tooltip("Текущая 'разогретость' (0.0 - 1.0)")]
    [SerializeField] [Range(0f, 1f)] private float _rampUpEfficiency = 0.0f;
    [Tooltip("Время (сек) для 'разгона' от 0% до 100%")]
    public float rampUpTimeSeconds = 60.0f;
    [Tooltip("Время (сек) для 'остывания' от 100% до 0%")]
    public float rampDownTimeSeconds = 60.0f;
    
    [Header("Бонусы от Модулей")]
    [Tooltip("Производительность = База * (1.0 + (Кол-во модулей * X))")]
    public float productionPerModule = 0.25f;

    private float _currentModuleBonus = 1.0f; // (Множитель, 1.0 = 100%)
    
    [Header("Эффективность")]
    private float _efficiencyModifier = 1.0f; // 100% по дефолту

    private float _currentWorkforceCap = 1.0f;
    
    [Header("Состояние цикла")]
    [SerializeField]
    [Tooltip("Внутренний таймер. Накапливается до 'cycleTimeSeconds'")]
    private float _cycleTimer = 0f;
    
    public bool IsPaused { get; private set; } = false;
    [Header("Логистика Склада")]
    [SerializeField] private Warehouse _assignedWarehouse; // Склад, к которому мы "приписаны"
    private bool _hasWarehouseAccess = false; // Наш "пропуск" к работе
    
    private BuildingIdentity _identity;
    private GridSystem _gridSystem;
    private RoadManager _roadManager;

    void Awake()
    {
        _inputInv = GetComponent<BuildingInputInventory>();
        _outputInv = GetComponent<BuildingOutputInventory>();

        _identity = GetComponent<BuildingIdentity>();

        if (_inputInv == null && productionData != null && productionData.inputCosts.Count > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingInputInventory', но рецепт требует сырье!", this);
            
        if (_outputInv == null && productionData != null && productionData.outputYield.amount > 0)
            Debug.LogError($"На здании {gameObject.name} нет 'BuildingOutputInventory', но рецепт производит товар!", this);
        
        if (_outputInv != null)
        {
            _outputInv.OnFull += PauseProduction;
            _outputInv.OnSpaceAvailable += ResumeProduction;
        }
    }
    void Start()
    {
        // Хватаем менеджеры, нужные для поиска пути
        _gridSystem = FindFirstObjectByType<GridSystem>();
        _roadManager = RoadManager.Instance;
        
        // Запускаем поиск
        FindWarehouseAccess();
        WorkforceManager.Instance?.RegisterProducer(this);
    }
    
    private void OnDestroy()
    {
        if (_outputInv != null)
        {
            _outputInv.OnFull -= PauseProduction;
            _outputInv.OnSpaceAvailable -= ResumeProduction;
        }
        WorkforceManager.Instance?.UnregisterProducer(this);
    }

    void Update()
    {
        // 1. Проверка Паузы (старая)
        if (IsPaused || productionData == null)
            return;
            
        // 2. ПРОВЕРКА: Есть ли "пропуск" от склада? (старая)
        // (Эта переменная _hasWarehouseAccess у тебя уже есть из Блока 2)
        if (!_hasWarehouseAccess)
        {
            PauseProduction(); 
            return;
        }

        // --- ⬇️ НАЧАЛО НОВОГО БЛОКА ЛОГИКИ (ЗАДАЧА 10 и 11) ⬇️ ---
        
        // --- Шаг 1: Логика "Разгона" (Задача 10) ---
        // Проверяем, есть ли сырье
        bool hasInputs = (_inputInv != null) ? _inputInv.HasResources(productionData.inputCosts) : true;
        
        // Цель "разгона" (1.0 = греться, 0.0 = остывать)
        // (Мы "греемся" только если есть и сырье, и доступ к складу)
        float targetRampUp = (hasInputs && _hasWarehouseAccess) ? 1.0f : 0.0f;
        
        float rampSpeed;
        if (targetRampUp > _rampUpEfficiency)
            // Используем Mathf.Max для защиты от деления на ноль, если кто-то поставит 0
            rampSpeed = (Time.deltaTime / Mathf.Max(0.01f, rampUpTimeSeconds));
        else
            rampSpeed = (Time.deltaTime / Mathf.Max(0.01f, rampDownTimeSeconds));

        _rampUpEfficiency = Mathf.MoveTowards(_rampUpEfficiency, targetRampUp, rampSpeed);


        // --- Шаг 2: Логика "Рабочей Силы" (Идея 2) ---
        // Получаем наш "лимит" с "рынка труда" (напр, 0.83)
        _currentWorkforceCap = WorkforceManager.Instance != null ? WorkforceManager.Instance.GetWorkforceRatio() : 1.0f;


        // --- Шаг 3: Финальный Расчет Эффективности (Задача 11) ---
        
        // Рассчитываем финальную эффективность из ВСЕХ 4-х множителей:
        // 1. Разгон (0-100%)
        // 2. Лимит рабочих (0-100%)
        // 3. Слайдер (50-150%) (это твой _efficiencyModifier)
        // 4. Бонус модулей (100%+) (это твой _currentModuleBonus)
        float finalEfficiency = _rampUpEfficiency * _currentWorkforceCap * _efficiencyModifier * _currentModuleBonus;

        // ВАЖНО: Если эффективность 0 (стоим), то и цикл не считаем, чтобы не делить на ноль
        if (finalEfficiency <= 0.001f)
        {
            _cycleTimer = 0f; // Сбрасываем таймер цикла
            return; // Выходим из Update
        }

        // Рассчитываем время цикла (ЭТО ЗАМЕНЯЕТ СТАРУЮ СТРОКУ)
        float currentCycleTime = productionData.cycleTimeSeconds / finalEfficiency;

        // --- ⬆️ КОНЕЦ НОВОГО БЛОКА ЛОГИКИ ⬆️ ---


        // 4. Накапливаем таймер (этот код у тебя уже есть)
        _cycleTimer += Time.deltaTime;

        // 5. Ждем, пока таймер "дозреет" (этот код у тебя уже есть)
        if (_cycleTimer < currentCycleTime)
        {
            return; // Еще не время
        }
        
        // --- 6. ВРЕМЯ ПРИШЛО! (этот код у тебя уже есть) ---
        _cycleTimer -= currentCycleTime; // Сбрасываем таймер (с учетом "сдачи")

        // 7. Проверяем "Желудок" (Input) (этот код у тебя уже есть)
        if (_inputInv != null && !_inputInv.HasResources(productionData.inputCosts))
        {
            // Debug.Log($"[Producer] {gameObject.name} не хватает сырья.");
            return; // Нет сырья, ждем следующего цикла
        }

        // 8. Проверяем "Кошелек" (Output) (этот код у тебя уже есть)
        if (_outputInv != null && !_outputInv.HasSpace(productionData.outputYield.amount))
        {
            PauseProduction(); // Склад полон
            return;
        }
        
        // --- 9. ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ! ПРОИЗВОДИМ! (этот код у тебя уже есть) ---
        
        // а) "Съедаем" сырье
        if (_inputInv != null)
        {
            _inputInv.ConsumeResources(productionData.inputCosts);
        }
        
        // б) "Производим" товар
        if (_outputInv != null)
        {
            _outputInv.AddResource(productionData.outputYield.amount);
        }
    }
    private void FindWarehouseAccess()
    {
        // 1. Проверка систем
        if (_identity == null || _gridSystem == null || _roadManager == null)
        {
            Debug.LogError($"[Producer] {gameObject.name} не хватает систем для поиска пути!");
            _hasWarehouseAccess = false;
            return;
        }
        
        var roadGraph = _roadManager.GetRoadGraph();

        // 2. Найти наши "выходы" к дороге
        List<Vector2Int> myAccessPoints = LogisticsPathfinder.FindAllRoadAccess(_identity.rootGridPosition, _gridSystem, roadGraph);
        if (myAccessPoints.Count == 0)
        {
            Debug.LogWarning($"[Producer] {gameObject.name} не имеет доступа к дороге.");
            _hasWarehouseAccess = false;
            return;
        }

        // 3. Найти все склады
        Warehouse[] allWarehouses = FindObjectsByType<Warehouse>(FindObjectsSortMode.None);
        if (allWarehouses.Length == 0)
        {
            Debug.LogWarning($"[Producer] {gameObject.name} не нашел НИ ОДНОГО склада на карте.");
            _hasWarehouseAccess = false;
            return;
        }

        // 4. Рассчитать ВСЕ дистанции от НАС (1000 = "бесконечный" радиус)
        var distancesFromMe = LogisticsPathfinder.Distances_BFS_Multi(myAccessPoints, 1000, roadGraph);

        // 5. Найти ближайший доступный склад
        Warehouse nearestWarehouse = null;
        int minDistance = int.MaxValue;

        foreach (var warehouse in allWarehouses)
        {
            var warehouseIdentity = warehouse.GetComponent<BuildingIdentity>();
            if (warehouseIdentity == null) continue;
            
            // 6. Найти "входы" к ЭТОМУ складу
            List<Vector2Int> warehouseAccessPoints = LogisticsPathfinder.FindAllRoadAccess(warehouseIdentity.rootGridPosition, _gridSystem, roadGraph);
            
            // 7. Найти ближайшую точку "входа" в этот склад
            foreach (var entryPoint in warehouseAccessPoints)
            {
                // Если мы "дотягиваемся" до этой точки входа...
                if (distancesFromMe.TryGetValue(entryPoint, out int dist) && dist < minDistance)
                {
                    // ...это наш новый "лучший" кандидат
                    minDistance = dist;
                    nearestWarehouse = warehouse;
                }
            }
        }

        // 8. ФИНАЛЬНАЯ ПРОВЕРКА: Мы нашли склад И он в радиусе?
        if (nearestWarehouse != null && minDistance <= nearestWarehouse.roadRadius)
        {
            _assignedWarehouse = nearestWarehouse;
            _hasWarehouseAccess = true;
            Debug.Log($"[Producer] {gameObject.name} приписан к {nearestWarehouse.name} (Дистанция: {minDistance})");
        }
        else
        {
            _hasWarehouseAccess = false;
            if (nearestWarehouse != null)
                Debug.LogWarning($"[Producer] {gameObject.name} нашел {nearestWarehouse.name}, но он СЛИШКОМ ДАЛЕКО (Дист: {minDistance} > Радиус: {nearestWarehouse.roadRadius})");
            else
                Debug.LogWarning($"[Producer] {gameObject.name} не нашел ни одного *доступного* склада.");
        }
    }

    public void UpdateProductionRate(int moduleCount)
    {
        _currentModuleBonus = 1.0f + (moduleCount * productionPerModule);
        Debug.Log($"[Producer] {gameObject.name} обновил бонус. Модулей: {moduleCount}, Множитель: {_currentModuleBonus}x");
    }
    
    public void SetEfficiency(float normalizedValue)
    {
        _efficiencyModifier = normalizedValue;
    }
    public float GetEfficiency() => _efficiencyModifier;
    
    
    private void PauseProduction()
    {
        if (IsPaused) return;
        IsPaused = true;
        // Debug.Log($"Производство {gameObject.name} на ПАУЗЕ (склад полон).");
    }

    private void ResumeProduction()
    {
        if (!IsPaused) return;
        IsPaused = false;
        // Debug.Log($"Производство {gameObject.name} ВОЗОБНОВЛЕНО (место появилось).");
    }
    public bool GetHasWarehouseAccess() 
    { 
        return _hasWarehouseAccess; 
    }

    public float GetWorkforceCap() 
    { 
        return _currentWorkforceCap; 
    }

    public float GetFinalEfficiency()
    {
        // Этот код дублирует логику из Update() - это нормально
        return _rampUpEfficiency * _currentWorkforceCap * _efficiencyModifier * _currentModuleBonus;
    }

    public float GetProductionPerMinute()
    {
        if (productionData == null || productionData.outputYield == null) return 0f;
        
        float eff = GetFinalEfficiency();
        if (eff == 0) return 0f;
        
        float cyclesPerMinute = 60f / (productionData.cycleTimeSeconds / eff);
        return cyclesPerMinute * productionData.outputYield.amount;
    }

    public float GetConsumptionPerMinute(ResourceType type)
    {
        if (productionData == null || productionData.inputCosts == null) return 0f;
        
        float eff = GetFinalEfficiency();
        if (eff == 0) return 0f;
        
        ResourceCost cost = productionData.inputCosts.Find(c => c.resourceType == type);
        if (cost == null) return 0f;
        
        float cyclesPerMinute = 60f / (productionData.cycleTimeSeconds / eff);
        return cyclesPerMinute * cost.amount;
    }
}