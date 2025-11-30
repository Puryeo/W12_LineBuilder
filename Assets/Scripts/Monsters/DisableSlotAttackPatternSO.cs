using UnityEngine;

[CreateAssetMenu(fileName = "DisableSlotAttackPattern", menuName = "Monsters/DisableSlotAttackPatternSO")]
public class DisableSlotAttackPatternSO : AttackPatternSO
{
    [Header("Slot Targeting")]
    [Min(1)]
    [Tooltip("Number of slots to disable in one hit")]
    public int slotsToDisable = 1;

    [Tooltip("Whether row slots can be targeted")]
    public bool includeRows = true;

    [Tooltip("Whether column slots can be targeted")]
    public bool includeColumns = true;

    [Tooltip("Skip empty slots when choosing targets")]
    public bool requireOccupiedSlot = true;

    public DisableSlotAttackPatternSO()
    {
        attackType = AttackType.DisableSlot;
    }

    private void OnValidate()
    {
        attackType = AttackType.DisableSlot;
        slotsToDisable = Mathf.Max(1, slotsToDisable);
        if (!includeRows && !includeColumns)
            includeRows = true; // ensure at least one axis is enabled
    }
}
