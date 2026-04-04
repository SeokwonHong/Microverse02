using UnityEngine;

public class PlayerEnergy : MonoBehaviour
{

    private float maxEnergy = 10f;
    private float currentEnergy = 10f;
    private float minumEnergy = 0;  
    public float CurrentEnergy => currentEnergy;
    public float MaxEnergy => maxEnergy;
    public float  NormalizedEnergy =>maxEnergy <=0f?0f: currentEnergy / maxEnergy;

    public bool HasEnergy(float amount)
    {
        return currentEnergy >= amount;
    }

    public bool TryConsume(float amount)
    {
        if (currentEnergy < amount) return false;

        currentEnergy -= amount;
        currentEnergy = Mathf.Max(currentEnergy,0f); 
        return true;
    }

    public void AddEnergy(float amount)
    {
        currentEnergy += amount;
        currentEnergy = Mathf.Min(currentEnergy,maxEnergy);
    }


}
