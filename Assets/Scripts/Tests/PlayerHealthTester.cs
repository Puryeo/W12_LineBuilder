using UnityEngine;

public class PlayerHealthTester : MonoBehaviour
{
    public int maxHealth = 100;
    public int current = 100;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.H))
        {
            current = Mathf.Max(0, current - 10);
            GameEvents.RaiseOnPlayerHealthChanged(current, maxHealth, "PlayerHealthTester.H");
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            current = Mathf.Min(maxHealth, current + 10);
            GameEvents.RaiseOnPlayerHealthChanged(current, maxHealth, "PlayerHealthTester.J");
        }
    }
}