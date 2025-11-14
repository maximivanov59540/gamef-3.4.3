using UnityEngine;

public class PopulationManager : MonoBehaviour
{
    public int currentPopulation = 0;
    public int maxPopulation = 0;

    public void AddHousingCapacity(int amount)
    {
        maxPopulation += amount;
        Debug.Log("Лимит жилья увеличен на " + amount + ". Новый лимит: " + maxPopulation);
        
        // --- ⬇️ ДОБАВЬ ЭТУ СТРОКУ ⬇️ ---
        WorkforceManager.Instance?.UpdateAvailableWorkforce(maxPopulation);
    }
    void Start()
    {
        // Сообщаем WorkforceManager о стартовом населении (лимите жилья)
        WorkforceManager.Instance?.UpdateAvailableWorkforce(maxPopulation);
    }

    public void RemoveHousingCapacity(int amount)
    {
        maxPopulation -= amount;
        if (maxPopulation < 0)
        {
            maxPopulation = 0;
        }
        Debug.Log("Лимит жилья уменьшен на " + amount + ". Новый лимит: " + maxPopulation);
        
        // --- ⬇️ ДОБАВЬ ЭТУ СТРОКУ ⬇️ ---
        WorkforceManager.Instance?.UpdateAvailableWorkforce(maxPopulation);
    }
}