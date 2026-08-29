using UnityEngine;

public class Enemy : MonoBehaviour
{

    [SerializeField] protected float health;


    [SerializeField] protected float recoilLength;
    [SerializeField] protected float recoilFactor;
    [SerializeField] protected bool isRecoiling = false;
    [SerializeField] protected BaseSol player;



    [SerializeField] protected float speed;
    [SerializeField] protected float damage;
    [SerializeField] protected float parryStun = 1f;
    [SerializeField] protected float parryKnockback = 8f;
    protected float recoilTimer;
    protected  Rigidbody2D rb;
    protected bool isParried;
    protected float parryTime;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public virtual void Start()
    {

    }// ILLL NEVERRRRRRRRRRRRRRRRRRRRRRRR GET A JOBBBBBBBBBBB // LMFAOAOOAOA 81226
    
    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        player = BaseSol.Instance;
    }
    // Update is called once per frame
    protected virtual void Update()
    {
        if (health <= 0)
        {
            Destroy(gameObject);
        }
        if(isRecoiling)
        {
            if(recoilTimer < recoilLength)
            {
                recoilTimer += Time.deltaTime;
            }
            else
            {
                isRecoiling = false;
                recoilTimer = 0;
            }
            if (isParried)
            {
                parryTime -= Time.deltaTime;

                if (parryTime <= 0f)
                    isParried = false;

                return; 
            }
        }
    } 
    
    public virtual void EnemyHit(float _damageDone, Vector2 _hitDirection, float _hitForce)
    {
        health -= _damageDone;
        if(!isRecoiling)
        {
            rb.AddForce(-_hitForce * recoilFactor * _hitDirection);
        }
    }
    protected void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.CompareTag("Player") && !BaseSol.Instance.pState.invincible)
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

   parryTime = 0f;
    }
    protected virtual void Attack()
    {
        BaseSol.Instance.TakeDamage(damage);
    }



}
       

