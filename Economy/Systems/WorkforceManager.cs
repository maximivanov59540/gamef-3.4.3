using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Синглтон ("Мозг"), отвечающий за "Рынок Труда".
/// Считает, сколько рабочих "требуется" (от Производителей)
/// и сколько "доступно" (от PopulationManager),
/// а затем выдает "Коэффициент Рабочей Силы" (0.0 - 1.0).
/// </summary>
public class WorkforceManager : MonoBehaviour
{
    public static WorkforceManager Instance { get; private set; }

    [Tooltip("Включить/Выключить всю систему 'Рынка Труда'")]
    public bool workforceSystemEnabled = true;

    private int _totalRequiredWorkforce = 0;
    private int _totalAvailableWorkforce = 0;

    private List<ResourceProducer> _allProducers = new List<ResourceProducer>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public void RegisterProducer(ResourceProducer producer)
    {
        if (workforceSystemEnabled && producer != null)
        {
            _totalRequiredWorkforce += producer.workforceRequired;
            Debug.Log($"[Workforce] Зарегистрирован: {producer.name} (Требует: {producer.workforceRequired}). ОБЩАЯ ПОТРЕБНОСТЬ: {_totalRequiredWorkforce}");
        if (!_allProducers.Contains(producer))
                _allProducers.Add(producer);
        }
    }
    public void UnregisterProducer(ResourceProducer producer)
    {
        if (workforceSystemEnabled && producer != null)
        {
            _totalRequiredWorkforce -= producer.workforceRequired;
            Debug.Log($"[Workforce] Снят с регистрации: {producer.name}. ОБЩАЯ ПОТРЕБНОСТЬ: {_totalRequiredWorkforce}");
        _allProducers.Remove(producer);
        }
    }

    public void UpdateAvailableWorkforce(int totalPopulation)
    {
        _totalAvailableWorkforce = totalPopulation;
        Debug.Log($"[Workforce] Доступно рабочих: {_totalAvailableWorkforce}");
    }

    public float GetWorkforceRatio()
    {
        if (!workforceSystemEnabled)
            return 1.0f; // Система выключена, лимита нет
            
        if (_totalRequiredWorkforce == 0)
            return 1.0f; // Никто не требует рабочих, лимита нет

        // Главный расчет: (Напр., 80 / 100 = 0.8)
        float ratio = (float)_totalAvailableWorkforce / (float)_totalRequiredWorkforce;
        
        // Clamp01 гарантирует, что мы не вернем > 1.0 (если рабочих больше, чем нужно)
        return Mathf.Clamp01(ratio); 
    }
    public List<ResourceProducer> GetAllProducers()
    {
        return _allProducers;
    }
}