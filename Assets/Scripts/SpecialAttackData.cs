using UnityEngine;

[CreateAssetMenu(fileName = "NewSpecial", menuName = "SRPG/Special Attack")]
public class SpecialAttackData : ScriptableObject
{
    public string techniqueName = "Technique";
    [Min(1)] public int pipCost = 3;
    [Min(0f)] public float damageMultiplier = 1.5f;
    public bool bypassesArmor;
    public bool preventsCounter;
}
