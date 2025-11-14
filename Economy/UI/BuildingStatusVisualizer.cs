using UnityEngine;

/// <summary>
/// Показывает/прячет иконки состояния ("Zzz", "!")
/// над зданием в зависимости от его "мозгов" (Producer/Input).
/// </summary>
public class BuildingStatusVisualizer : MonoBehaviour
{
    [Header("Иконки (Префабы)")]
    [Tooltip("Префаб иконки 'Zzz' (склад полон)")]
    public GameObject ZzzIcon;
    [Tooltip("Префаб иконки '!' (нет сырья)")]
    public GameObject NoResourceIcon;
    [Tooltip("Префаб иконки 'Нет доступа к Складу'")]
    [SerializeField] private GameObject NoWarehouseIcon;
    [Tooltip("Префаб иконки 'Нет Рабочих'")]
    [SerializeField] private GameObject NoWorkersIcon;

    // --- Ссылки на "мозги" ---
    private ResourceProducer _producer;
    private BuildingInputInventory _inputInv;
    private BuildingOutputInventory _outputInv;

    private void Awake()
    {
        _producer = GetComponent<ResourceProducer>();
        _inputInv = GetComponent<BuildingInputInventory>();
        _outputInv = GetComponent<BuildingOutputInventory>();

        // Если у здания нет ни того, ни другого - скрипт не нужен
        if (_producer == null && _inputInv == null)
        {
            Destroy(this); // (Или 'this.enabled = false;')
            return;
        }
        
        // Прячем иконки на старте
        if (ZzzIcon) ZzzIcon.SetActive(false);
        if (NoResourceIcon) NoResourceIcon.SetActive(false);

        if (NoWarehouseIcon) NoWarehouseIcon.SetActive(false);
        if (NoWorkersIcon) NoWorkersIcon.SetActive(false);
    }

    private void Update()
    {
        // Сначала "прячем" все (проще, чем управлять флагами)
        if (ZzzIcon) ZzzIcon.SetActive(false);
        if (NoResourceIcon) NoResourceIcon.SetActive(false);
        if (NoWarehouseIcon) NoWarehouseIcon.SetActive(false);
        if (NoWorkersIcon) NoWorkersIcon.SetActive(false);

        // --- 1. ПРОВЕРКА "ЗАПРОСА СЫРЬЯ" (!) (Приоритет 1) ---
        // (Она не зависит от продюсера, поэтому первая)
        if (_inputInv != null && _inputInv.IsRequesting && NoResourceIcon)
        {
            NoResourceIcon.SetActive(true);
            return; // Выходим, "!" важнее всего
        }

        // Если нет продюсера, остальные иконки не нужны
        if (_producer == null) return;
        
        // --- 2. ПРИОРИТЕТ 2: "Склад полон" (Zzz) ---
        // (IsPaused - это общий флаг, мы проверяем *конкретную* причину)
        bool isFull = (_outputInv != null && !_outputInv.HasSpace(1)); // HasSpace(1) надежнее
        if (isFull && ZzzIcon) 
        {
            ZzzIcon.SetActive(true);
            return; // Выходим, т.к. это следующая по важности иконка
        }

        // --- 3. ПРИОРИТЕТ 3: "Нет доступа к складу" ---
        bool hasAccess = _producer.GetHasWarehouseAccess(); 
        if (!hasAccess && NoWarehouseIcon)
        {
            NoWarehouseIcon.SetActive(true);
            return;
        }

        // --- 4. ПРИОРИТЕТ 4: "Нет рабочих" ---
        float workforceCap = _producer.GetWorkforceCap(); 
        if (workforceCap < 0.99f && NoWorkersIcon) // 0.99f для защиты от float
        {
            NoWorkersIcon.SetActive(true);
            return;
        }
    }
}