// --- Файл: UI_ResourceBalancePanel.cs ---
using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// "Мозг" для UI-панели "Баланс Ресурсов" (Приход/Расход).
/// Собирает данные со всех ResourceProducer'ов и обновляет UI.
/// </summary>
public class UI_ResourceBalancePanel : MonoBehaviour
{
    [Tooltip("Как часто (в сек) обновлять панель. 1.0 = раз в секунду.")]
    [SerializeField] private float updateInterval = 1.0f;
    private float _tickTimer;

    // --- Ссылки на UI Тексты (задать в Инспекторе) ---
    [SerializeField] private TextMeshProUGUI woodBalanceText;
    [SerializeField] private TextMeshProUGUI stoneBalanceText;
    [SerializeField] private TextMeshProUGUI planksBalanceText;
    
    // --- Словари для хранения баланса (в минуту) ---
    private Dictionary<ResourceType, float> _productionBalance = new Dictionary<ResourceType, float>();
    private Dictionary<ResourceType, float> _consumptionBalance = new Dictionary<ResourceType, float>();
    
    private WorkforceManager _workforceManager;

    void Start()
    {
        _workforceManager = WorkforceManager.Instance;
        if (_workforceManager == null)
        {
            Debug.LogError("[UI_ResourceBalance] Не найден WorkforceManager! Панель не будет работать.");
            enabled = false;
        }
    }

    void Update()
    {
        _tickTimer -= Time.deltaTime;
        if (_tickTimer <= 0f)
        {
            _tickTimer = updateInterval;
            
            CalculateBalance();
            UpdateUI();
        }
    }

    /// <summary>
    /// (Шаг 1) Собирает данные со всех заводов
    /// </summary>
    private void CalculateBalance()
    {
        if (_workforceManager == null) return;

        // Очищаем старые данные
        _productionBalance.Clear();
        _consumptionBalance.Clear();

        List<ResourceProducer> allProducers = _workforceManager.GetAllProducers();

        foreach (var producer in allProducers)
        {
            if (producer == null || producer.enabled == false || producer.productionData == null)
                continue;

            // --- 1. Считаем ПРИХОД (от этого продюсера) ---
            float prodPerMin = producer.GetProductionPerMinute();
            if (prodPerMin > 0 && producer.productionData.outputYield != null)
            {
                ResourceType type = producer.productionData.outputYield.resourceType;
                _productionBalance[type] = _productionBalance.GetValueOrDefault(type, 0) + prodPerMin;
            }

            // --- 2. Считаем РАСХОД (от этого продюсера) ---
            if (producer.productionData.inputCosts != null)
            {
                foreach (var cost in producer.productionData.inputCosts)
                {
                    float consPerMin = producer.GetConsumptionPerMinute(cost.resourceType);
                    if (consPerMin > 0)
                    {
                        _consumptionBalance[cost.resourceType] = _consumptionBalance.GetValueOrDefault(cost.resourceType, 0) + consPerMin;
                    }
                }
            }
        }
        
        // --- 3. Считаем РАСХОД (от Жителей) ---
        // TODO: Когда у нас будет ResidenceManager, мы добавим его сюда
        // (Например: _consumptionBalance[ResourceType.Planks] += ResidenceManager.Instance.GetTotalPlanksConsumption();)
    }

    /// <summary>
    /// (Шаг 2) Обновляет тексты на UI
    /// </summary>
    private void UpdateUI()
    {
        // "F1" = 1 знак после запятой (напр. "+10.5")
        // "F0" = 0 знаков (напр. "+10")
        const string format = "F1"; 
        
        if (woodBalanceText)
            woodBalanceText.text = $"+{_productionBalance.GetValueOrDefault(ResourceType.Wood, 0).ToString(format)} / -{_consumptionBalance.GetValueOrDefault(ResourceType.Wood, 0).ToString(format)}";

        if (stoneBalanceText)
            stoneBalanceText.text = $"+{_productionBalance.GetValueOrDefault(ResourceType.Stone, 0).ToString(format)} / -{_consumptionBalance.GetValueOrDefault(ResourceType.Stone, 0).ToString(format)}";

        if (planksBalanceText)
            planksBalanceText.text = $"+{_productionBalance.GetValueOrDefault(ResourceType.Planks, 0).ToString(format)} / -{_consumptionBalance.GetValueOrDefault(ResourceType.Planks, 0).ToString(format)}";
    }
}