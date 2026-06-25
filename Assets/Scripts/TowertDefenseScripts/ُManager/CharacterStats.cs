using UnityEngine;

public class CharacterStats : MonoBehaviour
{
    public float currentHP ;
    public float maxHealth = 100;
    public void Init(float maxHP)
    {
        currentHP = Mathf.Max(1f, maxHP);
    }

    public bool IsAlive => currentHP > 0f;


}