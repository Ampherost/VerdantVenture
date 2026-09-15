using UnityEngine;

public enum DamageCategory { Physical, Special }

[CreateAssetMenu(fileName = "NewWeapon", menuName = "SRPG/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName = "Weapon";
    public int might;
    public int baseHit = 80;
    public int baseCrit;
    [Min(0)] public int weight;
    [Min(1)] public int minRange = 1;
    [Min(1)] public int maxRange = 1;
    public DamageCategory damageType = DamageCategory.Physical;

    private void OnValidate()
    {
        minRange = Mathf.Max(1, minRange);
        maxRange = Mathf.Max(minRange, maxRange);
    }
}
