using UnityEngine;

public class BasicZombie : ZombieBase
{
    public float speed = 1f;

    void Update()
    {
        // Move zombie left
        transform.Translate(Vector2.left * speed * slowMultiplier * Time.deltaTime);
    }
}