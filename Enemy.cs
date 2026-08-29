using UnityEngine;

public class Enemy : MonoBehaviour
{

    [SerializeField] protected float health;


    [SerializeField] protected float recoilLength;
    [SerializeField] protected float recoilFactor;
    [SerializeField] protected bool isRecoiling = false;
    [SerializeField] protected PlayerController player;


    [SerializeField] protected float speed;
    [SerializeField] protected float damage;
    [SerializeField] protected float parryStun = 1f;
    [SerializeField] protected float parryKnockback = 8f;
    protected float recoilTimer;
    protected Rigidbody2D rb;
    protected bool isParried;
    protected float parryTime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public virtual void Start()
    {

    }// ILLL NEVERRRRRRRRRRRRRRRRRRRRRRRR GET A JOBBBBBBBBBBB // LMFAOAOOAOA 81226

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        player = PlayerController.Instance;
    }
    // Update is called once per frame
    protected virtual void Update()
    {
        if (health <= 0)
        {
            Destroy(gameObject);
        }

        if (isParried)
        {
            parryTime -= Time.deltaTime;

            if (parryTime <= 0f)
                isParried = false;

            return;
        }

        if (isRecoiling)
        {
            if (recoilTimer < recoilLength)
            {
                recoilTimer += Time.deltaTime;
            }
            else
            {
                isRecoiling = false;
                recoilTimer = 0;
            }
        }
    }

    public virtual void EnemyHit(float _damageDone, Vector2 _hitDirection, float _hitForce)
    {
        health -= _damageDone;
        if (!isRecoiling)
        {
            isRecoiling = true;
            recoilTimer = 0f;
            rb.AddForce(-_hitForce * recoilFactor * _hitDirection, ForceMode2D.Impulse);
        }
    }
    protected void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.CompareTag("Player") && !PlayerController.Instance.pState.invincible)
        {
            Attack();
        }
    }

    public virtual void Parried(Vector2 parryDirection)
    {
        isParried = true;
        parryTime = parryStun;

        isRecoiling = false;
        recoilTimer = 0f;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(parryDirection.normalized * parryKnockback, ForceMode2D.Impulse);
    }
    protected virtual void Attack()
    {
        PlayerController.Instance.TakeDamage(damage);
    }



}
